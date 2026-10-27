using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Monsters;

[RegisterMonster]
public sealed class Transient : ModMonsterTemplate
{
    public override int MinInitialHp => 999;
    public override int MaxInitialHp => 999;
    public override MonsterAssetProfile AssetProfile => new("res://AllMatters/monsters/transient/transient.tscn");
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
        => RitsuGodotNodeFactories.CreateFromScene<NCreatureVisuals>(ResourceLoader.Load<PackedScene>(CustomVisualsPath!));

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<Powers.FadingPower>(context, Creature, 5, Creature, null);
        await PowerCmd.Apply<Powers.ShiftingPower>(context, Creature, 1, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var attack = new MoveState("ATTACK", Attack, [new SingleAttackIntent(() => 10 * Creature.CombatState!.RoundNumber)]);
        attack.FollowUpState = attack;
        return new MonsterMoveStateMachine([attack], attack);
    }

    private async Task Attack(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(10 * Creature.CombatState!.RoundNumber).FromMonster(this).WithAttackerAnim("Attack", 0f).Execute(null);
        if (Creature.IsDead) return;
        var fading = Creature.GetPower<Powers.FadingPower>()!;
        if (fading.Amount <= 1) await CreatureCmd.Kill(Creature);
        else await PowerCmd.Decrement(fading);
    }

    public override CreatureAnimator GenerateAnimator(MegaSprite controller)
    {
        var idle = new AnimState("Idle", true);
        var animator = new CreatureAnimator(idle, controller);
        animator.AddAnyState("Attack", new AnimState("Attack") { NextState = idle });
        animator.AddAnyState("Hit", new AnimState("Hurt") { NextState = idle });
        return animator;
    }
}
