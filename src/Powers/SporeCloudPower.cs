using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Powers;

[RegisterPower]
public sealed class SporeCloudPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/powers/spore_cloud.png",
        BigIconPath: "res://AllMatters/images/powers/spore_cloud.png");

    // Like native Infested, this power remains in the death chain until removal.
    // Let its commands settle even when its owner is the last primary enemy.
    public override bool ShouldStopCombatFromEnding() => true;

    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature target,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        if (target != Owner || wasRemovalPrevented)
            return;
        foreach (var player in CombatState.Players.Where(player => player.Creature.IsAlive))
            await PowerCmd.Apply<VulnerablePower>(choiceContext, player.Creature, Amount, Owner, null);
    }
}
