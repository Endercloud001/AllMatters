using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace AllMatters.Jigsaw;

// Native SaveManager logs write failures and suppresses them. Keep the native
// report, but also prevent a puzzle's normal exit from claiming a failed save.
[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun), [typeof(SerializableRun), typeof(bool)])]
internal static class JigsawSaveObservation
{
    private sealed class Writes
    {
        public Task Tail = Task.CompletedTask;
    }
    private static readonly ConditionalWeakTable<RunSaveManager, Writes> queues = new();
    private static readonly MethodInfo saveMethod = AccessTools.Method(typeof(RunSaveManager),
        nameof(RunSaveManager.SaveRun), [typeof(SerializableRun), typeof(bool)]);
    [ThreadStatic] private static bool invoking;

    private static bool Prefix(RunSaveManager __instance, SerializableRun save, bool isMultiplayer, ref Task __result)
    {
        // Consume the bypass once, so saves requested by native callbacks still queue.
        if (invoking) { invoking = false; return true; }
        if (JigsawNativeSession.Current is not { } session) return true;
        var queue = queues.GetValue(__instance, _ => new());
        var completion = new TaskCompletionSource();
        Task previous;
        lock (queue)
        {
            previous = queue.Tail;
            __result = queue.Tail = session.TrackNativeSave(completion.Task);
        }
        _ = WriteAfter(previous, completion, __instance, save, isMultiplayer);
        return false;
    }

    private static async Task WriteAfter(Task previous, TaskCompletionSource completion,
        RunSaveManager manager, SerializableRun save, bool multiplayer)
    {
        try
        {
            try { await previous; } catch { /* A later complete checkpoint may retry a failed write. */ }
            Task write;
            invoking = true;
            try { write = (Task)saveMethod.Invoke(manager, [save, multiplayer])!; }
            finally { invoking = false; }
            await write;
            completion.SetResult();
        }
        catch (Exception error) { completion.SetException(error is TargetInvocationException { InnerException: { } cause } ? cause : error); }
    }
}
