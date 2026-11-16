using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Jigsaw;

internal static class JigsawHuntingSoulFysh
{
    private sealed record OpeningSource(RunState Run, CombatRoom Room, EncounterModel Encounter, JigsawNativeSession Session, JigsawRun Map);
    private static readonly ConditionalWeakTable<MonsterModel, OpeningSource> minions = new();

    internal static bool IsHuntedEncounter(IRunState runState, EncounterModel encounter)
    {
        if (encounter is not SoulFyshBoss || runState is not RunState native ||
            !ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), native) ||
            JigsawNativeSession.TryGet(native)?.Map is not { } map ||
            native.Players.FirstOrDefault() is not { } owner || !JigsawHuntingNative.RootIsHunted(owner) ||
            native is not { Act: Underdocks, CurrentRoomCount: 1, CurrentRoom: CombatRoom { ParentEventId: null } room } ||
            !ReferenceEquals(room.Encounter, encounter))
            return false;
        int? nodeId = map.CurrentNode ?? map.PendingEntry?.NodeId;
        return nodeId != null && map.Nodes.Single(node => node.Id == nodeId).Room is JigsawRoom.Boss or JigsawRoom.SecondBoss;
    }

    internal static void Mark(MonsterModel monster, RunState run, EncounterModel encounter) =>
        minions.Add(monster, new OpeningSource(run, (CombatRoom)run.CurrentRoom!, encounter,
            JigsawNativeSession.TryGet(run)!, JigsawNativeSession.TryGet(run)!.Map!));
    internal static bool IsMarked(MonsterModel monster) => minions.TryGetValue(monster, out _);

    internal static async Task GrantOpening(Task original, MonsterModel monster)
    {
        if (!minions.TryGetValue(monster, out var source))
        { await original; return; }
        bool consumed;
        try
        { await original; }
        finally { consumed = minions.Remove(monster); }
        if (!consumed || !ReferenceEquals(JigsawNativeSession.TryGet(source.Run), source.Session) ||
            !ReferenceEquals(source.Session.Map, source.Map) || monster.CombatState is not { } combat || !ReferenceEquals(combat.RunState, source.Run) ||
            !ReferenceEquals(CombatManager.Instance.DebugOnlyGetState(), combat) ||
            !ReferenceEquals(source.Run.CurrentRoom, source.Room) || !ReferenceEquals(source.Room.CombatState, combat) ||
            !ReferenceEquals(combat.Encounter, source.Encounter) || !combat.ContainsCreature(monster.Creature) ||
            !monster.Creature.IsAlive || CombatManager.Instance.IsEnding || !IsHuntedEncounter(source.Run, source.Encounter))
            return;
        await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(), monster.Creature, 1, monster.Creature, null);
    }
}

[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.GenerateMonstersWithSlots))]
internal static class JigsawHuntingSoulFyshEnemies
{
    private static void Postfix(EncounterModel __instance, IRunState runState, ref IReadOnlyList<(MonsterModel, string?)> ____monstersWithSlots)
    {
        if (!JigsawHuntingSoulFysh.IsHuntedEncounter(runState, __instance))
            return;
        var left = ModelDb.Monster<Toadpole>().ToMutable();
        var right = ModelDb.Monster<SludgeSpinner>().ToMutable();
        JigsawHuntingSoulFysh.Mark(left, (RunState)runState, __instance);
        JigsawHuntingSoulFysh.Mark(right, (RunState)runState, __instance);
        // No authored scene slots: the native list positions these unchanged
        // monsters on either side of the same original SoulFysh.
        ____monstersWithSlots = [(left, null), .. ____monstersWithSlots, (right, null)];
    }
}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.AfterAddedToRoom))]
internal static class JigsawHuntingSoulFyshMinionOpening
{
    private static void Postfix(MonsterModel __instance, ref Task __result)
    {
        if (JigsawHuntingSoulFysh.IsMarked(__instance))
            __result = JigsawHuntingSoulFysh.GrantOpening(__result, __instance);
    }
}

// The native slotless row is just wide enough for the three creature visuals,
// but the Soul Fysh's HP bar extends into both adjacent target hitboxes.
// Inset the row and open a narrow gap on each side of the original Boss.
[HarmonyPatch(typeof(NCombatRoom), "PositionEnemies")]
internal static class JigsawHuntingSoulFyshLayout
{
    private static void Postfix(List<NCreature> creatures)
    {
        if (creatures.Count != 3 || creatures[0].Entity.Monster is not Toadpole ||
            creatures[1].Entity.Monster is not SoulFysh || creatures[2].Entity.Monster is not SludgeSpinner ||
            creatures[1].Entity.CombatState is not { } combat ||
            !JigsawHuntingSoulFysh.IsHuntedEncounter(combat.RunState, combat.Encounter))
            return;

        creatures[0].Position += new Vector2(-35, 0);
        creatures[1].Position += new Vector2(-20, 0);
        creatures[2].Position += new Vector2(-5, 0);
    }
}
