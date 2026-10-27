using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.HoverTips;

namespace AllMatters.Events;

[HarmonyPatch]
internal static class NativeLightBranchVars
{
    private static IEnumerable<MethodBase> TargetMethods()
        => new[] { typeof(SunkenTreasury), typeof(WoodCarvings) }.Select(t => AccessTools.PropertyGetter(t, "CanonicalVars"));
    private static void Postfix(ref IEnumerable<DynamicVar> __result)
        => __result = __result.Append(new DynamicVar("AmBranch", 0)).ToArray();
}

[HarmonyPatch(typeof(SunkenTreasury), nameof(SunkenTreasury.CalculateVars))]
internal static class TreasuryBranch
{
    private static void Postfix(SunkenTreasury __instance)
    {
        bool two = __instance.Rng.NextInt(2) == 1;
        __instance.DynamicVars["AmBranch"].BaseValue = two ? 2 : 1;
        __instance.DynamicVars["LargeChestGold"].BaseValue = two ? __instance.Rng.NextInt(455, 545) : __instance.Rng.NextInt(303, 364);
    }
}

[HarmonyPatch(typeof(SunkenTreasury), "SecondChest")]
internal static class TreasuryChest
{
    private static bool Prefix(SunkenTreasury __instance, ref Task __result)
    {
        __result = Open(__instance);
        return false;
    }
    private static async Task Open(SunkenTreasury model)
    {
        await PlayerCmd.GainGold(model.DynamicVars["LargeChestGold"].BaseValue, model.Owner!);
        for (int i = 0; i < model.DynamicVars["AmBranch"].IntValue; i++) await CardPileCmd.AddCurseToDeck<Greed>(model.Owner!);
        var loc = new LocString("events", "SUNKEN_TREASURY.pages.SECOND_CHEST.description");
        loc.Add("Monologue", new LocString("characters", model.Owner!.Character.Id.Entry + ".goldMonologue"));
        AccessTools.Method(typeof(EventModel), "SetEventFinished").Invoke(model, [loc]);
    }
}

[HarmonyPatch]
internal static class WoodSnakeBranch
{
    // CalculateVars is inherited: Harmony must target the base method and filter.
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(EventModel), nameof(EventModel.CalculateVars));
    private static void Postfix(EventModel __instance)
    {
        if (__instance is WoodCarvings)
            __instance.DynamicVars["AmBranch"].BaseValue = __instance.Rng.NextInt(2);
    }
}

[HarmonyPatch(typeof(WoodCarvings), "GenerateInitialOptions")]
internal static class WoodSnakeOption
{
    private static void Postfix(WoodCarvings __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (__instance.DynamicVars["AmBranch"].IntValue != 1) return;
        var list = __result.ToList();
        list[1] = NativeEventActions.Option(__instance, "WOOD_CARVINGS.pages.INITIAL.options.AM_OIL", async () =>
        {
            await PotionCmd.TryToProcure<SneckoOil>(__instance.Owner!);
            NativeEventActions.Finish(__instance, "WOOD_CARVINGS.pages.SNAKE.description");
        }, HoverTipFactory.FromPotion<SneckoOil>());
        __result = list;
    }
}

[HarmonyPatch]
internal static class PunchActPools
{
    private static IEnumerable<MethodBase> TargetMethods() => typeof(ActModel).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(ActModel)))
        .Select(t => t.GetProperty("AllEvents", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)?.GetMethod)
        .OfType<MethodBase>();
    private static void Postfix(ActModel __instance, ref IEnumerable<EventModel> __result)
    {
        if (__instance.Index is 0 or 1) __result = __result.Append(ModelDb.Event<PunchOff>()).Append(ModelDb.Event<WhisperingHollow>())
            .Append(ModelDb.Event<DenseVegetation>()).DistinctBy(e => e.Id);
    }
}

// The native event hands out a SpecialCardReward only after victory. Healing
// follows successful delivery, so declining, defeat and other special cards do not heal.
[HarmonyPatch(typeof(SpecialCardReward), "OnSelect")]
internal static class LanternKeyHealing
{
    private static void Postfix(SpecialCardReward __instance, CardModel ____card, ref Task<bool> __result)
    {
        if (____card is LanternKey && __instance.Player.RunState.CurrentRoom is CombatRoom { Encounter: MysteriousKnightEventEncounter })
            __result = Heal(__result, __instance, ____card);
    }
    private static async Task<bool> Heal(Task<bool> original, SpecialCardReward reward, CardModel card)
    {
        bool selected = await original;
        if (selected && reward.Player.Deck.Cards.Contains(card)) await CreatureCmd.Heal(reward.Player.Creature, 20);
        return selected;
    }
}

[HarmonyPatch(typeof(PunchOff), nameof(PunchOff.IsAllowed))]
internal static class PunchAdmission
{
    private static void Postfix(MegaCrit.Sts2.Core.Runs.IRunState runState, ref bool __result)
        => __result &= runState.CurrentActIndex is 0 or 1;
}
