using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Entities.Relics;
using AllMatters.Encounters;
using AllMatters.Monsters;
using AllMatters.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.HoverTips;

internal static class MindBloomTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Mind Bloom shared choices use floor 37/38 and grant exact owner gold, healing and curses once", NonCombatChoices),
        ("Mind Bloom awakening upgrades every legal card and blocks only its owner's event, rest and combat healing", Awakening),
        ("Mind Bloom enters one two-Lagavulin fight and grants each participant exact boss-style rewards with restorable room", FightRewards),
        ("Mind Bloom Lagavulins sleep independently, wake on unblocked damage or three turns, and use native multiplayer HP", SleepingLagavulins),
        ("Mind Bloom native reward hooks retain additional rewards and upgraded candidates", RewardModifications),
        ("Mind Bloom Lagavulin retains normal and ascension HP, attack and strength/dexterity drain", AscensionMoves),
        ("Mind Bloom portrait, bilingual text, power icons, Spine animations and two encounter slots load from PCK", Presentation),
        ("Mind Bloom option copy uses requested colors and native relic and curse hover previews", OptionPresentation),
    ];

    private static EventModel Canonical => ModelDb.AllEvents.SingleOrDefault(m => m.Id.Entry == "ALL_MATTERS_EVENT_MIND_BLOOM")
        ?? throw new Exception("Mind Bloom must be registered.");

    private static async Task NonCombatChoices()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (int count in new[] { 1, 2 })
        foreach (int floor in new[] { 37, 38 })
        {
            var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
            var players = count == 1 ? new[] { first } : new[] { first, second };
            var run = RunState.CreateForTest(players, acts: [], seed: "BLOOM_CHOICES");
            run.CurrentActIndex = 2;
            for (int i = 0; i < floor; i++) run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, Canonical.Id);
            Check(Canonical.IsAllowed(run), "Act 3 must be admitted.");
            foreach (int act in new[] { 0, 1 }) { run.CurrentActIndex = act; Check(!Canonical.IsAllowed(run), "Other acts must be excluded."); }
            run.CurrentActIndex = 2;
            first.Gold = 11; second.Gold = 28;
            int otherDeckCount = second.Deck.Cards.Count;
            var deckCounts = players.ToDictionary(p => p, p => p.Deck.Cards.Count);
            foreach (var player in players) player.Creature.SetCurrentHpInternal(7);
            var models = new List<EventModel>();
            foreach (var player in players) models.Add(await Begin(player));
            Check(models.All(m => m.IsShared && m.CurrentOptions.Count == 3), "Shared event must have exactly three choices.");
            string branch = floor == 37 ? ".GOLD" : ".HEAL";
            var choices = models.Select(m => m.CurrentOptions.Single(o => o.TextKey.EndsWith(branch))).ToArray();
            Check(models.All(m => m.CurrentOptions.All(o => !o.TextKey.EndsWith(floor == 37 ? ".HEAL" : ".GOLD"))), "Rich and healthy must be mutually exclusive.");
            await choices[0].Chosen();
            Check(second.Gold == 28 && second.Deck.Cards.Count == otherDeckCount, "The handler must not grant effects to a teammate.");
            for (int i = 1; i < count; i++) await choices[i].Chosen();
            foreach (var choice in choices) await choice.Chosen();
            foreach (var player in players)
            {
                Check(player.Deck.Cards.Count == deckCounts[player] + 2, "One shared choice must add exactly two curses per owner.");
                if (floor == 37)
                    Check(player.Gold == (player == first ? 1010 : 1027) && player.Deck.Cards.Count(c => c is Normality) == 1 && player.Deck.Cards.Count(c => c is Greed) == 1, "Rich grants 999, one Normality and one Greed.");
                else
                    Check(player.Creature.CurrentHp == player.Creature.MaxHp && player.Deck.Cards.Count(c => c is Enthralled) == 1 && player.Deck.Cards.Count(c => c is Regret) == 1, "Healthy fully heals and grants Enthralled and Regret.");
                var restored = Player.FromSerializable(player.ToSerializable());
                Check(restored.Gold == player.Gold && restored.Creature.CurrentHp == player.Creature.CurrentHp && restored.Deck.Cards.Select(c => c.Id).SequenceEqual(player.Deck.Cards.Select(c => c.Id)),
                    "Native completed-inventory restoration must retain effects without applying them twice.");
            }
            Check(models.All(m => m.IsFinished), "Noncombat choices must finish.");
        }
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

    private static async Task Awakening()
    {
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([player, other], acts: [], seed: "BLOOM_AWAKE");
        RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
        try
        {
            using (HeadlessCombatInProgress.EnterEvent())
            {
                var curse = run.CreateCard<Regret>(player);
                await CardPileCmd.Add(curse, PileType.Deck);
                var upgradeable = player.Deck.Cards.Where(c => c.IsUpgradable).ToArray();
                var model = await Begin(player);
                var option = model.CurrentOptions.Single(o => o.TextKey.EndsWith(".UPGRADE"));
                await option.Chosen(); await option.Chosen();
                Check(upgradeable.Length > 0 && upgradeable.All(c => c.IsUpgraded) && !curse.IsUpgraded, "Upgrade all and only legal deck instances.");
                Check(player.Relics.Count(r => r.Id.Entry == "ALL_MATTERS_MARK_OF_THE_BLOOM") == 1 && other.Relics.All(r => r.Id.Entry != "ALL_MATTERS_MARK_OF_THE_BLOOM"), "Obtain exactly one owner mark.");
                foreach (var member in run.Players) member.Creature.SetCurrentHpInternal(10);
                await CreatureCmd.Heal(player.Creature, 20);
                await CreatureCmd.Heal(other.Creature, 20);
                Check(player.Creature.CurrentHp == 10 && other.Creature.CurrentHp == 30, "The run healing hook must isolate the marked owner.");
                await HealRestSiteOption.ExecuteRestSiteHeal(player, false);
                Check(player.Creature.CurrentHp == 10, "Rest healing must also be prevented.");
                player.Gold = 100;
                var cleric = ModelDb.AllEvents.Single(e => e.Id.Entry == "ALL_MATTERS_EVENT_CLERIC").ToMutable();
#if STS2_PUBLIC
                await cleric.BeginEvent(player, false);
#else
                await cleric.BeginEvent(player, null, false);
#endif
                await cleric.CurrentOptions[0].Chosen();
                Check(player.Creature.CurrentHp == 10 && player.Gold == 65, "Event healing pays its normal cost but must not bypass the mark.");
                var restored = Player.FromSerializable(player.ToSerializable());
                Check(restored.Relics.Count(r => r.Id.Entry == "ALL_MATTERS_MARK_OF_THE_BLOOM") == 1 && restored.Deck.Cards.Count(c => c.IsUpgraded) == upgradeable.Length,
                    "Native inventory save must retain the mark and upgrades without acquiring them twice.");
            }
            var combat = new CombatState(runState: run);
            foreach (var member in run.Players) { combat.AddPlayer(member); member.ResetCombatState(); }
            await CreatureCmd.Heal(player.Creature, 7);
            await CreatureCmd.Heal(other.Creature, 7);
            Check(player.Creature.CurrentHp == 10 && other.Creature.CurrentHp == 37, "Combat-associated healing must preserve owner isolation.");
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static async Task FightRewards()
    {
        foreach (ulong localId in new ulong[] { 1, 2 })
        {
            var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
            var run = RunState.CreateForTest([first, second], acts: [], seed: "BLOOM_FIGHT");
            run.CurrentActIndex = 2;
            run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, Canonical.Id);
            LocalContext.NetId = localId;
            RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
            HeadlessBanditRoomEntrance.Active = true;
            HeadlessBanditRoomEntrance.Rooms.Clear();
            try
            {
                var a = Canonical.ToMutable(); var b = Canonical.ToMutable();
#if STS2_PUBLIC
                await a.BeginEvent(first, false); await b.BeginEvent(second, false);
#else
                var synchronizer = new EventCombatSynchronizer(run, run);
                await a.BeginEvent(first, synchronizer, false); await b.BeginEvent(second, synchronizer, false);
                synchronizer.InitializeForEvent(localId == 1 ? a : b);
#endif
                await a.CurrentOptions[0].Chosen(); await b.CurrentOptions[0].Chosen();
                Check(HeadlessBanditRoomEntrance.Rooms.Count == 1, "Shared selection must enter exactly one room.");
                var room = HeadlessBanditRoomEntrance.Rooms.Single();
                Check(room.RoomType != RoomType.Boss && room.ParentEventId == Canonical.Id && !room.ShouldResumeParentEventAfterCombat && room.ShouldCreateCombat,
                    "A portrait event must create one terminal event combat, without a Boss room or second fight.");
                room.Encounter!.GenerateMonstersWithSlots(run);
                Check(room.Encounter.MonstersWithSlots.Count == 2 && room.Encounter.MonstersWithSlots.All(m => m.Item1.GetType().Name == "Lagavulin")
                    && !ReferenceEquals(room.Encounter.MonstersWithSlots[0].Item1, room.Encounter.MonstersWithSlots[1].Item1), "Create two distinct one-era Lagavulins.");
                foreach (var player in run.Players)
                {
                    var rewards = new RewardsSet(player).WithRewardsFromRoom(room);
                    Check(rewards.Rewards.Count == 3 && rewards.Rewards.OfType<GoldReward>().Count() == 1 && rewards.Rewards.OfType<RelicReward>().Count() == 1 && rewards.Rewards.OfType<CardReward>().Count() == 1,
                        "Only the three specified rewards may be generated, without default drops.");
                    await rewards.GenerateWithoutOffering();
                    var gold = rewards.Rewards.OfType<GoldReward>().Single();
                    var relic = rewards.Rewards.OfType<RelicReward>().Single();
                    var cards = rewards.Rewards.OfType<CardReward>().Single();
                    var pool = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint).Select(c => c.Id).ToHashSet();
                    Check(gold.Amount == 100 && relic.Relic!.Rarity == RelicRarity.Rare, "Each participant gets 100 gold and a legal random rare relic.");
                    Check(cards.CanSkip && cards.Cards.Count() == 3 && cards.Cards.All(c => c.Rarity == CardRarity.Rare && pool.Contains(c.Id) && !c.IsUpgraded), "Cards use the native unlocked class boss reward rules and remain skippable.");
                    using (HeadlessCombatInProgress.EnterEvent())
                    {
                        int before = player.Gold;
                        await gold.SelectUnsynchronized(); await relic.SelectUnsynchronized();
                        Check(player.Gold == before + 100 && player.Relics.Any(r => r.Id == relic.Relic!.Id), "Native reward selection must pay the owning participant.");
                        LocalContext.NetId = player.NetId;
                        int beforeCards = player.Deck.Cards.Count;
                        var selector = new ChooseReward(player == second);
                        using (CardSelectCmd.UseSelector(selector)) await cards.SelectUnsynchronized();
                        Check(player.Deck.Cards.Count == beforeCards + (player == second ? 0 : 1), "Native card reward must support both choosing and skipping.");
                        LocalContext.NetId = localId;
                    }
                }
                room.MarkPreFinished();
                var restored = CombatRoom.FromSerializable(room.ToSerializable(), run);
                Check(restored.ParentEventId == Canonical.Id && !restored.ShouldResumeParentEventAfterCombat && restored.ExtraRewards.Count == 0,
                    "Native legal room save must round-trip without serializing a flagged CardReward.");
                var restoredRewards = new RewardsSet(first).WithRewardsFromRoom(restored);
                Check(restoredRewards.Rewards.Count == 3 && restoredRewards.Rewards.OfType<CardReward>().Single().CanSkip, "Restored room retains the specified reward policy.");
            }
            finally
            {
                HeadlessBanditRoomEntrance.Active = false;
                LocalContext.NetId = 1;
                RunManager.Instance.CleanUp(false);
            }
        }
    }

    private static async Task SleepingLagavulins()
    {
        foreach (int count in new[] { 1, 2 })
        {
            var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
            var players = count == 1 ? new[] { first } : new[] { first, second };
            var run = RunState.CreateForTest(players, acts: [], seed: "BLOOM_SLEEP");
            run.CurrentActIndex = 2;
            var encounter = ModelDb.Encounter<MindBloomEncounter>().ToMutable();
            encounter.GenerateMonstersWithSlots(run);
            var combat = new CombatState(encounter, run) { RoundNumber = 1 };
            foreach (var player in players) { combat.AddPlayer(player); player.ResetCombatState(); }
            foreach (var (monster, slot) in encounter.MonstersWithSlots)
            {
                var creature = combat.CreateCreature(monster, CombatSide.Enemy, slot);
                combat.AddCreature(creature);
                monster.SetUpForCombat();
                await monster.AfterAddedToRoom();
                monster.RollMove(combat.PlayerCreatures);
                Check(monster.NextMove.Id == "SLEEP" && creature.GetPowerAmount<AsleepLagavulinPower>() == 3, "Both must start asleep for three turns.");
                Check(monster.MinInitialHp == 109 && monster.MaxInitialHp == 111, "Base normal HP is inherited from Balance.");
                var allowedHp = Enumerable.Range(109, 3).Select(hp => Creature.ScaleHpForMultiplayer(hp, encounter, count, 2));
                Check(count == 1 ? creature.MaxHp is >= 109 and <= 111 : allowedHp.Any(hp => Math.Abs(hp - creature.MaxHp) < 1), "Use exactly native multiplayer scaling, not a second custom multiplier.");
                await creature.GetPower<AsleepLagavulinPower>()!.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Player, combat.PlayerCreatures, combat);
                Check(creature.Block > 0, "Sleeping Metallicize must grant initial block.");
            }
            var left = combat.Enemies[0]; var right = combat.Enemies[1];
            var context = new ThrowingPlayerChoiceContext();
            await CreatureCmd.Damage(context, left, left.Block, ValueProp.Unpowered, first.Creature);
            Check(left.HasPower<AsleepLagavulinPower>() && right.HasPower<AsleepLagavulinPower>(), "Fully blocked damage must wake neither monster.");
            await CreatureCmd.Damage(context, left, 1, ValueProp.Unpowered, first.Creature);
            Check(((Lagavulin)left.Monster!).IsAwake && !left.HasPower<AsleepLagavulinPower>() && !left.HasPower<MetallicizePower>() && right.GetPowerAmount<AsleepLagavulinPower>() == 3,
                "Unblocked damage must wake only the damaged monster and remove its Metallicize.");
            for (int turn = 1; turn <= 3; turn++)
            {
                await right.Monster!.NextMove.PerformMove(combat.PlayerCreatures);
                right.Monster!.MoveStateMachine!.OnMovePerformed(right.Monster.NextMove);
                var sleep = right.GetPower<AsleepLagavulinPower>()!;
                await sleep.BeforeSideTurnEndVeryEarly(context, CombatSide.Enemy, combat.Enemies);
                if (right.GetPower<MetallicizePower>() is { } metal) await metal.BeforeSideTurnEndEarly(context, CombatSide.Enemy, combat.Enemies);
                await sleep.AfterSideTurnEnd(context, CombatSide.Enemy, combat.Enemies);
                Check(right.GetPowerAmount<AsleepLagavulinPower>() == 3 - turn, "Natural wake must advance only this monster's own sleep countdown.");
                right.Monster.RollMove(combat.PlayerCreatures);
            }
            Check(((Lagavulin)right.Monster!).IsAwake && !right.HasPower<MetallicizePower>(), "Natural wake removes Metallicize on the third enemy turn.");
            Check(right.Monster!.NextMove.Id == "ATTACK", "Natural wake starts attacking, not the starts-awake variant's debuff.");
        }
    }

    private static async Task RewardModifications()
    {
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player], acts: [], seed: "BLOOM_REWARD_HOOKS");
        player.AddRelicInternal(ModelDb.Relic<PrayerWheel>().ToMutable());
        player.AddRelicInternal(ModelDb.Relic<MoltenEgg>().ToMutable());
        player.AddRelicInternal(ModelDb.Relic<FrozenEgg>().ToMutable());
        player.AddRelicInternal(ModelDb.Relic<ToxicEgg>().ToMutable());
        RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
        try
        {
            var room = new CombatRoom(ModelDb.Encounter<MindBloomEncounter>().ToMutable(), run);
            var rewards = new RewardsSet(player).WithRewardsFromRoom(room);
            var bossCards = rewards.Rewards.OfType<CardReward>().Single();
            await rewards.GenerateWithoutOffering();
            Check(rewards.Rewards.OfType<CardReward>().Count() == 2, "Native Prayer Wheel reward-list modification remains active in a monster-type event combat.");
            Check(bossCards.Cards.Count() == 3 && bossCards.Cards.All(c => c.Rarity == CardRarity.Rare), "The original reward must still use boss rarity.");
            Check(rewards.Rewards.OfType<CardReward>().SelectMany(r => r.Cards).All(c => c.IsUpgraded), "Native egg modifications must upgrade every offered attack, skill and power.");
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static async Task AscensionMoves()
    {
        foreach (int ascension in new[] { 0, Math.Max((int)AscensionLevel.ToughEnemies, (int)AscensionLevel.DeadlyEnemies) })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var run = RunState.CreateForTest([player], acts: [], ascensionLevel: ascension, seed: "BLOOM_ASCENSION");
            RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
            try
            {
                var combat = new CombatState(ModelDb.Encounter<MindBloomEncounter>().ToMutable(), run);
                combat.AddPlayer(player); player.ResetCombatState();
                var monster = ModelDb.Monster<Lagavulin>().ToMutable();
                var creature = combat.CreateCreature(monster, CombatSide.Enemy, "lagavulin_left");
                combat.AddCreature(creature); monster.SetUpForCombat();
                Check(monster.MinInitialHp == (ascension == 0 ? 109 : 112) && monster.MaxInitialHp == (ascension == 0 ? 111 : 115), "Use the unchanged Balance HP ascension values.");
                // After sleeping powers have gone, the same state machine starts with attacks.
                for (int attack = 0; attack < 2; attack++)
                {
                    monster.RollMove(combat.PlayerCreatures);
                    Check(monster.NextMove.Id == "ATTACK" && ((SingleAttackIntent)monster.NextMove.Intents.Single()).GetTotalDamage(combat.PlayerCreatures, creature) == (ascension == 0 ? 18 : 20), "Use unchanged attack value and native intent.");
                    await monster.NextMove.PerformMove(combat.PlayerCreatures);
                    monster.MoveStateMachine!.OnMovePerformed(monster.NextMove);
                }
                monster.RollMove(combat.PlayerCreatures);
                Check(monster.NextMove.Id == "DEBUFF", "Two attacks lead to the drain move.");
                await monster.NextMove.PerformMove(combat.PlayerCreatures);
                Check(player.Creature.GetPowerAmount<StrengthPower>() == (ascension == 0 ? -1 : -2) && player.Creature.GetPowerAmount<DexterityPower>() == (ascension == 0 ? -1 : -2), "Drain both strength and dexterity using the inherited ascension amounts.");
            }
            finally { RunManager.Instance.CleanUp(false); }
        }
    }

    private static async Task Presentation()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        Check(Canonical.CreateInitialPortrait().GetWidth() == 3440, "Load the event illustration from the shipped pack.");
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        var monster = ModelDb.Monster<Lagavulin>();
        using (var visuals = monster.CreateVisuals())
        {
            tree.Root.AddChild(visuals);
            var skeleton = visuals.SpineBody?.GetSkeleton()?.GetData();
            Check(skeleton != null && new[] { "Idle_1", "Idle_2", "Coming_out", "Attack", "Debuff", "Hit" }.All(skeleton.HasAnimation), "Native visuals must bind all sleeping, awake and action animations.");
            Check(visuals.HasNode("SleepingBounds") && visuals.HasNode("AwakeBounds"), "Retain both visual bounds containers.");
            tree.Root.RemoveChild(visuals); visuals.Free();
        }
        using (var scene = ModelDb.Encounter<MindBloomEncounter>().CreateScene())
        {
            Check(scene.HasNode("lagavulin_left") && scene.HasNode("lagavulin_right"), "The encounter must provide two distinct creature slots.");
            scene.Free();
        }
        foreach (string path in new[] { "powers/asleep_lagavulin.png", "powers/metallicize.png", "relics/mark_of_the_bloom.png", "relics/big/mark_of_the_bloom.png", "relics/outlines/mark_of_the_bloom.png" })
            Check(ResourceLoader.Load<Texture2D>("res://AllMatters/images/" + path).GetWidth() > 0, "Pack missing " + path);
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                Check(Canonical.Title.GetFormattedText() == (language == "zhs" ? "心灵绽放" : "Mind Bloom"), "Event title must match language.");
                Check(ModelDb.Encounter<MindBloomEncounter>().Title.GetFormattedText() == (language == "zhs" ? "双乐加维林" : "Two Lagavulins"), "Encounter history must have a localized title.");
                foreach (string table in new[] { "events", "monsters", "powers", "relics", "encounters" })
                {
                    var entries = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(Godot.FileAccess.GetFileAsString($"res://AllMatters/localization/{language}/{table}.json"))!;
                    foreach (var entry in entries.Where(e => e.Key.Contains("MIND_BLOOM") || e.Key.Contains("LAGAVULIN") || e.Key.Contains("METALLICIZE") || e.Key.Contains("MARK_OF_THE_BLOOM")))
                        Check(new LocString(table, entry.Key).GetRawText() == entry.Value, "Localized model key not merged: " + entry.Key);
                }
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
    }

    private static async Task OptionPresentation()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        try
        {
            foreach (string language in new[] { "zhs", "eng" })
            foreach (int floor in new[] { 37, 38 })
            {
                LocManager.Instance.SetLanguage(language);
                var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
                var run = RunState.CreateForTest([player], acts: [], seed: "BLOOM_OPTION_PRESENTATION");
                for (int i = 0; i < floor; i++) run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, Canonical.Id);
                var model = await Begin(player);
                var fight = model.CurrentOptions[0]; var upgrade = model.CurrentOptions[1]; var third = model.CurrentOptions[2];
                Check(!fight.HoverTips.Any(), "Fight has no item reward preview.");
                Check(upgrade.HoverTips.Any(t => t.CanonicalModel is AllMatters.Relics.MarkOfTheBloom), "Awakening must preview the actual Mark of the Bloom effect.");
                var curseIds = third.HoverTips.OfType<CardHoverTip>().Select(t => t.Card.Id).ToArray();
                ModelId[] expected = floor == 37
                    ? [ModelDb.Card<Normality>().Id, ModelDb.Card<Greed>().Id]
                    : [ModelDb.Card<Enthralled>().Id, ModelDb.Card<Regret>().Id];
                Check(curseIds.SequenceEqual(expected), "Preview both actual curse cards, including the native Enthralled model.");
                if (language == "zhs")
                {
                    Check(fight.Description.GetRawText() == "与两只[red]乐加维林[/red]战斗，胜利获得[gold]丰厚的奖励[/gold]。", "Fight copy and red/gold emphasis must match the request.");
                    Check(upgrade.Description.GetRawText() == "升级你的所有手牌，获得[red]绽放印记[/red]。", "Awakening copy is presentation only; deck upgrades remain covered by behavior tests.");
                    Check(third.Description.GetRawText() == (floor == 37
                        ? "获得999金币。将[red]凡庸[/red]和[red]贪婪[/red]加入你的牌组。"
                        : "[green]回复你的所有生命[/green]，将[red]执迷[/red]与[red]悔恨[/red]加入你的牌组"), "Curse names and full healing must use the requested copy and colors.");
                }
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
    }

    private sealed class ChooseReward(bool skip) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect) => throw new InvalidOperationException();
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => new() { card = skip ? null : options[0].Card };
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
