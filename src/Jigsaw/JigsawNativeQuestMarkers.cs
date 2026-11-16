using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Jigsaw;

internal static class JigsawNativeQuestMarkers
{
    private sealed class Work
    {
        internal Task Removal = Task.CompletedTask;
    }
    private static readonly ConditionalWeakTable<JigsawNativeSession, Work> work = new();
    internal static void FinalizeMap(JigsawNativeSession session)
    {
        if (session.Map is { } map)
            map.FinalizeSpoils(session.Native.Players.Any(p => p.Deck.Cards.OfType<SpoilsMap>().Any()), session.Native.CurrentActIndex);
    }

    internal static void Prepare(JigsawNativeSession session)
    {
        if (session.Map is not { } map)
            return;
        var pending = work.GetValue(session, _ => new Work());
        if (map.NoChestRemovalPending && pending.Removal.IsCompleted)
        {
            var cards = session.Native.Players.SelectMany(p => p.Deck.Cards.OfType<SpoilsMap>()).ToArray();
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Removal = completion.Task;
            _ = Remove(session, map, cards, completion);
        }
        var coat = session.Native.Players.SelectMany(p => p.Relics.OfType<FurCoat>())
            .FirstOrDefault(c => c.FurCoatActIndex == session.Native.CurrentActIndex);
        if (coat != null)
            map.MarkFur(coat.DynamicVars["Combats"].IntValue);
    }
    internal static Task Drain(JigsawNativeSession session) => work.GetValue(session, _ => new Work()).Removal;
    private static async Task Remove(JigsawNativeSession session, JigsawRun map, SpoilsMap[] cards, TaskCompletionSource completion)
    {
        try
        {
            // This is the same visible RemoveFromDeck command used by Guilty.
            foreach (var card in cards)
            {
                if (JigsawNativeSession.Current != session || !ReferenceEquals(session.Map, map))
                    throw new OperationCanceledException("藏宝图定稿会话已改变。");
                await CardPileCmd.RemoveFromDeck(card);
            }
            if (JigsawNativeSession.Current != session || !ReferenceEquals(session.Map, map))
                throw new OperationCanceledException("藏宝图定稿会话已改变。");
            map.CompleteNoChestRemoval();
            completion.SetResult();
        }
        catch (Exception error) { completion.SetException(error); }
    }
    internal static async Task<int> Pay(Player player, JigsawNativeSession session, JigsawRun map, AbstractRoom? room)
    {
        if (!map.ClaimSpoils(player.NetId))
            return 0;
        int gold = 0;
        foreach (var card in player.Deck.Cards.OfType<SpoilsMap>().ToArray())
        {
            if (JigsawNativeSession.Current != session || !ReferenceEquals(session.Map, map) || !ReferenceEquals(session.Native.CurrentRoom, room))
                throw new OperationCanceledException("藏宝图宝箱已改变。");
            gold += await card.OnQuestComplete();
        }
        return gold;
    }
    internal static bool FurActive(IRunState run) => run is RunState native &&
        JigsawNativeSession.TryGet(native)?.Map is { } map && (map.PendingEntry?.NodeId ?? map.CurrentNode) is { } node && map.HasFurMarker(node);
    internal static Func<bool> FurGuard(IRunState run, ICombatState combat)
    {
        var session = run is RunState native ? JigsawNativeSession.TryGet(native) : null;
        var map = session?.Map;
        long? entry = map?.QuestEntry;
        return () => session != null && JigsawNativeSession.Current == session && ReferenceEquals(session.Map, map) &&
            map?.QuestEntry == entry && session.Native.CurrentRoom is CombatRoom room &&
            ReferenceEquals(room.CombatState, combat) && FurActive(run);
    }
}

[HarmonyPatch(typeof(FurCoat), nameof(FurCoat.AfterObtained))]
internal static class JigsawFurPickup
{
    private static bool Prefix(FurCoat __instance, ref Task __result)
    {
        if (!JigsawNewRunMode.IsEffective(__instance.Owner.RunState))
            return true;
        __instance.FurCoatActIndex = __instance.Owner.RunState.CurrentActIndex;
        if (JigsawNativeSession.TryGet((RunState)__instance.Owner.RunState) is { } session)
            session.OnEdited();
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch]
internal static class JigsawQuestNativeMap
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(FurCoat), nameof(FurCoat.ModifyGeneratedMapLate));
        yield return AccessTools.Method(typeof(SpoilsMap), nameof(SpoilsMap.ModifyGeneratedMap));
        yield return AccessTools.Method(typeof(SpoilsMap), nameof(SpoilsMap.ModifyGeneratedMapLate));
    }
    private static bool Prefix(IRunState runState, ActMap map, ref ActMap __result)
    {
        if (!JigsawNewRunMode.IsEffective(runState))
            return true;
        __result = map;
        return false;
    }
}

[HarmonyPatch(typeof(SpoilsMap), nameof(SpoilsMap.AfterMapGenerated))]
internal static class JigsawSpoilsNativeMapReady
{
    private static bool Prefix(SpoilsMap __instance, ref Task __result)
    {
        if (!JigsawNewRunMode.IsEffective(__instance.Owner.RunState))
            return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(OneOffSynchronizer), "TryHandleSpoilsMap", [typeof(Player)])]
internal static class JigsawSpoilsPayment
{
    private static bool Prefix(Player player, ref Task<int> __result)
    {
        if (!JigsawNewRunMode.IsEffective(player.RunState))
            return true;
        __result = Task.FromResult(0);
        return false;
    }
}

[HarmonyPatch]
internal static class JigsawFurNativeCombat
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(FurCoat), nameof(FurCoat.BeforeCombatStart));
        yield return AccessTools.Method(typeof(FurCoat), nameof(FurCoat.AfterCreatureAddedToCombat));
    }
    private static bool Prefix(FurCoat __instance, ref Task __result)
    {
        if (!JigsawNewRunMode.IsEffective(__instance.Owner.RunState))
            return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class JigsawFurOpening
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(IRunState runState, ICombatState? combatState, ref Task __result)
    {
        if (combatState != null && JigsawNativeQuestMarkers.FurActive(runState))
            __result = Apply(__result, JigsawNativeQuestMarkers.FurGuard(runState, combatState), combatState);
    }
    private static async Task Apply(Task original, Func<bool> active, ICombatState combat)
    {
        await original;
        foreach (var enemy in combat.Enemies.ToArray())
        {
            if (!active())
                return;
            await CreatureCmd.SetCurrentHp(enemy, 1);
        }
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCreatureAddedToCombat))]
internal static class JigsawFurSummon
{
    private static void Postfix(ICombatState combatState, Creature creature, ref Task __result)
    {
        if (creature.Side == CombatSide.Enemy && RunManager.Instance.DebugOnlyGetState() is { } native && JigsawNativeQuestMarkers.FurActive(native))
            __result = Apply(__result, JigsawNativeQuestMarkers.FurGuard(native, combatState), creature);
    }
    private static async Task Apply(Task original, Func<bool> active, Creature creature)
    {
        await original;
        if (active())
            await CreatureCmd.SetCurrentHp(creature, 1);
    }
}
