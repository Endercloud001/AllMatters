using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.addons.mega_text;
using Lagavulin = AllMatters.Monsters.Lagavulin;

namespace AllMatters.Jigsaw;

// The automatic row squeezes body gaps to five and raises the Matriarch,
// overlapping the added Lagavulin and both wider HP displays. Pack whole
// enemy subtrees by their complete horizontal envelopes, at native scale.
[HarmonyPatch(typeof(NCombatRoom), "CreateEnemyNodes")]
internal static class JigsawHuntingMatriarchLayout
{
    private static readonly System.Reflection.MethodInfo updateNavigation =
        AccessTools.Method(typeof(NCombatRoom), "UpdateCreatureNavigation");

    private static void Postfix(NCombatRoom __instance)
    {
        var enemies = __instance.CreatureNodes.Where(node => node.Entity.IsEnemy).ToArray();
        if (enemies.Length != 2 || enemies[0].Entity.Monster is not Lagavulin added ||
            enemies[1].Entity.Monster is not LagavulinMatriarch ||
            enemies.Any(node => node.Entity.SlotName != null || !node.Entity.IsAlive))
            return;
        var combat = enemies[1].Entity.CombatState;
        if (combat?.RunState is not RunState run || combat.Encounter is not LagavulinMatriarchBoss { HasScene: false } encounter ||
            run.CurrentRoom is not CombatRoom room || !ReferenceEquals(room.CombatState, combat) ||
            enemies.Any(node => !ReferenceEquals(node.Entity.CombatState, combat) || !combat.ContainsCreature(node.Entity)) ||
            !JigsawHuntingUnderdocks.IsHuntedMatriarchEncounter(run, encounter) ||
            !JigsawHuntingUnderdocks.IsMatriarchMinion(added))
            return;

        // Native room creation precedes AfterCreatureAdded. Its exact added
        // instance still owns the generation mark here; the original awaited
        // Minion grant consumes that mark afterwards. Never extend its lifetime.
        var parent = (CanvasItem)enemies[0].GetParent();
        var toParent = parent.GetGlobalTransform().AffineInverse();
        var envelopes = enemies.Select(node => Envelope(node, toParent)).ToArray();
        const float clearance = 24f;
        float playerRight = __instance.CreatureNodes.Where(node => node.Entity.IsPlayer || node.Entity.PetOwner != null)
            .Max(node => Envelope(node, toParent).End.X);
        float roomRight = (toParent * (__instance.GetGlobalTransform() * new Vector2(__instance.Size.X, 0))).X;
        float leftLimit = playerRight + clearance;
        float rightLimit = roomRight - clearance;
        float width = envelopes.Sum(bounds => bounds.Size.X) + clearance;
        float cursor = Mathf.Max(leftLimit, (leftLimit + rightLimit - width) * 0.5f);
        for (int index = 0; index < enemies.Length; index++)
        {
            enemies[index].Position += new Vector2(cursor - envelopes[index].Position.X, 0);
            cursor += envelopes[index].Size.X + clearance;
        }
        // The real ring must follow the moved nodes. The remaining native
        // camera/aspect work still runs; neither players nor local hitboxes move.
        updateNavigation.Invoke(__instance, null);
    }

    private static Rect2 Envelope(NCreature node, Transform2D toParent)
    {
        var display = node.GetNode<Control>("%HealthBar");
        var bar = display.GetNode<NHealthBar>("%HealthBar");
        var label = bar.GetNode<MegaLabel>("%HpLabel");
        var toNode = node.GetGlobalTransform().AffineInverse();
        var hitbox = toNode * node.Hitbox.GetGlobalRect();
        var hpTarget = toNode * display.GetNode<Control>("%HpBarHitbox").GetGlobalRect();
        // NHealthBar defers its resize. Include its original final width now,
        // instead of packing the temporary template bar and losing the gap.
        float padding = node.Entity.Monster is { } monster ? (24f - monster.HpBarSizeReduction) * 0.5f : 0f;
        var finalHp = new Rect2(new Vector2(hitbox.Position.X - padding, hpTarget.Position.Y),
            new Vector2(hitbox.Size.X + padding * 2f, hpTarget.Size.Y));
        var text = label.GetGlobalRect();
        var transform = label.GetGlobalTransform();
        var scale = new Vector2(transform.X.Length(), transform.Y.Length());
        var content = label.GetCombinedMinimumSize() * scale;
        float horizontal = Mathf.Max(0f, content.X - text.Size.X);
        float outline = label.GetThemeConstant("outline_size");
        text = text.GrowIndividual(horizontal + outline * scale.X, 0, horizontal + outline * scale.X, 0);
        return (toParent * node.GetGlobalTransform() * hitbox.Merge(hpTarget).Merge(finalHp))
            .Merge(toParent * bar.HpBarContainer.GetGlobalRect()).Merge(toParent * text);
    }
}
