// SPDX-License-Identifier: GPL-3.0-only
// Adapted from Balance MutagenicDecayPower (GPLv3); delay the entire loss to turn three.
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Powers;

[RegisterPower]
public sealed class MutagenicDecayPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/powers/mutagenic_decay.png",
        BigIconPath: "res://AllMatters/images/powers/big/mutagenic_decay.png");

    // Amount is the remaining number of owner turns, using the native power lifecycle.
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Amount <= 0) return;
        if (Amount == 1)
        {
            Flash();
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, -3, Owner, null);
        }
        await PowerCmd.Decrement(this);
    }
}
