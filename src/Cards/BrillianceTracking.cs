using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace AllMatters.Cards;

internal static class BrillianceTracking
{
    // Key by the actual combat and creature: a new combat starts at zero, while
    // normal replay rebuilds the total from the same power-change commands.
    private static readonly ConditionalWeakTable<ICombatState, Dictionary<Creature, int>> Totals = new();
    internal static int Total(Creature owner) => owner.CombatState != null && Totals.TryGetValue(owner.CombatState, out var totals)
        ? totals.GetValueOrDefault(owner) : 0;
    internal static void Add(Creature owner, int amount)
    {
        if (owner.CombatState == null) return;
        var totals = Totals.GetOrCreateValue(owner.CombatState);
        totals[owner] = totals.GetValueOrDefault(owner) + amount;
    }
    internal sealed record Previous(int Amount);
    internal static readonly ConditionalWeakTable<PowerModel, Previous> Before = new();
    internal sealed record Restoration(Creature Owner, Type PowerType);
    internal static readonly AsyncLocal<Restoration?> NaturalRestoration = new();

    internal static int Gained(PowerModel power)
    {
        if (power.Owner.Player == null || power.TypeForCurrentAmount != PowerType.Buff) return 0;
        if (NaturalRestoration.Value is { } restoration && restoration.Owner == power.Owner && restoration.PowerType == power.GetType()) return 0;
        int previous = Before.TryGetValue(power, out var before) ? Math.Max(0, before.Amount) : 0;
        int current = Math.Max(0, power.Amount);
        return power.StackType == PowerStackType.Single ? (previous == 0 && current > 0 ? 1 : 0) : Math.Max(0, current - previous);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforePowerAmountChanged))]
internal static class BrillianceBeforePower
{
    private static void Prefix(PowerModel power)
    {
        BrillianceTracking.Before.Remove(power);
        BrillianceTracking.Before.Add(power, new(power.Amount));
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterPowerAmountChanged))]
internal static class BrillianceAfterPower
{
    private static void Prefix(PowerModel power, out int __state) => __state = BrillianceTracking.Gained(power);
    private static void Postfix(PowerModel power, int __state, ref Task __result)
    {
        if (__state > 0) __result = Record(__result, power.Owner, __state);
    }
    private static async Task Record(Task original, Creature owner, int gained)
    {
        await original;
        BrillianceTracking.Add(owner, gained);
    }
}

[HarmonyPatch]
internal static class BrillianceNaturalRestoration
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { typeof(TemporaryStrengthPower), typeof(TemporaryDexterityPower), typeof(TemporaryFocusPower) }
        .Select(t => AccessTools.Method(t, "AfterSideTurnEnd"));
    private static void Prefix(PowerModel __instance, out BrillianceTracking.Restoration? __state)
    {
        __state = BrillianceTracking.NaturalRestoration.Value;
        if (__instance.Type == PowerType.Debuff)
        {
            Type type = __instance switch { TemporaryStrengthPower => typeof(StrengthPower), TemporaryDexterityPower => typeof(DexterityPower), _ => typeof(FocusPower) };
            BrillianceTracking.NaturalRestoration.Value = new(__instance.Owner, type);
        }
    }
    private static void Postfix(BrillianceTracking.Restoration? __state) => BrillianceTracking.NaturalRestoration.Value = __state;
}
