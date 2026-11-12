using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Jigsaw;

internal static class JigsawHuntingUnderdocks
{
    private sealed class OpeningSkittish;
    private static readonly ConditionalWeakTable<SkittishPower, OpeningSkittish> openingSkittish = new();
    private sealed class OpeningColonyPower;
    private static readonly ConditionalWeakTable<PowerModel, OpeningColonyPower> openingColony = new();

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
