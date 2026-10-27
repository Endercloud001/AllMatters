using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.TestSupport;

internal static class NativeEventTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Native events Bugslayer rest heals 25 percent only its owner and finishes once", Rest),
        ("Native events Toric Toughness gives 15 now and 7 next turn with upgrade removing Exhaust", Toric),
        ("Native events Peck removes at most 15 block before its three or four hits", PeckBlock),
        ("Native events Big Mushroom adds 40 Max HP and keeps opening hand penalty", BigMushroomPickup),
        ("Native events Fragrant Mushroom loses 15 HP and upgrades the chosen cards", FragrantPickup),
        ("Native events treasury shows and grants the same one or two curse gold combination", Treasury),
        ("Native events Punch Off escape cancels the punching loop", PunchEscape),
        ("Native events reward options include their relic previews", Previews),
        ("Native events native leave options finish without changing the deck", Leave),
        ("Native events text shows actual upgrades, fixed Designant and specified effects", Text),
        ("Native events Lantern Key reward heals only in the knight victory reward context", Lantern),
        ("Native events Wongo leave loses 8 HP without downgrading cards", Wongo),
        ("Native events wood snake oil branch is fixed and available without enchantable cards", Wood),
    ];

    internal static Player Player(string seed = "NATIVE_EVENTS")
    {
        var player = MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        RunState.CreateForTest([player], acts: [], seed: seed);
        return player;
    }

    internal static async Task<EventModel> Begin<T>(Player player) where T : EventModel
    {
        var model = ModelDb.Event<T>().ToMutable();
#if STS2_PUBLIC
        await model.BeginEvent(player, false);
#else
        await model.BeginEvent(player, new MegaCrit.Sts2.Core.Multiplayer.Game.EventCombatSynchronizer(player.RunState, player.RunState), false);
#endif
        return model;
    }

    private static async Task Rest()
    {
        var player = Player();
        player.Creature.SetMaxHpInternal(83);
        player.Creature.SetCurrentHpInternal(10);
        var model = await Begin<Bugslayer>(player);
        var option = model.CurrentOptions.SingleOrDefault(o => o.TextKey.EndsWith(".AM_REST"));
        Check(option != null, "Bugslayer must offer rest alongside both original cards.");
        Check(option!.Description.GetFormattedText().Contains("20") && !option.Description.GetFormattedText().Contains("25%"), "Rest displays the calculated integer HP.");
        await option!.Chosen();
        Check(model.IsFinished && player.Creature.CurrentHp == 30, "Rest must heal floor(83 * .25) and finish.");
        await option.Chosen();
        Check(player.Creature.CurrentHp == 30, "A repeated choice must not heal twice.");
    }

    internal static (Player Player, CombatState Combat) Combat()
    {
        var player = Player();
        var combat = new CombatState(runState: player.RunState) { RoundNumber = 1 };
        combat.AddPlayer(player);
        player.ResetCombatState();
        return (player, combat);
    }

    private static async Task Toric()
    {
        foreach (bool upgraded in new[] { false, true })
        {
            var (p, combat) = Combat();
            var card = combat.CreateCard<ToricToughness>(p);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            string description = card.GetDescriptionForPile(PileType.Deck);
            Check(description.Split("Exhaust").Length - 1 == (upgraded ? 0 : 1), "Keyword renderer shows Exhaust exactly once before upgrade and zero after.");
            Check(card.Keywords.Contains(CardKeyword.Exhaust) != upgraded, "Only ordinary Toric Toughness exhausts.");
            await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), null, true, default, skipCardPileVisuals: true);
            Check(p.Creature.Block == 15, "The current turn receives 15 block at either upgrade level.");
#if STS2_PUBLIC
            await CreatureCmd.LoseBlock(p.Creature, 15);
#else
            await CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), p.Creature, 15, null);
#endif
            var power = p.Creature.GetPower<ToricToughnessPower>()!;
            await power.AfterBlockCleared(p.Creature);
            Check(p.Creature.Block == 7 && !p.Creature.HasPower<ToricToughnessPower>(), "Only the next turn receives 7 block.");
        }
    }

    private static async Task PeckBlock()
    {
        foreach (int block in new[] { 0, 10, 20 })
        foreach (bool upgraded in new[] { false, true })
        {
            var (p, combat) = Combat();
            var monster = ModelDb.Monster<AllMatters.Monsters.Lagavulin>().ToMutable();
            var target = combat.CreateCreature(monster, CombatSide.Enemy, "target");
            combat.AddCreature(target);
            target.SetMaxHpInternal(100); target.SetCurrentHpInternal(100);
            await CreatureCmd.GainBlock(target, block, ValueProp.Unpowered, null);
            var card = combat.CreateCard<Peck>(p);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), target, true, default, skipCardPileVisuals: true);
            int damage = (upgraded ? 8 : 6) - Math.Max(0, block - 15);
            Check(target.CurrentHp == 100 - damage && target.Block == 0, "Remove at most 15 block, then deal three/four separate 2-damage hits.");
        }
    }

    private static async Task BigMushroomPickup()
    {
        var (p, _) = Combat();
        int max = p.Creature.MaxHp;
        var relic = await RelicCmd.Obtain<BigMushroom>(p);
        Check(p.Creature.MaxHp == max + 40, "Native mushroom must grant 40 maximum HP globally.");
        while (p.PlayerCombatState!.TurnNumber < 1) p.PlayerCombatState.IncrementTurnNumber();
        Check(relic.ModifyHandDraw(p, 5) == 3, "Opening draw penalty remains two.");
        p.PlayerCombatState.IncrementTurnNumber();
        Check(relic.ModifyHandDraw(p, 5) == 5, "Later turns do not lose draws.");
    }

    private static async Task FragrantPickup()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = Player();
        int hp = p.Creature.CurrentHp;
        var selected = p.Deck.Cards.Take(2).ToArray();
        var e = await Begin<HungryForMushrooms>(p);
        Check(e.CurrentOptions[1].Description.GetFormattedText().Contains("choose", StringComparison.OrdinalIgnoreCase)
            && !e.CurrentOptions[1].Description.GetFormattedText().Contains("random", StringComparison.OrdinalIgnoreCase), "Event describes chosen upgrades rather than random upgrades.");
        using (CardSelectCmd.UseSelector(new Select(selected))) await e.CurrentOptions[1].Chosen();
        Check(p.Relics.OfType<FragrantMushroom>().Count() == 1, "Event obtains the native relic that owns the pickup effect.");
        Check(p.Creature.CurrentHp == hp - 15, "Fragrant mushroom pays 15 HP.");
        Check(selected.All(c => c.IsUpgraded) && p.Deck.Cards.Except(selected).All(c => !c.IsUpgraded), "Upgrade only the two chosen cards.");
    }

    private static async Task Treasury()
    {
        var seen = new HashSet<int>();
        for (int i = 0; i < 20; i++)
        {
            var p = Player("TREASURY" + i);
            var e = await Begin<SunkenTreasury>(p);
            int gold = p.Gold, curses = p.Deck.Cards.Count(c => c is Greed);
            int amount = e.DynamicVars["LargeChestGold"].IntValue;
            await e.CurrentOptions[1].Chosen();
            int count = p.Deck.Cards.Count(c => c is Greed) - curses;
            seen.Add(count);
            Check(p.Gold == gold + amount && (count == 1 ? amount is >= 303 and <= 363 : count == 2 && amount is >= 455 and <= 544), "Displayed gold and actual curse count must match the rolled branch.");
            Check(p.Deck.Cards.Where(c => c is Greed).Distinct().Count() == count && e.IsFinished, "Two curses must be separate instances and the event finishes.");
        }
        Check(seen.SetEquals([1, 2]), "Exercise both treasury branches.");
    }

    private static async Task PunchEscape()
    {
        var e = await Begin<PunchOff>(Player());
        using var cts = new CancellationTokenSource();
        HarmonyLib.AccessTools.Field(typeof(PunchOff), "_punchCts").SetValue(e, cts);
        await e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_LEAVE")).Chosen();
        Check(e.IsFinished && cts.IsCancellationRequested, "Escape cancels the native animation loop before opening the map.");
    }

    private static async Task Previews()
    {
        var e = await Begin<HungryForMushrooms>(Player());
        foreach (var option in e.CurrentOptions.Where(o => o.Relic != null))
            Check(option.HoverTips.OfType<MegaCrit.Sts2.Core.HoverTips.HoverTip>()
                .Any(t => t.CanonicalModel?.Id == option.Relic!.Id), "Relic options include the awarded relic itself, not only keyword tips.");
        var teas = await Begin<TeaMaster>(Player());
        Type[] types = [typeof(BoneTea), typeof(EmberTea), typeof(TeaOfDiscourtesy)];
        for (int i = 0; i < types.Length; i++)
            Check(teas.CurrentOptions[i].HoverTips.OfType<MegaCrit.Sts2.Core.HoverTips.HoverTip>()
                .Any(t => t.CanonicalModel?.GetType() == types[i]), "Every known tea reward includes its relic preview.");
        var mirror = await Begin<Reflections>(Player());
        Check(mirror.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_MOVE")).HoverTips
            .OfType<MegaCrit.Sts2.Core.HoverTips.HoverTip>().Any(t => t.CanonicalModel is DollysMirror), "Possible mirror reward has a relic preview.");
    }

    private static async Task Leave()
    {
        foreach (var e in new[] { ModelDb.Event<CrystalSphere>().ToMutable(), ModelDb.Event<InfestedAutomaton>().ToMutable() })
        {
            var p = Player();
            var cards = p.Deck.Cards.ToArray();
#if STS2_PUBLIC
            await e.BeginEvent(p, false);
#else
            await e.BeginEvent(p, null, false);
#endif
            await e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_LEAVE")).Chosen();
            Check(e.IsFinished && p.Deck.Cards.SequenceEqual(cards), "Leave must preserve the original deck and finish directly.");
        }
    }

    private static Task Text()
    {
        try
        {
            foreach (string lang in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(lang);
                var text = new LocString("events", "TABLET_OF_TRUTH.pages.DECIPHER_5.description").GetFormattedText();
                Check(text.Contains("[i]Designant.[/i]"), "Final decipher text must use the fixed italic word.");
                var toric = ModelDb.Card<ToricToughness>().ToMutable();
                var preview = toric.GetDescriptionForUpgradePreview();
                Check(preview.Contains("15") && preview.Contains("7"), "Preview must distinguish current and next turn block.");
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
        return Task.CompletedTask;
    }

    private static async Task Lantern()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (bool knight in new[] { false, true })
        {
            var p = Player();
            var run = (RunState)p.RunState;
            if (knight) run.PushRoom(new MegaCrit.Sts2.Core.Rooms.CombatRoom(new CombatState(ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.MysteriousKnightEventEncounter>().ToMutable(), run)));
            p.Creature.SetCurrentHpInternal(10);
            var reward = new MegaCrit.Sts2.Core.Rewards.SpecialCardReward(run.CreateCard<LanternKey>(p), p);
            await reward.SelectUnsynchronized();
            Check(p.Deck.Cards.Any(c => c is LanternKey) && p.Creature.CurrentHp == (knight ? 30 : 10), "Deliver key and heal 20 only for the knight event reward.");
        }
    }

    private static async Task Wongo()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = Player();
        p.Gold = 0;
        var card = p.Deck.Cards[0]; card.UpgradeInternal(); card.FinalizeUpgradeInternal();
        int hp = p.Creature.CurrentHp;
        var e = await Begin<WelcomeToWongos>(p);
        await e.CurrentOptions.Single(o => o.TextKey.EndsWith(".LEAVE")).Chosen();
        Check(e.IsFinished && p.Creature.CurrentHp == hp - 8 && card.IsUpgraded, "Leave costs eight HP and preserves upgrades.");
    }

    private static async Task Wood()
    {
        bool found = false;
        for (int i = 0; i < 20; i++)
        {
            var p = Player("WOOD" + i);
            var e = await Begin<WoodCarvings>(p);
            var oil = e.CurrentOptions.SingleOrDefault(o => o.TextKey.EndsWith(".AM_OIL"));
            if (oil == null) continue;
            await oil.Chosen();
            Check(e.IsFinished && p.Potions.Any(potion => potion is MegaCrit.Sts2.Core.Models.Potions.SneckoOil), "Snake alternative grants actual Snecko Oil.");
            found = true;
            break;
        }
        Check(found, "Exercise the snake oil branch.");
    }

    internal sealed class Select(params CardModel[] selected) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            Check(selected.Length >= minSelect && selected.Length <= maxSelect && selected.All(options.Contains), "Selection must contain only legal cards in the requested count.");
            return Task.FromResult<IEnumerable<CardModel>>(selected);
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => throw new InvalidOperationException("This is a deck selection, not a reward.");
    }

    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
