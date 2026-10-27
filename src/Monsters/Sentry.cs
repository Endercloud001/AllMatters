// SPDX-License-Identifier: GPL-3.0-only
// Adapted from Balance Sentry; upstream credits ActsFromThePast.
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;

namespace AllMatters.Monsters;

[RegisterMonster]
public sealed class Sentry : ModMonsterTemplate
{
    public const string BOLT = "BOLT";
    public const string BEAM = "BEAM";

    public override MonsterAssetProfile AssetProfile => new("res://AllMatters/monsters/sentry/sentry.tscn");
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
        => RitsuGodotNodeFactories.CreateFromScene<NCreatureVisuals>(ResourceLoader.Load<PackedScene>(CustomVisualsPath!));
    public override int MinInitialHp => 38;
    public override int MaxInitialHp => 42;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), Creature, 1m, Creature, null);
    }

    private bool _boltFirst;
    public bool BoltFirst
    {
        get => _boltFirst;
        set { AssertMutable(); _boltFirst = value; }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        MoveState bolt = new(BOLT, BoltMove, [new StatusIntent(2)]);
        MoveState beam = new(BEAM, BeamMove, [new SingleAttackIntent(9)]);
        bolt.FollowUpState = beam;
        beam.FollowUpState = bolt;
        return new MonsterMoveStateMachine([bolt, beam], BoltFirst ? bolt : beam);
    }

    private async Task BoltMove(IReadOnlyList<Creature> targets)
    {
        string animation = MegaCrit.Sts2.Core.Random.Rng.Chaotic.NextInt(3) switch { 0 => "spaz1", 1 => "spaz2", _ => "spaz3" };
        await CreatureCmd.TriggerAnim(Creature, animation, 0f);
        NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short);
        await Cmd.Wait(0.4f);
        await CardPileCmd.AddToCombatAndPreview<Dazed>(targets.Where(target => target.IsAlive).ToArray(),
            PileType.Discard, 2, (Player?)null);
    }

    private async Task BeamMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.TriggerAnim(Creature, "attack", 0f);
        NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short);
        await Cmd.Wait(0.25f);
        await DamageCmd.Attack(9).FromMonster(this).WithHitFx("vfx/vfx_attack_pierce").Execute(null);
    }

    public override CreatureAnimator GenerateAnimator(MegaSprite controller)
    {
        AnimState idle = new("idle", true);
        AnimState attack = new("attack");
        AnimState bolt1 = new("spaz1");
        AnimState bolt2 = new("spaz2");
        AnimState bolt3 = new("spaz3");
        AnimState hit = new("hit");
        attack.NextState = idle;
        bolt1.NextState = idle;
        bolt2.NextState = idle;
        bolt3.NextState = idle;
        hit.NextState = idle;
        CreatureAnimator animator = new(idle, controller);
        animator.AddAnyState("Attack", attack);
        animator.AddAnyState("spaz1", bolt1);
        animator.AddAnyState("spaz2", bolt2);
        animator.AddAnyState("spaz3", bolt3);
        animator.AddAnyState("Hit", hit);
        MegaAnimationState animationState = controller.GetAnimationState();
        animationState.SetTimeScale(2f);
        MegaTrackEntry? current = animationState.GetCurrent(0);
        current?.SetTrackTime(MegaCrit.Sts2.Core.Random.Rng.Chaotic.NextFloat(current.GetAnimationEnd()));
        animationState.Update(0f);
        animationState.Apply(controller.GetSkeleton()!);
        return animator;
    }
}

// This encounter specifies one Artifact even in multiplayer.
[HarmonyPatch(typeof(ArtifactPower), nameof(ArtifactPower.GetScaledAmountForMultiplayer))]
internal static class SentryArtifactAmount
{
    private static void Postfix(decimal amount, Creature target, ref decimal __result)
    {
        if (target.Monster is Sentry) __result = amount;
    }
}
