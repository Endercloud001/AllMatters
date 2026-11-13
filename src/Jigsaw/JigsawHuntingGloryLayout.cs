using AllMatters.Monsters;
using Godot;
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
    private static readonly System.Reflection.MethodInfo updateNavigation =
        AccessTools.Method(typeof(NCombatRoom), "UpdateCreatureNavigation");

    private static void Postfix(NCombatRoom __instance)
    {
        var enemies = __instance.CreatureNodes.Where(node => node.Entity.IsEnemy).OrderBy(node => node.GlobalPosition.X).ToArray();
        var combat = enemies.FirstOrDefault()?.Entity.CombatState;
        if (combat?.RunState.Act is not Glory || combat.Encounter is not { HasScene: false, RoomType: RoomType.Monster } encounter ||
            !JigsawHuntingCombat.HasLayout(encounter) || !JigsawHuntingCombat.IsOrdinaryRoot(combat.RunState) ||
            !enemies.Any(node => node.Entity.Monster is Mystic))
            return;

        var parent = (CanvasItem)enemies[0].GetParent();
        var toParent = parent.GetGlobalTransform().AffineInverse();
        const float clearance = 24f;
        float left = __instance.CreatureNodes.Where(node => node.Entity.IsPlayer || node.Entity.PetOwner != null)
            .Max(node => (toParent * node.GetGlobalTransform() * Envelope(node)).End.X) + clearance;
        float roomRight = (toParent * (__instance.GetGlobalTransform() * new Vector2(__instance.Size.X, 0))).X;
        var bounds = enemies.Select(Envelope).ToArray();
        float width = bounds.Sum(rect => rect.Size.X);
        float scale = Mathf.Min(1f, (roomRight - clearance - left - clearance * (enemies.Length - 1)) / width);
        float prefix = 0;
        for (int index = 0; index < enemies.Length; index++)
        {
            // Original AdjustCreatureScaleForAspectRatio uses the unscaled
            // visual half-width plus fifteen, even when NCreature is scaled.
            float nativeHalf = enemies[index].Visuals.Bounds.Size.X * 0.5f;
            float centre = prefix - bounds[index].Position.X;
            scale = Mathf.Min(scale, (roomRight - 15f - left - clearance * index - nativeHalf) / centre);
            prefix += bounds[index].Size.X;
        }
        float cursor = left;
        for (int index = 0; index < enemies.Length; index++)
        {
            enemies[index].Scale = Vector2.One * scale;
            enemies[index].Position = new Vector2(cursor - bounds[index].Position.X * scale, 200f);
            cursor += bounds[index].Size.X * scale + clearance;
        }
        // Rebuild the real whole-room ring after the actual positions change.
        // The remaining native _Ready and aspect-ratio adjustment run normally.
        updateNavigation.Invoke(__instance, null);
    }

    private static Rect2 Envelope(NCreature node)
    {
        var toNode = node.GetGlobalTransform().AffineInverse();
        var hitbox = toNode * node.Hitbox.GetGlobalRect();
        var display = node.GetNode<Control>("%HealthBar");
        var hpTarget = toNode * display.GetNode<Control>("%HpBarHitbox").GetGlobalRect();
        // The native HP resize is deferred. Include its known final layout now,
        // using NHealthBar.UpdateLayoutForCreatureBounds' original formula.
        float padding = node.Entity.Monster is { } monster ? (24f - monster.HpBarSizeReduction) * 0.5f : 0f;
        var hp = new Rect2(new Vector2(hitbox.Position.X - padding, hpTarget.Position.Y),
            new Vector2(hitbox.Size.X + padding * 2, hpTarget.Size.Y));
        return hitbox.Merge(hpTarget).Merge(hp);
    }
}
