using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Relics;

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_BLUE_CANDLE")]
public sealed class BlueCandle : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/blue_candle.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/blue_candle.png",
        BigIconPath: "res://AllMatters/images/relics/blue_candle.png");

    internal static bool Enables(CardModel card) => !card.IsCanonical && card.Type == CardType.Curse
        && card.CombatState != null && card.Owner.Relics.OfType<BlueCandle>().Any();

    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        if (card.Owner != Owner || card.Type != CardType.Curse) return false;
        bool removed = keywords.Remove(CardKeyword.Unplayable);
        return keywords.Add(CardKeyword.Exhaust) || removed;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner || cardPlay.Card.Type != CardType.Curse) return;
        Flash();
        await CreatureCmd.Damage(choiceContext, Owner.Creature, 2, ValueProp.Unblockable | ValueProp.Unpowered, null, null);
    }
}

// Native negative costs return before local/global modifiers. Supply the
// candle's default only while that calculation runs, then restore the original
// base even on failure. Existing nonnegative curse costs follow the native path.
[HarmonyPatch(typeof(CardEnergyCost), nameof(CardEnergyCost.GetWithModifiers))]
internal static class BlueCandleDefaultCost
{
    private static void Prefix(CardModel ____card, ref int ____base, out int? __state)
    {
        __state = null;
        if (____base >= 0 || !BlueCandle.Enables(____card)) return;
        __state = ____base;
        ____base = 1;
    }
    private static void Postfix(ref int ____base, int? __state)
    {
        if (__state.HasValue) ____base = __state.Value;
    }
    private static Exception? Finalizer(Exception? __exception, ref int ____base, int? __state)
    {
        if (__exception != null && __state.HasValue) ____base = __state.Value;
        return __exception;
    }
}
