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
[RegisterCard(typeof(EventCardPool), FullPublicEntry = "ALL_MATTERS_DISARM")]
public sealed class Disarm : ModCardTemplate
{
    public Disarm() : base(1, CardType.Skill, CardRarity.Event, TargetType.AnyEnemy) { }
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<IroncladCardPool>();
    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://AllMatters/images/card_portraits/disarm.png");
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("StrengthLoss", 2)];
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<StrengthPower>()];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<StrengthPower>(choiceContext, cardPlay.Target!, -DynamicVars["StrengthLoss"].BaseValue, Owner.Creature, this);
    }
    protected override void OnUpgrade() { DynamicVars["StrengthLoss"].UpgradeValueBy(1); }

}
