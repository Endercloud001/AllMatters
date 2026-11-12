using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Jigsaw;

internal static class JigsawHuntingActThree
{
    private sealed class OpeningArtifact;
    private static readonly ConditionalWeakTable<ArtifactPower, OpeningArtifact> fixedArtifacts = new();

    internal static bool IsHuntedElite(MonsterModel monster) =>
        monster.CombatState.RunState is { Act: Glory, CurrentRoomCount: 1, CurrentRoom: CombatRoom room } run &&
        ReferenceEquals(room.Encounter, monster.CombatState.Encounter) && room.Encounter.RoomType == RoomType.Elite &&
        run.Players.FirstOrDefault() is { } player && JigsawHuntingNative.RootIsHunted(player);

    internal static async Task GrantMechaArtifact(MechaKnight monster)
    {
        var artifact = (ArtifactPower)ModelDb.Power<ArtifactPower>().ToMutable();
        fixedArtifacts.Add(artifact, new OpeningArtifact());
        try
        {
            // Both target versions' original initializer only awaits the empty
            // MonsterModel base initializer and grants Artifact. Keep one grant.
            await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), artifact, monster.Creature, 5, monster.Creature, null);
            monster.SetMoveImmediate((MoveState)monster.MoveStateMachine!.States["FLAMETHROWER_MOVE"], forceTransition: true);
        }
        finally { fixedArtifacts.Remove(artifact); }
    }

    // Consume the exception for this exact opening instance and this one scale
    // call. Awaited hooks may independently grant Artifact to the same creature.
    internal static bool ConsumeFixedArtifact(ArtifactPower artifact) => fixedArtifacts.Remove(artifact);
}

[HarmonyPatch(typeof(MechaKnight), nameof(MechaKnight.AfterAddedToRoom))]
internal static class JigsawHuntingMechaArtifact
{
    private static bool Prefix(MechaKnight __instance, ref Task __result)
    {
        if (!JigsawHuntingActThree.IsHuntedElite(__instance))
            return true;
        __result = JigsawHuntingActThree.GrantMechaArtifact(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(ArtifactPower), nameof(ArtifactPower.GetScaledAmountForMultiplayer))]
internal static class JigsawHuntingMechaArtifactScaling
{
    private static bool Prefix(ArtifactPower __instance, decimal amount, ref decimal __result)
    {
        if (!JigsawHuntingActThree.ConsumeFixedArtifact(__instance))
            return true;
        __result = amount;
        return false;
    }
}

[HarmonyPatch(typeof(SoulNexus), nameof(SoulNexus.AfterAddedToRoom))]
internal static class JigsawHuntingSoulNexusOpening
{
    private static void Postfix(SoulNexus __instance, ref Task __result)
    {
        if (JigsawHuntingActThree.IsHuntedElite(__instance))
            __result = AddTerritorial(__result, __instance);
    }

    private static async Task AddTerritorial(Task original, SoulNexus monster)
    {
        await original;
        await PowerCmd.Apply<TerritorialPower>(new ThrowingPlayerChoiceContext(), monster.Creature, 1, monster.Creature, null);
    }
}
