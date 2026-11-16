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
        TraceAction("CAST", "begin", combat);
        try
        {
            await CreatureCmd.TriggerAnim(Creature, "Attack", 0f);
            CardModel[] statuses = [ModelDb.Card<Dazed>(), ModelDb.Card<Burn>(), ModelDb.Card<VoidCard>(),
                ModelDb.Card<Toxic>(), ModelDb.Card<Beckon>(), ModelDb.Card<Slimed>()];
            foreach (var player in combat.Players)
            {
                if (!CanAct(combat))
                    return;
                if (!player.Creature.IsAlive)
                    continue;
                var card = combat.CreateCard(statuses[Rng.NextInt(statuses.Length)], player);
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, player);
                TraceAction("CAST", "generated", combat, generated: card);
            }
        }
        finally { TraceAction("CAST", "end", combat); }
    }

    private async Task Heal(IReadOnlyList<Creature> targets)
    {
        var combat = CombatState;
        TraceAction("HEAL", "begin", combat);
        try
        {
            await CreatureCmd.TriggerAnim(Creature, "Attack", 0f);
            if (!CanAct(combat))
                return;
            var injured = combat.Enemies.Where(enemy => enemy.IsAlive && enemy.CurrentHp < enemy.MaxHp).ToArray();
            if (injured.Length != 0)
            {
                var target = injured[Rng.NextInt(injured.Length)];
                int beforeHp = target.CurrentHp;
                await CreatureCmd.Heal(target, 12);
                TraceAction("HEAL", "healed", combat, target: target, beforeHp: beforeHp);
            }
        }
        finally { TraceAction("HEAL", "end", combat); }
    }

    private async Task Buff(IReadOnlyList<Creature> targets)
    {
        var combat = CombatState;
        TraceAction("BUFF", "begin", combat);
        try
        {
            await CreatureCmd.TriggerAnim(Creature, "Attack", 0f);
            if (!CanAct(combat))
                return;
            var living = combat.Enemies.Where(enemy => enemy.IsAlive).ToArray();
            if (living.Length != 0)
                await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), living[Rng.NextInt(living.Length)], 2, Creature, null);
        }
        finally { TraceAction("BUFF", "end", combat); }
    }

    private void TraceAction(string action, string phase, ICombatState combat,
        CardModel? generated = null, Creature? target = null, int? beforeHp = null)
    {
        try
        {
            var enemies = combat.Enemies.Select((enemy, index) => new
            { index, modelId = enemy.Monster?.Id.ToString(), hp = enemy.CurrentHp }).ToArray();
            var players = combat.Players.Select((player, index) => new
            { index, discard = player.PlayerCombatState?.DiscardPile.Cards.Count }).ToArray();
            int? ownerIndex = generated == null ? null : combat.Players.Select((player, index) =>
                ReferenceEquals(player, generated.Owner) ? (int?)index : null).FirstOrDefault(index => index != null);
            int? targetIndex = target == null ? null : combat.Enemies.Select((enemy, index) =>
                ReferenceEquals(enemy, target) ? (int?)index : null).FirstOrDefault(index => index != null);
            GD.Print("[AllMatters][MysticAction] " + System.Text.Json.JsonSerializer.Serialize(new
            {
                action,
                phase,
                nextMoveId = NextMove?.Id,
                enemies,
                players,
                generatedStatusId = generated?.Id.ToString(),
                ownerIndex,
                targetIndex,
                targetModelId = target?.Monster?.Id.ToString(),
                beforeHp,
                afterHp = target?.CurrentHp
            }));
        }
        catch (Exception)
        {
            // A diagnostic sink failure must not replace or interrupt the original action.
        }
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
