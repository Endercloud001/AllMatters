using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
namespace AllMatters.Cards;
[RegisterCard(typeof(EventCardPool), FullPublicEntry = "ALL_MATTERS_ENDLESS_AGONY")]
public sealed class EndlessAgony : ModCardTemplate
{
    public EndlessAgony() : base(0, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy) { }
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<SilentCardPool>();
    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://AllMatters/images/card_portraits/endless_agony.png");
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4, ValueProp.Move)];
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ReturningCardAttack.Single(this, cardPlay, choiceContext);

    }
    protected override void OnUpgrade() { DynamicVars.Damage.UpgradeValueBy(2); }
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card == this) await CardPileCmd.AddGeneratedCardToCombat(CreateClone(), PileType.Hand, Owner);
    }
}
