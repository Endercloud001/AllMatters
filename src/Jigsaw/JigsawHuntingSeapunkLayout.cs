using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Jigsaw;

// Scene markers are centres; the added Seapunk's HP display extends farther
// than its body. Automatic placement can also squeeze the original displays.
[HarmonyPatch(typeof(NCombatRoom), "CreateEnemyNodes")]
internal static class JigsawHuntingSeapunkLayout
{
    private static readonly System.Reflection.PropertyInfo encounterSlots =
        AccessTools.Property(typeof(NCombatRoom), "EncounterSlots");
    private static readonly System.Reflection.MethodInfo updateNavigation =
        AccessTools.Method(typeof(NCombatRoom), "UpdateCreatureNavigation");

    private static void Postfix(NCombatRoom __instance)
    {
        var enemies = __instance.CreatureNodes.Where(node => node.Entity.IsEnemy).ToArray();
        var combat = enemies.FirstOrDefault()?.Entity.CombatState;
        if (combat?.RunState is not RunState run || !JigsawHuntingCombat.IsOrdinaryRoot(run) ||
            run.Act is not Underdocks || combat.Encounter is not { } encounter ||
            !JigsawHuntingCombat.HasLayout(encounter))
            return;

        if (!encounter.HasScene)
        {
            if (encounter.MonstersWithSlots.LastOrDefault().Item1 is not Seapunk addedMonster ||
                !enemies.Any(node => ReferenceEquals(node.Entity, addedMonster.Creature)))
                return;
            PositionAutomatic(__instance, enemies);
            return;
        }

        var addition = enemies.SingleOrDefault(node =>
            node.Entity.SlotName == JigsawHuntingCombat.AdditionSlot && node.Entity.Monster is Seapunk);
        if (addition == null)
            return;
        var scene = (Control?)encounterSlots.GetValue(__instance);
        var marker = scene?.GetNodeOrNull<Marker2D>(JigsawHuntingCombat.AdditionSlot);
        if (marker == null)
            return;

        var originals = enemies.Where(node => !ReferenceEquals(node, addition)).ToArray();
        if (originals.Length == 0)
            return;
        const float clearance = 24f;
        float originalLeft = originals.Min(node => (node.GetGlobalTransform() * Envelope(node)).Position.X);
        float additionRight = (addition.GetGlobalTransform() * Envelope(addition)).End.X;
        float shift = originalLeft - clearance - additionRight;
        if (shift >= 0f)
            return;
        var offset = new Vector2(shift, 0f);
        marker.GlobalPosition += offset;
        addition.GlobalPosition += offset;
        // Native AddCreature builds links before its final slot positioning.
        // The outer initializer only applies aspect-ratio scale afterwards.
        updateNavigation.Invoke(__instance, null);
    }

    private static void PositionAutomatic(NCombatRoom room, NCreature[] enemies)
    {
        enemies = enemies.OrderBy(node => node.GlobalPosition.X).ToArray();
        var parent = (CanvasItem)enemies[0].GetParent();
        var toParent = parent.GetGlobalTransform().AffineInverse();
        const float clearance = 24f;
        float left = room.CreatureNodes.Where(node => node.Entity.IsPlayer || node.Entity.PetOwner != null)
            .Max(node => (toParent * node.GetGlobalTransform() * Envelope(node)).End.X) + clearance;
        float roomRight = (toParent * (room.GetGlobalTransform() * new Vector2(room.Size.X, 0))).X;
        // Native automatic combat reserves the enemy container's own area.
        // A player-derived negative left edge pulls the whole group into the centre.
        left = Mathf.Max(left, clearance);
        var bounds = enemies.Select(Envelope).ToArray();
        float width = bounds.Sum(rect => rect.Size.X);
        float scale = Mathf.Min(1f, (roomRight - clearance - left - clearance * (enemies.Length - 1)) / width);
        float prefix = 0f;
        for (int index = 0; index < enemies.Length; index++)
        {
            // Native aspect-ratio adjustment uses the unscaled visual half-width.
            float nativeHalf = enemies[index].Visuals.Bounds.Size.X * 0.5f;
            float centre = prefix - bounds[index].Position.X;
            scale = Mathf.Min(scale, (roomRight - 15f - left - clearance * index - nativeHalf) / centre);
            prefix += bounds[index].Size.X;
        }
        float nativeArea = 960f / enemies[0].Entity.CombatState!.Encounter!.GetCameraScaling();
        float nativeBodies = enemies.Sum(node => node.Visuals.Bounds.Size.X);
        float nativeGap = 70f;
        float nativeWidth = nativeBodies + nativeGap * (enemies.Length - 1);
        float nativeLeft = Math.Max((nativeArea - nativeWidth) * 0.5f, 150f);
        if (nativeLeft + nativeWidth > nativeArea)
        {
            nativeGap = Math.Max((nativeArea - 150f - nativeBodies) / (enemies.Length - 1), 5f);
            nativeWidth = nativeBodies + nativeGap * (enemies.Length - 1);
            nativeLeft = (nativeArea - nativeWidth) * 0.5f;
        }
        float nativeCentre = nativeLeft + nativeWidth * 0.5f;
        // Fit the complete displays around the native centre rather than
        // clamping a wide, full-size row leftwards toward the players.
        float centredWidth = 2f * Mathf.Min(nativeCentre - left, roomRight - clearance - nativeCentre);
        scale = Mathf.Min(scale, (centredWidth - clearance * (enemies.Length - 1)) / width);
        float groupWidth = width * scale + clearance * (enemies.Length - 1);
        float cursor = Mathf.Clamp(nativeLeft + nativeWidth * 0.5f - groupWidth * 0.5f,
            left, roomRight - clearance - groupWidth);
        for (int index = 0; index < enemies.Length; index++)
        {
            enemies[index].Scale = Vector2.One * scale;
            enemies[index].Position = new Vector2(cursor - bounds[index].Position.X * scale, 200f);
            cursor += bounds[index].Size.X * scale + clearance;
        }
        updateNavigation.Invoke(room, null);
    }

    private static Rect2 Envelope(NCreature node)
    {
        var toNode = node.GetGlobalTransform().AffineInverse();
        var hitbox = toNode * node.Hitbox.GetGlobalRect();
        var hpTarget = toNode * node.GetNode<Control>("%HealthBar").GetNode<Control>("%HpBarHitbox").GetGlobalRect();
        float padding = node.Entity.Monster is { } monster ? (24f - monster.HpBarSizeReduction) * 0.5f : 0f;
        var hp = new Rect2(new Vector2(hitbox.Position.X - padding, hpTarget.Position.Y),
            new Vector2(hitbox.Size.X + padding * 2f, hpTarget.Size.Y));
        return hitbox.Merge(hpTarget).Merge(hp);
    }
}
