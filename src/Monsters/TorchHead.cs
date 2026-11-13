using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;

namespace AllMatters.Monsters;

[RegisterMonster]
public sealed class TorchHead : ModMonsterTemplate
{
    public override int MinInitialHp => 50;
    public override int MaxInitialHp => 50;
    public override MonsterAssetProfile AssetProfile => new("res://AllMatters/monsters/torch_head/torch_head.tscn");
    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScene<NCreatureVisuals>(ResourceLoader.Load<PackedScene>(CustomVisualsPath!));

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null);
        // Native enemy-side additions do not roll an intent until the next
        // player phase. Present the one real attack immediately; birth timing
        // remains owned by native SpawnedThisTurn and the enemy snapshot.
        RollMove(CombatState.PlayerCreatures);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var attack = new MoveState("ATTACK", Attack, new SingleAttackIntent(10));
        attack.FollowUpState = attack;
        return new MonsterMoveStateMachine([attack], attack);
    }

    private async Task Attack(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(10).FromMonster(this).WithAttackerAnim("Attack", 0f).Execute(null);

    public override CreatureAnimator GenerateAnimator(MegaSprite controller)
    {
        var idle = new AnimState("idle", true);
        var animator = new CreatureAnimator(idle, controller);
        animator.AddAnyState("Attack", new AnimState("halberd_slam") { NextState = idle });
        // This source skeleton has no hurt animation.
        animator.AddAnyState("Hit", idle);
        return animator;
    }
}
