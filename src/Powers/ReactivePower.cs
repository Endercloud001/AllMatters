using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Powers;

[RegisterPower]
public sealed class ReactivePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/powers/reactive.png", BigIconPath: "res://AllMatters/images/powers/reactive.png");

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && result.UnblockedDamage > 0 && props.IsPoweredAttack()
            && Owner.Monster is Monsters.WrithingMass mass && Owner.IsAlive)
        {
            Flash();
            mass.ReactToAttack();
        }
        return Task.CompletedTask;
    }
}
