using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Hooks;
using AllMatters.Relics;
using AllMatters.Cards;
using MegaCrit.Sts2.Core.Random;

internal static class NativeSelectionTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Native selections light door upgrades non-Basic cards before Basic starters", Light),
        ("Native selections morphic group automatically raises two lowest legal cards one rarity", Morphic),
        ("Native selections hollow shows9 or18 HP for one or two real transformations", Hollow),
        ("Native selections Zen replaces both removal branches together and preserves250-gold timing", Zen),
        ("Native selections graft heals25 and delivers the displayed Chrysalis alternative", Grafter),
        ("Native selections Chrysalis generates3 or5 legal owner Skills free for combat", ChrysalisSkills),
        ("Native selections amalgamation pays two removals for one upgraded merged-pool Rare", Amalgamation),
        ("Native selections wisp pays10 percent through native HP commands and locks no targets", Wisp),
        ("Native selections cursed key costs5 Max HP, grants owner energy and rolls once per chest", Key),
    ];
    private static async Task Light()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        for (int i = 0; i < 4; i++)
        {
            var p = NativeEventTests.Player("LIGHT" + i);
            var first = p.RunState.CreateCard<Anger>(p);
            var second = p.RunState.CreateCard<Uppercut>(p);
            await CardPileCmd.Add(first, PileType.Deck); await CardPileCmd.Add(second, PileType.Deck);
            var e = await NativeEventTests.Begin<DoorsOfLightAndDark>(p);
            await e.CurrentOptions[0].Chosen();
            NativeEventTests.Check(first.IsUpgraded && second.IsUpgraded && p.Deck.Cards.Where(c => c.Rarity == CardRarity.Basic).All(c => !c.IsUpgraded),
                "Both non-Basic cards must be upgraded before any starter.");
        }
    }

    private static async Task Morphic()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (var rarity in new[] { CardRarity.Basic, CardRarity.Common, CardRarity.Uncommon })
        {
            var p = NativeEventTests.Player();
            foreach (var card in p.Deck.Cards.ToArray()) p.Deck.RemoveInternal(card);
            var originals = p.Character.CardPool.GetUnlockedCards(p.UnlockState, p.RunState.CardMultiplayerConstraint).Where(c => c.Rarity == rarity).Take(2)
                .Select(c => p.RunState.CreateCard(c, p)).ToArray();
            if (rarity == CardRarity.Basic) { originals[0].UpgradeInternal(); originals[0].FinalizeUpgradeInternal(); }
            await CardPileCmd.Add(originals, PileType.Deck);
            p.Gold = 100;
            var e = await NativeEventTests.Begin<MorphicGrove>(p);
            await e.CurrentOptions[0].Chosen();
            var expected = rarity == CardRarity.Basic ? CardRarity.Common : rarity == CardRarity.Common ? CardRarity.Uncommon : CardRarity.Rare;
            NativeEventTests.Check(e.IsFinished && p.Gold == 0 && p.Deck.Cards.Count == 2 && p.Deck.Cards.All(c => c.Rarity == expected && !originals.Contains(c)
                && c.Pool.Id == p.Character.CardPool.Id), "Pay original all-Gold cost and transform both cards to next character rarity.");
        }
    }

    private static async Task Hollow()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        for (int i = 0; i < 6; i++)
        {
            var p = NativeEventTests.Player("HOLLOW" + i);
            var e = await NativeEventTests.Begin<WhisperingHollow>(p);
            int cost = e.DynamicVars.HpLoss.IntValue, hp = p.Creature.CurrentHp;
            var selected = p.Deck.Cards.Take(cost / 9).ToArray();
            using (CardSelectCmd.UseSelector(new NativeEventTests.Select(selected))) await e.CurrentOptions[1].Chosen();
            NativeEventTests.Check(cost is 9 or 18 && p.Creature.CurrentHp == hp - cost && selected.All(c => !p.Deck.Cards.Contains(c)) && e.IsFinished,
                "Displayed9/18 cost must accompany exactly one/two selected transformations.");
            NativeEventTests.Check(e.DynamicVars.Gold.IntValue is >= 16 and <= 34, "Gold option must show16–34.");
        }
    }

    private static async Task Zen()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        for (int i = 0; i < 20; i++)
        {
            var p = NativeEventTests.Player("ZEN" + i); p.Gold = 250;
            var e = await NativeEventTests.Begin<ZenWeaver>(p);
            if (!e.CurrentOptions[1].TextKey.EndsWith(".AM_UPGRADE_1")) continue;
            NativeEventTests.Check(e.CurrentOptions[2].TextKey.EndsWith(".AM_UPGRADE_2"), "A single branch roll must replace both options.");
            var selected = p.Deck.Cards.Take(2).ToArray();
            using (CardSelectCmd.UseSelector(new NativeEventTests.Select(selected))) await e.CurrentOptions[2].Chosen();
            NativeEventTests.Check(e.IsFinished && p.Gold == 0 && selected.All(c => c.IsUpgraded && p.Deck.Cards.Contains(c)), "Upgrade the selected two, then pay250.");
            var shortage = NativeEventTests.Player("ZEN" + i); shortage.Gold = 250;
            foreach (var c in shortage.Deck.Cards) { c.UpgradeInternal(); c.FinalizeUpgradeInternal(); }
            var shortageEvent = await NativeEventTests.Begin<ZenWeaver>(shortage);
            await shortageEvent.CurrentOptions[2].Chosen();
            NativeEventTests.Check(shortageEvent.IsFinished && shortage.Gold == 0, "Keep the confirmed native Zen shortage and payment behavior even with no upgrade targets.");
            return;
        }
        throw new Exception("Upgrade variant not exercised.");
    }

    private static async Task Grafter()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        for (int i = 0; i < 20; i++)
        {
            var p = NativeEventTests.Player("GRAFTER" + i); p.Creature.SetCurrentHpInternal(10);
            var e = await NativeEventTests.Begin<SpiritGrafter>(p);
            if (!e.CurrentOptions[0].TextKey.EndsWith(".AM_CHRYSALIS")) continue;
            await e.CurrentOptions[0].Chosen();
            NativeEventTests.Check(e.IsFinished && p.Creature.CurrentHp == 35 && p.Deck.Cards.Last() is Chrysalis, "Heal25 and grant the displayed card.");
            return;
        }
        throw new Exception("Chrysalis variant not exercised.");
    }

    private static async Task ChrysalisSkills()
    {
        CharacterModel[] characters = [ModelDb.Character<Ironclad>(), ModelDb.Character<Silent>(), ModelDb.Character<Regent>(), ModelDb.Character<Necrobinder>(), ModelDb.Character<Defect>()];
        foreach (var character in characters)
        foreach (bool upgraded in new[] { false, true })
        for (int seed = 0; seed < 8; seed++)
        {
            var p = Player.CreateForNewRun(character, UnlockState.all, 1);
            var run = RunState.CreateForTest([p], acts: [], seed: "CHRYSALIS" + seed);
            var combat = new CombatState(runState: run); combat.AddPlayer(p); p.ResetCombatState();
            p.PlayerCombatState!.Energy = 3;
            var card = combat.CreateCard<Chrysalis>(p);
            if (upgraded) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
            await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), null, true, default, skipCardPileVisuals: true);
            var generated = p.PlayerCombatState.DrawPile.Cards;
            NativeEventTests.Check(generated.Count == (upgraded ? 5 : 3) && generated.All(c => c.Type == CardType.Skill && c.Pool.Id == character.CardPool.Id
                && c.CanBeGeneratedInCombat && !c.DynamicVars.Any(v => v.Value is MegaCrit.Sts2.Core.Localization.DynamicVars.HealVar)), "Generate only legal nonhealing character skills,3/5 instances.");
            foreach (var skill in generated)
            {
                NativeEventTests.Check(skill.EnergyCost.GetAmountToSpend() == 0, "Generated skill must be free: " + skill.Id);
                skill.EndOfTurnCleanup();
                NativeEventTests.Check(skill.EnergyCost.GetAmountToSpend() == 0, "Zero cost must survive turn cleanup.");
                var spent = await skill.SpendResources();
                NativeEventTests.Check(spent.Item1 == 0 && p.PlayerCombatState.Energy == 3, "Native resource payment must spend no energy.");
            }
        }
    }

    private static async Task Amalgamation()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player();
        var originals = p.Deck.Cards.ToArray();
        var e = await NativeEventTests.Begin<Amalgamator>(p);
        await e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_FUSE")).Chosen();
        var added = p.Deck.Cards.Except(originals).Single();
        NativeEventTests.Check(e.IsFinished && originals.Except(p.Deck.Cards).Count() == 2 && added.Rarity == CardRarity.Rare && added.IsUpgraded
            && (added.Pool.Id == p.Character.CardPool.Id || added.Pool is ColorlessCardPool), "Remove2 actual cards and add1 upgraded Rare from the merged legal pool.");
    }

    private static async Task Wisp()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player(); p.Creature.SetMaxHpInternal(83); p.Creature.SetCurrentHpInternal(30);
        var e = await NativeEventTests.Begin<LostWisp>(p);
        var target = p.Deck.Cards[0];
        using (CardSelectCmd.UseSelector(new NativeEventTests.Select(target))) await e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_BURN")).Chosen();
        NativeEventTests.Check(e.IsFinished && p.Creature.CurrentHp == 22 && !p.Deck.Cards.Contains(target), "Native10 percent rounds83 to8 loss and removes selected card.");
        foreach (var card in p.Deck.Cards.ToArray()) p.Deck.RemoveInternal(card);
        e = await NativeEventTests.Begin<LostWisp>(p);
        NativeEventTests.Check(e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_BURN")).IsLocked, "No target must lock new paid choice.");
    }

    private static async Task Key()
    {
        using (HeadlessCombatInProgress.EnterEvent())
        {
            var p = NativeEventTests.Player(); int max = p.Creature.MaxHp;
            var e = await NativeEventTests.Begin<DoorsOfLightAndDark>(p);
            await e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_KEY")).Chosen();
            NativeEventTests.Check(p.Creature.MaxHp == max - 5 && p.Relics.OfType<CursedKey>().Count() == 1, "Key costs5 Max HP.");
        }
        var (owner, _) = NativeEventTests.Combat();
        var key = await RelicCmd.Obtain<CursedKey>(owner);
        owner.PlayerCombatState!.Energy = 0;
        await key.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), owner);
        NativeEventTests.Check(owner.PlayerCombatState.Energy == 1, "Key grants1 energy at owner turn start.");
        using var scope = HeadlessCombatInProgress.EnterEvent();
        int cursed = 0, clear = 0;
        for (int seed = 0; seed < 12; seed++)
        {
            var p = NativeEventTests.Player("KEY_CHEST" + seed);
            var run = (RunState)p.RunState;
            var localId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
            RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
            try
            {
                await RelicCmd.Obtain<CursedKey>(p);
                var room = new TreasureRoom(0); run.PushRoom(room);
                int before = p.Deck.Cards.Count;
                var rng = PlayerRngSet.FromSerializable(p.PlayerRng.ToSerializable()).Rewards;
                rng.NextInt(42, 53); // Native chest gold resolves before the key.
                bool expected = rng.NextInt(2) == 0;
                await RunManager.Instance.OneOffSynchronizer.DoLocalTreasureRoomRewards();
                int added = p.Deck.Cards.Count - before;
                NativeEventTests.Check(added == (expected ? 1 : 0), "Actual chest opening follows one50 percent player reward roll.");
                if (added == 1) cursed++; else clear++;
                await RunManager.Instance.OneOffSynchronizer.DoLocalTreasureRoomRewards();
                NativeEventTests.Check(p.Deck.Cards.Count == before + added, "Reopening same chest must not reroll or add another curse.");
            }
            finally
            {
                RunManager.Instance.CleanUp(false);
                MegaCrit.Sts2.Core.Context.LocalContext.NetId = localId;
            }
        }
        NativeEventTests.Check(cursed > 0 && clear > 0, "Exercise both chest outcomes.");
    }
}
