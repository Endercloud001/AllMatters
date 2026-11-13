using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Jigsaw;

[HarmonyPatch(typeof(WaterfallGiant), nameof(WaterfallGiant.TriggerAboutToBlowState))]
internal static class JigsawHuntingWaterfall
{
    private static readonly ConditionalWeakTable<ArtifactPower, object> fixedArtifacts = new();

    private static void Postfix(WaterfallGiant __instance, ref Task __result)
    {
        var combat = __instance.CombatState;
        if (combat?.RunState is RunState native && IsCurrent(__instance, native, combat))
            __result = Cleanse(__result, __instance, native, combat);
    }

    private static bool IsCurrent(WaterfallGiant monster, RunState native, ICombatState combat)
    {
        if (!ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), native) ||
            !ReferenceEquals(monster.CombatState, combat) || !combat.ContainsCreature(monster.Creature) ||
            !monster.Creature.IsAlive || CombatManager.Instance.IsEnding ||
            JigsawNativeSession.TryGet(native)?.Map is not { CurrentNode: not null, CurrentRoomHunted: true } map ||
            native.Players.FirstOrDefault() is not { } owner || !JigsawHuntingNative.RootIsHunted(owner) ||
            native is not
            {
                Act: Underdocks, CurrentRoomCount: 1,
                CurrentRoom: CombatRoom { ParentEventId: null, Encounter: WaterfallGiantBoss, RoomType: RoomType.Boss } room
            } ||
            !ReferenceEquals(room.CombatState, combat))
            return false;
        return map.Nodes.Single(node => node.Id == map.CurrentNode).Room is JigsawRoom.Boss or JigsawRoom.SecondBoss;
    }

    private static async Task Cleanse(Task original, WaterfallGiant monster, RunState native, ICombatState combat)
    {
        // Keep the native transition and its exceptions before applying the
        // hunting addition; a late task must never modify a replacement run.
        await original;
        if (!IsCurrent(monster, native, combat))
            return;
        var debuffs = monster.Creature.Powers.Where(power =>
            power.IsVisible && power.TypeForCurrentAmount == PowerType.Debuff).ToArray();
        foreach (var power in debuffs)
        {
            if (!IsCurrent(monster, native, combat))
                return;
            await PowerCmd.Remove(power);
        }
        if (!IsCurrent(monster, native, combat))
            return;
        var artifact = (ArtifactPower)ModelDb.Power<ArtifactPower>().ToMutable();
        fixedArtifacts.Add(artifact, new object());
        try
        {
            await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), artifact, monster.Creature, 1, monster.Creature, null);
        }
        finally { fixedArtifacts.Remove(artifact); }
    }

    // Only this grant's exact new instance consumes one scaling exception.
    // Existing stacks and independent sources retain native PowerCmd behavior.
    internal static bool ConsumeFixedArtifact(ArtifactPower artifact) => fixedArtifacts.Remove(artifact);

    internal static bool PreserveAfterRevival(PowerModel power)
    {
        if (power.IsVisible && power.TypeForCurrentAmount == PowerType.Debuff ||
            power.Owner.Monster is not WaterfallGiant monster ||
            !ReferenceEquals(power.Owner, monster.Creature) || !monster.Creature.Powers.Contains(power) ||
            monster.CombatState is not { RunState: RunState native } combat ||
            !IsCurrent(monster, native, combat))
            return false;
        return monster.Creature.HasPower<SteamEruptionPower>() && monster.NextMove.Id == "ABOUT_TO_BLOW_MOVE";
    }
}

// Native Kill clears powers after awaiting Steam Eruption's revival. Keep the
// surviving phase's buffs/hidden state there; ordinary final death still clears.
[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldPowerBeRemovedOnDeath))]
internal static class JigsawHuntingWaterfallRevivalPowers
{
    private static void Postfix(PowerModel power, ref bool __result)
    {
        if (__result && JigsawHuntingWaterfall.PreserveAfterRevival(power))
            __result = false;
    }
}

[HarmonyPatch(typeof(ArtifactPower), nameof(ArtifactPower.GetScaledAmountForMultiplayer))]
internal static class JigsawHuntingWaterfallArtifactScaling
{
    private static bool Prefix(ArtifactPower __instance, decimal amount, ref decimal __result)
    {
        if (!JigsawHuntingWaterfall.ConsumeFixedArtifact(__instance))
            return true;
        __result = amount;
        return false;
    }
}
