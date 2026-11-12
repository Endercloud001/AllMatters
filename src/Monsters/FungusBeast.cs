using Godot;
using AllMatters.Powers;
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
public sealed class FungusBeast : ModMonsterTemplate
{
    private int consecutiveBites;
    public override int MinInitialHp => 28;
    public override int MaxInitialHp => 28;
    public override MonsterAssetProfile AssetProfile => new("res://AllMatters/monsters/fungi_beast/fungi_beast.tscn");
    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScene<NCreatureVisuals>(ResourceLoader.Load<PackedScene>(CustomVisualsPath!));

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<SporeCloudPower>(new ThrowingPlayerChoiceContext(), Creature, 2, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var bite = new MoveState("BITE", Bite, [new SingleAttackIntent(6)]);
        var grow = new MoveState("GROW", Grow, [new BuffIntent()]);
        var choose = new ConditionalBranchState("CHOOSE");
        choose.AddState(grow, () => consecutiveBites >= 2 || Rng.NextInt(100) < 40);
        choose.AddState(bite, () => true);
        bite.FollowUpState = choose;
        grow.FollowUpState = bite;
        return new MonsterMoveStateMachine([bite, grow, choose], bite);
    }

    private async Task Bite(IReadOnlyList<Creature> targets)
    {
        consecutiveBites++;
        await DamageCmd.Attack(6).FromMonster(this).WithAttackerAnim("Attack", 0f).Execute(null);
    }

    private async Task Grow(IReadOnlyList<Creature> targets)
    {
        consecutiveBites = 0;
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, 3, Creature, null);
    }

    public override CreatureAnimator GenerateAnimator(MegaSprite controller)
    {
        var idle = new AnimState("Idle", true);
        var attack = new AnimState("Attack") { NextState = idle };
        var hit = new AnimState("Hit") { NextState = idle };
        var animator = new CreatureAnimator(idle, controller);
        animator.AddAnyState("Attack", attack);
        animator.AddAnyState("Hit", hit);
        return animator;
    }
}
