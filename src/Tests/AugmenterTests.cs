using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using Godot;
using System.Text.Json;

internal static class AugmenterTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Augmenter JAX choice grants one ordinary Innate event skill to its owner and finishes", JaxChoice),
        ("Augmenter admission checks Act 2 and every player's removable cards", Admission),
        ("Augmenter transforms exactly two legal cards and upgrades the new instances only", Transformation),
        ("Augmenter transforms colorless and event or curse cards into upgraded character cards only", ClassOnlyTransformation),
        ("Augmenter exchanges the actual starter relic for all five characters and locks missing or replaced starters", StarterExchange),
        ("Augmenter Mutagenic Strength decays once on the third owner turn and resets next combat; Artifact blocks decay", Decay),
        ("Augmenter JAX ordinary and upgraded plays cost zero energy, lose 3 HP and grant 2 or 3 strength", JaxPlay),
        ("Augmenter bilingual event, card, relic and power text and artwork load from its PCK", Presentation),
    ];

    private static EventModel Canonical => ModelDb.AllEvents.SingleOrDefault(model => model.Id.Entry == "ALL_MATTERS_EVENT_AUGMENTER")
        ?? throw new Exception("Augmenter is missing from registered events.");

    private static Task Admission()
    {
        var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([first, second], acts: [], seed: "AUGMENTER");
        foreach (int act in new[] { 0, 1, 2 })
        {
            run.CurrentActIndex = act;
            Check(Canonical.IsAllowed(run) == (act == 1), "Only Act 2 is eligible.");
        }
        run.CurrentActIndex = 1;
        foreach (var player in run.Players)
        {
            var originals = player.Deck.Cards.ToArray();
            foreach (var card in originals) player.Deck.RemoveInternal(card);
            player.Deck.AddInternal(run.CreateCard(ModelDb.Card<StrikeIronclad>(), player));
            player.Deck.AddInternal(run.CreateCard(ModelDb.Card<Greed>(), player));
            Check(!Canonical.IsAllowed(run), "One removable card plus an Eternal curse is insufficient for either player.");
            player.Deck.AddInternal(run.CreateCard(ModelDb.Card<DefendIronclad>(), player));
            Check(Canonical.IsAllowed(run), "Two removable cards each suffice even alongside an Eternal curse.");
        }
        return Task.CompletedTask;
    }

    private static async Task Transformation()
    {
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([player, other], acts: [], seed: "AUGMENTER");
        var old = player.Deck.Cards.Take(2).ToArray();
        var keep = player.Deck.Cards.Skip(2).ToArray();
        var teammates = other.Deck.Cards.ToArray();
        var model = await Begin(player);
        var option = model.CurrentOptions.Single(option => option.TextKey.EndsWith(".TRANSFORM"));
        using (CardSelectCmd.UseSelector(new SelectTwo(old))) await option.Chosen();
        var added = player.Deck.Cards.Except(keep).ToArray();
        Check(model.IsFinished && added.Length == 2 && added.All(card => card.IsUpgraded && card.Owner == player), "Exactly two new owned cards must be upgraded.");
        Check(old.All(card => !player.Deck.Cards.Contains(card) && !card.IsUpgraded), "Replaced objects must not be upgraded.");
        Check(added.All(card => !ModelDb.GetById<CardModel>(card.Id).IsUpgraded), "Canonical templates must stay ordinary.");
        Check(keep.All(card => !card.IsUpgraded) && other.Deck.Cards.SequenceEqual(teammates), "Unselected cards and teammate must be unchanged.");
        await option.Chosen();
        Check(player.Deck.Cards.Except(keep).SequenceEqual(added), "Repeated choice must not transform again.");
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        player.Deck.AddInternal(run.CreateCard(ModelDb.Card<Greed>(), player));
        model = await Begin(player);
        Check(model.CurrentOptions.Single(option => option.TextKey.EndsWith(".TRANSFORM_LOCKED")).IsLocked, "Insufficient legal targets must lock transformation.");
    }

    private static async Task StarterExchange()
    {
        CharacterModel[] characters = [ModelDb.Character<Ironclad>(), ModelDb.Character<Silent>(), ModelDb.Character<Regent>(), ModelDb.Character<Necrobinder>(), ModelDb.Character<Defect>()];
        foreach (var character in characters)
        {
            foreach (bool replaced in new[] { false, true })
            {
                var player = Player.CreateForNewRun(character, UnlockState.all, 1);
                var other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
                RunState.CreateForTest([player, other], acts: [], seed: "AUGMENTER");
                var starter = player.Relics.Single(relic => character.StartingRelics.Any(original => original.Id == relic.Id));
                await RelicCmd.Remove(starter);
                var mask = await RelicCmd.Obtain<RedMask>(player);
                if (replaced)
                {
                    var locked = await Begin(player);
                    var option = locked.CurrentOptions.Single(option => option.TextKey.EndsWith(".MUTAGENS_LOCKED"));
                    Check(option.IsLocked, "A replacement cannot pay the starter cost.");
                    await option.Chosen();
                    Check(player.Relics.SequenceEqual(new[] { mask }), "A locked choice cannot grant a free relic or remove a replacement.");
                    await RelicCmd.Remove(mask);
                    locked = await Begin(player);
                    Check(locked.CurrentOptions.Single(option => option.TextKey.EndsWith(".MUTAGENS_LOCKED")).IsLocked, "An empty relic bar cannot pay either.");
                    continue;
                }
                await RelicCmd.Obtain(starter, player);
                Check(player.Relics.First() == mask, "Prepare starter in a later slot.");
                var teammates = other.Relics.ToArray();
                var model = await Begin(player);
                var exchange = model.CurrentOptions.Single(option => option.TextKey.EndsWith(".MUTAGENS"));
                await exchange.Chosen();
                Check(model.IsFinished && player.Relics.Count == 2 && player.Relics.Contains(mask) && !player.Relics.Contains(starter), "Exchange must remove the actual starter and keep the unrelated first relic.");
                Check(player.Relics.Any(relic => relic.Id.Entry == "ALL_MATTERS_MUTAGENIC_STRENGTH"), "Exchange must grant Mutagenic Strength.");
                await exchange.Chosen();
                Check(player.Relics.Count == 2 && other.Relics.SequenceEqual(teammates), "Exchange must happen once and affect only its owner.");
            }
        }
    }

    private static async Task ClassOnlyTransformation()
    {
        CharacterModel[] characters = [ModelDb.Character<Ironclad>(), ModelDb.Character<Silent>(), ModelDb.Character<Regent>(), ModelDb.Character<Necrobinder>(), ModelDb.Character<Defect>()];
        foreach (var character in characters)
        {
            foreach (bool eventAndCurse in new[] { false, true })
            {
                var player = Player.CreateForNewRun(character, UnlockState.all, 1);
                var run = RunState.CreateForTest([player], acts: [], seed: "AUGMENTER_CLASS_ONLY");
                foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
                IEnumerable<CardModel> sources = eventAndCurse
                    ? [ModelDb.Card<AllMatters.Cards.Jax>(), ModelDb.Card<Regret>()]
                    : ModelDb.CardPool<ColorlessCardPool>().GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
                        .Where(card => card.Rarity == CardRarity.Uncommon).Take(2);
                var originals = sources.Select(card => run.CreateCard(card, player)).ToArray();
                Check(originals.Length == 2, "Prepare two transformation sources.");
                foreach (var card in originals) player.Deck.AddInternal(card);
                var model = await Begin(player);
                using (CardSelectCmd.UseSelector(new SelectTwo(originals)))
                    await model.CurrentOptions.Single(option => option.TextKey.EndsWith(".TRANSFORM")).Chosen();
                var results = player.Deck.Cards;
                var legal = character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint).Select(card => card.Id).ToHashSet();
                Check(model.IsFinished && results.Count == 2 && results.All(card => card.Pool == character.CardPool && legal.Contains(card.Id)
                    && card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare),
                    "Non-character sources must yield only legal reward cards of the owning character, never colorless or another character's cards.");
                Check(results.All(card => card.IsUpgraded && card.Owner == player) && originals.All(card => !card.IsUpgraded), "Upgrade only the two resulting instances.");
            }
        }
    }

    private static async Task Decay()
    {
        foreach (bool artifact in new[] { false, true })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
            var run = RunState.CreateForTest([player, other], acts: [], seed: "AUGMENTER");
            var model = await Begin(player);
            await model.CurrentOptions.Single(option => option.TextKey.EndsWith(".MUTAGENS")).Chosen();
            for (int battle = 0; battle < 2; battle++)
            {
                // The headless host supplies room entry and turn hooks; it does not run the network/room action loop.
                var combat = new CombatState(runState: run);
                foreach (var member in run.Players)
                {
                    combat.AddPlayer(member);
                    member.ResetCombatState();
                    foreach (var power in member.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
                }
                if (artifact) await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), player.Creature, 1, player.Creature, null);
                await Hook.AfterRoomEntered(run, new CombatRoom(combat));
                Check(Strength(player) == 3 && Strength(other) == 0, "Each new combat must grant exactly 3 strength only to its owner.");
                if (artifact) Check(!player.Creature.Powers.Any(power => power.Id.Entry == "ALL_MATTERS_POWER_MUTAGENIC_DECAY_POWER"), "Artifact must block the decay Debuff.");
                for (int turn = 1; turn <= 5; turn++)
                {
                    await EndTurn(combat, CombatSide.Enemy, player);
                    await EndTurn(combat, CombatSide.Player, other);
                    Check(Strength(player) == (artifact || turn <= 3 ? 3 : 0), "Enemy and teammate turns must not advance owner decay.");
                    await EndTurn(combat, CombatSide.Player, player);
                    Check(Strength(player) == (artifact || turn < 3 ? 3 : 0), "Only the third owner turn removes 3, once; Artifact preserves the gain.");
                }
                Check(!player.Creature.Powers.Any(power => power.Id.Entry == "ALL_MATTERS_POWER_MUTAGENIC_DECAY_POWER"), "Decay must be gone after resolution or Artifact prevention.");
            }
        }
    }

    private static decimal Strength(Player player) => player.Creature.Powers.OfType<StrengthPower>().Sum(power => power.Amount);

    private static Task EndTurn(CombatState combat, CombatSide side, Player participant)
    {
#if STS2_PUBLIC
        return Hook.AfterTurnEnd(combat, side, [participant.Creature]);
#else
        return Hook.AfterSideTurnEnd(combat, side, [participant.Creature]);
#endif
    }

    private static async Task JaxPlay()
    {
        foreach (bool upgraded in new[] { false, true })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var run = RunState.CreateForTest([player], acts: [], seed: "AUGMENTER");
            var combat = new CombatState(runState: run);
            combat.AddPlayer(player);
            player.ResetCombatState();
            player.Creature.SetCurrentHpInternal(50);
            var card = combat.CreateCard<AllMatters.Cards.Jax>(player);
            if (upgraded)
            {
                card.UpgradeInternal();
                card.FinalizeUpgradeInternal();
            }
            Check(card.EnergyCost.Canonical == 0 && card.Keywords.Contains(CardKeyword.Innate), "Both JAX versions must be zero cost and Innate.");
            await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), null, true, default, skipCardPileVisuals: true);
            Check(player.Creature.CurrentHp == 47 && Strength(player) == (upgraded ? 3 : 2), "JAX must lose 3 HP and gain the specified strength.");
            Check(!ModelDb.Card<AllMatters.Cards.Jax>().IsUpgraded, "Playing and upgrading an instance must not upgrade its template.");
        }
    }

    private static async Task Presentation()
    {
        Check(Canonical.CreateInitialPortrait().GetWidth() > 0, "The event portrait must load.");
        Check(ModelDb.Card<AllMatters.Cards.Jax>().Portrait.GetWidth() > 0, "The card portrait must load through its actual model path.");
        foreach (string path in new[] { "relics/mutagenic_strength.png", "relics/outlines/mutagenic_strength.png", "relics/big/mutagenic_strength.png", "powers/mutagenic_decay.png", "powers/big/mutagenic_decay.png" })
            Check(ResourceLoader.Load<Texture2D>($"res://AllMatters/images/{path}").GetWidth() > 0, $"Missing {path} in PCK.");
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                foreach (string table in new[] { "events", "cards", "relics", "powers" })
                {
                    var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(Godot.FileAccess.GetFileAsString($"res://AllMatters/localization/{language}/{table}.json"))!;
                    foreach (var entry in entries.Where(entry => entry.Key.Contains("AUGMENTER") || entry.Key.Contains("JAX") || entry.Key.Contains("MUTAGENIC")))
                        Check(new LocString(table, entry.Key).GetRawText() == entry.Value, $"Missing localized {entry.Key}.");
                }
                var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
                RunState.CreateForTest([player], acts: [], seed: "AUGMENTER");
                var model = await Begin(player);
                Check(model.Title.GetFormattedText() == (language == "eng" ? "Augmenter" : "增益研究者"), "Event title must follow language.");
                Check(model.CurrentOptions.Count == 3 && model.CurrentOptions.All(option => !option.IsLocked), "All three choices must be visible and usable.");
                var card = ModelDb.Card<AllMatters.Cards.Jax>().ToMutable();
                var description = card.GetDescriptionForPile(PileType.None);
                Check(description.Contains("3") && description.Contains("2") && !description.Contains('{'), "Ordinary card text must resolve 3 HP and 2 Strength.");
                card.UpgradeInternal();
                card.FinalizeUpgradeInternal();
                Check(card.GetDescriptionForPile(PileType.None).Contains("3"), "Upgraded text must resolve Strength.");
                var relic = ModelDb.Relic<AllMatters.Relics.MutagenicStrength>();
                var relicText = new LocString("relics", relic.Id.Entry + ".description");
                relic.DynamicVars.AddTo(relicText);
                Check(relicText.GetFormattedText().Contains("3") && !relicText.GetFormattedText().Contains('{'), "Relic strength and decay must format correctly.");
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
    }

    private sealed class SelectTwo(CardModel[] selected) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            Check(minSelect == 2 && maxSelect == 2 && selected.All(options.Contains), "Selection must require exactly two eligible cards.");
            Check(options.All(card => card.IsRemovable && card.IsTransformable), "All offered cards must be legal transformation targets.");
            return Task.FromResult<IEnumerable<CardModel>>(selected);
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new InvalidOperationException("Not a reward.");
    }

    private static async Task JaxChoice()
    {
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        RunState.CreateForTest([player, other], acts: [], seed: "AUGMENTER");
        int before = player.Deck.Cards.Count;
        int otherBefore = other.Deck.Cards.Count;
        var model = await Begin(player);
        var option = model.CurrentOptions.Single(option => option.TextKey.EndsWith(".JAX"));
        await option.Chosen();
        var card = player.Deck.Cards.Last();
        Check(model.IsFinished && player.Deck.Cards.Count == before + 1, "JAX must finish and add exactly one card.");
        Check(card.Id.Entry == "ALL_MATTERS_JAX" && !card.IsUpgraded && card.Keywords.Contains(CardKeyword.Innate), "The ordinary JAX must already be Innate.");
        Check(card.Type == CardType.Skill && card.Rarity == CardRarity.Event, "JAX must be an event skill.");
        await option.Chosen();
        Check(player.Deck.Cards.Count == before + 1 && other.Deck.Cards.Count == otherBefore, "No duplicate award or teammate effect.");
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

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
