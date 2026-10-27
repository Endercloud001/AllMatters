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

[RegisterCard(typeof(EventCardPool), FullPublicEntry = "ALL_MATTERS_BULLSEYE")]
public sealed class Bullseye : ModCardTemplate
{
    public Bullseye() : base(1, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy) { }
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<DefectCardPool>();
    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://AllMatters/images/card_portraits/bullseye.png");
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<LockOnPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move), new DynamicVar("LockOn", 2)];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await Attack(choiceContext, cardPlay);
        await PowerCmd.Apply<LockOnPower>(choiceContext, cardPlay.Target!, DynamicVars["LockOn"].BaseValue, Owner.Creature, this);
    }
    protected override void OnUpgrade() { DynamicVars.Damage.UpgradeValueBy(3); DynamicVars["LockOn"].UpgradeValueBy(1); }
    private async Task Attack(PlayerChoiceContext context, CardPlay play)
    {
#if STS2_PUBLIC
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(play.Target!).Execute(context);
#else
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target!).Execute(context);
#endif
    }
}
