using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.RestSite;
using static NativeEventTests;

internal static class NativeVegetationTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Native vegetation offers one to three rest-site actions without Mend or Hatch, then fights once", Offers),
        ("Native vegetation Miniature Tent still allows only one rest action", Tent),
        ("Native vegetation can appear in both Overgrowth and Hive", Pools),
        ("Native vegetation smith upgrades selected card without healing and cancellation permits retry", Smith),
        ("Native vegetation Cook removes two cards and gains Max HP", Cook),
        ("Native vegetation Lift and Kindle update their native relics", RelicActions),
        ("Native vegetation Trudge pays shown gold and loses eight HP once", Trudge),
        ("Native vegetation bilingual copy displays actual action values", Text),
        ("Native vegetation native encounter starts once after action and preserves room restoration", Fight),
        ("Native vegetation samples three from many legal actions with reproducible offers", Sampling),
        ("Native vegetation Dig and Clone deliver native relic and deck results", DigAndClone),
        ("Native vegetation excludes available Mend and Hatch and isolates player results", ExclusionAndIsolation)
    ];

    private static IEnumerable<MegaCrit.Sts2.Core.Events.EventOption> Rest(EventModel model)
        => model.CurrentOptions.Where(o => o.TextKey.Contains(".AM_REST_"));

    private static Task Offers() => WithPlayer(async player =>
    {
        await RelicCmd.Obtain<Shovel>(player);
        player.Creature.SetCurrentHpInternal(10);
        await RelicCmd.Obtain<RegalPillow>(player);
        var model = await NativeEventTests.Begin<DenseVegetation>(player);
        var rest = Rest(model).ToArray();
        NativeEventTests.Check(rest.Length is >= 1 and <= 3, "Offer one to three rest-site actions.");
        NativeEventTests.Check(rest.All(o => !o.TextKey.EndsWith("_MEND") && !o.TextKey.EndsWith("_HATCH")), "Mend and Hatch are excluded.");
        NativeEventTests.Check(model.CurrentOptions.Any(o => o.TextKey.EndsWith(".TRUDGE_ON")), "Trudge On remains.");
        await rest.Single(o => o.TextKey.EndsWith("_HEAL")).Chosen();
        Check(player.Creature.CurrentHp == 49, "80 Max HP: 24 healing plus Pillow's 15, from 10 to 49.");
        NativeEventTests.Check(!model.IsFinished && model.CurrentOptions.Count == 1 && model.CurrentOptions[0].TextKey.EndsWith(".FIGHT"), "After one rest action only the fight option remains.");
    });

    private static Task Tent() => WithPlayer(async player =>
    {
        await RelicCmd.Obtain<MiniatureTent>(player);
        await RelicCmd.Obtain<Shovel>(player);
        var model = await NativeEventTests.Begin<DenseVegetation>(player);
        var stale = Rest(model).Single(o => o.TextKey.EndsWith("_SMITH"));
        await Rest(model).Single(o => o.TextKey.EndsWith("_HEAL")).Chosen();
        await stale.Chosen();
        Check(player.Deck.Cards.All(c => !c.IsUpgraded), "Tent cannot execute stale second actions.");
        NativeEventTests.Check(Rest(model).Count() == 0 && model.CurrentOptions.Count == 1, "The tent never turns the rest actions into a multi-choice.");
    });

    private static Task Pools()
    {
        foreach (var act in new ActModel[] { ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>() })
            NativeEventTests.Check(act.AllEvents.Any(e => e is DenseVegetation), "DenseVegetation is in " + act.Id.Entry);
        return Task.CompletedTask;
    }

    private static MegaCrit.Sts2.Core.Events.EventOption Action(EventModel model, string id)
        => Rest(model).Single(o => o.TextKey.EndsWith("_" + id));

    // Initialize real selection/reward services, without any rest-site room or UI.
    private static async Task WithPlayer(Func<MegaCrit.Sts2.Core.Entities.Players.Player, Task> test, bool multiplayer = false)
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var previousNetId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
        MegaCrit.Sts2.Core.Context.LocalContext.NetId = 1;
        var player = multiplayer
            ? MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(MegaCrit.Sts2.Core.Unlocks.UnlockState.all, 1)
            : Player("VEGETATION_ACTION");
        if (multiplayer)
            RunState.CreateForTest([player, MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun<MegaCrit.Sts2.Core.Models.Characters.Silent>(MegaCrit.Sts2.Core.Unlocks.UnlockState.all, 2)], acts: [], seed: "VEGETATION_PARTY");
        RunManager.Instance.SetUpTest((RunState)player.RunState, new NetSingleplayerGameService());
        try { await test(player); }
        finally
        {
            RunManager.Instance.CleanUp(false);
            MegaCrit.Sts2.Core.Context.LocalContext.NetId = previousNetId;
        }
    }

    private static Task Smith() => WithPlayer(async player =>
    {
        player.Creature.SetCurrentHpInternal(10);
        await RelicCmd.Obtain<RegalPillow>(player);
        var model = await Begin<DenseVegetation>(player);
        var keys = model.CurrentOptions.Select(o => o.TextKey).ToArray();
        using (CardSelectCmd.UseSelector(new Cancel())) await Action(model, "SMITH").Chosen();
        Check(model.CurrentOptions.Select(o => o.TextKey).SequenceEqual(keys) && player.Deck.Cards.All(c => !c.IsUpgraded), "Cancel preserves offers without performing an action.");
        var card = player.Deck.Cards.First();
        using (CardSelectCmd.UseSelector(new Select(card))) await Action(model, "SMITH").Chosen();
        Check(card.IsUpgraded && player.Deck.Cards.Except([card]).All(c => !c.IsUpgraded), "Upgrade exactly the selected permanent card.");
        Check(player.Creature.CurrentHp == 10 && model.CurrentOptions.Single().TextKey.EndsWith(".FIGHT"), "Smith does not trigger healing and completes before combat.");
        foreach (var c in player.Deck.Cards.Where(c => c.IsUpgradable)) { c.UpgradeInternal(); c.FinalizeUpgradeInternal(); }
        var exhausted = await Begin<DenseVegetation>(player);
        Check(Rest(exhausted).All(o => !o.TextKey.EndsWith("_SMITH")), "No Smith offer without targets.");
    });

    private static Task Cook() => WithPlayer(async player =>
    {
        await RelicCmd.Obtain<MeatCleaver>(player);
        var model = await Begin<DenseVegetation>(player);
        var cards = player.Deck.Cards.Take(2).ToArray();
        using (CardSelectCmd.UseSelector(new Select(cards))) await Action(model, "COOK").Chosen();
#if STS2_PUBLIC
        const int expectedMaxHp = 89;
#else
        const int expectedMaxHp = 85;
#endif
        Check(cards.All(c => !player.Deck.Cards.Contains(c)) && player.Creature.MaxHp == expectedMaxHp, "Cook removes two chosen cards and grants native Max HP (public +9, beta +5).");
        Check(model.CurrentOptions.Single().TextKey.EndsWith(".FIGHT"), "Cook completes before combat is offered.");
    });

    private static async Task RelicActions()
    {
        await WithPlayer(async player =>
        {
            var girya = await RelicCmd.Obtain<Girya>(player);
            var model = await Begin<DenseVegetation>(player);
            await Action(model, "LIFT").Chosen();
            Check(girya.TimesLifted == 1, "Lift increments native Girya.");
            girya.TimesLifted = 3;
            var exhausted = await Begin<DenseVegetation>(player);
            Check(Rest(exhausted).All(o => !o.TextKey.EndsWith("_LIFT")), "Fully used Girya offers no Lift.");
        });
        await WithPlayer(async player =>
        {
            var candle = await RelicCmd.Obtain<PumpkinCandle>(player);
            candle.KindleCount = 0;
            var model = await Begin<DenseVegetation>(player);
            await Action(model, "KINDLE").Chosen();
            Check(candle.KindleCount == 5, "Kindle restores the native five-charge candle counter.");
        });
    }

    private static Task Trudge() => WithPlayer(async player =>
    {
        var model = await Begin<DenseVegetation>(player);
        int hp = player.Creature.CurrentHp, gold = player.Gold, reward = model.DynamicVars.Gold.IntValue;
        var stale = Action(model, "SMITH");
        var trudge = model.CurrentOptions.Single(o => o.TextKey.EndsWith(".TRUDGE_ON"));
        await trudge.Chosen(); await trudge.Chosen(); await stale.Chosen();
        Check(reward is >= 61 and <= 99 && player.Gold == gold + reward && player.Creature.CurrentHp == hp - 8 && model.IsFinished,
            "Trudge retains the displayed gold and eight damage exactly once.");
    });

    private static Task Text() => WithPlayer(async player =>
    {
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                var model = await Begin<DenseVegetation>(player);
                foreach (var option in Rest(model))
                {
                    string text = option.Description.GetFormattedText();
                    Check(!text.Contains('{') && text.Contains(language == "zhs" ? "进入战斗" : "Enter combat"), "Action descriptions format in both languages.");
                }
                Check(Action(model, "HEAL").Description.GetFormattedText().Contains("24"), "Heal displays 30 percent of 80.");
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
    });

    private static Task Fight() => WithPlayer(async player =>
    {
        var run = (RunState)player.RunState;
        run.AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.Unknown, MegaCrit.Sts2.Core.Rooms.RoomType.Event, ModelDb.Event<DenseVegetation>().Id);
        var model = ModelDb.Event<DenseVegetation>().ToMutable();
#if STS2_PUBLIC
        await model.BeginEvent(player, false);
#else
        var synchronizer = new MegaCrit.Sts2.Core.Multiplayer.Game.EventCombatSynchronizer(run, run);
        await model.BeginEvent(player, synchronizer, false);
        synchronizer.InitializeForEvent(model);
#endif
        HeadlessBanditRoomEntrance.Active = true;
        HeadlessBanditRoomEntrance.Rooms.Clear();
        try
        {
            var card = player.Deck.Cards.First();
            using (CardSelectCmd.UseSelector(new Select(card))) await Action(model, "SMITH").Chosen();
            Check(card.IsUpgraded && HeadlessBanditRoomEntrance.Rooms.Count == 0, "Action completes before combat entrance.");
            var fight = model.CurrentOptions.Single();
            await fight.Chosen(); await fight.Chosen();
            var room = HeadlessBanditRoomEntrance.Rooms.Single();
            Check(room.Encounter is MegaCrit.Sts2.Core.Models.Encounters.DenseVegetationEventEncounter
                && room.ParentEventId == model.Id && !room.ShouldResumeParentEventAfterCombat && room.ExtraRewards.Count == 0,
                "Enter the unmodified native encounter exactly once, with its original exit and reward policy.");
            room.MarkPreFinished();
            var restored = MegaCrit.Sts2.Core.Rooms.CombatRoom.FromSerializable(room.ToSerializable(), run);
            Check(restored.Encounter!.Id == room.Encounter.Id && restored.ParentEventId == model.Id && card.IsUpgraded,
                "Restoring the combat room keeps encounter identity and the permanent upgrade.");
        }
        finally { HeadlessBanditRoomEntrance.Active = false; }
    });

    private static Task Sampling() => WithPlayer(async player =>
    {
        await RelicCmd.Obtain<Shovel>(player);
        await RelicCmd.Obtain<Girya>(player);
        await RelicCmd.Obtain<MeatCleaver>(player);
        var model = await Begin<DenseVegetation>(player);
        var keys = Rest(model).Select(o => o.TextKey).ToArray();
        Check(keys.Length == 3 && keys.Distinct().Count() == 3, "Five legal actions yield three distinct choices.");
        var sameVisit = await Begin<DenseVegetation>(player);
        Check(Rest(sameVisit).Select(o => o.TextKey).SequenceEqual(keys)
            && sameVisit.DynamicVars.Gold.IntValue == model.DynamicVars.Gold.IntValue,
            "Re-entering an unchanged run reproduces actions and displayed gold.");
    });

    private static async Task DigAndClone()
    {
        await WithPlayer(async player =>
        {
            await RelicCmd.Obtain<Shovel>(player);
            int count = player.Relics.Count;
            var model = await Begin<DenseVegetation>(player);
            await Action(model, "DIG").Chosen();
            Check(player.Relics.Count == count + 1 && model.CurrentOptions.Single().TextKey.EndsWith(".FIGHT"), "Dig obtains one native relic before combat.");
        });
        await WithPlayer(async player =>
        {
            var card = player.Deck.Cards.First();
            using (CardSelectCmd.UseSelector(new Select(card))) await RelicCmd.Obtain<PaelsGrowth>(player);
            int count = player.Deck.Cards.Count;
            var model = await Begin<DenseVegetation>(player);
            await Action(model, "CLONE").Chosen();
            Check(player.Deck.Cards.Count == count + 1 && player.Deck.Cards.Last().Id == card.Id
                && !ReferenceEquals(player.Deck.Cards.Last(), card), "Clone creates a separate permanent card from the native enchantment.");
        });
    }

    private static Task ExclusionAndIsolation() => WithPlayer(async player =>
    {
        await CardPileCmd.Add(player.RunState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.ByrdonisEgg>(player), PileType.Deck);
        var native = RestSiteOption.Generate(player);
        Check(native.Any(o => o is MendRestSiteOption) && native.Any(o => o is HatchRestSiteOption), "Fixture exposes real Mend and Hatch.");
        var model = await Begin<DenseVegetation>(player);
        Check(Rest(model).Count() == 2 && Rest(model).All(o => !o.TextKey.EndsWith("_MEND") && !o.TextKey.EndsWith("_HATCH")), "Exclude both actions before sampling, retaining Heal and Smith.");
        var other = player.RunState.Players.Single(p => p != player);
        int otherHp = other.Creature.CurrentHp, otherGold = other.Gold;
        var selected = player.Deck.Cards.First(c => c.IsUpgradable);
        using (CardSelectCmd.UseSelector(new Select(selected))) await Action(model, "SMITH").Chosen();
        Check(selected.IsUpgraded && other.Deck.Cards.All(c => !c.IsUpgraded)
            && other.Creature.CurrentHp == otherHp && other.Gold == otherGold, "Only the choosing owner's deck changes.");
    }, multiplayer: true);

    private sealed class Cancel : MegaCrit.Sts2.Core.TestSupport.ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
            => Task.FromResult<IEnumerable<CardModel>>([]);
        public MegaCrit.Sts2.Core.TestSupport.CardRewardSelection GetSelectedCardReward(
            IReadOnlyList<MegaCrit.Sts2.Core.Entities.Cards.CardCreationResult> options,
            IReadOnlyList<MegaCrit.Sts2.Core.Entities.CardRewardAlternatives.CardRewardAlternative> alternatives)
            => throw new InvalidOperationException("Not a card reward.");
    }
}
