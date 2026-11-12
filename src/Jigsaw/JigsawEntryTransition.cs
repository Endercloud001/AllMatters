using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace AllMatters.Jigsaw;

// Owns only the room transition around an ordinary puzzle destination entry.
internal sealed class JigsawEntryTransition : IDisposable
{
    private static JigsawEntryTransition? active;
    [ThreadStatic] private static JigsawEntryTransition? invoking;
    private static readonly AsyncLocal<NativeEntryScope?> nativeEntry = new();
    private readonly JigsawNativeSession session;
    private readonly NTransition transition;
    private readonly CancellationTokenSource displaced = new();
    private readonly CancellationTokenSource cancellation;
    private bool disposed;
    private JigsawEntryTransition(JigsawNativeSession session, NTransition transition)
    {
        this.session = session;
        this.transition = transition;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(displaced.Token, session.ClosedToken);
        active = this;
    }

    internal static JigsawEntryTransition? Begin(JigsawNativeSession session)
    {
#if STS2_PUBLIC
        if (!JigsawOrdinaryNativeReveal.Compatible)
            return null;
#endif
        // Existing headless room tests have no NGame scene owner.
        var transition = NGame.Instance?.Transition;
        if (transition == null)
            return null;
        if (transition.InTransition || active != null)
            throw new InvalidOperationException("上一次房间过场尚未结束，请稍后重试。");
        return new(session, transition);
    }
    internal bool IsCurrent => !disposed && !cancellation.IsCancellationRequested && active == this && JigsawNativeSession.Current == session &&
        GodotObject.IsInstanceValid(transition) && ReferenceEquals(NGame.Instance?.Transition, transition);
    internal IDisposable NativeEntry()
    {
        var previous = nativeEntry.Value;
        var scope = new NativeEntryScope(previous);
        nativeEntry.Value = scope;
        return scope;
    }
    private sealed class NativeEntryScope(NativeEntryScope? previous) : IDisposable
    {
        internal bool Active { get; private set; } = true;
        public void Dispose() { Active = false; nativeEntry.Value = previous; }
    }
    // Visual takeover cannot let this old native call reveal over the new owner.
    // The exact callsite remains deferred until this native invocation returns;
    // inherited execution contexts become inert when the scope is disposed.
    internal static bool DeferNativeReveal => nativeEntry.Value?.Active == true;
    internal static void ObserveNativeCall(NTransition target)
    {
        if (active is not { } owner || !ReferenceEquals(owner.transition, target) || invoking == owner)
            return;
        // Native transitions share one tween. A new caller may Kill the old tween
        // without faulting its task; stop awaiting/entering before it does so.
        active = null;
        owner.displaced.Cancel();
    }
    private Task Invoke(Func<Task> animation)
    {
        var previous = invoking;
        invoking = this;
        try
        { return animation(); }
        finally { invoking = previous; }
    }
    internal async Task FadeOut()
    {
        await Invoke(transition.RoomFadeOut).WaitAsync(cancellation.Token);
        if (!IsCurrent)
            throw new OperationCanceledException("房间过场的会话已结束。");
    }
    internal async Task Reveal()
    {
        if (IsCurrent)
            await Invoke(() => transition.RoomFadeIn()).WaitAsync(cancellation.Token);
    }
    public void Dispose()
    {
        disposed = true;
        if (active == this)
            active = null;
        cancellation.Dispose();
        displaced.Dispose();
    }
}

// Observe takeover without changing the external method's arguments, task or
// cancellation behavior. With no ordinary entry owner this is a no-op.
[HarmonyPatch]
internal static class JigsawEntryTransitionTakeover
{
    private static IEnumerable<MethodBase> TargetMethods() => new[]
    {
        AccessTools.Method(typeof(NTransition), nameof(NTransition.FadeOut)),
        AccessTools.Method(typeof(NTransition), nameof(NTransition.FadeIn)),
        AccessTools.Method(typeof(NTransition), nameof(NTransition.RoomFadeOut)),
        AccessTools.Method(typeof(NTransition), nameof(NTransition.RoomFadeIn))
    };
    [HarmonyPriority(Priority.First)]
    private static void Prefix(NTransition __instance) => JigsawEntryTransition.ObserveNativeCall(__instance);
}

#if STS2_PUBLIC
// Replace only the ordinary entry's final call, not FadeIn callers such as
// loading, acts, retries or event child rooms that happen to share its context.
[HarmonyPatch]
internal static class JigsawOrdinaryNativeReveal
{
    internal static bool Compatible { get; private set; }
    private static readonly MethodInfo native = AccessTools.Method(typeof(RunManager), "FadeIn", [typeof(bool)]);
    private static readonly Func<RunManager, bool, Task> reveal = native.CreateDelegate<Func<RunManager, bool, Task>>();
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(RunManager), nameof(RunManager.EnterMapPointInternal))
        .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var calls = code.Where(instruction => instruction.Calls(native)).ToArray();
        Compatible = calls.Length == 1;
        if (!Compatible)
        {
            Console.Error.WriteLine($"Jigsaw ordinary room transition is incompatible: expected one native entry reveal call, found {calls.Length}; retaining native entry behavior.");
            return code;
        }
        calls[0].opcode = OpCodes.Call;
        calls[0].operand = AccessTools.Method(typeof(JigsawOrdinaryNativeReveal), nameof(Reveal));
        return code;
    }
    private static Task Reveal(RunManager manager, bool showTransition) =>
        JigsawEntryTransition.DeferNativeReveal ? Task.CompletedTask : reveal(manager, showTransition);
}
#endif
