// SPDX-License-Identifier: GPL-3.0-only
// Adapted from MT-SUPER-POWER/STS2-Balance-MOD (GPLv3).
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Relics;

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_ENCHIRIDION")]
public sealed class Enchiridion : ModRelicTemplate
{
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/enchiridion.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/enchiridion.png",
        BigIconPath: "res://AllMatters/images/relics/big/enchiridion.png");

    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || Owner.Creature?.CombatState?.RoundNumber != 1)
            return;

        Flash();

        var powerCards = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.Type == CardType.Power)
            .ToList();

        CardModel card = CardFactory.GetDistinctForCombat(
            Owner,
            powerCards,
            1,
            Owner.RunState.Rng.CombatCardGeneration).First();

        card.SetToFreeThisTurn();
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
    }
}
