// SPDX-License-Identifier: GPL-3.0-only
// Adapted from MT-SUPER-POWER/STS2-Balance-MOD (GPLv3).
using MegaCrit.Sts2.Core.Combat;
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

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_NILRYS_CODEX")]
public sealed class NilrysCodex : ModRelicTemplate
{
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/nilrys_codex.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/nilrys_codex.png",
        BigIconPath: "res://AllMatters/images/relics/big/nilrys_codex.png");

    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task BeforeFlushLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
            return;

        if (CombatManager.Instance?.IsInProgress != true)
            return;

        Flash();

        var cardChoices = CardFactory.GetDistinctForCombat(
            Owner,
            Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint),
            3,
            Owner.RunState.Rng.CombatCardGeneration).ToList();

        foreach (CardModel card in cardChoices)
            CardCmd.Upgrade(card);

        CardModel? selectedCard = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            cardChoices,
            Owner,
            true);

        if (selectedCard == null)
            return;

        CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(
            selectedCard,
            PileType.Draw,
            Owner,
            CardPilePosition.Random);
        CardCmd.PreviewCardPileAdd(result);
    }
}
