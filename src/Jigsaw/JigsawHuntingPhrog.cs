using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
#if !STS2_PUBLIC
using MegaCrit.Sts2.Core.Helpers;
#endif

namespace AllMatters.Jigsaw;

// The authored encounter has only four worm markers. A second row leaves
// room for the fifth creature's hitbox, health bar and intent without extending
// into the players' target area. Small first-row offsets keep actual hitboxes
// clear of four players. Beta also needs a rightmost inset for its health bar;
// public's native aspect-ratio scaling requires the original rightmost slot.
[HarmonyPatch(typeof(EncounterModel), nameof(EncounterModel.CreateScene))]
internal static class JigsawHuntingPhrogLayout
{
    private static void Postfix(EncounterModel __instance, Control __result)
    {
        if (__instance is not PhrogParasiteElite || !__instance.MonstersWithSlots.Any(pair =>
                pair.Item1 is PhrogParasite phrog && JigsawHuntingActOne.HasSpecialRules(phrog)))
            return;
        var first = __result.GetNode<Marker2D>(PhrogParasiteElite.GetWrigglerSlotName(0));
        var second = __result.GetNode<Marker2D>(PhrogParasiteElite.GetWrigglerSlotName(1));
        var fifthPosition = new Vector2((first.Position.X + second.Position.X) / 2, first.Position.Y - 240);
        first.Position += new Vector2(45, 0);
        second.Position += new Vector2(15, 0);
#if !STS2_PUBLIC
        __result.GetNode<Marker2D>(PhrogParasiteElite.GetWrigglerSlotName(3)).Position -= new Vector2(24, 0);
#endif
        __result.AddChild(new Marker2D
        {
            Name = PhrogParasiteElite.GetWrigglerSlotName(4),
            Position = fifthPosition
        });
    }
}

// Keep the original four spawns, death/music behavior and version-specific
// timing. Only the fifth worm is added by this continuation.
[HarmonyPatch(typeof(InfestedPower), nameof(InfestedPower.AfterDeath))]
internal static class JigsawHuntingPhrogDeath
{
    private static void Postfix(InfestedPower __instance, Creature target, bool wasRemovalPrevented,
        float deathAnimLength, ref Task __result)
    {
        if (!wasRemovalPrevented && __instance.Owner == target && target.Monster is PhrogParasite phrog &&
            JigsawHuntingActOne.HasSpecialRules(phrog))
            __result = AddFifth(__instance, deathAnimLength, __result);
    }
    private static async Task AddFifth(InfestedPower infestation, float deathAnimLength, Task original)
    {
        var combat = infestation.CombatState;
        bool StillOwned() => combat != null && ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), combat.RunState) &&
            ReferenceEquals(infestation.Owner.CombatState, combat);
        try
        { await original; }
        catch when (!StillOwned()) { return; }
        if (!StillOwned())
            return;
        var worm = (Wriggler)ModelDb.Monster<Wriggler>().ToMutable();
        worm.StartStunned = true;
        var creature = await CreatureCmd.Add(worm, combat, infestation.Owner.Side,
            PhrogParasiteElite.GetWrigglerSlotName(4));
#if !STS2_PUBLIC
        creature.SetNodeVisible(false);
        TaskHelper.RunSafely(Reveal(creature, deathAnimLength));
#endif
    }
#if !STS2_PUBLIC
    private static async Task Reveal(Creature creature, float deathAnimLength)
    {
        var combat = creature.CombatState;
        await Cmd.CustomScaledWait(deathAnimLength, deathAnimLength);
        if (combat == null || !ReferenceEquals(creature.CombatState, combat) ||
            !ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), combat.RunState))
            return;
        creature.SetNodeVisible(true);
    }
#endif
}

[HarmonyPatch(typeof(Wriggler), "GenerateMoveStateMachine")]
internal static class JigsawHuntingFifthWormMove
{
    private static void Postfix(Wriggler __instance, MonsterMoveStateMachine __result)
    {
        if (__instance.Creature.SlotName == "wriggler5" &&
            __instance.CombatState.Encounter is PhrogParasiteElite &&
            JigsawHuntingActOne.HasSpecialRules(__instance))
            ((ConditionalBranchState)__result.States["INIT_MOVE"]).AddState(__result.States["NASTY_BITE_MOVE"], () => true);
    }
}
