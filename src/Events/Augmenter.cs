// SPDX-License-Identifier: GPL-3.0-only
// Adapted from MT-SUPER-POWER/STS2-Balance-MOD, Augmenter (GPLv3).
using AllMatters.Cards;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using AllMatters.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Events;

[RegisterSharedEvent]
public sealed class Augmenter : ModEventTemplate
{
    public override bool IsShared => false;
    public override EventAssetProfile AssetProfile => new(InitialPortraitPath: "res://AllMatters/images/events/augmenter.png");
    public override bool IsAllowed(IRunState runState) => runState.CurrentActIndex == 1
        && runState.Players.All(player => player.Deck.Cards.Count(card => card.IsRemovable) >= 2);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        bool canTransform = Owner!.Deck.Cards.Count(card => card.Type != CardType.Quest && card.IsTransformable) >= 2;
        bool canExchange = StartingRelic() != null;
        return
        [
            new(this, Jax, InitialOptionKey("JAX"), [HoverTipFactory.FromCard(ModelDb.Card<Jax>())]),
            new(this, canTransform ? Transform : null, InitialOptionKey(canTransform ? "TRANSFORM" : "TRANSFORM_LOCKED"), []),
            new(this, canExchange ? Mutagens : null, InitialOptionKey(canExchange ? "MUTAGENS" : "MUTAGENS_LOCKED"), HoverTipFactory.FromRelic(ModelDb.Relic<MutagenicStrength>()).ToArray()),
        ];
    }

    private RelicModel? StartingRelic() => Owner!.Relics.FirstOrDefault(relic => Owner.Character.StartingRelics.Any(starter => starter.Id == relic.Id));

    private async Task Mutagens()
    {
        var starter = StartingRelic() ?? throw new InvalidOperationException("The original starting relic is required.");
        await RelicCmd.Remove(starter);
        await RelicCmd.Obtain<MutagenicStrength>(Owner!);
        SetEventFinished(PageDescription("MUTAGENS"));
    }

    private async Task Jax()
    {
        var card = Owner!.RunState.CreateCard(ModelDb.Card<Jax>(), Owner);
        var result = await CardPileCmd.Add(card, PileType.Deck);
        CardCmd.PreviewCardPileAdd(result, 2f);
        SetEventFinished(PageDescription("JAX"));
    }

    private async Task Transform()
    {
        var owner = Owner!;
        var candidates = owner.Character.CardPool.GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint)
            .Where(card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare).ToArray();
        var preferences = new CardSelectorPrefs(L10NLookup($"{Id.Entry}.pages.TRANSFORM.selectionScreenPrompt"), 2) { Cancelable = false };
        var selected = await CardSelectCmd.FromDeckForTransformation(owner, preferences, card => new CardTransformation(card, candidates));
        foreach (var card in selected.ToArray())
        {
            var result = (await CardCmd.Transform(new CardTransformation(card, candidates).Yield(), owner.RunState.Rng.Niche, CardPreviewStyle.HorizontalLayout)).Single();
            CardCmd.Upgrade(result.cardAdded, CardPreviewStyle.HorizontalLayout);
        }
        SetEventFinished(PageDescription("TRANSFORM"));
    }
}
