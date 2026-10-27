using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;

namespace AllMatters.Monsters;

[RegisterMonster]
public sealed class Champion : ModMonsterTemplate
{
    public override int MinInitialHp => 320;
    public override int MaxInitialHp => 320;
    public override MonsterAssetProfile AssetProfile => new("res://AllMatters/monsters/champion/champ.tscn");
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
        => RitsuGodotNodeFactories.CreateFromScene<NCreatureVisuals>(ResourceLoader.Load<PackedScene>(CustomVisualsPath!));

    private bool _enraged;
    private int _defenses;
    private int _regularMovesUntilExecute;
    private string? _lastMove;

    public override Task BeforeCombatStart()
    {
        Say("OPEN");
        return Task.CompletedTask;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var defend = new MoveState("DEFEND", Defend, [new DefendIntent()]);
        var taunt = new MoveState("TAUNT", Taunt, [new DebuffIntent()]);
        var slash = new MoveState("SLASH", Slash, [new SingleAttackIntent(18)]);
        var slap = new MoveState("SLAP", Slap, [new DebuffIntent()]);
        var anger = new MoveState("ANGER", Anger, [new BuffIntent()]) { MustPerformOnceBeforeTransitioning = true };
        var execute = new MoveState("EXECUTE", Execute, [new MultiAttackIntent(10, 2)]);
        var regular = new RandomBranchState("REGULAR");
        // STS1 eligibility: no consecutive regular move, at most two defenses.
        regular.AddBranch(defend, 1, () => _lastMove != "DEFEND" && _defenses < 2 ? 1f : 0f);
        regular.AddBranch(taunt, 1, () => _lastMove != "TAUNT" ? 1f : 0f);
        regular.AddBranch(slash, 1, () => _lastMove != "SLASH" ? 1f : 0f);
        regular.AddBranch(slap, 1, () => _lastMove != "SLAP" ? 1f : 0f);
        var next = new ConditionalBranchState("NEXT");
        next.AddState(execute, () => _enraged && _regularMovesUntilExecute == 0);
        next.AddState(regular, () => true);
        foreach (var move in new[] { defend, taunt, slash, slap, anger, execute }) move.FollowUpState = next;
        return new MonsterMoveStateMachine([defend, taunt, slash, slap, anger, execute, regular, next], defend);
    }

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Creature && Creature.IsAlive && !_enraged && Creature.CurrentHp * 2 < Creature.MaxHp)
        {
            _enraged = true;
            SetMoveImmediate((MoveState)MoveStateMachine!.States["ANGER"], true);
        }
        return Task.CompletedTask;
    }

    private void RegularMove(string id)
    {
        _lastMove = id;
        if (_enraged) _regularMovesUntilExecute--;
    }

    private async Task Defend(IReadOnlyList<Creature> targets)
    {
        RegularMove("DEFEND");
        _defenses++;
        Say("DEFEND");
        await CreatureCmd.GainBlock(Creature, 15, ValueProp.Move, null);
    }

    private async Task Taunt(IReadOnlyList<Creature> targets)
    {
        RegularMove("TAUNT");
        Say("TAUNT");
        foreach (var target in targets.Where(t => t.IsAlive))
        {
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), target, 2, Creature, null);
            await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), target, 2, Creature, null);
        }
    }

    private async Task Slash(IReadOnlyList<Creature> targets)
    {
        RegularMove("SLASH");
        await DamageCmd.Attack(18).FromMonster(this).WithAttackerAnim("Attack", 0f).Execute(null);
    }

    private async Task Slap(IReadOnlyList<Creature> targets)
    {
        RegularMove("SLAP");
        await CreatureCmd.TriggerAnim(Creature, "Attack", 0f);
        foreach (var target in targets.Where(t => t.IsAlive))
        {
            await PowerCmd.Apply<FrailPower>(new ThrowingPlayerChoiceContext(), target, 2, Creature, null);
            await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), target, 2, Creature, null);
        }
    }

    private async Task Anger(IReadOnlyList<Creature> targets)
    {
        _lastMove = "ANGER";
        _regularMovesUntilExecute = 0;
        Say("ANGER");
        foreach (var power in Creature.Powers.Where(p => p.Type == PowerType.Debuff).ToArray())
            await PowerCmd.Remove(power);
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, 9, Creature, null);
    }

    private async Task Execute(IReadOnlyList<Creature> targets)
    {
        _lastMove = "EXECUTE";
        _regularMovesUntilExecute = 2;
        await DamageCmd.Attack(10).WithHitCount(2).FromMonster(this).WithAttackerAnim("Attack", 0f).Execute(null);
    }

    private void Say(string key)
        => TalkCmd.Play(new LocString("monsters", "ALL_MATTERS_MONSTER_CHAMPION.dialog." + key),
            Creature, MegaCrit.Sts2.Core.Nodes.Vfx.VfxColor.White, MegaCrit.Sts2.Core.Nodes.Vfx.VfxDuration.Long);

    public override CreatureAnimator GenerateAnimator(MegaSprite controller)
    {
        var idle = new AnimState("Idle", true);
        var animator = new CreatureAnimator(idle, controller);
        animator.AddAnyState("Attack", new AnimState("Attack") { NextState = idle });
        animator.AddAnyState("Hit", new AnimState("Hit") { NextState = idle });
        return animator;
    }
}
