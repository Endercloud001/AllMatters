using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Localization;

internal static class ClericTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Cleric requires 35 gold from every player, not removable cards", Admission),
        ("Cleric healing costs 35, rounds down, and finishes once", Healing),
        ("Cleric options require full payment and a removable card; leave is free", OptionsAndLeave),
        ("Cleric purification removes only the selected card and charges only its owner once", Purification),
        ("Cleric pack loads its portrait and bilingual dynamic option text", Presentation),
    ];

    private static EventModel Canonical => ModelDb.AllEvents.SingleOrDefault(model => model.Id.Entry == "ALL_MATTERS_EVENT_CLERIC")
        ?? throw new Exception("Cleric is missing from the registered event models.");

    private static Task Admission()
    {
        Player first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        Player second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        RunState run = RunState.CreateForTest([first, second], acts: [], seed: "CLERIC");
        first.Gold = second.Gold = 35;
        foreach (var player in new[] { first, second })
            foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        Check(Canonical.IsAllowed(run), "35 gold each must allow the event even with empty decks.");
        second.Gold = 34;
        Check(!Canonical.IsAllowed(run), "One player below 35 must exclude the event.");
        second.Gold = 75;
        first.Gold = 34;
        Check(!Canonical.IsAllowed(run), "The first player's gold must also be checked.");
        return Task.CompletedTask;
    }

    private static async Task Healing()
    {
        Player player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        RunState.CreateForTest([player], acts: [], seed: "CLERIC");
        player.Gold = 35;
        player.Creature.SetMaxHpInternal(83);
        player.Creature.SetCurrentHpInternal(10);
        EventModel model = Canonical.ToMutable();
#if STS2_PUBLIC
        await model.BeginEvent(player, false);
#else
        await model.BeginEvent(player, null, false);
#endif
        var option = model.CurrentOptions.Single(option => option.TextKey.EndsWith(".HEAL"));
        await option.Chosen();
        Check(player.Gold == 0 && player.Creature.CurrentHp == 30, "Healing must cost 35 and restore 20 of 83 max HP.");
        Check(model.IsFinished && model.CurrentOptions.Count == 0, "A service must finish the event.");
        await option.Chosen();
        Check(player.Gold == 0 && player.Creature.CurrentHp == 30, "The same choice must not charge or heal twice.");
    }

    private static async Task OptionsAndLeave()
    {
        foreach (int gold in new[] { 34, 35, 74, 75 })
        {
            Player player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var run = RunState.CreateForTest([player], acts: [], seed: "CLERIC");
            player.Gold = gold;
            player.Creature.SetCurrentHpInternal(10);
            foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
            player.Deck.AddInternal(run.CreateCard(ModelDb.Card<StrikeIronclad>(), player));
            EventModel model = await Begin(player);
            Check(model.CurrentOptions.Count == 3, "Heal, Purify and Leave must always be shown.");
            Check(model.CurrentOptions[0].IsLocked == (gold < 35), "Heal requires 35 gold.");
            Check(model.CurrentOptions[1].IsLocked == (gold < 75), "Purify requires 75 gold.");
            if (model.CurrentOptions[1].IsLocked)
            {
                await model.CurrentOptions[1].Chosen();
                Check(player.Gold == gold && player.Deck.Cards.Count == 1, "Locked options must not charge or remove cards.");
            }
            await model.CurrentOptions[2].Chosen();
            Check(model.IsFinished && player.Gold == gold && player.Creature.CurrentHp == 10 && player.Deck.Cards.Count == 1,
                "Leaving must finish without changing gold, HP or deck.");
        }
        Player noTarget = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var noTargetRun = RunState.CreateForTest([noTarget], acts: [], seed: "CLERIC");
        noTarget.Gold = 75;
        foreach (var card in noTarget.Deck.Cards.ToArray()) noTarget.Deck.RemoveInternal(card);
        noTarget.Deck.AddInternal(noTargetRun.CreateCard(ModelDb.Card<Greed>(), noTarget));
        EventModel locked = await Begin(noTarget);
        Check(locked.CurrentOptions[1].IsLocked, "A permanent curse is not a Purify target even with 75 gold.");
    }

    private static async Task<EventModel> Begin(Player player)
    {
        EventModel model = Canonical.ToMutable();
#if STS2_PUBLIC
        await model.BeginEvent(player, false);
#else
        await model.BeginEvent(player, null, false);
#endif
        return model;
    }

    private static async Task Purification()
    {
        Player player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        Player other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([player, other], acts: [], seed: "CLERIC");
        player.Gold = 80;
        other.Gold = 100;
        int otherCards = other.Deck.Cards.Count;
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        CardModel keep = run.CreateCard(ModelDb.Card<StrikeIronclad>(), player);
        CardModel selected = run.CreateCard(ModelDb.Card<DefendIronclad>(), player);
        CardModel eternal = run.CreateCard(ModelDb.Card<Greed>(), player);
        foreach (var card in new[] { keep, selected, eternal }) player.Deck.AddInternal(card);
        EventModel model = await Begin(player);
        var option = model.CurrentOptions.Single(option => option.TextKey.EndsWith(".PURIFY"));
        using (CardSelectCmd.UseSelector(new SelectCard(selected))) await option.Chosen();
        Check(model.IsFinished && player.Gold == 5, "Purification must charge 75 and finish.");
        Check(player.Deck.Cards.SequenceEqual(new[] { keep, eternal }), "Only the chosen removable card must be removed.");
        Check(other.Gold == 100 && other.Deck.Cards.Count == otherCards, "A personal choice must not alter the other player.");
        await option.Chosen();
        Check(player.Gold == 5 && player.Deck.Cards.Count == 2, "Repeated choice must not remove or charge again.");
    }

    private sealed class SelectCard(CardModel selected) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            Check(minSelect == 1 && maxSelect == 1 && options.Contains(selected), "The selection must request one legal card.");
            Check(options.All(card => card.IsRemovable), "The selector must not offer permanent cards.");
            return Task.FromResult<IEnumerable<CardModel>>([selected]);
        }

        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new InvalidOperationException("Purification is not a card reward.");
    }

    private static async Task Presentation()
    {
        var portrait = Canonical.CreateInitialPortrait();
        Check(portrait.GetWidth() == 3440 && portrait.GetHeight() == 1616, "The shipped event portrait must load from the PCK.");
        foreach (string language in new[] { "eng", "zhs" })
        {
            try
            {
                LocManager.Instance.SetLanguage(language);
                Player player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
                RunState.CreateForTest([player], acts: [], seed: "CLERIC");
                player.Gold = 75;
                player.Creature.SetMaxHpInternal(83);
                EventModel model = await Begin(player);
                Check(model.Title.GetFormattedText() == (language == "zhs" ? "牧师" : "Cleric"), "The event title must match the selected language.");
                Check(model.CurrentOptions[0].Title.GetFormattedText() == (language == "zhs" ? "治疗" : "Heal"), "The heal title must be localized.");
                // The real NEventOptionButton adds event vars before formatting.
                model.DynamicVars.AddTo(model.CurrentOptions[0].Description);
                string description = model.CurrentOptions[0].Description.GetFormattedText();
                Check(description.Contains("35") && description.Contains("20") && !description.Contains("{Heal"), "Actual cost and rounded healing must replace dynamic placeholders.");
            }
            finally { LocManager.Instance.SetLanguage("eng"); }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
