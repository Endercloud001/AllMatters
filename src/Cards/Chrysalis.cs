using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Cards;

[RegisterCard(typeof(EventCardPool), FullPublicEntry = "ALL_MATTERS_CHRYSALIS")]
public sealed class Chrysalis : ModCardTemplate
{
    public Chrysalis() : base(2, CardType.Skill, CardRarity.Event, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://AllMatters/images/card_portraits/chrysalis.png");
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(3)];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var pool = Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.Type == CardType.Skill && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .Where(c => !c.DynamicVars.Any(v => v.Value is HealVar));
        var cards = CardFactory.GetForCombat(Owner, pool, DynamicVars.Cards.IntValue, Owner.RunState.Rng.CombatCardGeneration).ToList();
        foreach (var card in cards)
        {
            card.EnergyCost.SetThisCombat(0);
            if (card.EnergyCost.CostsX) ChrysalisFreeX.Cards[card] = true;
        }
        await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Draw, Owner);
    }
    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(2);
}

// Native X costs bypass local cost modifiers. Keep CostsX intact so native
// capture and X resolution still run, with the actual zero energy spent.
[HarmonyLib.HarmonyPatch(typeof(CardEnergyCost), nameof(CardEnergyCost.GetAmountToSpend))]
internal static class ChrysalisFreeX
{
    internal static readonly STS2RitsuLib.Utils.SavedAttachedState<MegaCrit.Sts2.Core.Models.CardModel, bool> Cards = new("AllMattersChrysalisFreeX");
    internal static bool Applies(MegaCrit.Sts2.Core.Models.CardModel card)
        => !card.IsCanonical && card.CombatState != null && Cards.GetValueOrDefault(card);
    private static void Postfix(MegaCrit.Sts2.Core.Models.CardModel ____card, ref int __result)
    {
        if (Applies(____card)) __result = 0;
    }
}

[HarmonyLib.HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Cards.NCard), "UpdateEnergyCostVisuals")]
internal static class ChrysalisFreeXDisplay
{
    private static void Postfix(MegaCrit.Sts2.Core.Nodes.Cards.NCard __instance, MegaCrit.Sts2.addons.mega_text.MegaLabel ____energyLabel)
    {
        if (__instance.Visibility == MegaCrit.Sts2.Core.Entities.UI.ModelVisibility.Visible && ChrysalisFreeX.Applies(__instance.Model))
            ____energyLabel.SetTextAutoSize("0");
    }
}
