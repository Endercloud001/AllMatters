using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using System.Reflection;

// The host supplies an active combat without starting the game's UI/network action loop.
// Card models, factories, RNG, hooks, power application and pile commands remain real.
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.IsInProgress), MethodType.Getter)]
internal static class HeadlessCombatInProgress
{
    private static bool Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.IsEnding), MethodType.Getter)]
internal static class HeadlessCombatNotEnding
{
    private static bool Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}

// The public branch routes empty-hand notifications through the absent global room.
[HarmonyPatch]
internal static class HeadlessEmptyHandNotification
{
    private static IEnumerable<MethodBase> TargetMethods() => typeof(CombatManager).GetMethods()
        .Where(method => method.Name == nameof(CombatManager.CheckForEmptyHand));

    private static bool Prefix(ref Task __result)
    {
        __result = Task.CompletedTask;
        return false;
    }
}
