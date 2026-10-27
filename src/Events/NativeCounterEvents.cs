using AllMatters.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Utils;

namespace AllMatters.Events;

internal static class NativeCounterVariant
{
    private static readonly SavedAttachedState<EventModel, int> Rolls = new("AllMattersCounterEventRoll");
    internal static int Roll(EventModel model)
    {
        int value = Rolls.GetValueOrDefault(model);
        if (value == 0) Rolls[model] = value = model.Rng.NextInt(100) + 1;
        return value - 1;
    }
}

[HarmonyPatch(typeof(TeaMaster), "GenerateInitialOptions")]
internal static class NativeTeaChoice
{
    private static void Postfix(TeaMaster __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        int roll = NativeCounterVariant.Roll(model);
        int effect = roll < 33 ? 0 : roll < 66 ? 1 : 2;
        var options = __result.ToList();
        options[^1] = NativeEventActions.Option(model, "TEA_MASTER.pages.INITIAL.options.AM_DISCOURTESY_" + effect, async () =>
        {
            var tea = await RelicCmd.Obtain<TeaOfDiscourtesy>(model.Owner!);
            DiscourtesyVariant.Effect[tea] = effect;
            NativeEventActions.Finish(model, "TEA_MASTER.pages.TEA_OF_DISCOURTESY.description");
        });
        __result = options;
    }
}

[HarmonyPatch(typeof(WaterloggedScriptorium), "GenerateInitialOptions")]
internal static class NativeInkChoice
{
    private static void Postfix(WaterloggedScriptorium __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        if (NativeCounterVariant.Roll(model) >= 50) return;
        var options = __result.ToList();
        options[0] = NativeEventActions.Option(model, "WATERLOGGED_SCRIPTORIUM.pages.INITIAL.options.AM_INK", async () =>
        {
            await RelicCmd.Obtain<InkBottle>(model.Owner!);
            NativeEventActions.Finish(model, "WATERLOGGED_SCRIPTORIUM.pages.AM_INK.description");
        }, HoverTipFactory.FromRelic<InkBottle>().ToArray());
        __result = options;
    }
}

[HarmonyPatch(typeof(Symbiote), "GenerateInitialOptions")]
internal static class NativeVirusChoice
{
    private static void Postfix(Symbiote __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        __result = __result.Append(NativeEventActions.Option(model, "SYMBIOTE.pages.INITIAL.options.AM_ACCEPT", async () =>
        {
            await RelicCmd.Obtain<SymbioticVirus>(model.Owner!);
            NativeEventActions.Finish(model, "SYMBIOTE.pages.AM_ACCEPT.description");
        }, HoverTipFactory.FromRelic<SymbioticVirus>().ToArray())).ToArray();
    }
}
