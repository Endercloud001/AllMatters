// Original throwaway rule models. Exact species visuals are read only from the
// author's optional public PCK; no upstream DLL or artwork is distributed here.
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

public sealed class PrototypeHuntingFungus : MonsterModel
{
    protected override string VisualsPath=>"res://ActsFromThePast/monsters/fungi_beast/fungi_beast.tscn";
    public override CreatureAnimator GenerateAnimator(MegaSprite controller)=>PrototypeHuntingVisualAnimator.Create(controller,"Idle","Attack","Hit");
    private int _bites;
    public override int MinInitialHp => 28;
    public override int MaxInitialHp => 28;
    public override Task AfterAddedToRoom() => PowerCmd.Apply<PrototypeHuntingSpores>(new ThrowingPlayerChoiceContext(), Creature, 2, Creature, null);
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var bite = new MoveState("BITE", async _ => { _bites++; await DamageCmd.Attack(6).FromMonster(this).Execute(null); }, new SingleAttackIntent(6));
        var grow = new MoveState("GROW", async _ => { _bites = 0; await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Creature, 3, Creature, null); }, new BuffIntent());
        var branch = new ConditionalBranchState("CHOOSE");
        branch.AddState(grow, () => _bites >= 2 || Rng.NextInt(100) < 40);
        branch.AddState(bite, () => true);
        bite.FollowUpState = branch; grow.FollowUpState = bite;
        return new MonsterMoveStateMachine([bite, grow, branch], bite);
    }
}

public sealed class PrototypeHuntingSpores : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override async Task AfterDeath(PlayerChoiceContext context, Creature target, bool prevented, float length)
    {
        if (prevented || target != Owner) return;
        foreach (var player in CombatState.Players.Where(p => p.Creature.IsAlive))
            await PowerCmd.Apply<VulnerablePower>(context, player.Creature, Amount, Owner, null);
    }
}

public sealed class PrototypeHuntingDeathShield : PowerModel
{
    public override PowerType Type=>PowerType.Buff;
    public override PowerStackType StackType=>PowerStackType.Single;
    public override bool ShouldDie(Creature creature)=>creature!=Owner;
    public override async Task AfterPreventingDeath(Creature creature)
    {
        if(creature!=Owner)return;
        await CreatureCmd.Heal(creature,1);await PowerCmd.Remove(this);
    }
}

public sealed class PrototypeHuntingMystic : MonsterModel
{
    protected override string VisualsPath=>"res://ActsFromThePast/monsters/mystic/mystic.tscn";
    public override CreatureAnimator GenerateAnimator(MegaSprite controller)=>PrototypeHuntingVisualAnimator.Create(controller,"Idle","Attack","Hit");
    public override int MinInitialHp => 56;
    public override int MaxInitialHp => 56;
    public List<int> StatusRolls { get; } = [];
    public List<uint?> HealTargets { get; } = [];
    public List<uint?> BuffTargets { get; } = [];
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var cast = new MoveState("CAST", async _ =>
        {
            CardModel[] cards = [ModelDb.Card<Dazed>(), ModelDb.Card<Burn>(), ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.Void>(), ModelDb.Card<Toxic>(), ModelDb.Card<Beckon>(), ModelDb.Card<Slimed>()];
            foreach (var p in CombatState.Players.Where(p => p.Creature.IsAlive))
            {
                int roll = Rng.NextInt(6); StatusRolls.Add(roll);
                await CardPileCmd.AddGeneratedCardToCombat(CombatState.CreateCard(cards[roll], p), PileType.Discard, p);
            }
        }, new StatusIntent(1));
        var heal = new MoveState("HEAL", async _ =>
        {
            var injured = CombatState.Enemies.Where(e => e.IsAlive && e.CurrentHp < e.MaxHp).ToArray();
            if (injured.Length == 0) { HealTargets.Add(null); return; }
            var target = injured[Rng.NextInt(injured.Length)];
            HealTargets.Add(target.CombatId); await CreatureCmd.Heal(target, 12);
        }, new HealIntent());
        var buff = new MoveState("BUFF", async _ =>
        {
            var living = CombatState.Enemies.Where(e => e.IsAlive).ToArray();
            var target = living[Rng.NextInt(living.Length)]; BuffTargets.Add(target.CombatId);
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), target, 2, Creature, null);
        }, new BuffIntent());
        cast.FollowUpState = heal; heal.FollowUpState = buff; buff.FollowUpState = cast;
        return new MonsterMoveStateMachine([cast, heal, buff], cast);
    }
}

public sealed class PrototypeHuntingTorch : MonsterModel
{
    protected override string VisualsPath=>"res://ActsFromThePast/monsters/torch_head/torch_head.tscn";
    public override CreatureAnimator GenerateAnimator(MegaSprite controller)=>PrototypeHuntingVisualAnimator.Create(controller,"idle","halberd_slam","idle");
    public override int MinInitialHp => 50;
    public override int MaxInitialHp => 50;
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var hit = new MoveState("HIT", async _ => await DamageCmd.Attack(10).FromMonster(this).Execute(null), new SingleAttackIntent(10));
        hit.FollowUpState = hit;
        return new MonsterMoveStateMachine([hit], hit);
    }
}

// Only resolves the case-sensitive original animation names for this probe.
internal static class PrototypeHuntingVisualAnimator
{
    internal static CreatureAnimator Create(MegaSprite sprite,string idleName,string attackName,string hitName)
    {
        var idle=new AnimState(idleName,true);var attack=new AnimState(attackName){NextState=idle};var hit=new AnimState(hitName){NextState=idle};
        var animator=new CreatureAnimator(idle,sprite);animator.AddAnyState("Idle",idle);animator.AddAnyState("Attack",attack);animator.AddAnyState("Hit",hit);animator.AddAnyState("Cast",attack);
        return animator;
    }
}

