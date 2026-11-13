using AllMatters.Monsters;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using System.Runtime.CompilerServices;

namespace AllMatters.Jigsaw;

internal static class JigsawHuntingQueen
{
    internal const string ScenePath = "res://AllMatters/encounters/hunting_queen.tscn";
    internal const string LeftTorch = "all_matters_torch_left";
    internal const string RightTorch = "all_matters_torch_right";
    private static readonly ConditionalWeakTable<Queen, Fight> fights = new();

    private sealed class Fight(Queen queen, ICombatState combat, RunState run,
        JigsawNativeSession session, JigsawRun map, CombatRoom room, Creature amalgam, MoveState rekindle)
    {
        internal readonly Queen Queen = queen;
        internal readonly ICombatState Combat = combat;
        internal readonly RunState Run = run;
        internal readonly JigsawNativeSession Session = session;
        internal readonly JigsawRun Map = map;
        internal readonly CombatRoom Room = room;
        internal readonly Creature Amalgam = amalgam;
        internal readonly MoveState Rekindle = rekindle;
        internal readonly List<Creature> Torches = [];
        internal bool Queued;
        internal bool Summoned;

        internal bool IsCurrent => ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), Run) &&
            ReferenceEquals(JigsawNativeSession.TryGet(Run), Session) && ReferenceEquals(Session.Map, Map) &&
            ReferenceEquals(Run.CurrentRoom, Room) && ReferenceEquals(Room.CombatState, Combat) &&
            Combat.Encounter is { } encounter && IsHuntedEncounter(encounter, Run) &&
            ReferenceEquals(Queen.Creature.CombatState, Combat) && Combat.ContainsCreature(Queen.Creature) &&
            Queen.Creature.IsAlive && !CombatManager.Instance.IsEnding;
    }

    internal static bool IsHuntedEncounter(EncounterModel encounter, IRunState native)
    {
        if (encounter is not QueenBoss || native is not RunState run ||
            !ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), run) || run.Players.Count == 0 ||
            run.CurrentRoomCount != 1 || run.CurrentRoom is not CombatRoom { ParentEventId: null } room ||
            !ReferenceEquals(room.Encounter, encounter) || JigsawNativeSession.TryGet(run)?.Map is not { } map ||
            !JigsawHuntingNative.RootIsHunted(run.Players[0]) || run.Act is not Glory)
            return false;
        int? node = map.CurrentNode ?? map.PendingEntry?.NodeId;
        return node != null && map.Nodes.Single(item => item.Id == node).Room is JigsawRoom.Boss or JigsawRoom.SecondBoss;
    }

    internal static void AddMove(Queen queen, MonsterMoveStateMachine machine)
    {
        if (!queen.IsMutable || queen.Creature is not { CombatState: { } combat } ||
            combat.RunState is not RunState run || combat.Encounter is not { } encounter || !IsHuntedEncounter(encounter, run))
            return;
        var amalgam = combat.Enemies.FirstOrDefault(enemy => enemy.Monster is TorchHeadAmalgam);
        if (amalgam == null)
            return;
        var rekindle = new MoveState("REKINDLE", _ => Rekindle(queen), new BuffIntent())
        {
            MustPerformOnceBeforeTransitioning = true,
            FollowUpState = machine.States["ENRAGE_MOVE"]
        };
        machine.States.Add(rekindle.Id, rekindle);
        fights.Add(queen, new Fight(queen, combat, run, JigsawNativeSession.TryGet(run)!,
            JigsawNativeSession.TryGet(run)!.Map!, (CombatRoom)run.CurrentRoom!, amalgam, rekindle));
    }

    internal static bool AllowNativeDeath(Queen queen, Creature creature, bool prevented) =>
        !fights.TryGetValue(queen, out var fight) || !fight.IsCurrent || creature.Monster is not TorchHeadAmalgam ||
        (!prevented && !fight.Queued && ReferenceEquals(creature, fight.Amalgam));

    internal static async Task AfterDeath(Task original, Queen queen, Creature creature, bool prevented)
    {
        await original;
        if (prevented || !fights.TryGetValue(queen, out var fight) || !fight.IsCurrent || fight.Queued ||
            !ReferenceEquals(creature, fight.Amalgam) || creature.IsAlive)
            return;
        fight.Queued = true;
        queen.SetMoveImmediate(fight.Rekindle, true);
    }

    private static async Task Rekindle(Queen queen)
    {
        if (!fights.TryGetValue(queen, out var fight) || !fight.IsCurrent || !fight.Queued || fight.Summoned)
            return;
        fight.Summoned = true;
        await CreatureCmd.TriggerAnim(queen.Creature, "Cast", 0.5f);
        foreach (string slot in new[] { LeftTorch, RightTorch })
        {
            if (!fight.IsCurrent)
                return;
            var torch = fight.Combat.CreateCreature(ModelDb.Monster<TorchHead>().ToMutable(), CombatSide.Enemy, slot);
            // Track ownership before the awaited native add, including partial
            // native command completion. An interrupted fight never retries.
            fight.Torches.Add(torch);
            await CreatureCmd.Add(torch);
        }
    }

    internal static async Task AfterEnrage(Task original, Queen queen)
    {
        fights.TryGetValue(queen, out var fight);
        await original;
        if (fight == null || !fight.IsCurrent || !fight.Summoned)
            return;
        foreach (var torch in fight.Torches)
        {
            if (!fight.IsCurrent)
                return;
            if (torch.IsAlive && fight.Combat.ContainsCreature(torch) && ReferenceEquals(torch.CombatState, fight.Combat))
                await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), torch, 2, queen.Creature, null);
        }
    }
}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.SetUpForCombat))]
internal static class JigsawQueenRekindleMove
{
    private static void Postfix(MonsterModel __instance)
    {
        // GenerateMoveStateMachine also serves intent/asset previews. Bind only
        // the actual machine that native combat setup assigned to this Queen.
        if (__instance is Queen queen && queen.MoveStateMachine is { } machine)
            JigsawHuntingQueen.AddMove(queen, machine);
    }
}

[HarmonyPatch(typeof(Queen), nameof(Queen.AfterDeath))]
internal static class JigsawQueenAmalgamDeath
{
    private static bool Prefix(Queen __instance, Creature creature, bool wasRemovalPrevented, ref Task __result)
    {
        if (JigsawHuntingQueen.AllowNativeDeath(__instance, creature, wasRemovalPrevented))
            return true;
        __result = Task.CompletedTask;
        return false;
    }

    private static void Postfix(Queen __instance, Creature creature, bool wasRemovalPrevented, ref Task __result) =>
        __result = JigsawHuntingQueen.AfterDeath(__result, __instance, creature, wasRemovalPrevented);
}

[HarmonyPatch(typeof(Queen), "EnrageMove")]
internal static class JigsawQueenTorchEnrage
{
    private static void Postfix(Queen __instance, ref Task __result) => __result = JigsawHuntingQueen.AfterEnrage(__result, __instance);
}

[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.GetAssetPaths))]
internal static class JigsawQueenRekindleAssets
{
    private static void Postfix(EncounterModel __instance, IRunState runState, ref IEnumerable<string> __result)
    {
        if (JigsawHuntingQueen.IsHuntedEncounter(__instance, runState))
            __result = __result.Concat(ModelDb.Monster<TorchHead>().AssetPaths).Append(JigsawHuntingQueen.ScenePath).Distinct();
    }
}

[HarmonyPatch(typeof(QueenBoss), nameof(QueenBoss.Slots), MethodType.Getter)]
internal static class JigsawQueenRekindleSlots
{
    private static void Postfix(QueenBoss __instance, ref IReadOnlyList<string> __result)
    {
        if (RunManager.Instance.DebugOnlyGetState() is { } run && JigsawHuntingQueen.IsHuntedEncounter(__instance, run))
            __result = ["amalgam", JigsawHuntingQueen.LeftTorch, JigsawHuntingQueen.RightTorch, "queen"];
    }
}

[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.CreateScene))]
internal static class JigsawQueenRekindleScene
{
    private static bool Prefix(EncounterModel __instance, ref Control __result)
    {
        if (RunManager.Instance.DebugOnlyGetState() is not { } run || !JigsawHuntingQueen.IsHuntedEncounter(__instance, run))
            return true;
        __result = PreloadManager.Cache.GetScene(JigsawHuntingQueen.ScenePath).Instantiate<Control>();
        return false;
    }
}
