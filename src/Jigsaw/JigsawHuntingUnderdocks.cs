using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Lagavulin = AllMatters.Monsters.Lagavulin;

namespace AllMatters.Jigsaw;

internal static class JigsawHuntingUnderdocks
{
    private sealed class OpeningSkittish;
    private static readonly ConditionalWeakTable<SkittishPower, OpeningSkittish> openingSkittish = new();
    private sealed class OpeningColonyPower;
    private static readonly ConditionalWeakTable<PowerModel, OpeningColonyPower> openingColony = new();
    private static readonly ConditionalWeakTable<Lagavulin, object> matriarchMinions = new();

    internal static bool IsHuntedMatriarchEncounter(IRunState runState, EncounterModel encounter)
    {
        if (encounter is not LagavulinMatriarchBoss || runState is not RunState native ||
            !ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), native) ||
            JigsawNativeSession.TryGet(native)?.Map is not { } map ||
            native.Players.FirstOrDefault() is not { } owner || !JigsawHuntingNative.RootIsHunted(owner) ||
            native is not { Act: Underdocks, CurrentRoomCount: 1, CurrentRoom: CombatRoom { ParentEventId: null } room } ||
            !ReferenceEquals(room.Encounter, encounter))
            return false;
        int? nodeId = map.CurrentNode ?? map.PendingEntry?.NodeId;
        return nodeId != null && map.Nodes.Single(node => node.Id == nodeId).Room is JigsawRoom.Boss or JigsawRoom.SecondBoss;
    }

    internal static void MarkMatriarchMinion(Lagavulin monster) => matriarchMinions.Add(monster, new object());
    internal static bool IsMatriarchMinion(Lagavulin monster) => matriarchMinions.TryGetValue(monster, out _);

    internal static async Task GrantMatriarchMinion(Task original, Lagavulin monster)
    {
        await original;
        if (!matriarchMinions.Remove(monster) || monster.CombatState is not { } combat ||
            !combat.ContainsCreature(monster.Creature) || !monster.Creature.IsAlive || CombatManager.Instance.IsEnding ||
            combat.RunState.CurrentRoom is not CombatRoom room || !ReferenceEquals(room.CombatState, combat) ||
            !IsHuntedMatriarchEncounter(combat.RunState, combat.Encounter))
            return;
        await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(), monster.Creature, 1, monster.Creature, null);
    }

    internal static bool IsHuntedCombat(MonsterModel monster) =>
        monster.CombatState.RunState is { Act: Underdocks, CurrentRoomCount: 1, CurrentRoom: CombatRoom room } native &&
        room.ParentEventId == null && room.RoomType is RoomType.Monster or RoomType.Elite &&
        ReferenceEquals(room.CombatState, monster.CombatState) && native.Players.FirstOrDefault() is { } player &&
        JigsawHuntingNative.RootIsHunted(player);

    internal static async Task GrantColonyOpening(SkulkingColony colony)
    {
        // Native base initialization is empty. Keep its single shell grant and
        // append Plating, each with an exemption for this exact mutable source.
        await Grant(ModelDb.Power<HardenedShellPower>().ToMutable(), 20);
        await Grant(ModelDb.Power<PlatingPower>().ToMutable(), 6);
        async Task Grant(PowerModel power, int amount)
        {
            openingColony.Add(power, new OpeningColonyPower());
            try
            { await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), power, colony.Creature, amount, colony.Creature, null); }
            finally { openingColony.Remove(power); }
        }
    }

    internal static bool ConsumeColonyOpening(PowerModel power) => openingColony.Remove(power);

    internal static bool IsHuntedElite(MonsterModel monster) =>
        monster.CombatState.RunState is { Act: Underdocks, CurrentRoomCount: 1, CurrentRoom: CombatRoom room } native &&
        room.ParentEventId == null && room.RoomType == RoomType.Elite &&
        ReferenceEquals(room.CombatState, monster.CombatState) && native.Players.FirstOrDefault() is { } player &&
        JigsawHuntingNative.RootIsHunted(player);

    internal static async Task GrantGardenerSkittish(PhantasmalGardener gardener)
    {
        // Both versions' native initializer awaits an empty base initializer,
        // then grants this one power. Preserve one native grant and its history.
        var power = (SkittishPower)ModelDb.Power<SkittishPower>().ToMutable();
        openingSkittish.Add(power, new OpeningSkittish());
        try
        {
            await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), power, gardener.Creature, 9, gardener.Creature, null);
        }
        finally { openingSkittish.Remove(power); }
    }

    internal static bool ConsumeOpeningSkittish(SkittishPower power) => openingSkittish.Remove(power);

    internal static bool IsHuntedMatriarchBoss(LagavulinMatriarch monster) =>
        monster.CombatState.RunState is { Act: Underdocks, CurrentRoomCount: 1, CurrentRoom: CombatRoom room } native &&
        room.ParentEventId == null && room.Encounter is LagavulinMatriarchBoss &&
        ReferenceEquals(room.CombatState, monster.CombatState) && native.Players.FirstOrDefault() is { } player &&
        JigsawHuntingNative.RootIsHunted(player);
}

[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.GenerateMonstersWithSlots))]
internal static class JigsawHuntingMatriarchEnemies
{
    private static void Postfix(EncounterModel __instance, IRunState runState, ref IReadOnlyList<(MonsterModel, string?)> ____monstersWithSlots)
    {
        if (!JigsawHuntingUnderdocks.IsHuntedMatriarchEncounter(runState, __instance))
            return;
        var minion = (Lagavulin)ModelDb.Monster<Lagavulin>().ToMutable();
        JigsawHuntingUnderdocks.MarkMatriarchMinion(minion);
        // This encounter has no authored slots. Native list positioning puts
        // the added monster left of the untouched original Matriarch.
        ____monstersWithSlots = [(minion, null), .. ____monstersWithSlots];
    }
}

[HarmonyPatch(typeof(Lagavulin), nameof(Lagavulin.AfterAddedToRoom))]
internal static class JigsawHuntingMatriarchMinionOpening
{
    private static void Postfix(Lagavulin __instance, ref Task __result)
    {
        if (JigsawHuntingUnderdocks.IsMatriarchMinion(__instance))
            __result = JigsawHuntingUnderdocks.GrantMatriarchMinion(__result, __instance);
    }
}

[HarmonyPatch(typeof(LagavulinMatriarch), nameof(LagavulinMatriarch.AfterAddedToRoom))]
internal static class JigsawHuntingMatriarchHealth
{
    private static void Postfix(LagavulinMatriarch __instance, ref Task __result)
    {
        if (JigsawHuntingUnderdocks.IsHuntedMatriarchBoss(__instance))
            __result = HalveHealth(__result, __instance);
    }

    private static async Task HalveHealth(Task native, LagavulinMatriarch monster)
    {
        await native;
        if (JigsawHuntingUnderdocks.IsHuntedMatriarchBoss(monster))
            await CreatureCmd.SetMaxAndCurrentHp(monster.Creature, monster.Creature.MaxHp / 2);
    }
}

[HarmonyPatch(typeof(SkulkingColony), nameof(SkulkingColony.AfterAddedToRoom))]
internal static class JigsawHuntingColonyOpening
{
    private static bool Prefix(SkulkingColony __instance, ref Task __result)
    {
        if (!JigsawHuntingUnderdocks.IsHuntedCombat(__instance))
            return true;
        __result = JigsawHuntingUnderdocks.GrantColonyOpening(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class JigsawHuntingColonyFixedPower
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
    [ AccessTools.Method(typeof(PowerModel), nameof(PowerModel.GetScaledAmountForMultiplayer)),
      AccessTools.Method(typeof(PlatingPower), nameof(PlatingPower.GetScaledAmountForMultiplayer)) ];

    private static bool Prefix(PowerModel __instance, decimal amount, ref decimal __result)
    {
        if (!JigsawHuntingUnderdocks.ConsumeColonyOpening(__instance))
            return true;
        __result = amount;
        return false;
    }
}

[HarmonyPatch(typeof(TerrorEel), "TerrorMove")]
internal static class JigsawHuntingEelTerror
{
    private static void Postfix(TerrorEel __instance, IReadOnlyList<Creature> targets, ref Task __result)
    {
        if (JigsawHuntingUnderdocks.IsHuntedCombat(__instance))
            __result = AddWeakness(__result, __instance, targets);
    }

    private static async Task AddWeakness(Task native, TerrorEel eel, IReadOnlyList<Creature> targets)
    {
        await native;
        await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), targets, 99, eel.Creature, null);
    }
}

[HarmonyPatch(typeof(PhantasmalGardener), nameof(PhantasmalGardener.AfterAddedToRoom))]
internal static class JigsawHuntingGardenerOpening
{
    private static bool Prefix(PhantasmalGardener __instance, ref Task __result)
    {
        if (!JigsawHuntingUnderdocks.IsHuntedElite(__instance))
            return true;
        __result = JigsawHuntingUnderdocks.GrantGardenerSkittish(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(SkittishPower), nameof(SkittishPower.GetScaledAmountForMultiplayer))]
internal static class JigsawHuntingGardenerFixedSkittish
{
    private static bool Prefix(SkittishPower __instance, decimal amount, ref decimal __result)
    {
        if (!JigsawHuntingUnderdocks.ConsumeOpeningSkittish(__instance))
            return true;
        __result = amount;
        return false;
    }
}
