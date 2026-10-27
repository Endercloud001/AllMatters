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
[RegisterCard(typeof(EventCardPool), FullPublicEntry = "ALL_MATTERS_REAPER")]
public sealed class Reaper : ModCardTemplate
{
    public Reaper() : base(2, CardType.Attack, CardRarity.Event, TargetType.AllEnemies) { }
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<IroncladCardPool>();
    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://AllMatters/images/card_portraits/reaper.png");
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4, ValueProp.Move)];
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
#if STS2_PUBLIC
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).TargetingAllOpponents(CombatState).Execute(choiceContext);
#else
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).TargetingAllOpponents(CombatState).Execute(choiceContext);
#endif
        // Both targets clamp UnblockedDamage to actual HP lost in LoseHpInternal;
        // subtracting OverkillDamage again would under-heal lethal hits.
        decimal heal = attack.Results.SelectMany(results => results).Sum(result => Math.Max(0, result.UnblockedDamage));
        if (heal > 0) await CreatureCmd.Heal(Owner.Creature, heal);
    }
    protected override void OnUpgrade() { DynamicVars.Damage.UpgradeValueBy(2); }

}
