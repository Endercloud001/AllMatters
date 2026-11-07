using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace AllMatters.Jigsaw;

// Canonicalization builds a new native save from gameplay modifiers, where the
// saved-only jigsaw identity was deliberately removed during restoration.
[HarmonyPatch(typeof(RunManager), nameof(RunManager.CanonicalizeSave))]
internal static class JigsawLobbyCheckpoint
{
    [ThreadStatic] private static int validating;
    internal static bool IsValidating => validating > 0;

    private static void Prefix(SerializableRun save,out bool __state)
    {
        __state=JigsawSaveSource.IsJigsaw(save);
        if(__state)validating++;
    }
    private static void Finalizer(bool __state)
    {
        if(__state)validating--;
    }
    private static void Postfix(SerializableRun __result,bool __state)
    {
        if(!__state)return;
        var identity = ModelDb.Modifier<AllMattersJigsawMode>();
        if (!__result.Modifiers.Any(m => m.Id == identity.Id))
            __result.Modifiers.Add(identity.ToMutable().ToSerializable());
    }
}
