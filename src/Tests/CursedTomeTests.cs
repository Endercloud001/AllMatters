using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using AllMatters.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Localization;
using System.Text.Json;
using Godot;

internal static class CursedTomeTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Cursed Tome reading costs 1, 2, 3 then 15 and offers exactly three books once", Reading),
        ("Cursed Tome admission excludes every book on either player outside Act 2", Admission),
        ("Cursed Tome leave, stop and same-book choices are independent at every ascension", IndependentChoices),
        ("Cursed Tome lethal choices warn and resolve death or native Lizard Tail before books", DeathAndRevival),
        ("Cursed Tome teammate can finish independently after native multiplayer lethal damage resolution", MultiplayerLethal),
        ("Cursed Tome Necronomicon repeats only the first eligible resolved-cost attack per owner turn", RepeatAttack),
        ("Cursed Tome Necronomicurse is Eternal and returns to hand after actual exhaust", Curse),
        ("Cursed Tome Nilry offers upgraded class candidates, supports skip, and preserves templates", Nilry),
        ("Cursed Tome Enchiridion generates a legal free power only on the first owner turn of each combat", EnchiridionGeneration),
        ("Cursed Tome inventory save restore retains one book and curse without repeating pickup", InventoryRestore),
        ("Cursed Tome native room-entry snapshots restart reading without stacking costs or rewards", EntrySnapshotRestore),
        ("Cursed Tome bilingual pages and book or curse artwork load from the PCK", Presentation),
    ];

    private static EventModel Canonical => ModelDb.AllEvents.SingleOrDefault(model => model.Id.Entry == "ALL_MATTERS_EVENT_CURSED_TOME")
        ?? throw new Exception("Cursed Tome is missing from registered events.");

    private static async Task Reading()
    {
        using var eventScope = HeadlessCombatInProgress.EnterEvent();
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        RunState.CreateForTest([player, other], acts: [], seed: "CURSED_TOME");
        player.Creature.SetCurrentHpInternal(60);
        var model = await Begin(player);
        Check(!model.IsShared && model.CurrentOptions.Count == 2, "Reading and leaving must be personal choices.");
        await Choose(model, "READ");
        Check(player.Creature.CurrentHp == 60, "Opening the book costs no HP.");
        foreach (int hp in new[] { 59, 57, 54 })
        {
            Check(model.CurrentOptions.Count == 1, "Intermediate pages only allow continuing.");
            await Choose(model, "CONTINUE");
            Check(player.Creature.CurrentHp == hp, "Pages cost 1, 2, then 3 HP.");
        }
        await Choose(model, "OBTAIN");
        Check(player.Creature.CurrentHp == 39 && model.CurrentOptions.Count == 3, "Pay 15 before choosing among three books.");
        var options = model.CurrentOptions.ToArray();
        int before = player.Relics.Count;
        await Choose(model, "NECRONOMICON");
        foreach (var option in options) await option.Chosen();
        Check(model.IsFinished && player.Relics.Count == before + 1, "Only one book can be awarded, even from stale options.");
        Check(player.Deck.Cards.Count(card => card.Id.Entry == "ALL_MATTERS_NECRONOMICURSE") == 1, "Necronomicon must add its curse exactly once.");
        Check(other.Relics.Count == 1 && other.Deck.Cards.All(card => card.Id.Entry != "ALL_MATTERS_NECRONOMICURSE"), "The teammate must not receive this book or curse.");
    }

    private static Task Choose(EventModel model, string key) => model.CurrentOptions.Single(option => option.TextKey.EndsWith("." + key)).Chosen();

    private static async Task Admission()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (var book in new RelicModel[] { ModelDb.Relic<Necronomicon>(), ModelDb.Relic<NilrysCodex>(), ModelDb.Relic<Enchiridion>() })
        foreach (int holder in new[] { 0, 1 })
        {
            var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
            var run = RunState.CreateForTest([first, second], acts: [], seed: "BOOK_ADMISSION");
            foreach (int act in new[] { 0, 1, 2 })
            {
                run.CurrentActIndex = act;
                Check(Canonical.IsAllowed(run) == (act == 1), "Only Act 2 is allowed.");
            }
            run.CurrentActIndex = 1;
            await RelicCmd.Obtain(book.ToMutable(), run.Players[holder]);
            Check(!Canonical.IsAllowed(run), "Any of the three books on either player must exclude the event.");
        }
    }

    private static async Task IndependentChoices()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        for (int ascension = 0; ascension <= 10; ascension++)
        {
            var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
            RunState.CreateForTest([first, second], acts: [], ascensionLevel: ascension, seed: "BOOK_CHOICES");
            first.Creature.SetCurrentHpInternal(60);
            second.Creature.SetCurrentHpInternal(60);
            var leave = await Begin(first);
            await Choose(leave, "LEAVE");
            Check(leave.IsFinished && first.Creature.CurrentHp == 60, "Leaving is free.");
            var stop = await ReadToEnd(first);
            var take = await ReadToEnd(second);
            await Choose(stop, "STOP");
            Check(stop.IsFinished && first.Creature.CurrentHp == 51 && !take.IsFinished, "Stop costs a total of 9 HP and leaves teammate choosing.");
            await Choose(take, "OBTAIN");
            await Choose(take, "NILRYS_CODEX");
            Check(second.Creature.CurrentHp == 39 && second.Relics.OfType<NilrysCodex>().Count() == 1, "Take costs 21 HP at all ascensions.");
            var sameBook = await ReadToEnd(first);
            await Choose(sameBook, "OBTAIN");
            await Choose(sameBook, "NILRYS_CODEX");
            Check(first.Relics.OfType<NilrysCodex>().Count() == 1, "A teammate's choice cannot remove a book from your choices.");
        }
    }

    private static async Task<EventModel> ReadToEnd(Player player)
    {
        var model = await Begin(player);
        await Choose(model, "READ");
        for (int page = 0; page < 3; page++) await Choose(model, "CONTINUE");
        return model;
    }

    private static async Task DeathAndRevival()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (int lethalPage in new[] { 1, 2, 3, 15, -3 })
        foreach (bool revive in new[] { false, true })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            RunState.CreateForTest([player], acts: [], seed: "BOOK_DEATH");
            player.Creature.SetCurrentHpInternal(60);
            LizardTail? tail = revive ? await RelicCmd.Obtain<LizardTail>(player) : null;
            var model = await Begin(player);
            await Choose(model, "READ");
            for (int page = 1; page <= 3; page++)
            {
                if (lethalPage == page) break;
                await Choose(model, "CONTINUE");
            }
            int damage = Math.Abs(lethalPage);
            player.Creature.SetCurrentHpInternal(damage);
            var option = model.CurrentOptions.Single(option => option.TextKey.EndsWith(lethalPage == 15 ? ".OBTAIN" : lethalPage == -3 ? ".STOP" : ".CONTINUE"));
            Check(!option.IsLocked && option.WillKillPlayer!(player), "Lethal choices must remain selectable with the native warning.");
            await option.Chosen();
            Check(!player.Relics.Any(relic => relic is Necronomicon or NilrysCodex or Enchiridion), "Damage alone never grants a book.");
            if (!revive)
                Check(player.Creature.IsDead && model.IsFinished, "Confirmed death must finish the event without a book.");
            else
            {
                Check(!player.Creature.IsDead && tail!.WasUsed && player.Creature.CurrentHp > 0, "Native death prevention must finish before checking survival.");
                Check(model.IsFinished == (lethalPage == -3), "A revived reader continues unless choosing Stop.");
                if (lethalPage == 15)
                {
                    Check(model.CurrentOptions.Count == 3, "Surviving the take cost must offer books.");
                    await Choose(model, "ENCHIRIDION");
                    Check(player.Relics.OfType<Enchiridion>().Count() == 1, "A revived reader can claim the chosen book.");
                }
            }
        }
    }

    private static async Task RepeatAttack()
    {
        var (player, other, combat) = CreateCombat();
        var book = await RelicCmd.Obtain<Necronomicon>(player);
        var context = new ThrowingPlayerChoiceContext();
        var attack = combat.CreateCard<Bash>(player);
        var cheap = combat.CreateCard<StrikeIronclad>(player);
        var skill = combat.CreateCard<FlameBarrier>(player);
        var teammate = combat.CreateCard<Bash>(other);
        for (int battle = 0; battle < 2; battle++)
        {
            for (int turn = 0; turn < 2; turn++)
            {
                await book.AfterPlayerTurnStart(context, player);
                foreach (var ineligible in new CardModel[] { cheap, skill, teammate })
                    Check(Hook.ModifyCardPlayCount(combat, ineligible, 1, null, out _) == 1, "Cheap attacks, skills and teammate attacks cannot consume the repeat.");
                attack.SetToFreeThisTurn();
                Check(Hook.ModifyCardPlayCount(combat, attack, 1, null, out _) == 1, "Resolved free attacks do not qualify even with printed cost 2.");
                attack = combat.CreateCard<Bash>(player);
                int count = Hook.ModifyCardPlayCount(combat, attack, 1, null, out var modifiers);
                Check(count == 2, "First qualifying attack gets one additional play.");
                await Hook.AfterModifyingCardPlayCount(combat, attack, modifiers);
                Check(Hook.ModifyCardPlayCount(combat, attack, 1, null, out _) == 1, "A second qualifying attack cannot repeat again.");
                await book.AfterPlayerTurnStart(context, other);
                Check(Hook.ModifyCardPlayCount(combat, attack, 1, null, out _) == 1, "Teammate turn cannot reset the owner's charge.");
            }
            await book.AfterCombatEnd(new CombatRoom(combat));
        }
    }

    private static async Task MultiplayerLethal()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        RunState.CreateForTest([first, second], acts: [], seed: "BOOK_MULTIPLAYER_LETHAL");
        first.Creature.SetCurrentHpInternal(60);
        second.Creature.SetCurrentHpInternal(60);
        var dying = await ReadToEnd(first);
        var survivor = await ReadToEnd(second);
        first.Creature.SetCurrentHpInternal(1);
        await Choose(dying, "OBTAIN");
#if STS2_PUBLIC
        Check(first.Creature.IsDead && dying.IsFinished, "Public confirms this player's death and ends only their event.");
#else
        // 0.111.0's native Kill prevents out-of-combat multiplayer deaths by
        // healing to 1 HP. Preserve that safety behavior rather than force Kill.
        Check(first.Creature.CurrentHp == 1 && !dying.IsFinished && dying.CurrentOptions.Count == 3,
            "Beta's native multiplayer survival safeguard must finish before offering books.");
#endif
        Check(!survivor.IsFinished && !first.Relics.Any(relic => relic is Necronomicon or NilrysCodex or Enchiridion), "The teammate's choices stay open and the lethal action itself grants no book.");
        await Choose(survivor, "OBTAIN");
        await Choose(survivor, "ENCHIRIDION");
        Check(survivor.IsFinished && second.Relics.OfType<Enchiridion>().Count() == 1, "The teammate can still finish independently.");
    }

    private static async Task Curse()
    {
        var (player, _, combat) = CreateCombat();
        var curse = combat.CreateCard<AllMatters.Cards.Necronomicurse>(player);
        Check(!curse.IsRemovable && curse.Keywords.Contains(CardKeyword.Eternal) && !curse.IsUpgradable && curse.Keywords.Contains(CardKeyword.Unplayable), "The curse must be unplayable, Eternal and unupgradable.");
        await CardPileCmd.AddGeneratedCardToCombat(curse, PileType.Hand, player);
        await CardCmd.Exhaust(new ThrowingPlayerChoiceContext(), curse);
        Check(player.PlayerCombatState!.Hand.Cards.Contains(curse) && !player.PlayerCombatState.ExhaustPile.Cards.Contains(curse), "Exhaust must return the same curse to hand.");
    }

    private static async Task Nilry()
    {
        var (player, other, combat) = CreateCombat();
        var book = await RelicCmd.Obtain<NilrysCodex>(player);
        var selector = new ChooseGenerated(player);
        using (CardSelectCmd.UseSelector(selector))
        {
            await book.BeforeFlushLate(new ThrowingPlayerChoiceContext(), other);
            Check(selector.Calls == 0, "Teammate flush must not offer cards.");
            await book.BeforeFlushLate(new ThrowingPlayerChoiceContext(), player);
            Check(selector.Calls == 1 && player.PlayerCombatState!.DrawPile.Cards.Single() == selector.Chosen, "The selected upgraded instance must be shuffled into the owner's draw pile.");
            selector.Skip = true;
            await book.BeforeFlushLate(new ThrowingPlayerChoiceContext(), player);
            Check(selector.Calls == 2 && player.PlayerCombatState!.DrawPile.Cards.Count == 1, "Skipping must add no card.");
        }
    }

    private sealed class ChooseGenerated(Player player) : ICardSelector
    {
        public int Calls { get; private set; }
        public bool Skip { get; set; }
        public CardModel? Chosen { get; private set; }
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            var cards = options.ToArray();
            var legal = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint).Select(card => card.Id).ToHashSet();
            Check(cards.Length == 3 && cards.Select(card => card.Id).Distinct().Count() == 3 && cards.All(card => card.IsUpgraded && card.Owner == player && legal.Contains(card.Id)), "Offer three distinct upgraded legal class cards.");
            Check(cards.All(card => !ModelDb.GetById<CardModel>(card.Id).IsUpgraded), "Candidate upgrades must not mutate canonical templates.");
            Check(minSelect == 0 && maxSelect == 1, "The choice is skippable.");
            Calls++;
            Chosen = Skip ? null : cards[0];
            return Task.FromResult<IEnumerable<CardModel>>(Chosen == null ? [] : [Chosen]);
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives) => throw new InvalidOperationException();
    }

    private static async Task EnchiridionGeneration()
    {
        var (player, other, combat) = CreateCombat();
        var book = await RelicCmd.Obtain<Enchiridion>(player);
        for (int battle = 0; battle < 2; battle++)
        {
            player.ResetCombatState();
            combat.RoundNumber = 1;
            await book.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), other);
            Check(player.PlayerCombatState!.Hand.Cards.Count == 0, "Teammate turns cannot generate a power.");
            await book.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), player);
            var card = player.PlayerCombatState.Hand.Cards.Single();
            Check(card.Type == CardType.Power && card.Owner == player && card.Pool == player.Character.CardPool && card.EnergyCost.GetResolved() == 0, "Generate one owned class power free this turn.");
            card.EndOfTurnCleanup();
            Check(card.EnergyCost.GetResolved() == card.EnergyCost.Canonical, "The free cost expires after this turn.");
            combat.RoundNumber = 2;
            await book.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), player);
            Check(player.PlayerCombatState.Hand.Cards.Count == 1 && other.PlayerCombatState!.Hand.Cards.Count == 0, "Later turns and other players get no additional power.");
        }
    }

    private static (Player Player, Player Other, CombatState Combat) CreateCombat()
    {
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([player, other], acts: [], seed: "BOOK_COMBAT");
        var combat = new CombatState(runState: run);
        foreach (var member in run.Players) { combat.AddPlayer(member); member.ResetCombatState(); }
        return (player, other, combat);
    }

    private static async Task InventoryRestore()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        RunState.CreateForTest([player], acts: [], seed: "BOOK_SAVE");
        await RelicCmd.Obtain<Necronomicon>(player);
        int hp = player.Creature.CurrentHp;
        var restored = Player.FromSerializable(player.ToSerializable());
        Check(restored.Creature.CurrentHp == hp && restored.Relics.OfType<Necronomicon>().Count() == 1
            && restored.Deck.Cards.Count(card => card is AllMatters.Cards.Necronomicurse) == 1, "Inventory restore must not re-run pickup and duplicate the curse.");
    }

    private static async Task EntrySnapshotRestore()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var initial = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        RunState.CreateForTest([initial], acts: [], seed: "BOOK_ENTRY_SAVE");
        initial.Creature.SetCurrentHpInternal(60);
        var entryInventory = initial.ToSerializable();
        var room = new EventRoom(Canonical).ToSerializable();
        Check(new EventRoom(room).CanonicalEvent.Id == Canonical.Id, "The native room record restores this event's identity.");
        // Normal events save at map entry, not after individual options. Each
        // interruption therefore restores BOTH the event and the entry inventory.
        for (int choicesBeforeReload = 0; choicesBeforeReload <= 6; choicesBeforeReload++)
        {
            var interrupted = Player.FromSerializable(entryInventory);
            RunState.CreateForTest([interrupted], acts: [], seed: "BOOK_ENTRY_SAVE");
            var model = await Begin(interrupted);
            string[] choices = ["READ", "CONTINUE", "CONTINUE", "CONTINUE", "OBTAIN", "NECRONOMICON"];
            foreach (string choice in choices.Take(choicesBeforeReload)) await Choose(model, choice);
            var loaded = Player.FromSerializable(entryInventory);
            RunState.CreateForTest([loaded], acts: [], seed: "BOOK_ENTRY_SAVE");
            Check(loaded.Creature.CurrentHp == 60 && loaded.Relics.Count == 1, "Loading the entry snapshot restores all pre-event costs and rewards together.");
            var restarted = await ReadToEnd(loaded);
            await Choose(restarted, "OBTAIN");
            await Choose(restarted, "NECRONOMICON");
            Check(loaded.Creature.CurrentHp == 39 && loaded.Relics.OfType<Necronomicon>().Count() == 1
                && loaded.Deck.Cards.Count(card => card is AllMatters.Cards.Necronomicurse) == 1, "Replaying from the supported snapshot pays only once and grants only one book and curse.");
        }
    }

    private static Task Presentation()
    {
        Check(Canonical.CreateInitialPortrait().GetWidth() > 0 && ModelDb.Card<AllMatters.Cards.Necronomicurse>().Portrait.GetWidth() > 0, "Actual event and curse portraits must load.");
        foreach (string book in new[] { "necronomicon", "nilrys_codex", "enchiridion" })
        foreach (string variant in new[] { "", "outlines/", "big/" })
            Check(ResourceLoader.Load<Texture2D>($"res://AllMatters/images/relics/{variant}{book}.png").GetWidth() > 0, "Each book icon variant must load.");
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                foreach (string table in new[] { "events", "relics", "cards" })
                {
                    var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(Godot.FileAccess.GetFileAsString($"res://AllMatters/localization/{language}/{table}.json"))!;
                    foreach (var entry in entries.Where(entry => new[] { "CURSED_TOME", "NECRONOM", "NILRYS", "ENCHIRIDION" }.Any(entry.Key.Contains)))
                        Check(new LocString(table, entry.Key).GetRawText() == entry.Value, $"Missing localized entry {entry.Key}.");
                }
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
        return Task.CompletedTask;
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
