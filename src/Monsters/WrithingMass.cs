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
public sealed class WrithingMass : ModMonsterTemplate
{
    public override int MinInitialHp => 75;
    public override int MaxInitialHp => 75;
    public override MonsterAssetProfile AssetProfile => new("res://AllMatters/monsters/writhing_mass/writhing_mass.tscn");
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
        => RitsuGodotNodeFactories.CreateFromScene<NCreatureVisuals>(ResourceLoader.Load<PackedScene>(CustomVisualsPath!));

    private string? _lastAction;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<Powers.ReactivePower>(new ThrowingPlayerChoiceContext(), Creature, 1, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var parasite = new MoveState("PARASITE", Parasite, [new StatusIntent(1)]) { MustPerformOnceBeforeTransitioning = true };
        var wither = new MoveState("WITHER", Wither, [new DebuffIntent()]);
        var flail = new MoveState("FLAIL", Flail, [new MultiAttackIntent(5, 3)]);
        var shield = new MoveState("SHIELD", Shield, [new DefendIntent()]);
        foreach (var move in new[] { parasite, wither, flail, shield }) move.FollowUpState = shield;
        return new MonsterMoveStateMachine([parasite, wither, flail, shield], parasite);
    }

    internal void ReactToAttack()
    {
        if (_lastAction == null || Creature.IsDead) return;
        var choices = new[] { "WITHER", "FLAIL", "SHIELD" }.Where(id => id != _lastAction).ToArray();
        var move = (MoveState)MoveStateMachine!.States[choices[RunRng.MonsterAi.NextInt(choices.Length)]];
        SetMoveImmediate(move);
    }

    private async Task Parasite(IReadOnlyList<Creature> targets)
    {
        _lastAction = "PARASITE";
        await CreatureCmd.TriggerAnim(Creature, "Attack", 0f);
        await CardPileCmd.AddToCombatAndPreview<SporeMind>(targets.Where(t => t.IsAlive).ToArray(), PileType.Draw, 1, (Player?)null);
    }

    private async Task Wither(IReadOnlyList<Creature> targets)
    {
        _lastAction = "WITHER";
        await CreatureCmd.TriggerAnim(Creature, "Attack", 0f);
        foreach (var target in targets.Where(t => t.IsAlive))
        {
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), target, 1, Creature, null);
            await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), target, 1, Creature, null);
        }
    }

    private async Task Flail(IReadOnlyList<Creature> targets)
    {
        _lastAction = "FLAIL";
        await DamageCmd.Attack(5).WithHitCount(3).FromMonster(this).WithAttackerAnim("Attack", 0f).Execute(null);
    }

    private async Task Shield(IReadOnlyList<Creature> targets)
    {
        _lastAction = "SHIELD";
        await CreatureCmd.TriggerAnim(Creature, "Attack", 0f);
        await CreatureCmd.GainBlock(Creature, 16, ValueProp.Move, null);
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
