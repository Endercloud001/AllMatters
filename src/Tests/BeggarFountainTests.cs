using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Localization;
using Godot;
using System.Text.Json;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

internal static class BeggarFountainTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Old Beggar admission checks every player's 74/75 gold, regardless of deck", BeggarAdmission),
        ("Old Beggar locks incomplete payment or no target and leaving changes nothing", BeggarOptions),
        ("Old Beggar charges once before mandatory selection and removes only its owner's chosen card", BeggarPayment),
        ("Divine Fountain requires a removable curse from every player", FountainAdmission),
        ("Divine Fountain removes all eligible curses only for its owner without side effects", FountainDrink),
        ("Beggar Fountain packs load bilingual pages and portraits including the Cleric reveal", Presentation),
        ("Beggar Fountain native inventory saves restore entry choices and completed effects at supported boundaries", SaveBoundaries),
    ];

    private static EventModel Canonical(string entry) => ModelDb.AllEvents.SingleOrDefault(model => model.Id.Entry == entry)
        ?? throw new Exception($"{entry} is missing from registered event models.");

    private static Task BeggarAdmission()
    {
        var (first, second, run) = Party();
        first.Gold = second.Gold = 75;
        Check(Canonical("ALL_MATTERS_EVENT_OLD_BEGGAR").IsAllowed(run), "Empty decks must not raise the admission requirement.");
        foreach (var player in new[] { first, second })
        {
            player.Gold = 74;
            Check(!Canonical("ALL_MATTERS_EVENT_OLD_BEGGAR").IsAllowed(run), "Every player must have at least 75 gold.");
            player.Gold = 75;
        }
        return Task.CompletedTask;
    }

    private static (Player First, Player Second, RunState Run) Party()
    {
        var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([first, second], acts: [], seed: "BEGGAR_FOUNTAIN");
        foreach (var player in run.Players)
            foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        return (first, second, run);
    }

    private static async Task BeggarOptions()
    {
        foreach (int gold in new[] { 74, 75 })
        {
            foreach (bool hasTarget in new[] { false, true })
            {
                var (player, _, run) = Party();
                player.Gold = gold;
                Add<Greed>(run, player);
                if (hasTarget) Add<StrikeIronclad>(run, player);
                var before = player.Deck.Cards.ToArray();
                decimal hp = player.Creature.CurrentHp;
                var model = await Begin("ALL_MATTERS_EVENT_OLD_BEGGAR", player);
                Check(!model.IsShared && model.CurrentOptions.Count == 2, "Give Gold and Leave are individual choices.");
                var give = model.CurrentOptions[0];
                Check(give.IsLocked == (gold < 75 || !hasTarget), "Giving requires full payment and a removable target.");
                if (give.IsLocked)
                {
                    await give.Chosen();
                    Check(player.Gold == gold && player.Deck.Cards.SequenceEqual(before), "Locked giving must do nothing.");
                }
                await model.CurrentOptions[1].Chosen();
                Check(model.IsFinished && player.Gold == gold && player.Creature.CurrentHp == hp
                    && player.Deck.Cards.SequenceEqual(before), "Leaving must preserve all resources.");
            }
        }
    }

    private static async Task BeggarPayment()
    {
        var (player, other, run) = Party();
        player.Gold = 80;
        other.Gold = 100;
        var keep = Add<StrikeIronclad>(run, player);
        var selected = Add<DefendIronclad>(run, player);
        var eternal = Add<Greed>(run, player);
        var otherCard = Add<Regret>(run, other);
        var otherEvent = await Begin("ALL_MATTERS_EVENT_OLD_BEGGAR", other);
        var model = await Begin("ALL_MATTERS_EVENT_OLD_BEGGAR", player);
        var give = model.CurrentOptions[0];
        await give.Chosen();
        Check(player.Gold == 5 && player.Deck.Cards.Count == 3 && !model.IsFinished,
            "Payment must lead to the reveal page before removing any card.");
        Check(model.CurrentOptions.Count == 1 && model.CurrentOptions[0].TextKey.EndsWith(".REMOVE_CARD"),
            "After payment the only option must continue to purification.");
        await give.Chosen();
        Check(player.Gold == 5, "A stale payment choice must not charge twice.");
        var purify = model.CurrentOptions[0];
        using (CardSelectCmd.UseSelector(new SelectCard(selected))) await purify.Chosen();
        Check(model.IsFinished && player.Gold == 5 && player.Deck.Cards.SequenceEqual(new[] { keep, eternal }),
            "Exactly the selected legal card must be removed, with no second charge.");
        await purify.Chosen();
        Check(player.Gold == 5 && player.Deck.Cards.Count == 2, "A stale purification choice cannot repeat removal.");
        Check(other.Gold == 100 && other.Deck.Cards.SequenceEqual(new[] { otherCard }) && !otherEvent.IsFinished,
            "The other player's resources and pending independent choice must remain unchanged.");
        await otherEvent.CurrentOptions[1].Chosen();
    }

    private static CardModel Add<T>(RunState run, Player player) where T : CardModel
    {
        var card = run.CreateCard(ModelDb.Card<T>(), player);
        player.Deck.AddInternal(card);
        return card;
    }

    private static Task FountainAdmission()
    {
        var (first, second, run) = Party();
        var fountain = Canonical("ALL_MATTERS_EVENT_THE_DIVINE_FOUNTAIN");
        Check(!fountain.IsShared && !fountain.IsAllowed(run), "Empty decks cannot enter the fountain.");
        Add<Regret>(run, first);
        Add<Greed>(run, second);
        Add<StrikeIronclad>(run, second);
        var protectedCurse = Add<Regret>(run, second);
        protectedCurse.AddKeyword(CardKeyword.Eternal);
        Check(!fountain.IsAllowed(run), "Removable non-curses and Eternal curses do not satisfy admission.");
        Add<Regret>(run, second);
        Check(fountain.IsAllowed(run), "Every player now has a removable curse.");
        foreach (var card in first.Deck.Cards.ToArray()) first.Deck.RemoveInternal(card);
        Check(!fountain.IsAllowed(run), "The first player must also satisfy admission.");
        return Task.CompletedTask;
    }

    private static async Task FountainDrink()
    {
        var (player, other, run) = Party();
        player.Gold = 81;
        player.Creature.SetCurrentHpInternal(12);
        var attack = Add<StrikeIronclad>(run, player);
        var permanent = Add<Greed>(run, player);
        // The book ticket owns Necronomicurse. Exercise its Eternal removal boundary
        // with a legal native curse instance instead of introducing that model here.
        var protectedCurse = Add<Regret>(run, player);
        protectedCurse.AddKeyword(CardKeyword.Eternal);
        Add<Regret>(run, player);
        Add<Regret>(run, player);
        Add<Injury>(run, player);
        var otherCurse = Add<Regret>(run, other);
        int otherGold = other.Gold;
        var otherEvent = await Begin("ALL_MATTERS_EVENT_THE_DIVINE_FOUNTAIN", other);
        var model = await Begin("ALL_MATTERS_EVENT_THE_DIVINE_FOUNTAIN", player);
        Check(model.CurrentOptions.Count == 2, "Drink and Leave must be the only choices.");
        var drink = model.CurrentOptions[0];
        await drink.Chosen();
        Check(model.IsFinished && player.Deck.Cards.SequenceEqual(new[] { attack, permanent, protectedCurse }),
            "All removable curses, including duplicates, must be removed; other cards must remain.");
        Check(player.Gold == 81 && player.Creature.CurrentHp == 12, "Drinking has no gold or HP side effects.");
        await drink.Chosen();
        Check(player.Deck.Cards.Count == 3 && player.Creature.CurrentHp == 12, "Drinking cannot repeat after completion.");
        Check(other.Deck.Cards.SequenceEqual(new[] { otherCurse }) && other.Gold == otherGold && !otherEvent.IsFinished,
            "A teammate's independent choice and resources must remain pending and unchanged.");
        await otherEvent.CurrentOptions[1].Chosen();
        Check(otherEvent.IsFinished && other.Deck.Cards.SequenceEqual(new[] { otherCurse }) && other.Gold == otherGold,
            "Leaving must preserve the teammate's curse and gold.");
    }

    private static async Task Presentation()
    {
        foreach (string language in new[] { "eng", "zhs" })
        {
            try
            {
                LocManager.Instance.SetLanguage(language);
                foreach (var (entry, title, option) in new[]
                {
                    ("OLD_BEGGAR", language == "zhs" ? "老乞丐" : "Old Beggar", language == "zhs" ? "给金币" : "Give Gold"),
                    ("THE_DIVINE_FOUNTAIN", language == "zhs" ? "神圣泉水" : "The Divine Fountain", language == "zhs" ? "喝水" : "Drink"),
                })
                {
                    var (player, _, run) = Party();
                    player.Gold = 80;
                    var card = Add<Regret>(run, player);
                    var model = await Begin("ALL_MATTERS_EVENT_" + entry, player);
                    Check(model.Title.GetFormattedText() == title, "The event title must use the selected language.");
                    var portrait = model.CreateInitialPortrait();
                    Check(portrait.GetWidth() == 3440 && portrait.GetHeight() == 1616, "The exported portrait must load.");
                    Check(model.CurrentOptions[0].Title.GetFormattedText() == option, "The action title must be localized.");
                    model.DynamicVars.AddTo(model.CurrentOptions[0].Description);
                    string description = model.CurrentOptions[0].Description.GetFormattedText();
                    Check(!description.Contains('{') && (entry != "OLD_BEGGAR" || description.Contains("75")),
                        "Option text must resolve the actual price.");
                    if (entry == "OLD_BEGGAR")
                    {
                        Check(model.GetAssetPaths(run).Contains("res://AllMatters/images/events/cleric.png"),
                            "The reveal portrait must be included when preloading the beggar.");
                        var node = new Control();
                        var image = new TextureRect { Name = "Portrait", Texture = portrait };
                        node.AddChild(image);
                        model.SetNode(node);
                        try
                        {
                            await model.CurrentOptions[0].Chosen();
                            Check(image.Texture == Canonical("ALL_MATTERS_EVENT_CLERIC").CreateInitialPortrait(),
                                "Payment must change the portrait to Cleric.");
                            Check(model.CurrentOptions[0].Title.GetFormattedText() == (language == "zhs" ? "净化" : "Purify"),
                                "The reveal page must offer localized purification.");
                            using (CardSelectCmd.UseSelector(new SelectCard(card))) await model.CurrentOptions[0].Chosen();
                        }
                        finally { node.Free(); }
                    }
                    else await model.CurrentOptions[0].Chosen();
                    Check(model.IsFinished, "The localized service must finish.");
                }
            }
            finally { LocManager.Instance.SetLanguage("eng"); }
        }
    }

    private sealed class SelectCard(CardModel selected) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            Check(minSelect == 1 && maxSelect == 1 && options.Contains(selected) && options.All(card => card.IsRemovable),
                "Purification must request exactly one legal card.");
            return Task.FromResult<IEnumerable<CardModel>>([selected]);
        }

        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new InvalidOperationException("Purification is not a reward.");
    }

    private static async Task SaveBoundaries()
    {
#if !STS2_PUBLIC
        MegaCrit.Sts2.Core.Modding.AssemblyInfo.Init();
#endif
        MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache.Init();
        var (player, other, run) = Party();
        player.Gold = 80;
        other.Gold = 100;
        var card = Add<Regret>(run, player);
        Add<StrikeIronclad>(run, player);
        Add<Regret>(run, other);
        // Ordinary events save on map-point entry, not on payment or in the selector.
        // Round-trip the real player format at that boundary, leaving disk/UI lifecycle to playtesting.
        var entrySave = Snapshot(run.Players);
        var model = await Begin("ALL_MATTERS_EVENT_OLD_BEGGAR", player);
        await model.CurrentOptions[0].Chosen();
        var selector = new PendingSelection(card);
        using (CardSelectCmd.UseSelector(selector))
        {
            var removal = model.CurrentOptions[0].Chosen();
            Check(selector.WasRequested && !removal.IsCompleted && player.Gold == 5 && player.Deck.Cards.Count == 2,
                "Payment must already be spent while mandatory selection remains pending.");
            var restored = Restore(entrySave);
            Check(restored[0].Gold == 80 && restored[0].Deck.Cards.Count == 2,
                "Loading the supported entry snapshot rolls back both payment and unfinished removal.");
            var restarted = await Begin("ALL_MATTERS_EVENT_OLD_BEGGAR", restored[0]);
            Check(restarted.CurrentOptions.Count == 2 && !restarted.CurrentOptions[0].IsLocked,
                "The entry snapshot must restore the player's unmade choice, not skip purification.");
            await restarted.CurrentOptions[1].Chosen();
            selector.Complete();
            await removal;
        }
        var completed = Restore(Snapshot(run.Players));
        Check(completed[0].Gold == 5 && completed[0].Deck.Cards.Count == 1
            && completed[0].Deck.Cards.Single().Id == ModelDb.Card<StrikeIronclad>().Id,
            "The inventory saved on the next node must contain exactly one payment and removal.");
        Check(completed[1].Gold == 100 && completed[1].Deck.Cards.Single().Id == ModelDb.Card<Regret>().Id,
            "Saving must retain the other player's independent resources.");

        var fountain = await Begin("ALL_MATTERS_EVENT_THE_DIVINE_FOUNTAIN", other);
        var beforeDrink = Snapshot(run.Players);
        await fountain.CurrentOptions[0].Chosen();
        var afterDrink = Restore(Snapshot(run.Players));
        Check(afterDrink[1].Deck.Cards.Count == 0 && afterDrink[1].Gold == 100,
            "Completed cleansing survives the native inventory format without touching gold.");
        var before = Restore(beforeDrink);
        var unfinished = await Begin("ALL_MATTERS_EVENT_THE_DIVINE_FOUNTAIN", before[1]);
        Check(before[1].Deck.Cards.Single().Id == ModelDb.Card<Regret>().Id && !unfinished.IsFinished,
            "Loading the pre-drink node must restore the curse and a pending choice.");
        await unfinished.CurrentOptions[0].Chosen();
        Check(before[1].Deck.Cards.Count == 0, "The restored choice must remain executable once.");
    }

    private static string[] Snapshot(IEnumerable<Player> players) => players.Select(player =>
        JsonSerializer.Serialize(player.ToSerializable(), JsonSerializationUtility.GetTypeInfo<SerializablePlayer>())).ToArray();

    private static Player[] Restore(string[] snapshots)
    {
        var players = snapshots.Select(json => Player.FromSerializable(
            JsonSerializer.Deserialize(json, JsonSerializationUtility.GetTypeInfo<SerializablePlayer>())!)).ToArray();
        RunState.CreateForTest(players, acts: [], seed: "BEGGAR_FOUNTAIN");
        return players;
    }

    private sealed class PendingSelection(CardModel selected) : ICardSelector
    {
        private readonly TaskCompletionSource<IEnumerable<CardModel>> _answer = new();
        public bool WasRequested { get; private set; }

        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            Check(minSelect == 1 && maxSelect == 1 && options.Contains(selected), "The pending selector requires one legal card.");
            WasRequested = true;
            return _answer.Task;
        }

        public void Complete() => _answer.SetResult([selected]);

        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new InvalidOperationException("Purification is not a reward.");
    }

    private static async Task<EventModel> Begin(string entry, Player player)
    {
        var model = Canonical(entry).ToMutable();
#if STS2_PUBLIC
        await model.BeginEvent(player, false);
#else
        await model.BeginEvent(player, null, false);
#endif
        return model;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
