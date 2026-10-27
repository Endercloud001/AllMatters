using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync;
using MegaCrit.Sts2.Core.Unlocks;
using System.Reflection;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

internal static class WhisperingHollowTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Whispering Hollow burn choice fixes shared page at inclusive half health", SharedPage),
        ("Whispering Hollow burn enters ordinary monster combat with elite loot and returns", BurnFight),
        ("Whispering Hollow Mass locks Parasite then rerolls per unblocked attack and repeats fallback shield", Mass),
        ("Whispering Hollow real synchronizer waits for unanimous whole-page choices", Unanimity),
        ("Whispering Hollow Dead Branch generates one legal owner card for each real exhaustion", Branch),
        ("Whispering Hollow packed bilingual relic, Spore portrait and complete Mass animations load", Presentation),
        ("Whispering Hollow victory cleans all permanent Spores even when personal loot is forfeited", PersonalRewards),
        ("Whispering Hollow shared threshold survives save load and resets for a later room", SharedSave),
        ("Whispering Hollow Mass scales HP normally and keeps fixed all-player moves at high ascension", MultiplayerMass),
        ("Whispering Hollow Dead Branch ignores ally exhaustion and uses native full-hand overflow", BranchOwnership),
    ];

    private static async Task SharedPage()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (int hp in new[] { 40, 39 })
        {
            var p = NativeEventTests.Player("HOLLOW_BURN");
            p.Creature.SetMaxHpInternal(80);
            p.Creature.SetCurrentHpInternal(hp);
            var e = await NativeEventTests.Begin<WhisperingHollow>(p);
            NativeEventTests.Check(e.IsShared == (hp == 40), "Exactly 50% qualifies for an entirely shared page.");
            NativeEventTests.Check(e.CurrentOptions.Count == (hp == 40 ? 3 : 2), "Only qualifying pages offer Burn.");
            p.Creature.SetCurrentHpInternal(hp == 40 ? 1 : 80);
            NativeEventTests.Check(e.IsShared == (hp == 40), "HP changes do not switch the initialized sharing policy.");
            var rebuilt = await NativeEventTests.Begin<WhisperingHollow>(p);
            NativeEventTests.Check(rebuilt.IsShared == (hp == 40), "Rebuilding the same room keeps its original sharing policy.");
        }
    }

    private static async Task MultiplayerMass()
    {
        var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([first, second], acts: [], ascensionLevel: 10, seed: "HOLLOW_MULTIPLAYER");
        var encounter = ModelDb.Encounter<AllMatters.Encounters.WhisperingHollowEncounter>().ToMutable();
        var combat = new CombatState(encounter, run);
        foreach (var p in run.Players) { combat.AddPlayer(p); p.ResetCombatState(); }
        encounter.GenerateMonstersWithSlots(run);
        var mass = encounter.MonstersWithSlots.Single().Item1;
        var creature = combat.CreateCreature(mass, CombatSide.Enemy, "mass");
        combat.AddCreature(creature); mass.SetUpForCombat();
        NativeEventTests.Check(mass.MinInitialHp == 75 && mass.MaxInitialHp == 75
            && creature.MaxHp == Creature.ScaleHpForMultiplayer(75, encounter, 2, run.CurrentActIndex)
            && encounter.RoomType == RoomType.Monster, "75 base HP gets native multiplayer scaling, ordinary identity and no ascension HP bonus.");
        var insect = await RelicCmd.Obtain<AllMatters.Relics.PreservedInsect>(first);
        await insect.BeforeCombatStart();
        NativeEventTests.Check(!creature.HasPower<ShrinkPower>(), "Elite-only relics do not treat the Mass as elite.");
        await mass.AfterAddedToRoom(); mass.RollMove(combat.PlayerCreatures);
        await mass.NextMove.PerformMove(combat.PlayerCreatures);
        foreach (var p in run.Players)
            NativeEventTests.Check(p.PlayerCombatState!.DrawPile.Cards.OfType<SporeMind>().Count() == 1, "Parasite puts one Spore in each living player's Draw.");
        var flail = (MoveState)mass.MoveStateMachine!.States["FLAIL"];
        var before = run.Players.Select(p => p.Creature.CurrentHp).ToArray();
        await flail.PerformMove(combat.PlayerCreatures);
        for (int i = 0; i < run.Players.Count; i++) NativeEventTests.Check(run.Players[i].Creature.CurrentHp == before[i] - 15, "Every player takes 5x3, including at high ascension.");
        await ((MoveState)mass.MoveStateMachine.States["WITHER"]).PerformMove(combat.PlayerCreatures);
        foreach (var p in run.Players)
            NativeEventTests.Check(p.Creature.GetPowerAmount<WeakPower>() == 1 && p.Creature.GetPowerAmount<VulnerablePower>() == 1, "Debuff layers do not scale with players or ascension.");
    }

    private static async Task SharedSave()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([first, second], acts: [], seed: "HOLLOW_SAVE");
        first.Creature.SetMaxHpInternal(80); first.Creature.SetCurrentHpInternal(40);
        second.Creature.SetMaxHpInternal(81); second.Creature.SetCurrentHpInternal(40);
        RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
        try
        {
            var e = await NativeEventTests.Begin<WhisperingHollow>(first);
            NativeEventTests.Check(!e.IsShared && e.CurrentOptions.Count == 2, "One ally below half keeps all original personal choices.");
            second.Creature.SetCurrentHpInternal(41);
            var restored = RunState.FromSerializable(RunManager.Instance.ToSave(null));
            var sameRoom = await NativeEventTests.Begin<WhisperingHollow>(restored.Players[0]);
            NativeEventTests.Check(!sameRoom.IsShared, "Healing before a snapshot cannot change the saved page policy.");
            restored.AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.Unknown, RoomType.Event, ModelDb.Event<WhisperingHollow>().Id);
            var laterRoom = await NativeEventTests.Begin<WhisperingHollow>(restored.Players[0]);
            NativeEventTests.Check(laterRoom.IsShared && laterRoom.CurrentOptions.Count == 3, "A later room recomputes the inclusive team threshold.");
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static async Task PersonalRewards()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var ally = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([p, ally], acts: [], seed: "HOLLOW_REWARDS");
        RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
        try
        {
            var room = new CombatRoom(ModelDb.Encounter<AllMatters.Encounters.WhisperingHollowEncounter>().ToMutable(), run)
            { ParentEventId = ModelDb.Event<WhisperingHollow>().Id, ShouldResumeParentEventAfterCombat = true };
            foreach (var player in run.Players)
            {
                await CardPileCmd.Add(run.CreateCard<SporeMind>(player), PileType.Deck);
                await CardPileCmd.Add(run.CreateCard<SporeMind>(player), PileType.Deck);
            }
            AllMatters.Encounters.PersonalCombatRewards.Forfeit(p);
            await RelicCmd.Obtain<MegaCrit.Sts2.Core.Models.Relics.PrayerWheel>(ally);
            var blocked = await RewardsCmd.GenerateForRoomEnd(p, room);
            NativeEventTests.Check(blocked.Rewards.Count == 0 && !p.Deck.Cards.OfType<SporeMind>().Any(), "Forfeiture blocks Branch and all loot, but not permanent Spore cleanup.");
            var eligible = await RewardsCmd.GenerateForRoomEnd(ally, room);
            NativeEventTests.Check(eligible.Rewards.OfType<RelicReward>().Count() == 2
                && eligible.Rewards.OfType<RelicReward>().Count(r => r.Relic is AllMatters.Relics.DeadBranch) == 1
                && eligible.Rewards.OfType<GoldReward>().Count() == 1 && eligible.Rewards.OfType<CardReward>().Count() == 1
                && !ally.Deck.Cards.OfType<SporeMind>().Any(), "The ally gets full elite loot and a Branch after clearing all Spores; Prayer Wheel must not add ordinary-room card rewards.");
            room.MarkPreFinished();
            var restored = CombatRoom.FromSerializable(room.ToSerializable(), run);
            NativeEventTests.Check(restored.RoomType == RoomType.Monster && restored.ShouldResumeParentEventAfterCombat,
                "Room restoration retains ordinary identity and return policy.");
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static async Task Presentation()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas");
        var relic = ModelDb.Relic<AllMatters.Relics.DeadBranch>();
        var mass = ModelDb.Monster<AllMatters.Monsters.WrithingMass>();
        NativeEventTests.Check(relic.Icon != null && relic.IconOutline != null && relic.BigIcon != null, "All three Branch textures load.");
        NativeEventTests.Check(ModelDb.Card<SporeMind>().Portrait != null, "Reuse the native Spore Mind portrait.");
        using (var scene = ModelDb.Encounter<AllMatters.Encounters.WhisperingHollowEncounter>().CreateScene())
        {
            NativeEventTests.Check(scene.HasNode("mass"), "Encounter scene contains its monster slot.");
            scene.Free();
        }
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
        using var visuals = mass.CreateVisuals();
        tree.Root.AddChild(visuals);
        try
        {
            var skeleton = visuals.SpineBody!.GetSkeleton()!.GetData()!;
            NativeEventTests.Check(new[] { "Idle", "Attack", "Hit" }.All(skeleton.HasAnimation), "Full converted Mass skeleton contains idle, attack and hit animations.");
            var animator = mass.GenerateAnimator(visuals.SpineBody);
            foreach (string action in new[] { "Attack", "Hit" }) animator.SetTrigger(action);
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                NativeEventTests.Check(relic.DynamicDescription.GetFormattedText().Contains(language == "zhs" ? "消耗" : "Exhaust"), "Localized Branch description matches exhaustion.");
                var p = NativeEventTests.Player();
                var e = await NativeEventTests.Begin<WhisperingHollow>(p);
                NativeEventTests.Check(e.CurrentOptions[2].Description.GetRawText().Contains("[red]")
                    && e.CurrentOptions[2].Description.GetRawText().Contains("[purple]")
                    && e.CurrentOptions[2].HoverTips.Any(), "Burn has red battle, purple Branch and relic hover preview.");
                string ending = new LocString("events", "WHISPERING_HOLLOW.pages.AM_BURN.description").GetFormattedText();
                NativeEventTests.Check(ending.Contains("[red]") && ending.Contains("[gold]"), "Ending keeps the requested red creature and gold treasure.");
                NativeEventTests.Check(ModelDb.Power<AllMatters.Powers.ReactivePower>().Icon != null, "Reactive's icon loads.");
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); tree.Root.RemoveChild(visuals); visuals.Free(); }
    }

    private static async Task Branch()
    {
        var (p, combat) = NativeEventTests.Combat();
        await RelicCmd.Obtain<AllMatters.Relics.DeadBranch>(p);
        var context = new ThrowingPlayerChoiceContext();
        for (int i = 0; i < 3; i++)
        {
            var card = combat.CreateCard<DefendIronclad>(p);
            await CardPileCmd.Add(card, PileType.Hand);
            int hand = p.PlayerCombatState!.Hand.Cards.Count;
            await CardCmd.Exhaust(context, card, causedByEthereal: i == 2, skipVisuals: true);
            NativeEventTests.Check(p.PlayerCombatState.Hand.Cards.Count == hand && p.PlayerCombatState.ExhaustPile.Cards.Contains(card), "Each exhaustion replaces the exhausted card with one generated card.");
            var generated = p.PlayerCombatState.Hand.Cards.Last();
            NativeEventTests.Check(generated.Owner == p && generated.DeckVersion == null && !generated.IsUpgraded
                && generated.Pool.Id == p.Character.CardPool.Id && generated.CanBeGeneratedInCombat, "Generated card is legal, mutable and belongs to its owner character pool.");
        }
    }

    private static async Task BranchOwnership()
    {
        var p = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var ally = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([p, ally], acts: [], seed: "BRANCH_OWNER");
        var combat = HeadlessTestSetup.Combat(run);
        await RelicCmd.Obtain<AllMatters.Relics.DeadBranch>(p);
        var context = new ThrowingPlayerChoiceContext();
        var otherCard = combat.CreateCard<DefendSilent>(ally);
        await CardPileCmd.Add(otherCard, PileType.Hand);
        var rng = HeadlessTestSetup.RngPosition(run.Rng.CombatCardGeneration);
        await CardCmd.Exhaust(context, otherCard, skipVisuals: true);
        NativeEventTests.Check(p.PlayerCombatState!.Hand.Cards.Count == 0 && ally.PlayerCombatState!.Hand.Cards.Count == 0
            && HeadlessTestSetup.RngPosition(run.Rng.CombatCardGeneration) == rng, "Ally exhaustion generates nothing and consumes no card-generation RNG.");
        for (int i = 0; i < 10; i++) await CardPileCmd.Add(combat.CreateCard<DefendIronclad>(p), PileType.Hand);
        var ownCard = combat.CreateCard<DefendIronclad>(p);
        await CardPileCmd.Add(ownCard, PileType.Draw);
        await CardCmd.Exhaust(context, ownCard, skipVisuals: true);
        NativeEventTests.Check(p.PlayerCombatState.Hand.Cards.Count == 10 && p.PlayerCombatState.DiscardPile.Cards.Count == 1,
            "Native generation sends the extra card to Discard when Hand is full.");
    }

    private static async Task Unanimity()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([first, second], acts: [], seed: "HOLLOW_VOTES");
        var service = DispatchProxy.Create<INetGameService, EventNetwork>();
        var network = (EventNetwork)service;
        var buffer = new RunLocationTargetedMessageBuffer(service);
#if STS2_PUBLIC
        using var sync = new EventSynchronizer(buffer, service, run, 1, run.Rng.Seed);
#else
        using var sync = new EventSynchronizer(buffer, service, run, run, 1, run.Rng.Seed);
#endif
        sync.BeginEvent(ModelDb.Event<WhisperingHollow>());
        NativeEventTests.Check(sync.IsShared && sync.Events.All(e => e.IsShared && e.CurrentOptions.Count == 3), "The real synchronizer sees the initialized shared page.");
        int gold = first.Gold;
        // Hug and Gold must agree too; mixed votes cannot randomly execute a choice.
        sync.ChooseLocalOption(1);
        network.Receive(new VotedForSharedEventOptionMessage { optionIndex = 0, pageIndex = 0, location = buffer.CurrentLocation }, 2);
        NativeEventTests.Check(sync.Events.All(e => !e.IsFinished) && first.Gold == gold, "Mixed votes do not resolve or pay anything.");
        using (CardSelectCmd.UseSelector(new FirstCards()))
        {
            network.Receive(new VotedForSharedEventOptionMessage { optionIndex = 1, pageIndex = 0, location = buffer.CurrentLocation }, 2);
            await sync.AwaitPendingOptionTasks();
        }
        NativeEventTests.Check(sync.Events.All(e => e.IsFinished), "Matching votes execute the noncombat choice for both players.");
    }

    private sealed class FirstCards : MegaCrit.Sts2.Core.TestSupport.ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
            => Task.FromResult(options.Take(minSelect));
        public MegaCrit.Sts2.Core.TestSupport.CardRewardSelection GetSelectedCardReward(IReadOnlyList<MegaCrit.Sts2.Core.Entities.Cards.CardCreationResult> options,
            IReadOnlyList<MegaCrit.Sts2.Core.Entities.CardRewardAlternatives.CardRewardAlternative> alternatives) => throw new NotSupportedException();
    }

    // Only the transport is substituted; messages go through the native buffer and synchronizer.
    public class EventNetwork : DispatchProxy
    {
        private readonly Dictionary<Type, Delegate> _handlers = new();
        public void Receive<T>(T message, ulong sender) => _handlers[typeof(T)].DynamicInvoke(message, sender);
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "RegisterMessageHandler") _handlers[method.GetGenericArguments()[0]] = (Delegate)args![0]!;
            if (method.Name == "UnregisterMessageHandler") _handlers.Remove(method.GetGenericArguments()[0]);
            if (method.Name == "get_Type") return new NetSingleplayerGameService().Type;
            if (method.Name == "get_NetId") return 1UL;
            return method.ReturnType != typeof(void) && method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
        }
    }

    private static async Task Mass()
    {
        var (p, combat) = NativeEventTests.Combat();
        var monster = (AllMatters.Monsters.WrithingMass)ModelDb.Monster<AllMatters.Monsters.WrithingMass>().ToMutable();
        var creature = combat.CreateCreature(monster, CombatSide.Enemy, "mass");
        combat.AddCreature(creature); monster.SetUpForCombat();
        await monster.AfterAddedToRoom();
        monster.RollMove(combat.PlayerCreatures);
        var context = new ThrowingPlayerChoiceContext();
        await CreatureCmd.Damage(context, creature, 1, ValueProp.Move, p.Creature);
        NativeEventTests.Check(monster.NextMove.Id == "PARASITE", "Opening Parasite cannot be rerolled by attacks.");
        int deck = p.Deck.Cards.Count;
        await monster.NextMove.PerformMove(combat.PlayerCreatures);
        monster.MoveStateMachine!.OnMovePerformed(monster.NextMove);
        NativeEventTests.Check(p.PlayerCombatState!.DrawPile.Cards.OfType<SporeMind>().Count() == 1 && p.Deck.Cards.Count == deck,
            "Parasite creates a combat-only Spore Mind in Draw.");
        for (int i = 0; i < 2; i++)
        {
            monster.RollMove(combat.PlayerCreatures);
            NativeEventTests.Check(monster.NextMove.Id == "SHIELD", "Without attacks the shield fallback can repeat.");
            int block = creature.Block;
            await monster.NextMove.PerformMove(combat.PlayerCreatures);
            monster.MoveStateMachine.OnMovePerformed(monster.NextMove);
            NativeEventTests.Check(creature.Block == block + 16, "Shield grants exactly 16 block.");
        }
#if STS2_PUBLIC
        await CreatureCmd.LoseBlock(creature, creature.Block);
#else
        await CreatureCmd.LoseBlock(context, creature, creature.Block, null);
#endif
        monster.RollMove(combat.PlayerCreatures);
        creature.SetMaxHpInternal(999); creature.SetCurrentHpInternal(999);
        var observed = new HashSet<string>();
        for (int i = 0; i < 20; i++)
        {
            await CreatureCmd.Damage(context, creature, 1, ValueProp.Move, p.Creature);
            NativeEventTests.Check(monster.NextMove.Id != "SHIELD" && monster.NextMove.Id != "PARASITE", "Rerolls exclude the last actual move, not merely the displayed intent.");
            observed.Add(monster.NextMove.Id);
        }
        NativeEventTests.Check(observed.SetEquals(["WITHER", "FLAIL"]), "Both remaining equal-probability actions are reachable.");
        var rng = monster.RunRng.MonsterAi;
#if STS2_PUBLIC
        var expected = new Rng(rng.Seed, rng.Counter);
#else
        var expected = new Rng(rng.ToSerializable());
#endif
        for (int i = 0; i < 3; i++) expected.NextInt(2);
        var peck = combat.CreateCard<Peck>(p);
        await peck.OnPlayWrapper(context, creature, true, default, skipCardPileVisuals: true);
        NativeEventTests.Check(HeadlessTestSetup.RngPosition(rng) == HeadlessTestSetup.RngPosition(expected), "One real three-hit card rerolls independently on all three unblocked hits.");
        var before = monster.NextMove;
        var rngBefore = HeadlessTestSetup.RngPosition(rng);
        await CreatureCmd.Damage(context, creature, 1, ValueProp.Unpowered, p.Creature);
        await CreatureCmd.GainBlock(creature, 1, ValueProp.Unpowered, null);
        await CreatureCmd.Damage(context, creature, 1, ValueProp.Move, p.Creature);
        NativeEventTests.Check(monster.NextMove == before && HeadlessTestSetup.RngPosition(rng) == rngBefore, "Non-attack damage and fully blocked attacks keep the intent and RNG.");
        int hp = p.Creature.CurrentHp;
        await monster.MoveStateMachine.States.Values.OfType<MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState>().Single(m => m.Id == "FLAIL").PerformMove(combat.PlayerCreatures);
        NativeEventTests.Check(p.Creature.CurrentHp == hp - 15, "Flail deals 5 damage three times.");
        await monster.MoveStateMachine.States.Values.OfType<MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState>().Single(m => m.Id == "WITHER").PerformMove(combat.PlayerCreatures);
        NativeEventTests.Check(p.Creature.GetPowerAmount<WeakPower>() == 1 && p.Creature.GetPowerAmount<VulnerablePower>() == 1, "Wither applies one Weak and Vulnerable.");
    }

    private static async Task BurnFight()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player("HOLLOW_FIGHT");
        var run = (RunState)p.RunState;
        var previous = LocalContext.NetId;
        LocalContext.NetId = p.NetId;
        RunManager.Instance.SetUpTest(run, new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
        HeadlessBanditRoomEntrance.Active = true;
        HeadlessBanditRoomEntrance.Rooms.Clear();
        try
        {
            var e = ModelDb.Event<WhisperingHollow>().ToMutable();
#if STS2_PUBLIC
            await e.BeginEvent(p, false);
#else
            var sync = new MegaCrit.Sts2.Core.Multiplayer.Game.EventCombatSynchronizer(run, run);
            await e.BeginEvent(p, sync, false);
            sync.InitializeForEvent(e);
#endif
            await e.CurrentOptions[2].Chosen();
            NativeEventTests.Check(HeadlessBanditRoomEntrance.Rooms.Count == 1, "Burn enters one real event combat.");
            var room = HeadlessBanditRoomEntrance.Rooms.Single();
            NativeEventTests.Check(room.RoomType == RoomType.Monster && room.ShouldResumeParentEventAfterCombat, "The Mass keeps ordinary identity and returns to the event.");
            var rewards = new RewardsSet(p).WithRewardsFromRoom(room);
            NativeEventTests.Check(rewards.Rewards.OfType<RelicReward>().Count() == 2 && rewards.Rewards.OfType<CardReward>().Any(), "Elite relic/card rewards plus guaranteed Branch.");
            await e.Resume(room);
            NativeEventTests.Check(e.IsFinished, "The victory narrative ends with Continue.");
        }
        finally
        {
            HeadlessBanditRoomEntrance.Active = false;
            RunManager.Instance.CleanUp(false);
            LocalContext.NetId = previous;
        }
    }
}
