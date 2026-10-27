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
public sealed class ShiftingPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/powers/shifting.png", BigIconPath: "res://AllMatters/images/powers/shifting.png");
    private decimal _strengthLost;

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || result.UnblockedDamage <= 0 ) return;
        Flash();
        _strengthLost += result.UnblockedDamage;
        await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(choiceContext, Owner, -result.UnblockedDamage, Owner, null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || _strengthLost == 0 ) return;
        decimal restore = _strengthLost;
        _strengthLost = 0;
        await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(choiceContext, Owner, restore, Owner, null);
    }
}
