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
using AllMatters.Powers;
namespace AllMatters.Cards;
[RegisterCard(typeof(EventCardPool), FullPublicEntry = "ALL_MATTERS_OMEGA")]
public sealed class Omega : ModCardTemplate
{
    public Omega() : base(3, CardType.Power, CardRarity.Event, TargetType.Self) { }
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<ColorlessCardPool>();
    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://AllMatters/images/card_portraits/omega.png");
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("EndDamage", 50)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<OmegaPower>()];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<OmegaPower>(choiceContext, Owner.Creature, DynamicVars["EndDamage"].BaseValue, Owner.Creature, this);
    protected override void OnUpgrade() => DynamicVars["EndDamage"].UpgradeValueBy(10);
}
