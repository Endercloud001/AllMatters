using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Utils;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace AllMatters.Relics;

[HarmonyPatch(typeof(BoneTea), MethodType.Constructor)]
internal static class NativeBoneTeaDuration
{
    private static void Postfix(ref int ____combatsLeft) => ____combatsLeft = 2;
}

[HarmonyPatch(typeof(EmberTea), nameof(EmberTea.AfterRoomEntered))]
internal static class NativeEmberRoom
{
    // Combat start also covers fights embedded in an event room.
    private static bool Prefix(ref Task __result) { __result = Task.CompletedTask; return false; }
}

[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.BeforeCombatStart))]
internal static class NativeEmberStart
{
    private static void Postfix(AbstractModel __instance, ref Task __result)
    {
        if (__instance is EmberTea tea && !tea.IsUsedUp) __result = Apply(tea, __result);
    }
    private static async Task Apply(EmberTea tea, Task original)
    {
        await original;
        tea.CombatsLeft--;
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<StrengthPower>(context, tea.Owner.Creature, 1, tea.Owner.Creature, null);
        await PowerCmd.Apply<DexterityPower>(context, tea.Owner.Creature, 1, tea.Owner.Creature, null);
    }
}

internal static class DiscourtesyVariant
{
    internal static readonly SavedAttachedState<RelicModel, int> Effect = new("AllMattersDiscourtesyEffect");
}

[HarmonyPatch(typeof(RelicModel), "get_DynamicDescription")]
internal static class NativeDiscourtesyDescription
{
    private static void Postfix(RelicModel __instance, ref LocString __result)
    {
        if (__instance is TeaOfDiscourtesy && __instance.IsMutable)
            __result = new LocString("events", "TEA_MASTER.pages.INITIAL.options.AM_DISCOURTESY_"
                + DiscourtesyVariant.Effect.GetValueOrDefault(__instance) + ".description");
    }
}

[HarmonyPatch(typeof(EmberTea), "get_ExtraHoverTips")]
internal static class NativeEmberHoverTips
{
    private static void Postfix(ref IEnumerable<IHoverTip> __result) =>
        __result = [HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.FromPower<DexterityPower>()];
}

[HarmonyPatch(typeof(TeaOfDiscourtesy), "get_ExtraHoverTips")]
internal static class NativeDiscourtesyHoverTips
{
    private static void Postfix(TeaOfDiscourtesy __instance, ref IEnumerable<IHoverTip> __result)
    {
        int effect = __instance.IsMutable ? DiscourtesyVariant.Effect.GetValueOrDefault(__instance) : 0;
        __result = effect switch
        {
            2 => [HoverTipFactory.FromPower<ConfusedPower>()],
            1 => HoverTipFactory.FromCardWithCardHoverTips<Wound>(),
            _ => HoverTipFactory.FromCardWithCardHoverTips<Dazed>()
        };
    }
}

[HarmonyPatch(typeof(TeaOfDiscourtesy), nameof(TeaOfDiscourtesy.BeforeCombatStart))]
internal static class NativeDiscourtesyStart
{
    private static bool Prefix(TeaOfDiscourtesy __instance, ref Task __result)
    {
        __result = Apply(__instance);
        return false;
    }
    private static async Task Apply(TeaOfDiscourtesy tea)
    {
        if (tea.IsUsedUp) return;
        AccessTools.Property(typeof(TeaOfDiscourtesy), "CombatsLeft").SetValue(tea, 0);
        int effect = DiscourtesyVariant.Effect.GetValueOrDefault(tea);
        if (effect == 2)
            await PowerCmd.Apply<ConfusedPower>(new ThrowingPlayerChoiceContext(), tea.Owner.Creature, 1, tea.Owner.Creature, null);
        else if (effect == 1)
            await CardPileCmd.AddToCombatAndPreview<Wound>(tea.Owner.Creature, PileType.Draw, 2, tea.Owner, CardPilePosition.Random);
        else
            await CardPileCmd.AddToCombatAndPreview<Dazed>(tea.Owner.Creature, PileType.Draw, 2, tea.Owner, CardPilePosition.Random);
    }
}
