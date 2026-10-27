using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Cards;

[RegisterCard(typeof(EventCardPool), FullPublicEntry = "ALL_MATTERS_BRILLIANCE")]
public sealed class Brilliance : ModCardTemplate
{
    public Brilliance() : base(2, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://AllMatters/images/card_portraits/brilliance.png");
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(0), new ExtraDamageVar(1),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier((card, _) => BrillianceTracking.Total(card.Owner.Creature))
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
#if STS2_PUBLIC
        await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCard(this).Targeting(cardPlay.Target).Execute(choiceContext);
#else
        await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute(choiceContext);
#endif
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
