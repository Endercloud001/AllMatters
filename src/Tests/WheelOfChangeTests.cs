using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Localization;
using Godot;
using System.Text.Json;

internal static class WheelOfChangeTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Wheel of Change gives 100, 200 or 300 gold once to its owner", Gold),
        ("Wheel of Change six seeded results display before personal effects and cannot repeat", SixResults),
        ("Wheel of Change relics choose rarity before unequal pools and never escape general drops", RelicPools),
        ("Wheel of Change relics retain legal multiplayer chest fallback candidates", MultiplayerFallback),
        ("Wheel of Change empty relic pools or no removable cards explain and finish without reroll", EmptyResults),
        ("Wheel of Change native entry snapshots reproduce results without stacking effects", SaveBoundaries),
        ("Wheel of Change bilingual pack and actual spin scene agree with the result", Presentation),
    ];

    private static EventModel Canonical => ModelDb.AllEvents.Single(model => model.Id.Entry == "ALL_MATTERS_EVENT_WHEEL_OF_CHANGE");

    private static async Task Gold()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (int act in new[] { 0, 1, 2 })
        {
            var (player, model) = await Prepare(0, act);
            var other = player.RunState.Players[1];
            int gold = player.Gold, otherGold = other.Gold;
            var spin = model.CurrentOptions.Single();
            await spin.Chosen();
            Check(model.CurrentOptions.Single().TextKey.EndsWith(".GOLD"), "A gold spin must show the gold result before claiming.");
            var claim = model.CurrentOptions.Single();
            await claim.Chosen();
            await claim.Chosen();
            await spin.Chosen();
            Check(model.IsFinished && player.Gold == gold + new[] { 100, 200, 300 }[act], "Claim exactly the current act's gold once.");
            Check(other.Gold == otherGold, "A personal spin must not pay the teammate.");
        }
    }

    private static async Task SixResults()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        string[] results = ["GOLD", "RELIC", "HEAL", "CURSE", "REMOVE", "DAMAGE"];
        for (int outcome = 0; outcome < 6; outcome++)
        {
            var (player, model) = await Prepare(outcome);
            var other = player.RunState.Players[1];
            var otherBefore = other.ToSerializable();
            player.Creature.SetMaxHpInternal(83);
            player.Creature.SetCurrentHpInternal(40);
            player.Creature.GainBlockInternal(99);
            var selected = player.Deck.Cards.First(card => card.IsRemovable);
            int gold = player.Gold, cards = player.Deck.Cards.Count, relics = player.Relics.Count;
            SetBag(player, [ModelDb.Relic<Anchor>()]);
            Check(Canonical.IsAllowed(player.RunState) && !model.IsShared, "No admission restriction or shared decision is permitted.");
            await model.CurrentOptions.Single().Chosen();
            var claim = model.CurrentOptions.Single();
            Check(claim.TextKey.EndsWith("." + results[outcome]), "Each seeded uniform 0..5 draw must display its own result.");
            Check(player.Gold == gold && player.Creature.CurrentHp == 40 && player.Deck.Cards.Count == cards && player.Relics.Count == relics,
                "Displaying a result must not yet apply it.");
            using (CardSelectCmd.UseSelector(new SelectCard(selected))) await claim.Chosen();
            await claim.Chosen();
            Check(model.IsFinished && model.CurrentOptions.Count == 0, "Each result finishes after a single claim.");
            Check(player.Gold == gold + (outcome == 0 ? 100 : 0), "Only gold changes gold.");
            Check(player.Creature.CurrentHp == (outcome == 2 ? 83 : outcome == 5 ? 33 : 40), "Heal fills any maximum; injury loses exactly seven HP through block.");
            Check(player.Deck.Cards.Count == cards + (outcome == 3 ? 1 : outcome == 4 ? -1 : 0), "Curse adds once; removal deletes once.");
            if (outcome == 3) Check(player.Deck.Cards.OfType<BadLuck>().Count() == 1, "The native Bad Luck curse must be added, retaining Eternal.");
            if (outcome == 4) Check(!player.Deck.Cards.Contains(selected), "Remove the chosen instance.");
            Check(player.Relics.Count == relics + (outcome == 1 ? 1 : 0), "Only the relic result awards a relic.");
            Check(other.Gold == otherBefore.Gold && other.Creature.CurrentHp == otherBefore.CurrentHp && other.Deck.Cards.Count == otherBefore.Deck.Count,
                "All effects belong to the spinner, without touching the teammate.");
        }
    }

    private static void SetBag(Player player, IEnumerable<RelicModel> relics)
    {
        var save = player.RelicGrabBag.ToSerializable();
        foreach (var list in save.RelicIdLists.Values) list.Clear();
        foreach (var group in relics.GroupBy(relic => relic.Rarity)) save.RelicIdLists[group.Key] = group.Select(relic => relic.Id).ToList();
        player.RelicGrabBag.LoadFromSerializable(save);
    }

    private static async Task RelicPools()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        RelicModel common = ModelDb.Relic<Anchor>();
        RelicModel[] uncommon = ModelDb.RelicPool<SharedRelicPool>().GetUnlockedRelics(UnlockState.all)
            .Where(relic => relic.Rarity == RelicRarity.Uncommon).Take(3).ToArray();
        Check(uncommon.Length == 3, "Prepare three real target-version Uncommon relics rather than assuming STS1 rarities.");
        foreach (int tier in new[] { 0, 1 })
        {
            var (player, model) = await Prepare(1, tier: tier);
            SetBag(player, new[] { common }.Concat(uncommon).Append(RareRelic).Append(ModelDb.Relic<AllMatters.Relics.MutagenicStrength>()));
            await model.CurrentOptions.Single().Chosen();
            await model.CurrentOptions.Single().Chosen();
            var awarded = player.Relics.Last();
            Check(awarded.Rarity == (tier == 0 ? RelicRarity.Common : RelicRarity.Uncommon),
                "Each 50/50 tier draw chooses its tier even with unequal candidate counts.");
            Check(awarded.Id == common.Id || uncommon.Any(relic => relic.Id == awarded.Id), "Only prepared general-drop relics qualify.");
            Check(!player.RelicGrabBag.Contains(ModelDb.GetById<RelicModel>(awarded.Id)), "Native obtain removes the awarded relic from the player's bag.");
            Check(!player.RunState.SharedRelicGrabBag.Contains(ModelDb.GetById<RelicModel>(awarded.Id)), "Native obtain also removes it from the shared bag.");
        }
        foreach (var only in new[] { common, uncommon[0] })
        foreach (int tier in new[] { 0, 1 })
        {
            var (player, model) = await Prepare(1, tier: tier);
            SetBag(player, [only]);
            await model.CurrentOptions.Single().Chosen();
            await model.CurrentOptions.Single().Chosen();
            Check(player.Relics.Last().Id == only.Id, $"An empty rarity must fall back to {only.Id} ({only.Rarity}) from tier {tier}; got {player.Relics.Last().Id}. Page: {model.Description!.GetRawText()}");
        }
    }

    private static async Task EmptyResults()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (int outcome in new[] { 1, 4 })
        {
            var (player, model) = await Prepare(outcome);
            // Rare/event-only relics remain, while common/uncommon have no candidates.
            SetBag(player, [RareRelic, ModelDb.Relic<AllMatters.Relics.Necronomicon>()]);
            foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
            var eternal = player.RunState.CreateCard(ModelDb.Card<BadLuck>(), player);
            player.Deck.AddInternal(eternal);
            int gold = player.Gold, hp = player.Creature.CurrentHp, relics = player.Relics.Count;
            await model.CurrentOptions.Single().Chosen();
            Check(!model.IsFinished && model.CurrentOptions.Single().TextKey.EndsWith(outcome == 1 ? ".RELIC" : ".REMOVE"),
                "The original result must be shown even when it has no eligible target.");
            await model.CurrentOptions.Single().Chosen();
            Check(model.IsFinished && model.Description!.GetRawText().Contains(outcome == 1 ? "no eligible" : "no removable"), "Explain the missing target and finish.");
            Check(player.Gold == gold && player.Creature.CurrentHp == hp && player.Relics.Count == relics && player.Deck.Cards.SequenceEqual(new[] { eternal }),
                "No substitute reward, curse, injury or card removal is permitted.");
        }
    }

    private static RelicModel RareRelic => ModelDb.RelicPool<SharedRelicPool>().GetUnlockedRelics(UnlockState.all).First(relic => relic.Rarity == RelicRarity.Rare);

    private static async Task MultiplayerFallback()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        RelicModel[] fallbackRelics = [ModelDb.Relic<Anchor>(), ModelDb.Relic<HornCleat>()];
        foreach (int tier in new[] { 0, 1 })
        {
            var (player, model) = await Prepare(1, tier: tier);
            var fallback = fallbackRelics[tier];
            var otherTier = fallbackRelics[1 - tier];
            SetBag(player, [fallback, otherTier, RareRelic]);
            player.RelicGrabBag.MoveToFallback(fallback);
            Check(!player.RelicGrabBag.Contains(fallback), "Prepare a real chest fallback entry, absent from the primary queues.");
            await model.CurrentOptions.Single().Chosen();
            await model.CurrentOptions.Single().Chosen();
            Check(player.Relics.Last().Id == fallback.Id, "The selected tier's legal chest fallback must be awarded before falling back to the other tier.");
            Check(player.RelicGrabBag.PullFromFront(fallback.Rarity, relic => relic.Id == fallback.Id, player.RunState) == null,
                "The awarded chest fallback must also be consumed, not left for a second award.");
        }
        var (emptyPlayer, emptyModel) = await Prepare(1);
        SetBag(emptyPlayer, [RareRelic]);
        emptyPlayer.RelicGrabBag.MoveToFallback(RareRelic);
        int before = emptyPlayer.Relics.Count;
        await emptyModel.CurrentOptions.Single().Chosen();
        await emptyModel.CurrentOptions.Single().Chosen();
        Check(emptyPlayer.Relics.Count == before && emptyModel.IsFinished, "A Rare in the chest fallback must not bypass the tier restriction.");
    }

    private static async Task SaveBoundaries()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        for (int outcome = 0; outcome < 6; outcome++)
        {
            var (player, model) = await Prepare(outcome);
            SetBag(player, [ModelDb.Relic<Anchor>()]);
            player.Creature.SetCurrentHpInternal(40);
            var entry = player.ToSerializable();
            var seed = player.RunState.Rng.StringSeed;
            var bag = player.RelicGrabBag.ToSerializable();
            var room = new EventRoom(Canonical).ToSerializable();
            Check(new EventRoom(room).CanonicalEvent.Id == Canonical.Id, "Native room serialization retains the event identity.");
            await model.CurrentOptions.Single().Chosen();
            string result = model.CurrentOptions.Single().TextKey;
            using (CardSelectCmd.UseSelector(new SelectCard(player.Deck.Cards.First(card => card.IsRemovable))))
                await model.CurrentOptions.Single().Chosen();
            var completed = Player.FromSerializable(player.ToSerializable());
            Check(completed.Gold == player.Gold && completed.Creature.CurrentHp == player.Creature.CurrentHp && completed.Deck.Cards.Count == player.Deck.Cards.Count
                && completed.Relics.Count == player.Relics.Count, "Completed inventory restores without replaying effects.");
            var loaded = Player.FromSerializable(entry);
            RunState.CreateForTest([loaded, Player.CreateForNewRun<Silent>(UnlockState.all, 2)], acts: [], seed: seed);
            loaded.RelicGrabBag.LoadFromSerializable(bag);
            var restarted = await Begin(loaded);
            await restarted.CurrentOptions.Single().Chosen();
            Check(restarted.CurrentOptions.Single().TextKey == result, "Reloading the supported entry snapshot must reproduce the determined result.");
            using (CardSelectCmd.UseSelector(new SelectCard(loaded.Deck.Cards.First(card => card.IsRemovable))))
                await restarted.CurrentOptions.Single().Chosen();
            Check(loaded.Gold == player.Gold && loaded.Creature.CurrentHp == player.Creature.CurrentHp && loaded.Deck.Cards.Select(card => card.Id).SequenceEqual(player.Deck.Cards.Select(card => card.Id))
                && loaded.Relics.Select(relic => relic.Id).SequenceEqual(player.Relics.Select(relic => relic.Id)), "Replaying from entry produces exactly one effect, without stacking the interrupted branch.");
        }
    }

    private static async Task Presentation()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        Check(Canonical.CreateInitialPortrait().GetWidth() > 0, "The event image must load from the shipped PCK.");
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(Godot.FileAccess.GetFileAsString($"res://AllMatters/localization/{language}/events.json"))!;
                foreach (var entry in entries.Where(entry => entry.Key.Contains("WHEEL_OF_CHANGE")))
                    Check(new LocString("events", entry.Key).GetRawText() == entry.Value, "Every wheel page and option must be localized.");
                var (player, model) = await Prepare(0, 2);
                await model.CurrentOptions.Single().Chosen();
                var description = model.CurrentOptions.Single().Description;
                model.DynamicVars.AddTo(description);
                Check(description.GetFormattedText().Contains("300") && !description.GetFormattedText().Contains('{'), "The result must show the actual act's gold.");
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        for (int outcome = 0; outcome < 6; outcome++)
        {
            var (_, model) = await Prepare(outcome);
            var scene = new Control { Size = new Vector2(1280, 720) };
            tree.Root.AddChild(scene);
            model.SetNode(scene);
            try
            {
                var spin = model.CurrentOptions.Single().Chosen();
                Check(!spin.IsCompleted, "The local spin must wait for its real Godot animation.");
                var overlay = scene.GetChildren().OfType<Control>().Single();
                var disk = overlay.GetChildren().OfType<Node2D>().Single(node => node is not Polygon2D);
                Check(disk.GetChildren().OfType<Label>().Count() == 6, "All six localized result segments must be visible.");
                await spin;
                float angle = Mathf.PosMod(disk.RotationDegrees, 360);
                float selectedAngle = Mathf.PosMod(angle + outcome * 60, 360);
                Check(Math.Min(selectedAngle, 360 - selectedAngle) < 0.1f, "The pointer must land on the sector whose result is offered.");
                Check(model.CurrentOptions.Single().TextKey.EndsWith(new[] { ".GOLD", ".RELIC", ".HEAL", ".CURSE", ".REMOVE", ".DAMAGE" }[outcome]), "Animation and settlement use the same draw.");
            }
            finally { scene.Free(); }
        }
        // Room teardown while spinning must release the awaited option and overlay.
        var (_, interrupted) = await Prepare(0);
        var leaving = new Control { Size = new Vector2(1280, 720) };
        tree.Root.AddChild(leaving);
        interrupted.SetNode(leaving);
        var pending = interrupted.CurrentOptions.Single().Chosen();
        leaving.Free();
        await pending;
    }

    private sealed class SelectCard(CardModel selected) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            Check(minSelect == 1 && maxSelect == 1 && options.Contains(selected) && options.All(card => card.IsRemovable), "Require exactly one legal target.");
            return Task.FromResult<IEnumerable<CardModel>>([selected]);
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new InvalidOperationException("Wheel removal is not a card reward.");
    }

    private static async Task<(Player Player, EventModel Model)> Prepare(int outcome, int act = 0, int? tier = null)
    {
        for (int seed = 0; seed < 1000; seed++)
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
            var run = RunState.CreateForTest([player, other], acts: [], seed: "WHEEL_" + seed);
            run.CurrentActIndex = act;
            var model = await Begin(player);
            // Prepare a seed, without advancing the event RNG or replacing its behavior.
#if STS2_PUBLIC
            var rng = new Rng(model.Rng.Seed, model.Rng.Counter);
#else
            var rng = new Rng(model.Rng.ToSerializable());
#endif
            for (int skip = 0; skip < act; skip++) rng.NextInt(1);
            if (rng.NextInt(6) == outcome && (tier == null || rng.NextInt(2) == tier)) return (player, model);
        }
        throw new Exception("Could not prepare the requested wheel outcome.");
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
