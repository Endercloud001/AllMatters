using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Random;
using AllMatters.Encounters;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Multiplayer.Replay;
using MegaCrit.Sts2.Core.Saves;
using HarmonyLib;

internal static class ColosseumTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Colosseum first fight contains Living Fog and Damp Cultist, returns to intermission, and fleeing grants nothing", FirstFight),
        ("Colosseum resumes the real event synchronizer for both local owners and all three full native elite encounters", SecondFight),
        ("Colosseum native victory history restores intermission and deterministic second combat with native reward hooks", RestoreIntermission),
        ("Colosseum bilingual event, portrait and first encounter slots load from PCK", Presentation),
    ];

    private static EventModel Canonical => ModelDb.AllEvents.SingleOrDefault(e => e.Id.Entry == "ALL_MATTERS_EVENT_COLOSSEUM")
        ?? throw new Exception("Colosseum must be registered.");

    private static async Task FirstFight()
    {
        LocalContext.NetId = 1;
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player], acts: [], seed: "ARENA_FIRST");
        foreach (int act in new[] { 0, 1, 2 })
        {
            run.CurrentActIndex = act;
            Check(Canonical.IsAllowed(run) == (act == 2), "Only Act 3 is admitted.");
        }
        run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, Canonical.Id);
        RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
        HeadlessBanditRoomEntrance.Active = true;
        HeadlessBanditRoomEntrance.Rooms.Clear();
        try
        {
            var model = await Begin(player);
            Check(model.IsShared && model.CurrentOptions.Count == 1 && model.CurrentOptions[0].TextKey.EndsWith(".CONTINUE"), "The opening offers only Continue.");
            var proceed = model.CurrentOptions.Single();
            int openingGold = player.Gold, openingHp = player.Creature.CurrentHp;
            await proceed.Chosen(); await proceed.Chosen();
            Check(HeadlessBanditRoomEntrance.Rooms.Count == 0 && player.Gold == openingGold && player.Creature.CurrentHp == openingHp && model.CurrentOptions.Single().TextKey.Contains(".AWAKE.options.FIGHT"),
                "Continue only reveals the waking page and compulsory fight without starting combat or changing resources.");
            var fight = model.CurrentOptions.Single();
            await fight.Chosen(); await fight.Chosen();
            await proceed.Chosen();
            Check(HeadlessBanditRoomEntrance.Rooms.Count == 1, "The first fight must start only once.");
            var room = HeadlessBanditRoomEntrance.Rooms.Single();
            Check(room.ShouldResumeParentEventAfterCombat && room.ParentEventId == Canonical.Id && !room.Encounter!.ShouldGiveRewards,
                "First victory returns to the parent without a reward screen.");
            room.Encounter!.GenerateMonstersWithSlots(run);
            Check(room.Encounter.MonstersWithSlots.Select(m => m.Item1.GetType().Name).Order().SequenceEqual(new[] { "DampCultist", "LivingFog" }), "Use exactly the two specified native monsters.");
            Check(new RewardsSet(player).WithRewardsFromRoom(room).Rewards.Count == 0, "No gold, card, relic or potion drops.");
            await model.Resume(room);
            Check(model.CurrentOptions.Count == 2, "Return to the two intermission choices.");
            int gold = player.Gold, relics = player.Relics.Count, cards = player.Deck.Cards.Count;
            var flee = model.CurrentOptions.Single(o => o.TextKey.EndsWith(".FLEE"));
            await flee.Chosen(); await flee.Chosen(); await fight.Chosen();
            Check(model.IsFinished && player.Gold == gold && player.Relics.Count == relics && player.Deck.Cards.Count == cards && HeadlessBanditRoomEntrance.Rooms.Count == 1,
                "Fleeing ends the event without rewards or another fight, even on stale selections.");
        }
        finally
        {
            HeadlessBanditRoomEntrance.Active = false;
            RunManager.Instance.CombatReplayWriter.StopRecording();
            RunManager.Instance.CleanUp(false);
        }
    }

    private static async Task SecondFight()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        Type[] expected = [typeof(DecimillipedeElite), typeof(EntomancerElite), typeof(InfestedPrismsElite)];
        foreach (ulong localId in new ulong[] { 1, 2 })
        for (int branch = 0; branch < 3; branch++)
        {
            var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
            var run = RunState.CreateForTest([first, second], acts: [], seed: "ARENA_SECOND");
            run.CurrentActIndex = 2;
            run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, Canonical.Id);
            LocalContext.NetId = localId;
            RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
            var manager = RunManager.Instance;
            manager.CombatReplayWriter.IsEnabled = false;
            HeadlessBanditRoomEntrance.Active = true;
            HeadlessBanditRoomEntrance.Rooms.Clear();
            try
            {
                var sync = manager.EventSynchronizer;
                sync.BeginEvent(Canonical, false);
                await sync.AwaitPendingOptionTasks();
#if !STS2_PUBLIC
                sync.GenerateInternalCombatStateIfNecessary(sync.GetLocalEvent());
#endif
                var a = sync.GetEventForPlayer(first); var b = sync.GetEventForPlayer(second);
                foreach (var model in new[] { a, b }) await model.CurrentOptions.Single().Chosen();
                Check(HeadlessBanditRoomEntrance.Rooms.Count == 0, "Both owners must reach the waking page before any combat.");
                var initialChoices = new[] { a.CurrentOptions[0], b.CurrentOptions[0] };
                foreach (var option in initialChoices) await option.Chosen();
                Check(HeadlessBanditRoomEntrance.Rooms.Count == 1, "All participant handlers enter one first room.");
                var opening = HeadlessBanditRoomEntrance.Rooms.Single();
                opening.MarkPreFinished();
                var restoredOpening = CombatRoom.FromSerializable(opening.ToSerializable(), run);
                Check(restoredOpening.ShouldResumeParentEventAfterCombat && !restoredOpening.Encounter!.ShouldGiveRewards && restoredOpening.ExtraRewards.Count == 0,
                    "The native completed-first-room save retains its zero-reward return policy.");
                sync.ResumeEvents(restoredOpening);
                Check(a.CurrentOptions.Count == 2 && b.CurrentOptions.Count == 2, "Each owner returns to intermission.");
                // Choose inputs that exercise each discrete native RNG branch;
                // this is branch coverage, not a frequency estimate of fairness.
                while (PeekBranch(a.Rng, run.TotalFloor) != branch)
                    foreach (var model in new[] { a, b }) model.Rng.NextInt(1);
                var choices = new[] { a.CurrentOptions[0], b.CurrentOptions[0] };
                ColosseumReplayCapture.InitialState = null;
                manager.CombatReplayWriter.IsEnabled = true;
                foreach (var option in choices) await option.Chosen();
                Check(manager.CombatReplayWriter.IsRecordingReplay, $"The second room must initialize the stopped native recorder before its actions begin (rooms={HeadlessBanditRoomEntrance.Rooms.Count}, enabled={manager.CombatReplayWriter.IsEnabled}).");
                var replaySave = ColosseumReplayCapture.InitialState ?? throw new Exception("Missing native replay initial state.");
                Check(replaySave.PreFinishedRoom != null && !replaySave.PreFinishedRoom.IsPreFinished && replaySave.PreFinishedRoom.EncounterId == HeadlessBanditRoomEntrance.Rooms.Last().Encounter!.Id,
                    "The replay must restore the selected live second encounter instead of rerolling the map point.");
                var replayRun = RunState.FromSerializable(replaySave);
                var replayRoom = CombatRoom.FromSerializable(replaySave.PreFinishedRoom!, replayRun);
                Check(replayRoom.ParentEventId == Canonical.Id && !replayRoom.IsPreFinished && !replayRoom.ShouldResumeParentEventAfterCombat && new RewardsSet(first).WithRewardsFromRoom(replayRoom).Rewards.Count == 3,
                    "A native replay-restored live combat keeps its terminal event identity and exact reward policy.");
                Check(replaySave.MapPointHistory.Last().Last().Rooms.Last().ModelId == replayRoom.Encounter!.Id && run.CurrentMapPointHistoryEntry!.Rooms.Count == 1,
                    "The replay snapshot includes the upcoming second room without changing live run history.");
                manager.CombatReplayWriter.StopRecording();
                manager.CombatReplayWriter.IsEnabled = false;
                foreach (var option in choices.Concat(initialChoices)) await option.Chosen();
                Check(HeadlessBanditRoomEntrance.Rooms.Count == 2, "Stale callbacks cannot re-enter either fight.");
                var room = HeadlessBanditRoomEntrance.Rooms.Last();
                Check(room.Encounter!.GetType() == expected[branch] && !room.ShouldResumeParentEventAfterCombat && room.ParentEventId == Canonical.Id,
                    "Use exactly the selected native elite as a terminal event combat.");
                room.Encounter.GenerateMonstersWithSlots(run);
                Check(room.Encounter.MonstersWithSlots.Count == (branch == 0 ? 3 : 1), "Keep the full encounter, including all three centipede segments.");
                if (branch == 0)
                {
                    Check(room.Encounter.MonstersWithSlots.Select(m => m.Item1.GetType().Name).SequenceEqual(new[] { "DecimillipedeSegmentFront", "DecimillipedeSegmentMiddle", "DecimillipedeSegmentBack" }), "Keep front, middle and back segments in native order.");
                    using var scene = room.Encounter.CreateScene();
                    Check(new[] { "segment1", "segment2", "segment3" }.All(slot => scene.HasNode(slot)), "The dedicated native centipede scene must load all slots.");
                    scene.Free();
                }
                foreach (var player in run.Players)
                {
                    var rewards = new RewardsSet(player).WithRewardsFromRoom(room);
                    Check(rewards.Rewards.Count == 3 && rewards.Rewards.OfType<GoldReward>().Count() == 1 && rewards.Rewards.OfType<RelicReward>().Count() == 1 && rewards.Rewards.OfType<CardReward>().Count() == 1,
                        "Each participant gets only the specified trio, with no default elite drops.");
                    await rewards.GenerateWithoutOffering();
                    var gold = rewards.Rewards.OfType<GoldReward>().Single();
                    var relic = rewards.Rewards.OfType<RelicReward>().Single();
                    var cards = rewards.Rewards.OfType<CardReward>().Single();
                    var pool = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint).Select(c => c.Id).ToHashSet();
                    Check(gold.Amount == 100 && relic.Relic!.Rarity == RelicRarity.Rare && cards.CanSkip && cards.Cards.Count() == 3 && cards.Cards.All(c => c.Rarity == CardRarity.Rare && pool.Contains(c.Id)),
                        "Native generation must give 100 gold, a rare relic and three skippable unlocked class rares.");
                    int beforeGold = player.Gold, beforeDeck = player.Deck.Cards.Count;
                    LocalContext.NetId = player.NetId;
                    await gold.SelectUnsynchronized(); await relic.SelectUnsynchronized();
                    using (CardSelectCmd.UseSelector(new ChooseReward(player == second))) await cards.SelectUnsynchronized();
                    Check(player.Gold == beforeGold + 100 && player.Relics.Any(r => r.Id == relic.Relic!.Id) && player.Deck.Cards.Count == beforeDeck + (player == second ? 0 : 1),
                        "Native reward selection pays the owner, including choosing versus skipping the card.");
                    var inventory = Player.FromSerializable(player.ToSerializable());
                    Check(inventory.Gold == player.Gold && inventory.Relics.Select(r => r.Id).SequenceEqual(player.Relics.Select(r => r.Id)) && inventory.Deck.Cards.Select(c => c.Id).SequenceEqual(player.Deck.Cards.Select(c => c.Id)),
                        "Completed inventory round-trips without reapplying grants.");
                }
                LocalContext.NetId = localId;
                room.MarkPreFinished();
                var restored = CombatRoom.FromSerializable(room.ToSerializable(), run);
                Check(restored.Encounter!.GetType() == expected[branch] && restored.ParentEventId == Canonical.Id && !restored.ShouldResumeParentEventAfterCombat && restored.ExtraRewards.Count == 0,
                    "Native victory-room restoration keeps the chosen encounter and serializes no flagged card reward.");
                Check(new RewardsSet(first).WithRewardsFromRoom(restored).Rewards.Count == 3, "Restored second victory has the same exact reward policy.");
                // The same native encounter outside this parent event is unaffected.
                var ordinary = new CombatRoom(ModelDb.GetById<EncounterModel>(room.Encounter.Id).ToMutable(), run);
                Check(new RewardsSet(first).WithRewardsFromRoom(ordinary).Rewards.OfType<GoldReward>().Count() == 1, "Keep native map-elite reward generation.");
            }
            finally
            {
                HeadlessBanditRoomEntrance.Active = false;
                LocalContext.NetId = 1;
                manager.CombatReplayWriter.StopRecording();
                manager.CleanUp(false);
            }
        }
    }

    private sealed class ChooseReward(bool skip) : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect) => throw new InvalidOperationException();
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
            => new() { card = skip ? null : options[0].Card };
    }

    private static int PeekBranch(Rng source, int floor)
    {
#if STS2_PUBLIC
        var rng = new Rng(source.Seed, source.Counter);
#else
        var rng = new Rng(source.ToSerializable());
#endif
        for (int i = 0; i < floor; i++) rng.NextInt(1);
        return rng.NextInt(3);
    }

    private static async Task RestoreIntermission()
    {
        LocalContext.NetId = 1;
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player], acts: [], seed: "ARENA_RESTORE");
        run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, Canonical.Id);
        run.CurrentMapPointHistoryEntry!.Rooms.Add(new MapPointRoomHistoryEntry
        {
            ModelId = ModelDb.Encounter<ColosseumFirstEncounter>().Id,
            RoomType = RoomType.Monster,
            TurnsTaken = 3,
        });
        RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
        HeadlessBanditRoomEntrance.Active = true;
        HeadlessBanditRoomEntrance.Rooms.Clear();
        try
        {
            // Native EventRoom stores the event identity; model state is rebuilt
            // from victory history. This is not a full game load or room scheduler.
            var eventRoom = new EventRoom(new EventRoom(Canonical).ToSerializable());
            Check(eventRoom.CanonicalEvent.Id == Canonical.Id, "Parent event identity must round-trip.");
            var intermission = await Begin(player);
            Check(intermission.CurrentOptions.Count == 2 && intermission.CurrentOptions.All(o => o.TextKey.Contains(".POST_FIRST.")), "A victory history restores intermission, never the opening fight.");
            await intermission.CurrentOptions[0].Chosen();
            var second = HeadlessBanditRoomEntrance.Rooms.Single();
            var resumed = await Begin(player);
            await resumed.CurrentOptions[0].Chosen();
            Check(HeadlessBanditRoomEntrance.Rooms.Last().Encounter!.Id == second.Encounter!.Id, "Restoring the same visit must not redraw a different elite.");
            player.AddRelicInternal(ModelDb.Relic<MoltenEgg>().ToMutable());
            player.AddRelicInternal(ModelDb.Relic<FrozenEgg>().ToMutable());
            player.AddRelicInternal(ModelDb.Relic<ToxicEgg>().ToMutable());
            var rewards = new RewardsSet(player).WithRewardsFromRoom(second);
            await rewards.GenerateWithoutOffering();
            var cards = rewards.Rewards.OfType<CardReward>().Single();
            Check(cards.CanSkip && cards.Cards.Count() == 3 && cards.Cards.All(c => c.Rarity == CardRarity.Rare && c.IsUpgraded), "Native reward modification hooks must upgrade all candidate card types while retaining Boss rarity.");
        }
        finally
        {
            HeadlessBanditRoomEntrance.Active = false;
            RunManager.Instance.CombatReplayWriter.StopRecording();
            RunManager.Instance.CleanUp(false);
        }
    }

    private static async Task Presentation()
    {
        LocalContext.NetId = 1;
        using var scope = HeadlessCombatInProgress.EnterEvent();
        using var portrait = ResourceLoader.Load<Texture2D>("res://AllMatters/images/events/colosseum.png");
        Check(portrait != null && portrait.GetWidth() > 0, "The exported event portrait must load.");
        using var encounter = ModelDb.Encounter<ColosseumFirstEncounter>().CreateScene();
        Check(encounter.HasNode("fog") && encounter.HasNode("cultist"), "The first encounter must expose both layout slots.");
        encounter.Free();
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player], acts: [], seed: "ARENA_TEXT");
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                var model = await Begin(player);
                Check(model.Title.Exists() && model.InitialDescription.Exists() && model.CurrentOptions.Single().Description.Exists(), "Opening page and option must be localized.");
                await model.CurrentOptions.Single().Chosen();
                Check(model.CurrentOptions.Single().TextKey.Contains(".AWAKE.options.FIGHT") && model.CurrentOptions.Single().Description.Exists(), "The second page has a localized fight option.");
                var room = new CombatRoom(ModelDb.Encounter<ColosseumFirstEncounter>().ToMutable(), run);
                // Prepare the same public transition used on victory.
                await model.CurrentOptions.Single().Chosen();
                await model.Resume(room);
                Check(model.CurrentOptions.All(o => o.Description.Exists() && o.Title.Exists()), "Both intermission choices must be localized.");
                Check(ModelDb.Encounter<ColosseumFirstEncounter>().Title.Exists(), "Combat history must have a localized title.");
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
    }

    private static async Task<EventModel> Begin(Player player)
    {
        var model = Canonical.ToMutable();
#if STS2_PUBLIC
        await model.BeginEvent(player, false);
#else
        var sync = new EventCombatSynchronizer(player.RunState, player.RunState);
        await model.BeginEvent(player, sync, false);
        sync.InitializeForEvent(model);
#endif
        return model;
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}

// Observe the public recording boundary without replacing native recording or
// inspecting private writer state. No replay file is written by these tests.
[HarmonyPatch(typeof(CombatReplayWriter), nameof(CombatReplayWriter.RecordInitialState))]
internal static class ColosseumReplayCapture
{
    public static SerializableRun? InitialState;
    private static void Postfix(SerializableRun serializableRun)
    {
        if (HeadlessBanditRoomEntrance.Active) InitialState = serializableRun;
    }
}
