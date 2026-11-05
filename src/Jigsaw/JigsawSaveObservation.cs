using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace AllMatters.Jigsaw;

// Native SaveManager logs write failures and suppresses them. Keep the native
// report, but also prevent a puzzle's normal exit from claiming a failed save.
[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun), [typeof(SerializableRun), typeof(bool)])]
internal static class JigsawSaveObservation
{
    private static void Postfix(ref Task __result)
    {
        if (JigsawNativeSession.Current is { } session) __result = session.ObserveSave(__result);
    }
}
