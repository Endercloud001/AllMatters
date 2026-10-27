using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Powers;

[RegisterPower]
public sealed class ElectrodynamicsPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerAssetProfile AssetProfile => new(IconPath: "res://AllMatters/images/powers/electrodynamics.png", BigIconPath: "res://AllMatters/images/powers/electrodynamics.png");
}

[RegisterPower]
public sealed class LockOnPower : ModPowerTemplate
{
    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => new(IconPath: "res://AllMatters/images/powers/lock_on.png", BigIconPath: "res://AllMatters/images/powers/lock_on.png");
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner)) await PowerCmd.Decrement(this);
    }
}

// Keep the target's native damage pipeline. Only Lightning/Dark orb commands
// apply Lock-On, and only for the player who marked that enemy.
internal static class ReturningOrbDamage
{
    internal static decimal Amount(OrbModel orb, Creature target, decimal amount)
        => target.Powers.OfType<LockOnPower>().Any(mark => mark.Applier == orb.Owner.Creature) ? amount * 1.5m : amount;
}

[HarmonyPatch(typeof(LightningOrb), "ApplyLightningDamage")]
internal static class ElectrodynamicsLightning
{
    private static bool Prefix(LightningOrb __instance, decimal value, Creature? target, PlayerChoiceContext choiceContext,
#if !STS2_PUBLIC
        bool isEvoke,
#endif
        ref Task<IEnumerable<Creature>> __result)
    {
        var enemies = __instance.CombatState.GetOpponentsOf(__instance.Owner.Creature).Where(e => e.IsHittable).ToArray();
        if (!__instance.Owner.Creature.HasPower<ElectrodynamicsPower>() && !enemies.Any(e => e.Powers.OfType<LockOnPower>().Any(mark => mark.Applier == __instance.Owner.Creature))) return true;
        var targets = __instance.Owner.Creature.HasPower<ElectrodynamicsPower>() ? enemies
            : target != null ? [target] : enemies.Length == 0 ? [] : new[] { __instance.Owner.RunState.Rng.CombatTargets.NextItem(enemies) };
        __result = Hit(__instance, targets, value, choiceContext,
#if STS2_PUBLIC
            false
#else
            isEvoke
#endif
        );
        return false;
    }

    private static async Task<IEnumerable<Creature>> Hit(LightningOrb orb, Creature[] targets, decimal value, PlayerChoiceContext context, bool evoke)
    {
#if !STS2_PUBLIC
        if (evoke) AccessTools.Method(typeof(OrbModel), "ActivateEvoke").Invoke(orb, [targets]);
#endif
        if (targets.Length == 0) return targets;
        AccessTools.Method(typeof(OrbModel), "PlayEvokeSfx").Invoke(orb, null);
        foreach (var enemy in targets)
        {
            VfxCmd.PlayOnCreature(enemy, "vfx/vfx_attack_lightning");
            await CreatureCmd.Damage(context, enemy, ReturningOrbDamage.Amount(orb, enemy, value), ValueProp.Unpowered, orb.Owner.Creature);
        }
        return targets;
    }
}

[HarmonyPatch(typeof(DarkOrb), nameof(DarkOrb.Evoke))]
internal static class LockOnDark
{
    private static bool Prefix(DarkOrb __instance, PlayerChoiceContext playerChoiceContext, ref Task<IEnumerable<Creature>> __result)
    {
        var target = __instance.CombatState.HittableEnemies.MinBy(e => e.CurrentHp);
        if (target == null || !target.Powers.OfType<LockOnPower>().Any(mark => mark.Applier == __instance.Owner.Creature)) return true;
        __result = Hit(__instance, target, playerChoiceContext);
        return false;
    }
    private static async Task<IEnumerable<Creature>> Hit(DarkOrb orb, Creature target, PlayerChoiceContext context)
    {
        AccessTools.Method(typeof(OrbModel), "PlayEvokeSfx").Invoke(orb, null);
#if !STS2_PUBLIC
        AccessTools.Method(typeof(OrbModel), "ActivateEvoke").Invoke(orb, [new[] { target }]);
#endif
        await CreatureCmd.Damage(context, target, ReturningOrbDamage.Amount(orb, target, orb.EvokeVal), ValueProp.Unpowered, orb.Owner.Creature);
        return [target];
    }
}
