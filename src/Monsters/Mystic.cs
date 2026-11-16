using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;
using VoidCard = MegaCrit.Sts2.Core.Models.Cards.Void;

namespace AllMatters.Monsters;

[RegisterMonster]
public sealed class Mystic : ModMonsterTemplate
{
    public override int MinInitialHp => 56;
    public override int MaxInitialHp => 56;
    public override MonsterAssetProfile AssetProfile => new("res://AllMatters/monsters/mystic/mystic.tscn");
    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScene<NCreatureVisuals>(ResourceLoader.Load<PackedScene>(CustomVisualsPath!));

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var cast = new MoveState("CAST", Cast, new StatusIntent(1));
        var heal = new MoveState("HEAL", Heal, new HealIntent());
        var buff = new MoveState("BUFF", Buff, new BuffIntent());
        cast.FollowUpState = heal;
        heal.FollowUpState = buff;
        buff.FollowUpState = cast;
        return new MonsterMoveStateMachine([cast, heal, buff], cast);
    }

    private bool CanAct(ICombatState combat) => ReferenceEquals(CombatState, combat) &&
        combat.ContainsCreature(Creature) && Creature.IsAlive && !CombatManager.Instance.IsEnding;

    private async Task Cast(IReadOnlyList<Creature> targets)
    {
        var combat = CombatState;
        PlayCastLunge();
        CardModel[] statuses = [ModelDb.Card<Dazed>(), ModelDb.Card<Burn>(), ModelDb.Card<VoidCard>(),
            ModelDb.Card<Toxic>(), ModelDb.Card<Beckon>(), ModelDb.Card<Slimed>()];
        bool added = false;
        foreach (var player in combat.Players)
        {
            if (!CanAct(combat))
                return;
            if (!player.Creature.IsAlive)
                continue;
            var card = combat.CreateCard(statuses[Rng.NextInt(statuses.Length)], player);
            var result = await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, player);
            if (!result.success || !CanAct(combat))
                continue;
            CardCmd.PreviewCardPileAdd(result, 1.2f);
            added = true;
        }
        if (added)
            await Cmd.Wait(1f);
    }

    private void PlayCastLunge()
    {
        // The original Mystic lunges for her attack and raises her staff to heal.
        var visual = NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals?.GetNodeOrNull<Node2D>("Visuals");
        if (visual == null || !visual.IsInsideTree())
            return;
        var origin = visual.Position;
        var motion = visual.CreateTween();
        motion.TweenProperty(visual, "position", origin + Vector2.Left * 48f, 0.17);
        motion.TweenProperty(visual, "position", origin, 0.33);
    }

    private async Task Heal(IReadOnlyList<Creature> targets)
    {
        var combat = CombatState;
        await CreatureCmd.TriggerAnim(Creature, "Attack", 0f);
        if (!CanAct(combat))
            return;
        var injured = combat.Enemies.Where(enemy => enemy.IsAlive && enemy.CurrentHp < enemy.MaxHp).ToArray();
        if (injured.Length != 0)
            await CreatureCmd.Heal(injured[Rng.NextInt(injured.Length)], 12);
    }

    private async Task Buff(IReadOnlyList<Creature> targets)
    {
        var combat = CombatState;
        await CreatureCmd.TriggerAnim(Creature, "Attack", 0f);
        if (!CanAct(combat))
            return;
        var living = combat.Enemies.Where(enemy => enemy.IsAlive).ToArray();
        if (living.Length != 0)
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), living[Rng.NextInt(living.Length)], 2, Creature, null);
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
