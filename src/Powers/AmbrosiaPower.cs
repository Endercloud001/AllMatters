using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Powers;

[RegisterPower]
public sealed class AmbrosiaPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/potions/ambrosia.png",
        BigIconPath: "res://AllMatters/images/potions/ambrosia.png");
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource
#if !STS2_PUBLIC
        , CardPlay? cardPlay
#endif
        ) => dealer == Owner && target?.Side == CombatSide.Enemy && props.IsPoweredAttack() ? 3m : 1m;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}
