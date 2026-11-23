using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace AllMatters.Jigsaw;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer), [typeof(RunState), typeof(SerializableRun)])]
internal static class JigsawSingleplayerRestore
{
    private static void Postfix(ref Task __result, RunState state, SerializableRun save)
    {
        bool marked = JigsawSaveSource.IsJigsaw(save);
        if (marked || JigsawCheckpoint.Slot.TryGet(state, out _))
            __result = Finish(__result, state, save, marked);
    }

    private static async Task Finish(Task setup, RunState state, SerializableRun save, bool marked)
    {
        // Native setup assigns State before awaiting its reload counter write.
        await setup;
        JigsawNativeSession.Restore(state, marked, save);
    }
}
