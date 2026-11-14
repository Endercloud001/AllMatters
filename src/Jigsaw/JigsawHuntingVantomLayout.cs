using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace AllMatters.Jigsaw;

// The unslotted boss uses native automatic placement, whose body gaps do not
// account for the two Inklets' wider HP displays. Move whole native subtrees.
[HarmonyPatch(typeof(NCombatRoom), "CreateEnemyNodes")]
internal static class JigsawHuntingVantomLayout
{
    private static readonly System.Reflection.MethodInfo updateNavigation =
        AccessTools.Method(typeof(NCombatRoom), "UpdateCreatureNavigation");

    private static void Postfix(NCombatRoom __instance)
    {
        var enemies = __instance.CreatureNodes.Where(node => node.Entity.IsEnemy).ToArray();
        if (enemies.Length != 3 || enemies[0].Entity.Monster is not Inklet ||
            enemies[1].Entity.Monster is not Vantom || enemies[2].Entity.Monster is not Inklet ||
            enemies.Any(node => node.Entity.SlotName != null))
            return;
        var combat = enemies[1].Entity.CombatState;
        if (combat?.Encounter is not VantomBoss { HasScene: false } encounter ||
            !JigsawHuntingCombat.IsVantomBossRoot(combat.RunState, encounter))
            return;

        var parent = (CanvasItem)enemies[0].GetParent();
        var toParent = parent.GetGlobalTransform().AffineInverse();
        var envelopes = enemies.Select(node => Envelope(node, toParent)).ToArray();
        const float clearance = 24f;
        float playerRight = __instance.CreatureNodes.Where(node => node.Entity.IsPlayer)
            .Max(node => (toParent * node.Hitbox.GetGlobalRect()).End.X);
        float roomRight = (toParent * (__instance.GetGlobalTransform() * new Vector2(__instance.Size.X, 0))).X;
        float groupWidth = envelopes.Sum(rect => rect.Size.X) + clearance * (enemies.Length - 1);
        float leftLimit = playerRight + clearance;
        float rightLimit = roomRight - clearance;
        float cursor = Mathf.Max(leftLimit, (leftLimit + rightLimit - groupWidth) * 0.5f);
        for (int index = 0; index < enemies.Length; index++)
        {
            enemies[index].Position += new Vector2(cursor - envelopes[index].Position.X, 0);
            cursor += envelopes[index].Size.X + clearance;
        }
        updateNavigation.Invoke(__instance, null);
    }

    private static Rect2 Envelope(NCreature node, Transform2D toParent)
    {
        var display = node.GetNode<Control>("%HealthBar");
        var bar = display.GetNode<NHealthBar>("%HealthBar");
        var label = bar.GetNode<Control>("%HpLabel");
        return (toParent * node.Hitbox.GetGlobalRect())
            .Merge(toParent * display.GetNode<Control>("%HpBarHitbox").GetGlobalRect())
            .Merge(toParent * bar.HpBarContainer.GetGlobalRect())
            .Merge(toParent * label.GetGlobalRect());
    }
}
