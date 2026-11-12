using AllMatters.Monsters;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using System.Runtime.CompilerServices;
using Godot;

namespace AllMatters.Jigsaw;

internal static class JigsawHuntingCombat
{
    internal const string AdditionSlot = "all_matters_hunting_addition";
    private static readonly ConditionalWeakTable<EncounterModel, object> layouts = new();
    internal static void RegisterLayout(EncounterModel encounter) => layouts.GetValue(encounter, _ => new object());
    internal static bool HasLayout(EncounterModel encounter) => layouts.TryGetValue(encounter, out _);
    internal static bool IsVantomBossRoot(IRunState native, EncounterModel encounter)
    {
        if (encounter is not VantomBoss || native is not RunState run ||
            JigsawNativeSession.TryGet(run)?.Map is not { } map || run.Players.Count == 0 ||
            !JigsawHuntingNative.RootIsHunted(run.Players[0]) || run.Act is not Overgrowth ||
            run.CurrentRoomCount != 1 || run.CurrentRoom is not CombatRoom { ParentEventId: null } room ||
            !ReferenceEquals(room.Encounter, encounter))
            return false;
        int? nodeId = map.CurrentNode ?? map.PendingEntry?.NodeId;
        return nodeId != null && map.Nodes.Single(node => node.Id == nodeId).Room is JigsawRoom.Boss or JigsawRoom.SecondBoss;
    }
    internal static bool IsOrdinaryRoot(IRunState native)
    {
        if (native is not RunState run || JigsawNativeSession.TryGet(run)?.Map is not { } map ||
            run.Players.Count == 0 || !JigsawHuntingNative.RootIsHunted(run.Players[0]))
            return false;
        int? nodeId = map.CurrentNode ?? map.PendingEntry?.NodeId;
        return nodeId != null && map.Nodes.Single(node => node.Id == nodeId).Room is JigsawRoom.Combat or JigsawRoom.Unknown;
    }
}

// Generate on the mutable encounter before native asset preloading and creature
// creation, so HP, monster RNG and multiplayer scaling use the native path once.
[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.GenerateMonstersWithSlots))]
internal static class JigsawHuntingOpeningEnemies
{
    private static void Postfix(EncounterModel __instance, IRunState runState, ref IReadOnlyList<(MonsterModel, string?)> ____monstersWithSlots)
    {
        if (JigsawHuntingCombat.IsVantomBossRoot(runState, __instance))
            ____monstersWithSlots = [(ModelDb.Monster<Inklet>().ToMutable(), null),
                .. ____monstersWithSlots, (ModelDb.Monster<Inklet>().ToMutable(), null)];
        if (JigsawHuntingCombat.IsOrdinaryRoot(runState) && runState.Act is Overgrowth)
        {
            JigsawHuntingCombat.RegisterLayout(__instance);
            ____monstersWithSlots = [.. ____monstersWithSlots, (ModelDb.Monster<FungusBeast>().ToMutable(),
                __instance.HasScene ? JigsawHuntingCombat.AdditionSlot : null)];
        }
    }
}

[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.CreateScene))]
internal static class JigsawHuntingEncounterLayout
{
    private static void Postfix(EncounterModel __instance, Control __result)
    {
        if (!JigsawHuntingCombat.HasLayout(__instance))
            return;
        var originalMarkers = __instance.Slots.Select(slot => __result.GetNodeOrNull<Marker2D>(slot))
            .Where(marker => marker != null).ToArray();
        float left = originalMarkers.Length == 0 ? 1200 : originalMarkers.Min(marker => marker!.Position.X);
        float ground = originalMarkers.Length == 0 ? 740 : originalMarkers.Max(marker => marker!.Position.Y);
        __result.AddChild(new Marker2D { Name = JigsawHuntingCombat.AdditionSlot, Position = new Vector2(left - 260, ground) });
    }
}
