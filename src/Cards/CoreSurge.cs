using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using AllMatters.Powers;

namespace AllMatters.Cards;

[RegisterCard(typeof(EventCardPool), FullPublicEntry = "ALL_MATTERS_CORE_SURGE")]
public sealed class CoreSurge : ModCardTemplate
{
    public CoreSurge() : base(1, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy) { }
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<DefectCardPool>();
    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://AllMatters/images/card_portraits/core_surge.png");
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<ArtifactPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(11, ValueProp.Move)];
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await Attack(choiceContext, cardPlay);
        await PowerCmd.Apply<ArtifactPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
    private async Task Attack(PlayerChoiceContext context, CardPlay play)
    {
#if STS2_PUBLIC
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(play.Target!).Execute(context);
#else
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target!).Execute(context);
#endif
    }
}
