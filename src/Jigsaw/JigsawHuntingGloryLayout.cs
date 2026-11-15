using AllMatters.Monsters;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Jigsaw;

// Native PositionEnemies compresses body gaps and then raises alternating
// creatures. The added Mystic can therefore put a narrow body's wider HP
// display inside its neighbour. Keep complete creature subtrees together.
[HarmonyPatch(typeof(NCombatRoom), "CreateEnemyNodes")]
internal static class JigsawHuntingGloryLayout
{
    private static void Postfix(NCombatRoom __instance)
    {
        var enemies = __instance.CreatureNodes.Where(node => node.Entity.IsEnemy).OrderBy(node => node.GlobalPosition.X).ToArray();
        var combat = enemies.FirstOrDefault()?.Entity.CombatState;
        if (combat?.RunState.Act is not Glory || combat.Encounter is not { HasScene: false, RoomType: RoomType.Monster } encounter ||
            !JigsawHuntingCombat.HasLayout(encounter) || !JigsawHuntingCombat.IsOrdinaryRoot(combat.RunState) ||
            !enemies.Any(node => node.Entity.Monster is Mystic))
            return;

        PositionAutomatic(__instance, enemies);
    }

    private static void PositionAutomatic(NCombatRoom room, NCreature[] enemies)
    {
        JigsawHuntingSeapunkLayout.PositionAutomatic(room, enemies);
    }
}
