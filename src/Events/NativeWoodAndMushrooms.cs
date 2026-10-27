using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;

namespace AllMatters.Events;

[HarmonyPatch(typeof(Peck), "OnPlay")]
internal static class PeckBlockRemoval
{
    private static bool Prefix(Peck __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
    {
        __result = Play(__instance, choiceContext, cardPlay);
        return false;
    }
    private static async Task Play(Peck card, PlayerChoiceContext context, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
#if STS2_PUBLIC
        await CreatureCmd.LoseBlock(play.Target, Math.Min(15, play.Target.Block));
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).WithHitCount(card.DynamicVars.Repeat.IntValue)
            .FromCard(card).Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
#else
        await CreatureCmd.LoseBlock(context, play.Target, Math.Min(15, play.Target.Block), card.Owner.Creature);
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).WithHitCount(card.DynamicVars.Repeat.IntValue)
            .FromCard(card, play).Targeting(play.Target).WithHitFx("vfx/vfx_attack_slash").Execute(context);
#endif
    }
}

[HarmonyPatch(typeof(ToricToughness), "OnPlay")]
internal static class ToricBlock
{
    private static bool Prefix(ToricToughness __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
    {
        __result = Play(__instance, choiceContext, cardPlay);
        return false;
    }
    private static async Task Play(ToricToughness card, PlayerChoiceContext context, CardPlay play)
    {
        await CreatureCmd.GainBlock(card.Owner.Creature, card.DynamicVars.Block, play);
        // One block-clear trigger on the next owner turn, with a separate base amount.
        (await PowerCmd.Apply<ToricToughnessPower>(context, card.Owner.Creature, 1, card.Owner.Creature, card))?.SetBlock(7);
    }
}

[HarmonyPatch(typeof(ToricToughness), "OnUpgrade")]
internal static class ToricUpgrade
{
    private static bool Prefix(ToricToughness __instance)
    {
        __instance.RemoveKeyword(CardKeyword.Exhaust);
        return false;
    }
}

[HarmonyPatch(typeof(CardModel), "get_CanonicalKeywords")]
internal static class ToricExhaust
{
    private static void Postfix(CardModel __instance, ref IEnumerable<CardKeyword> __result)
    {
        if (__instance is ToricToughness) __result = __result.Append(CardKeyword.Exhaust);
    }
}

[HarmonyPatch]
internal static class NativeWoodVars
{
    private static IEnumerable<MethodBase> TargetMethods()
        => new[] { typeof(ToricToughness), typeof(BigMushroom) }.Select(t => AccessTools.PropertyGetter(t, "CanonicalVars"));
    private static void Postfix(AbstractModel __instance, ref IEnumerable<DynamicVar> __result)
    {
        __result = __result.Select(v => __instance switch
        {
            ToricToughness when v is BlockVar => new BlockVar(15, ValueProp.Move),
            BigMushroom when v is MaxHpVar => new MaxHpVar(40),
            _ => v
        }).ToArray();
    }
}

[HarmonyPatch(typeof(FragrantMushroom), nameof(FragrantMushroom.AfterObtained))]
internal static class FragrantUpgradeSelection
{
    private static bool Prefix(FragrantMushroom __instance, ref Task __result)
    {
        __result = Obtain(__instance);
        return false;
    }
    private static async Task Obtain(FragrantMushroom relic)
    {
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), relic.Owner.Creature, 15,
            ValueProp.Unblockable | ValueProp.Unpowered, null, null);
        if (relic.Owner.Creature.IsDead) return;
        var cards = await CardSelectCmd.FromDeckForUpgrade(relic.Owner, new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, 2));
        CardCmd.Upgrade(cards, CardPreviewStyle.MessyLayout);
    }
}
