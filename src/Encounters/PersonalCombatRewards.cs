using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.RunData;

namespace AllMatters.Encounters;

// Keep eligibility through victory cleanup, when powers have already expired.
// Player combat reset is the boundary for both map and event fights.
public static class PersonalCombatRewards
{
    public sealed class EligibilityState { public bool Forfeited { get; set; } }
    private static readonly PlayerRunSavedData<EligibilityState> State =
        RunSavedDataStore.For(ModEntry.ModId).RegisterPerPlayer<EligibilityState>("combatRewardEligibility");
    internal static void Initialize() => _ = State;
    public static bool IsEligible(Player player) => !State.Get(player).Forfeited;
    public static void Forfeit(Player player) => State.Modify(player, state => state.Forfeited = true);
    internal static void Reset(Player player)
    {
        if (player.RunState is MegaCrit.Sts2.Core.Runs.RunState)
            State.Modify(player, state => state.Forfeited = false);
    }
    internal static void Filter(RewardsSet rewards)
    {
        if (rewards.Room is CombatRoom && !IsEligible(rewards.Player)) rewards.Rewards.Clear();
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.ResetCombatState))]
internal static class PersonalCombatRewardsReset
{
    private static void Postfix(Player __instance) => PersonalCombatRewards.Reset(__instance);
}

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class PersonalCombatRewardsFromRoom
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(RewardsSet __instance) => PersonalCombatRewards.Filter(__instance);
}

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.GenerateWithoutOffering))]
internal static class PersonalCombatRewardsGenerated
{
    private static void Postfix(RewardsSet __instance, ref Task __result) => __result = Filter(__instance, __result);
    private static async Task Filter(RewardsSet rewards, Task original)
    {
        await original;
        PersonalCombatRewards.Filter(rewards);
    }
}
