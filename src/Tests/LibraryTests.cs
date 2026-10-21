using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using HarmonyLib;
using AllMatters.Relics;

internal static class LibraryTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Library admits only Act 2 and sleeps once for rounded 33 percent of owner HP", Sleep),
        ("Library reading offers 20 distinct legal other-character cards and adds exactly one", Read),
        ("Library cleaning binds one eligible card and locks when no bottle remains", Clean),
        ("Library reading handles 0, 1, 19, 20 and 21 legal cards without padding", SmallPools),
        ("Library entry reload repeats candidates and story without stacking the discarded reward", ReadSave),
        ("Library bottles restore exact instance binding and preserve independent Innate on removal", BottleSave),
        ("Library bottles allow combat exhaustion and transformation, keeping next combat's original", BottleCombat),
        ("Library cleaning samples only available bottle kinds with one uniform roll", BottleKinds),
        ("Library PCK loads bilingual pages, prompts, actual healing and original bottle icons", Presentation),
    ];

    private static EventModel Canonical => ModelDb.AllEvents.Single(model => model.Id.Entry == "ALL_MATTERS_EVENT_THE_LIBRARY");

    private static async Task Sleep()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var owner = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([owner, other], acts: [], seed: "LIBRARY");
        foreach (int act in new[] { 0, 1, 2 })
        {
            run.CurrentActIndex = act;
            Check(Canonical.IsAllowed(run) == (act == 1), "Library must appear only in Act 2.");
        }
        owner.Creature.SetMaxHpInternal(83);
        owner.Creature.SetCurrentHpInternal(10);
        other.Creature.SetCurrentHpInternal(10);
        var model = await Begin(owner);
        Check(!model.IsShared, "Choices are personal.");
        var options = model.CurrentOptions.ToArray();
        await options.Single(option => option.TextKey.EndsWith(".SLEEP")).Chosen();
        foreach (var option in options) await option.Chosen();
        Check(owner.Creature.CurrentHp == 37 && other.Creature.CurrentHp == 10 && model.IsFinished,
            "Sleep restores floor(83 * .33) = 27 once and affects only the owner.");
    }

    private static async Task<EventModel> Begin(Player player)
    {
        var model = Canonical.ToMutable();
#if STS2_PUBLIC
        await model.BeginEvent(player, false);
#else
        await model.BeginEvent(player, null, false);
#endif
        return model;
    }

    private static async Task Read()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        CharacterModel[] characters = [ModelDb.Character<Ironclad>(), ModelDb.Character<Silent>(), ModelDb.Character<Regent>(),
            ModelDb.Character<Necrobinder>(), ModelDb.Character<Defect>()];
        foreach (var character in characters)
        foreach (bool multiplayer in new[] { false, true })
        {
            var player = Player.CreateForNewRun(character, multiplayer ? UnlockState.all : UnlockState.none, 1);
            var other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
            RunState.CreateForTest(multiplayer ? [player, other] : [player], acts: [], seed: "LIBRARY_READ");
            int before = player.Deck.Cards.Count;
            var model = await Begin(player);
            var selector = new Choose(cards =>
            {
                Check(cards.Length == 20 && cards.Select(card => card.Id).Distinct().Count() == 20, "Twenty unique choices are required.");
                foreach (var card in cards)
                {
                    Check(card.Pool.Id != character.CardPool.Id && characters.Any(c => c.Id != character.Id && c.CardPool.Id == card.Pool.Id),
                        "Only the other four original character pools are allowed.");
                    Check(card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare, "Exclude basic, event, ancient and token cards.");
                    Check(card.Pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint).Any(c => c.Id == card.Id), "Respect unlocks and player count.");
                    Check(!card.IsUpgraded && card.Owner == player, "Keep Balance's noncombat NoUpgradeRoll and assign the owner.");
                }
                return cards[3];
            });
            var options = model.CurrentOptions.ToArray();
            using (CardSelectCmd.UseSelector(selector)) await options[0].Chosen();
            foreach (var option in options) await option.Chosen();
            Check(player.Deck.Cards.Count == before + 1 && ReferenceEquals(player.Deck.Cards.Last(), selector.Selected) && model.IsFinished,
                "Add exactly the selected instance once, with stale choices ignored.");
        }
    }

    private static async Task Clean()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player], acts: [], seed: "LIBRARY_CLEAN");
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        var target = run.CreateCard(ModelDb.Card<StrikeIronclad>(), player);
        var twin = run.CreateCard(ModelDb.Card<StrikeIronclad>(), player);
        player.Deck.AddInternal(target);
        player.Deck.AddInternal(twin);
        var model = await Begin(player);
        var clean = model.CurrentOptions.Single(option => option.TextKey.EndsWith(".CLEAN"));
        using (CardSelectCmd.UseSelector(new Choose(cards =>
        {
            Check(cards.Length == 2 && cards.Contains(target) && cards.Contains(twin), "Only legal attack instances should be offered.");
            return target;
        }))) await clean.Chosen();
        await clean.Chosen();
        Check(player.Relics.Count(relic => relic.Id.Entry == "ALL_MATTERS_BOTTLED_FLAME") == 1, "Only Flame has a legal target; award it once.");
        Check(target.Keywords.Contains(CardKeyword.Innate) && !twin.Keywords.Contains(CardKeyword.Innate), "Bind one instance, not its name.");
        Check(!target.IsRemovable && !target.IsTransformable && twin.IsRemovable, "The bound deck instance is protected.");
        CardCmd.Upgrade(target);
        Check(target.IsUpgraded && target.Keywords.Contains(CardKeyword.Innate), "Upgrading keeps the binding.");
        var locked = await Begin(player);
        Check(locked.CurrentOptions.Single(option => option.TextKey.EndsWith(".CLEAN_LOCKED")).IsLocked,
            "Owned Flame and absent skill/power targets must lock cleaning.");
        var bottle = player.Relics.Single(relic => relic.Id.Entry == "ALL_MATTERS_BOTTLED_FLAME");
        await RelicCmd.Remove(bottle);
        Check(!target.Keywords.Contains(CardKeyword.Innate) && target.IsRemovable, "Losing the bottle removes only its grant and protection.");
        using (CardSelectCmd.UseSelector(new Choose(_ => target))) await RelicCmd.Obtain<BottledFlame>(player);
        target.AddKeyword(CardKeyword.Innate);
        await RelicCmd.Remove(player.Relics.OfType<BottledFlame>().Single());
        Check(target.Keywords.Contains(CardKeyword.Innate), "A later independent local Innate grant survives losing the bottle in the same session.");
    }

    private static async Task SmallPools()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var cards = new[] { ModelDb.Character<Silent>().CardPool, ModelDb.Character<Defect>().CardPool }
            .SelectMany(pool => pool.GetUnlockedCards(UnlockState.all, CardMultiplayerConstraint.SingleplayerOnly))
            .Where(card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare).Take(21).ToArray();
        foreach (int count in new[] { 0, 1, 19, 20, 21 })
        {
            LibraryPoolPreparation.Allowed = cards.Take(count).Select(card => card.Id).ToHashSet();
            try
            {
                var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
                RunState.CreateForTest([player], acts: [], seed: "SMALL_LIBRARY");
                var model = await Begin(player);
                int before = player.Deck.Cards.Count;
                var read = model.CurrentOptions[0];
                Check(read.IsLocked == (count == 0), "Only an empty legal pool locks reading.");
                var selector = new Choose(options =>
                {
                    Check(options.Length == Math.Min(count, 20) && options.Select(c => c.Id).Distinct().Count() == options.Length,
                        "No duplicate padding or omitted choices from a short pool.");
                    Check(options.All(card => LibraryPoolPreparation.Allowed.Contains(card.Id)), "Do not relax eligibility to fill the grid.");
                    return options[0];
                });
                using (CardSelectCmd.UseSelector(selector)) await read.Chosen();
                Check(player.Deck.Cards.Count == before + (count == 0 ? 0 : 1), "Select one card, or do nothing when locked.");
                if (count == 1) Check(player.Deck.Cards.Last().Id == cards[0].Id, "The sole candidate must be added without requiring a grid.");
            }
            finally { LibraryPoolPreparation.Allowed = null; }
        }
    }

    private static async Task ReadSave()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        RunState.CreateForTest([player], acts: [], seed: "LIBRARY_SAVE");
        var entry = player.ToSerializable();
        var room = new EventRoom(Canonical).ToSerializable();
        Check(!room.IsPreFinished && new EventRoom(room).CanonicalEvent.Id == Canonical.Id, "Native entry room retains the Library identity.");
        ModelId[]? first = null;
        string? story = null;
        foreach (bool restore in new[] { false, true })
        {
            if (restore)
            {
                player = Player.FromSerializable(entry);
                RunState.CreateForTest([player], acts: [], seed: "LIBRARY_SAVE");
            }
            int before = player.Deck.Cards.Count;
            var model = await Begin(player);
            var selector = new Choose(cards =>
            {
                if (restore) Check(cards.Select(card => card.Id).SequenceEqual(first!), "Reloading entry must repeat the candidate order.");
                else first = cards.Select(card => card.Id).ToArray();
                return cards[4];
            });
            using (CardSelectCmd.UseSelector(selector)) await model.CurrentOptions[0].Chosen();
            string text = model.Description!.GetFormattedText();
            if (restore) Check(text == story, "Reloading the same choice must repeat the book story.");
            else story = text;
            var loaded = Player.FromSerializable(player.ToSerializable());
            Check(loaded.Deck.Cards.Count == before + 1 && loaded.Deck.Cards.Last().Id == first![4], "Completed inventory restores one selected reward without pickup effects.");
        }
    }

    private static async Task BottleSave()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (bool independentInnate in new[] { false, true })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var run = RunState.CreateForTest([player], acts: [], seed: "BOTTLE_SAVE");
            CardModel canonical = independentInnate ? ModelDb.Card<Afterimage>() : ModelDb.Card<StrikeIronclad>();
            var target = run.CreateCard(canonical, player);
            var twin = run.CreateCard(canonical, player);
            player.Deck.AddInternal(twin);
            player.Deck.AddInternal(target);
            using (CardSelectCmd.UseSelector(new Choose(_ => target)))
                await RelicCmd.Obtain(independentInnate ? ModelDb.Relic<BottledTornado>().ToMutable() : ModelDb.Relic<BottledFlame>().ToMutable(), player);
            CardCmd.Upgrade(target);
            // Move a different same-name card; a saved deck index would now be wrong.
            player.Deck.RemoveInternal(twin);
            player.Deck.AddInternal(twin);
            var loaded = Player.FromSerializable(player.ToSerializable());
            RunState.CreateForTest([loaded], acts: [], seed: "BOTTLE_SAVE");
            var bound = loaded.Deck.Cards.Single(card => card.Id == target.Id && card.IsUpgraded);
            Check(bound.Keywords.Contains(CardKeyword.Innate) && !bound.IsRemovable && !bound.IsTransformable,
                "Native saved properties must restore the precise upgraded instance binding.");
            Check(loaded.Deck.Cards.Where(card => card.Id == target.Id && !card.IsUpgraded).All(card => !card.Keywords.Contains(CardKeyword.Innate)), "Do not bind identical unselected cards after loading.");
            bool rejected = false;
            try { await CardPileCmd.RemoveFromDeck(bound, false); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && loaded.Deck.Cards.Contains(bound), "Direct permanent removal must also preserve the binding.");
            await RelicCmd.Remove(loaded.Relics.OfType<BottledCard>().Single());
            Check(bound.Keywords.Contains(CardKeyword.Innate) == independentInnate && bound.IsRemovable && bound.IsTransformable,
                "Remove only the bottle's grant; independently added Innate survives removal and save/load.");
        }
    }

    private static async Task BottleCombat()
    {
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player], acts: [], seed: "BOTTLE_COMBAT");
        var target = player.Deck.Cards.First(card => card.Type == CardType.Attack);
        using (HeadlessCombatInProgress.EnterEvent())
        using (CardSelectCmd.UseSelector(new Choose(_ => target))) await RelicCmd.Obtain<BottledFlame>(player);
        foreach (bool transform in new[] { false, true, false })
        {
            var combat = new CombatState(runState: run);
            combat.AddPlayer(player);
            player.ResetCombatState();
            player.PopulateCombatState(new Rng(42), combat);
            var copy = player.PlayerCombatState!.DrawPile.Cards.Single(card => ReferenceEquals(card.DeckVersion, target));
            Check(copy.Keywords.Contains(CardKeyword.Innate) && copy.IsRemovable && copy.IsTransformable,
                "Every combat gives the original bound card Innate without applying deck restrictions to its combat instance.");
            if (transform)
            {
                var replacement = combat.CreateCard<DefendIronclad>(player);
                await CardCmd.Transform(copy, replacement, CardPreviewStyle.None);
                Check(!replacement.Keywords.Contains(CardKeyword.Innate) && replacement.DeckVersion == null, "Combat transformation must not transfer binding or the bottle's Innate.");
            }
            else
            {
                await CardCmd.Exhaust(new ThrowingPlayerChoiceContext(), copy, skipVisuals: true);
                Check(player.PlayerCombatState.ExhaustPile.Cards.Contains(copy), "Combat exhaustion is allowed.");
            }
            Check(player.Deck.Cards.Contains(target) && target.Keywords.Contains(CardKeyword.Innate) && !target.IsRemovable,
                "Combat actions leave the original permanent binding intact.");
        }
    }

    private static async Task BottleKinds()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var kinds = new[] { ModelDb.Relic<BottledFlame>(), (RelicModel)ModelDb.Relic<BottledLightning>(), ModelDb.Relic<BottledTornado>() };
        foreach (var relic in kinds)
        {
            Check(relic.Rarity == MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Event && ModelDb.RelicPool<EventRelicPool>().AllRelics.Contains(relic),
                "Bottles must remain event-only rather than common, shop or wheel drops.");
        }
        foreach (int mask in Enumerable.Range(0, 8))
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var run = RunState.CreateForTest([player], acts: [], seed: "BOTTLE_KINDS");
            foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
            CardModel[] targets = [run.CreateCard(ModelDb.Card<StrikeIronclad>(), player), run.CreateCard(ModelDb.Card<DefendIronclad>(), player), run.CreateCard(ModelDb.Card<Inflame>(), player)];
            for (int i = 0; i < 3; i++)
            {
                player.Deck.AddInternal(targets[i]);
                if ((mask & (1 << i)) == 0) targets[i].AddKeyword(CardKeyword.Innate);
            }
            var available = Enumerable.Range(0, 3).Where(i => (mask & (1 << i)) != 0).ToArray();
            var model = await Begin(player);
            var clean = model.CurrentOptions[2];
            Check(clean.IsLocked == (mask == 0), "Only types with a non-Innate target are eligible.");
            if (mask == 0) { await clean.Chosen(); continue; }
#if STS2_PUBLIC
            var reference = new Rng(model.Rng.Seed, model.Rng.Counter);
#else
            var reference = new Rng(model.Rng.ToSerializable());
#endif
            int expected = available[reference.NextInt(available.Length)];
            using (CardSelectCmd.UseSelector(new Choose(cards =>
            {
                Check(cards.Length == 1 && ReferenceEquals(cards[0], targets[expected]), "Each bottle must offer only its eligible card type.");
                return cards[0];
            }))) await clean.Chosen();
#if STS2_PUBLIC
            bool sameRng = model.Rng.Counter == reference.Counter;
#else
            var jsonOptions = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
            bool sameRng = System.Text.Json.JsonSerializer.Serialize(model.Rng.ToSerializable(), jsonOptions)
                == System.Text.Json.JsonSerializer.Serialize(reference.ToSerializable(), jsonOptions);
#endif
            Check(player.Relics.Count(relic => relic.Id == kinds[expected].Id) == 1 && sameRng,
                "One uniform index chooses among available kinds without rerolling unavailable bottles.");
        }
    }

    private static async Task Presentation()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        Check(Canonical.CreateInitialPortrait().GetWidth() > 0, "Library portrait must load from the exported PCK.");
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
                RunState.CreateForTest([player], acts: [], seed: "LIBRARY_TEXT");
                player.Creature.SetMaxHpInternal(83);
                var model = await Begin(player);
                Check(model.Title.GetFormattedText() == (language == "zhs" ? "大图书馆" : "The Library"), "Localize the event title.");
                model.DynamicVars.AddTo(model.CurrentOptions[1].Description);
                string sleep = model.CurrentOptions[1].Description.GetFormattedText();
                Check(sleep.Contains("27") && !sleep.Contains('{'), "Display the calculated, rounded healing amount.");
                foreach (string suffix in new[] { "READ", "SLEEP", "CLEAN" })
                    Check(model.CurrentOptions.Any(option => option.TextKey.EndsWith('.' + suffix)), "Offer Read, Sleep and Clean in that order.");
                var prompt = new LocString("events", Canonical.Id.Entry + ".pages.READ.selectionScreenPrompt");
                Check(!prompt.GetFormattedText().Contains("selectionScreenPrompt"), "Reading selection prompt must be translated.");
                foreach (var relic in new RelicModel[] { ModelDb.Relic<BottledFlame>(), ModelDb.Relic<BottledLightning>(), ModelDb.Relic<BottledTornado>() })
                {
                    var asset = ((BottledCard)relic).AssetProfile;
                    foreach (string? path in new[] { asset.IconPath, asset.IconOutlinePath, asset.BigIconPath })
                        Check(Godot.ResourceLoader.Load<Godot.Texture2D>(path!).GetWidth() > 0, "All bottle icon variants must load from PCK.");
                    Check(!relic.Title.GetFormattedText().Contains("ALL_MATTERS"), "Relic title must be localized.");
                    Check(!new LocString("relics", relic.Id.Entry + ".selectionScreenPrompt").GetFormattedText().Contains("selectionScreenPrompt"), "Bottle prompt must be translated.");
                }
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
    }

    private sealed class Choose(Func<CardModel[], CardModel> choose) : ICardSelector
    {
        public CardModel? Selected { get; private set; }
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            Check(minSelect == 1 && maxSelect == 1, "Selection must require exactly one card.");
            Selected = choose(options.ToArray());
            return Task.FromResult<IEnumerable<CardModel>>([Selected]);
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new InvalidOperationException("Library uses a mandatory grid.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

// A deliberately reduced legal source pool reaches scarcity cases through the
// real event/selector. It does not replace sampling, card creation or commands.
[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
internal static class LibraryPoolPreparation
{
    internal static HashSet<ModelId>? Allowed;
    private static void Postfix(ref IEnumerable<CardModel> __result)
    {
        if (Allowed != null) __result = __result.Where(card => Allowed.Contains(card.Id)).ToArray();
    }
}
