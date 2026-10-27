using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
namespace AllMatters.Powers;
[RegisterPower]
public sealed class TalkToTheHandPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;
    public override PowerAssetProfile AssetProfile => new(IconPath: "res://AllMatters/images/powers/talk_to_the_hand.png", BigIconPath: "res://AllMatters/images/powers/talk_to_the_hand.png");
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && dealer?.IsPlayer == true && dealer != target && props.IsPoweredAttack() && Applier?.IsAlive == true)
            await CreatureCmd.GainBlock(Applier, Amount, ValueProp.Unpowered, null);
    }
}
[RegisterPower]
public sealed class FastingPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => new(IconPath: "res://AllMatters/images/powers/fasting.png", BigIconPath: "res://AllMatters/images/powers/fasting.png");
    public override Task AfterEnergyReset(Player player)
        => player == Owner.Player ? PlayerCmd.LoseEnergy(Amount, player) : Task.CompletedTask;
}
[RegisterPower]
public sealed class OmegaPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => new(IconPath: "res://AllMatters/images/powers/omega.png", BigIconPath: "res://AllMatters/images/powers/omega.png");
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner)) return;
        foreach (var target in Owner.CombatState!.GetOpponentsOf(Owner).Where(c => c.IsHittable).ToArray())
            await CreatureCmd.Damage(choiceContext, target, Amount, ValueProp.Unpowered, Owner);
    }
}
