using AllMatters;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;

internal static class HelloWorldTests
{
    public static async Task<int> Run(string[] args)
    {
        TestMode.IsOn = true;
        LocalContext.NetId = 1;
        // The real game constructs saves before loading mods; preserve that ordering.
        SaveManager.Instance.InitSettingsDataForTest();
        SaveManager.Instance.InitPrefsDataForTest();
        // Install host lifecycle substitutes before framework patches prepare game methods.
        new Harmony("AllMatters.TestHost").PatchAll(typeof(HelloWorldTests).Assembly);
        STS2RitsuLib.RitsuLibFramework.Initialize();
        ModEntry.Initialize();
        try
        {
            await ModManager.Initialize(null!, null, null);
            SaveManager.Instance.SettingsSave.Language = "eng";
            LocManager.Initialize();
            foreach (Type type in ReflectionHelper.GetSubtypes<AbstractModel>()
                .Concat(typeof(ModEntry).Assembly.GetTypes().Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(AbstractModel)))))
            {
                ModelDb.Inject(type);
            }
#if !STS2_PUBLIC
            MegaCrit.Sts2.Core.Modding.AssemblyInfo.Init();
#endif
            MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache.Init();

            (string Name, Func<Task> Test)[] cases =
            [
                ("Upgrade does not add Innate", UpgradeWithoutInnate),
                ("Ordinary single source keeps original behavior", OrdinarySingle),
                ("Upgraded single source has its own power and upgrades its card", UpgradedSingle),
                ("Mixed sources match one original batch and RNG progression", MixedBatch),
                ("Presentation distinguishes upgraded card and power", Presentation),
                ("Two ordinary and three upgraded layers persist across turns", MultipleLayersAndTurns),
                ("Play order and hook order do not duplicate generation", OrderIndependence),
                ("Full hands keep upgraded overflow in the original destination", FullHand),
                ("Turn snapshots retain original timing", TurnSnapshot),
                ("Echo Form repeats upgraded power application", RepeatedPlay),
                ("Owner character pool, unlocks and multiplayer restrictions are retained", OriginalPool),
                ("Templates and unrelated generated cards stay unchanged", InstanceIsolation),
                ("Another player's turn does not trigger generation", OtherPlayerTurn),
                ("Chinese localization survives language changes", ChinesePresentation),
                ("Upgrade preview highlights only the added words", UpgradePreviewHighlight)
            ];
            var selected = cases.Concat(ClericTests.Cases).Concat(BeggarFountainTests.Cases).Concat(AugmenterTests.Cases).Concat(CursedTomeTests.Cases).Concat(WheelOfChangeTests.Cases).Concat(TombOfLordRedMaskTests.Cases)
                .Concat(LibraryTests.Cases).Concat(MaskedBanditsTests.Cases).Concat(MindBloomTests.Cases).Concat(ColosseumTests.Cases).Concat(NativeEventTests.Cases).Concat(NativeProbabilityTests.Cases).Concat(NativeRadianceTests.Cases).Concat(NativeNlothTests.Cases).Concat(NativeCandleTests.Cases).Concat(NativeSelectionTests.Cases).Concat(NativeRelicEventTests.Cases)
                .Concat(NativeCounterTests.Cases).Concat(TrashHeapTests.Cases).Concat(TrashHeapDefectTests.Cases).Concat(TrashHeapRedGreenTests.Cases)
                .Concat(AmbrosiaTests.Cases)
                .Concat(JungleMazeAdventureTests.Cases)
                .Concat(NativeLockedOptionTests.Cases).Concat(NativeVegetationTests.Cases)
                .Where(test => args.Length == 0 || test.Name.Contains(args[0], StringComparison.OrdinalIgnoreCase)).ToArray();
            Check(selected.Length > 0, "No test matches the supplied filter.");
            foreach (var test in selected)
            {
                await test.Test();
                Console.WriteLine("PASS: " + test.Name);
            }
            Console.WriteLine($"Passed {selected.Length} tests against {typeof(CardModel).Assembly.GetName().Version}.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static Task UpgradeWithoutInnate()
    {
        CardModel card = ModelDb.Card<HelloWorld>().ToMutable();
        Upgrade(card);
        Check(card.IsUpgraded && !card.Keywords.Contains(CardKeyword.Innate), "Upgrade must not grant Innate.");
        return Task.CompletedTask;
    }

    private static async Task OrdinarySingle()
    {
        var (player, combat) = CreateCombat();
        await Play(player, combat, false);
        await Turn(player, combat);
        Check(player.Creature.Powers.Single() is HelloWorldPower { Amount: 1 }, "Ordinary source must use the original power.");
        Check(player.PlayerCombatState!.Hand.Cards.Count == 1 && !player.PlayerCombatState.Hand.Cards[0].IsUpgraded,
            "Ordinary source must generate one ordinary card.");
    }

    private static async Task UpgradedSingle()
    {
        var (player, combat) = CreateCombat();
        await Play(player, combat, true);
        await Turn(player, combat);
        Check(player.Creature.Powers.Single() is not HelloWorldPower && player.Creature.Powers.Single().Amount == 1,
            "Upgraded source must have a separate power.");
        Check(player.PlayerCombatState!.Hand.Cards.Count == 1 && player.PlayerCombatState.Hand.Cards[0].IsUpgraded,
            "Upgraded source must generate one upgraded card.");
    }

    private static async Task MixedBatch()
    {
        var (referencePlayer, _) = CreateCombat();
        var expected = CardFactory.GetDistinctForCombat(referencePlayer,
            referencePlayer.Character.CardPool.GetUnlockedCards(referencePlayer.UnlockState, referencePlayer.RunState.CardMultiplayerConstraint)
                .Where(card => card.Rarity == CardRarity.Common),
            2, referencePlayer.RunState.Rng.CombatCardGeneration).Select(card => card.Id).ToArray();
        int expectedNext = referencePlayer.RunState.Rng.CombatCardGeneration.NextInt();

        var (player, combat) = CreateCombat();
        await Play(player, combat, false);
        await Play(player, combat, true);
        await Turn(player, combat);
        var cards = player.PlayerCombatState!.Hand.Cards;
        Check(cards.Count == 2 && cards.Count(card => card.IsUpgraded) == 1, "Mixed sources must generate two cards with one upgraded.");
        Check(cards.Select(card => card.Id).SequenceEqual(expected), "Mixed selection must match a single original batch.");
        Check(player.RunState.Rng.CombatCardGeneration.NextInt() == expectedNext, "Mixed generation must preserve original RNG progression.");
    }

    private static async Task Presentation()
    {
        var (player, combat) = CreateCombat();
        await Play(player, combat, true);
        PowerModel power = player.Creature.Powers.Single();
        Check(power.Title.GetFormattedText() == "Hello World+", "Upgraded power must have a localized title.");
        Check(power.PackedIconPath == ModelDb.Power<HelloWorldPower>().PackedIconPath, "Upgraded power must use an existing icon.");
        CardModel card = combat.CreateCard<HelloWorld>(player);
        string ordinary = card.GetDescriptionForPile(PileType.Hand);
        Upgrade(card);
        string upgraded = card.GetDescriptionForPile(PileType.Hand);
        Check(ordinary != upgraded && upgraded.Contains("Upgraded", StringComparison.OrdinalIgnoreCase), "Upgraded card text must explain generated upgrades.");
        Check(card.GetDescriptionForUpgradePreview().Contains("Upgraded", StringComparison.OrdinalIgnoreCase), "Upgrade preview must show upgraded generation.");
    }

    private static Task UpgradePreviewHighlight()
    {
        try
        {
            foreach (var (language, expectedWords) in new[] { ("zhs", "升级过的"), ("eng", "Upgraded") })
            {
                LocManager.Instance.SetLanguage(language);
                CardModel card = ModelDb.Card<HelloWorld>().ToMutable();
                string preview = card.GetDescriptionForUpgradePreview();
                var highlights = System.Text.RegularExpressions.Regex.Matches(preview, @"\[green\](.*?)\[/green\]",
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                Check(highlights.Count == 1, "Upgrade preview must have exactly one changed phrase.");
                string changedWords = System.Text.RegularExpressions.Regex.Replace(highlights[0].Groups[1].Value, @"\[[^\]]+\]", "");
                Check(changedWords.Trim() == expectedWords, "Only the added words must be green, not the whole description.");
                Check(card.GetDescriptionForPile(PileType.None) == new LocString("cards", "HELLO_WORLD.description").GetFormattedText(),
                    "Ordinary card text must remain identical to the original localization.");
                Upgrade(card);
                Check(card.GetDescriptionForUpgradePreview() == preview,
                    "Inspecting an already upgraded card must highlight the same changed words as its upgrade preview.");
                Check(card.GetDescriptionForPile(PileType.None).Contains(expectedWords)
                    && !card.GetDescriptionForPile(PileType.None).Contains("[green]"),
                    "Normal upgraded display must retain its text without preview coloring.");
            }
        }
        finally
        {
            LocManager.Instance.SetLanguage("eng");
        }
        return Task.CompletedTask;
    }

    private static async Task MultipleLayersAndTurns()
    {
        var (player, combat) = CreateCombat();
        for (int index = 0; index < 2; index++)
        {
            await Play(player, combat, false);
        }
        for (int index = 0; index < 3; index++)
        {
            await Play(player, combat, true);
        }
        Check(player.Creature.Powers.Count == 2 && player.Creature.GetPower<HelloWorldPower>()!.Amount == 2
            && player.Creature.Powers.Single(power => power is not HelloWorldPower).Amount == 3,
            "Separate powers must display their own accumulated contributions.");
        await Turn(player, combat);
        Check(player.PlayerCombatState!.Hand.Cards.Count == 5 && player.PlayerCombatState.Hand.Cards.Count(card => card.IsUpgraded) == 3,
            "Two ordinary and three upgraded layers must generate five cards with three upgraded.");
        await Turn(player, combat);
        Check(player.PlayerCombatState.Hand.Cards.Count == 10 && player.PlayerCombatState.Hand.Cards.Count(card => card.IsUpgraded) == 6,
            "Generation must repeat next turn without consuming layers.");
    }

    private static async Task OrderIndependence()
    {
        var (first, firstCombat) = CreateCombat();
        await Play(first, firstCombat, false);
        await Play(first, firstCombat, true);
        await Turn(first, firstCombat);

        var (second, secondCombat) = CreateCombat();
        await Play(second, secondCombat, true);
        await Play(second, secondCombat, false);
        foreach (PowerModel power in second.Creature.Powers)
        {
            power.AmountOnTurnStart = power.Amount;
        }
        foreach (PowerModel power in second.Creature.Powers.OrderBy(power => power is HelloWorldPower ? 1 : 0))
        {
            await power.BeforeHandDraw(second, new ThrowingPlayerChoiceContext(), secondCombat);
        }
        Check(first.PlayerCombatState!.Hand.Cards.Select(card => (card.Id, card.IsUpgraded))
            .SequenceEqual(second.PlayerCombatState!.Hand.Cards.Select(card => (card.Id, card.IsUpgraded))),
            "Play and hook order must not change a shared generation with identical RNG state.");
    }

    private static async Task FullHand()
    {
        var (player, combat) = CreateCombat();
        for (int index = 0; index < CardPile.MaxCardsInHand; index++)
        {
            player.PlayerCombatState!.Hand.AddInternal(combat.CreateCard<StrikeDefect>(player));
        }
        await Play(player, combat, false);
        await Play(player, combat, true);
        await Turn(player, combat);
        Check(player.PlayerCombatState!.Hand.Cards.Count == CardPile.MaxCardsInHand
            && player.PlayerCombatState.DiscardPile.Cards.Count == 2
            && player.PlayerCombatState.DiscardPile.Cards.Count(card => card.IsUpgraded) == 1,
            "Original pile command must send overflow to discard while preserving its upgrade.");
    }

    private static async Task TurnSnapshot()
    {
        var (player, combat) = CreateCombat();
        await Play(player, combat, false);
        PowerModel ordinary = player.Creature.GetPower<HelloWorldPower>()!;
        ordinary.AmountOnTurnStart = 1;
        await Play(player, combat, true);
        PowerModel upgraded = player.Creature.Powers.Single(power => power is not HelloWorldPower);
        Check(upgraded.AmountOnTurnStart == 0, "New power starts without a turn snapshot.");
        foreach (PowerModel power in player.Creature.Powers.ToArray())
        {
            await power.BeforeHandDraw(player, new ThrowingPlayerChoiceContext(), combat);
        }
        Check(player.PlayerCombatState!.Hand.Cards.Count == 1 && !player.PlayerCombatState.Hand.Cards[0].IsUpgraded,
            "Newly applied upgraded power must wait for the next snapshot.");
        await Turn(player, combat);
        Check(player.PlayerCombatState.Hand.Cards.Count == 3 && player.PlayerCombatState.Hand.Cards.Count(card => card.IsUpgraded) == 1,
            "Upgraded power participates in the following turn's shared batch.");
    }

    private static async Task RepeatedPlay()
    {
        var (player, combat) = CreateCombat();
        await PowerCmd.Apply<EchoFormPower>(new ThrowingPlayerChoiceContext(), player.Creature, 1, player.Creature, null);
        await Play(player, combat, true);
        PowerModel upgraded = player.Creature.Powers.Single(power => power is not EchoFormPower);
        Check(upgraded.Amount == 2, "Game-provided replay of a power must apply two upgraded layers.");
        await Turn(player, combat);
        Check(player.PlayerCombatState!.Hand.Cards.Count == 2 && player.PlayerCombatState.Hand.Cards.All(card => card.IsUpgraded),
            "Repeated upgraded application must generate two upgraded cards.");
        Check(player.PlayerCombatState.DiscardPile.Cards.Count == 0 && player.PlayerCombatState.ExhaustPile.Cards.Count == 0,
            "Played power must not enter discard or exhaust piles.");
    }

    private static async Task OriginalPool()
    {
        foreach (bool multiplayer in new[] { false, true })
        {
            var (reference, _) = CreateCombat(ModelDb.Character<Ironclad>(), UnlockState.none, multiplayer);
            ModelId[] expected = CardFactory.GetDistinctForCombat(reference,
                reference.Character.CardPool.GetUnlockedCards(reference.UnlockState, reference.RunState.CardMultiplayerConstraint)
                    .Where(card => card.Rarity == CardRarity.Common),
                2, reference.RunState.Rng.CombatCardGeneration).Select(card => card.Id).ToArray();
            var (player, combat) = CreateCombat(ModelDb.Character<Ironclad>(), UnlockState.none, multiplayer);
            await Play(player, combat, false);
            await Play(player, combat, true);
            await Turn(player, combat);
            Check(player.PlayerCombatState!.Hand.Cards.Select(card => card.Id).SequenceEqual(expected),
                "Mixed sources must use the owner's original pool with the same unlock and multiplayer filters.");
        }
    }

    private static async Task InstanceIsolation()
    {
        var (player, combat) = CreateCombat();
        await Play(player, combat, true);
        await Turn(player, combat);
        CardModel generated = player.PlayerCombatState!.Hand.Cards.Single();
        Check(generated.IsUpgraded && !ModelDb.GetById<CardModel>(generated.Id).IsUpgraded,
            "Generated upgrade must not mutate its canonical template.");
        CardModel unrelated = combat.CreateCard<BallLightning>(player);
        await CardPileCmd.AddGeneratedCardToCombat(unrelated, PileType.Hand, player);
        Check(!unrelated.IsUpgraded, "Other effects' generated cards must not receive the upgrade.");
    }

    private static async Task OtherPlayerTurn()
    {
        var (player, combat) = CreateCombat(multiplayer: true);
        await Play(player, combat, false);
        await Play(player, combat, true);
        Player other = combat.Players.Single(candidate => candidate != player);
        foreach (PowerModel power in player.Creature.Powers)
        {
            power.AmountOnTurnStart = power.Amount;
            await power.BeforeHandDraw(other, new ThrowingPlayerChoiceContext(), combat);
        }
        Check(player.PlayerCombatState!.Hand.Cards.Count == 0 && other.PlayerCombatState!.Hand.Cards.Count == 0,
            "Only the owning player's turn may generate cards.");
        await Turn(player, combat);
        Check(player.PlayerCombatState.Hand.Cards.Count == 2, "The owner's next turn must still generate its batch.");
    }

    private static async Task ChinesePresentation()
    {
        LocManager.Instance.SetLanguage("zhs");
        try
        {
            var (player, combat) = CreateCombat();
            await Play(player, combat, true);
            PowerModel power = player.Creature.Powers.Single();
            Check(power.Title.GetFormattedText() == "你好世界+", "Upgraded power must have its Chinese title.");
            CardModel card = combat.CreateCard<HelloWorld>(player);
            Check(card.GetDescriptionForPile(PileType.Hand) == new LocString("cards", "HELLO_WORLD.description").GetFormattedText(),
                "Ordinary card must keep its original localized description.");
            Check(card.GetDescriptionForUpgradePreview().Contains("升级过的"), "Upgrade preview must describe upgraded generation.");
            Upgrade(card);
            Check(card.GetDescriptionForPile(PileType.Hand).Contains("升级过的"), "Upgraded card must describe upgraded generation.");
            LocString description = power.Description;
            description.Add("Amount", 1);
            Check(description.GetFormattedText().Contains("升级过的"), "Power description must explain its own upgraded contribution.");
        }
        finally
        {
            LocManager.Instance.SetLanguage("eng");
        }
    }

    private static (Player Player, CombatState Combat) CreateCombat(CharacterModel? character = null, UnlockState? unlocks = null, bool multiplayer = false)
    {
        Player player = Player.CreateForNewRun(character ?? ModelDb.Character<Defect>(), unlocks ?? UnlockState.all, 1);
        Player[] players = multiplayer
            ? [player, Player.CreateForNewRun<Silent>(UnlockState.all, 2)]
            : [player];
        RunState run = RunState.CreateForTest(players, acts: [], seed: "HELLOWORLD");
        CombatState combat = new(runState: run);
        foreach (Player member in players)
        {
            combat.AddPlayer(member);
            member.ResetCombatState();
        }
        return (player, combat);
    }

    private static async Task Play(Player player, CombatState combat, bool upgraded)
    {
        CardModel card = combat.CreateCard<HelloWorld>(player);
        if (upgraded)
        {
            Upgrade(card);
        }
        await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), null, true, default, skipCardPileVisuals: true);
    }

    private static async Task Turn(Player player, CombatState combat)
    {
        foreach (PowerModel power in player.Creature.Powers)
        {
            power.AmountOnTurnStart = power.Amount;
        }
        foreach (PowerModel power in player.Creature.Powers.ToArray())
        {
            await power.BeforeHandDraw(player, new ThrowingPlayerChoiceContext(), combat);
        }
    }

    private static void Upgrade(CardModel card)
    {
        card.UpgradeInternal();
        card.FinalizeUpgradeInternal();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
