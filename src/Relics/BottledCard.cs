// SPDX-License-Identifier: GPL-3.0-only
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace AllMatters.Relics;

public abstract class BottledCard : ModRelicTemplate
{
    // Persist on the actual card, rather than a deck index or model ID: duplicate
    // cards and deck reordering must not move the binding to a different instance.
    internal static readonly SavedAttachedState<CardModel, string> Binding = new("AllMattersBottle");

    protected abstract CardType BoundType { get; }
    protected abstract string IconName { get; }
    public override RelicRarity Rarity => RelicRarity.Event;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://AllMatters/images/relics/{IconName}.svg",
        IconOutlinePath: $"res://AllMatters/images/relics/outlines/{IconName}.svg",
        BigIconPath: $"res://AllMatters/images/relics/{IconName}.svg");

    public bool CanBind(CardModel card) => card.Type == BoundType
        && !card.Keywords.Contains(CardKeyword.Innate) && string.IsNullOrEmpty(Binding.GetValueOrDefault(card));

    public bool IsAvailable(Player player) => player.Relics.All(relic => relic.Id != Id) && player.Deck.Cards.Any(CanBind);

    public override async Task AfterObtained()
    {
        var prefs = new CardSelectorPrefs(new LocString("relics", $"{Id.Entry}.selectionScreenPrompt"), 1) { Cancelable = false };
        var selected = (await CardSelectCmd.FromDeckGeneric(Owner, prefs, CanBind)).Single();
        Binding[selected] = Id.Entry;
    }

    public override Task AfterRemoved()
    {
        foreach (var card in Owner.Deck.Cards.Where(card => Binding.GetValueOrDefault(card) == Id.Entry))
            Binding.Remove(card);
        return Task.CompletedTask;
    }

    internal static bool IsBound(CardModel card)
    {
        if (card.IsCanonical) return false;
        var original = card.DeckVersion ?? card;
        string? entry = Binding.GetValueOrDefault(original);
        return !string.IsNullOrEmpty(entry) && original.Owner?.Relics.Any(relic => relic is BottledCard && relic.Id.Entry == entry) == true;
    }

    internal static bool IsProtected(CardModel card) => card.Pile?.Type == PileType.Deck && IsBound(card);
}
