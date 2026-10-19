using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace AllMatters.HelloWorld;

public sealed class AllMattersHelloWorldPlusPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Godot.Color AmountLabelColor => Godot.Colors.LightGreen;

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        // When both powers exist, the original power's hook owns their one shared batch.
        return Owner.GetPower<HelloWorldPower>() == null ? Generate(player, 0) : Task.CompletedTask;
    }

    internal async Task Generate(Player player, int ordinaryAmount)
    {
        if (player != Owner.Player || AmountOnTurnStart < 1)
        {
            return;
        }
        Flash();
        var cards = CardFactory.GetDistinctForCombat(player,
            player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
                .Where(card => card.Rarity == CardRarity.Common),
            ordinaryAmount + AmountOnTurnStart, player.RunState.Rng.CombatCardGeneration).ToList();
        // Assign upgrades by batch order, never by card strength or another random roll.
        CardCmd.Upgrade(cards.Take(AmountOnTurnStart), CardPreviewStyle.None);
        await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, player);
    }
}
