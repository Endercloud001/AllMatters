using AllMatters.Jigsaw;
using AllMatters.Encounters;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;
using System.Text.Json;

internal static class JigsawHuntingEventCombatTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("JigsawHuntingEventCombat actual second Colosseum battle restores its own whole before point", () => SecondBattle()),
     ("JigsawHuntingEventCombat second battle victory clears rollback and preserves terminal parent", () => SecondBattle(victory: true)),
     ("JigsawHuntingEventCombat native Vegetation restores post heal before fight without repeating rest", Vegetation),
     ("JigsawHuntingEventCombat scene encounters provide real markers for every added enemy", FormalSlots)];

    private static Task FormalSlots()
    {
        var native = RunState.CreateForTest([Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)],
            acts: [ModelDb.Act<Overgrowth>()], seed: "HUNTING84_FORMAL_SLOTS");
        var manager = RunManager.Instance;
        manager.SetUpTest(native, new NetSingleplayerGameService());
        try
        {
            var map = ReadyRoot(); JigsawCheckpoint.Capture(native, map); JigsawNativeSession.Restore(native);
            var canonical = ModelDb.Encounter<ColosseumFirstEncounter>();
            var encounter = canonical.ToMutable();
            using var original = canonical.CreateScene();
            var positions = canonical.Slots.ToDictionary(slot => slot, slot => original.GetNode<Marker2D>(slot).Position);
            encounter.GenerateMonstersWithSlots(native);
            using var scene = encounter.CreateScene();
            Check(encounter.MonstersWithSlots.Count(pair => pair.Item1 is AllMatters.Monsters.FungusBeast) == 1 &&
                encounter.MonstersWithSlots.All(pair => pair.Item2 != null && scene.HasNode(pair.Item2) &&
                    scene.GetNode(pair.Item2) is Marker2D),
                "Every generated enemy in a scene-bearing encounter has a real named Marker2D for native AddCreature and positioning.");
            Check(positions.All(pair => scene.GetNode<Marker2D>(pair.Key).Position == pair.Value),
                "Adding the hunting marker preserves every original authored slot and its exact position.");
            original.Free(); scene.Free();
            return Task.CompletedTask;
        }
        finally { CombatManager.Instance.Reset(true); manager.CleanUp(false); }
    }

    private static async Task Vegetation()
    {
        using var events = HeadlessCombatInProgress.EnterEvent();
        var native = RunState.CreateForTest([Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)],
            acts: [ModelDb.Act<Overgrowth>()], seed: "HUNTING84_VEGETATION");
        var manager = RunManager.Instance;
        manager.SetUpTest(native, new NetSingleplayerGameService());
        manager.CombatReplayWriter.IsEnabled = false;
        LocalContext.NetId = 1;
        var io = new MockGodotFileIo("hunting84-vegetation-memory-only");
        var saves = new RunSaveManager(84, io, new MigrationManager(io), forceSynchronous: true);
        var previousSave = JigsawNativeSaveHost.Save;
        JigsawNativeSaveHost.Save = room => saves.SaveRun(manager.ToSave(room), false);
        try
        {
            manager.GenerateRooms(); await manager.GenerateMap();
            var parent = ModelDb.Event<MegaCrit.Sts2.Core.Models.Events.DenseVegetation>();
            native.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, parent.Id);
            native.AddVisitedMapCoord(native.Map.StartingMapPoint.coord);
            var map = ReadyRoot();
            JigsawCheckpoint.Capture(native, map); JigsawNativeSession.Restore(native);
            native.Players[0].Creature.SetCurrentHpInternal(10);
            await manager.EnterRoom(new EventRoom(parent));
            var model = manager.EventSynchronizer.GetLocalEvent();
            await model.CurrentOptions.Single(option => option.TextKey.EndsWith("_HEAL")).Chosen();
            int healed = native.Players[0].Creature.CurrentHp;
            Check(healed > 10, "The native event action genuinely heals before the fight.");
            await model.CurrentOptions.Single(option => option.TextKey.EndsWith(".FIGHT")).Chosen();
            await Until(() => native.CurrentRoom is CombatRoom);
            var battle = (CombatRoom)native.CurrentRoom!;
            string opening = JigsawHuntingCombatTests.Opening(battle);
            string playerBefore = JsonSerializer.Serialize(native.Players[0].ToSerializable());
            string rngBefore = JsonSerializer.Serialize(native.Rng.ToSerializable());
            string eventRngBefore = EventRng(model);
            int history = native.CurrentMapPointHistoryEntry!.Rooms.Count;
            int steps = map.Steps;
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), native.Players[0].Creature,
                5, ValueProp.Unpowered, battle.CombatState.Enemies[0]);
            await PlayerCmd.GainGold(19, native.Players[0]); native.Rng.Niche.NextInt(100);
            await JigsawNativeSaveHost.Save(null);
            var saved = saves.LoadRunSave();
            Check(saved.Success && saved.SaveData != null, "The native event combat save parses.");
            CombatManager.Instance.Reset(true); manager.CleanUp(false);
            var restored = RunState.FromSerializable(saved.SaveData!);
            await manager.SetUpSavedSingleplayer(restored, saved.SaveData!);
            LocalContext.NetId = 1; await manager.GenerateMap(); await manager.LoadIntoLatestMapCoord(null);
            Check(restored.CurrentRoomCount == 2 && restored.BaseRoom is EventRoom &&
                restored.CurrentRoom is CombatRoom restoredBattle && restoredBattle.Encounter.Id == battle.Encounter.Id,
                "Vegetation resumes its accepted battle and parent stack, not a fresh rest page.");
            Check(JsonSerializer.Serialize(restored.Players[0].ToSerializable()) == playerBefore &&
                restored.Players[0].Creature.CurrentHp == healed && JsonSerializer.Serialize(restored.Rng.ToSerializable()) == rngBefore,
                "The complete source retains the finished healing action and rolls back combat effects and RNG.");
            Check(JigsawHuntingCombatTests.Opening((CombatRoom)restored.CurrentRoom!) == opening &&
                EventRng(manager.EventSynchronizer.GetLocalEvent()) == eventRngBefore &&
                restored.CurrentMapPointHistoryEntry!.Rooms.Count == history && JigsawNativeSession.Current!.Map!.Steps == steps,
                "The same native opening and owner event RNG rebuild once without duplicate history or exploration charge.");
            var restoredParent = manager.EventSynchronizer.GetLocalEvent();
            foreach (var staleRest in restoredParent.CurrentOptions.Where(option => option.TextKey.Contains(".AM_REST_")).ToArray())
                await staleRest.Chosen();
            Check(JsonSerializer.Serialize(restored.Players[0].ToSerializable()) == playerBefore,
                "No restored parent rest action can repeat the already completed healing behind its child battle.");
        }
        finally
        {
            JigsawNativeSaveHost.Save = previousSave;
            CombatManager.Instance.Reset(true); manager.CleanUp(false);
        }
    }

    private static async Task SecondBattle(bool victory = false)
    {
        using var selector = CardSelectCmd.UseSelector(new JigsawHuntingCombatTests.TakeCardReward());
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        using var events = HeadlessCombatInProgress.EnterEvent();
        var native = RunState.CreateForTest([Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)],
            acts: [ModelDb.Act<Overgrowth>()], seed: "HUNTING84_EVENT_SECOND");
        var manager = RunManager.Instance;
        manager.SetUpTest(native, new NetSingleplayerGameService());
        manager.CombatReplayWriter.IsEnabled = false;
        LocalContext.NetId = 1;
        var io = new MockGodotFileIo("hunting84-event-second-memory-only");
        var saves = new RunSaveManager(84, io, new MigrationManager(io), forceSynchronous: true);
        var previousSave = JigsawNativeSaveHost.Save;
        JigsawNativeSaveHost.Save = room => saves.SaveRun(manager.ToSave(room), false);
        var profile = new Harmony("AllMatters.Tests.Hunting84EventProfile");
        foreach (var method in new[] { AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay)),
            AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)),
            AccessTools.Method(typeof(SaveManager), nameof(SaveManager.UpdateProgressAfterCombatWon)) })
            profile.Patch(method, prefix: new HarmonyMethod(typeof(JigsawHuntingEventCombatTests), nameof(SkipProfile)));
        try
        {
            manager.GenerateRooms(); await manager.GenerateMap();
            var parent = ModelDb.Event<AllMatters.Events.Colosseum>();
            // Force the known two-combat event as an Overgrowth event source to
            // exercise current-ecology ordinary enhancement and named elites.
            native.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, parent.Id);
            native.AddVisitedMapCoord(native.Map.StartingMapPoint.coord);
            var map = ReadyRoot();
            JigsawCheckpoint.Capture(native, map); JigsawNativeSession.Restore(native);
            int steps = map.Steps;
            await manager.EnterRoom(new EventRoom(parent));
            var model = manager.EventSynchronizer.GetLocalEvent();
            await model.CurrentOptions.Single().Chosen();
            await model.CurrentOptions.Single().Chosen();
            await Until(() => native.CurrentRoom is CombatRoom);
            var first = (CombatRoom)native.CurrentRoom!;
            Check(first.Encounter is ColosseumFirstEncounter && FungusCount(first) == 1,
                "The real first embedded battle receives the current forest's ordinary addition once.");
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), native.Players[0].Creature,
                9, ValueProp.Unpowered, first.CombatState.Enemies[0]);
            AccessTools.Property(native.Players[0].PlayerCombatState!.GetType(), "TurnNumber")
                .SetValue(native.Players[0].PlayerCombatState, 1);
            await (Task)AccessTools.Method(typeof(CombatManager), "EndCombatInternal", Type.EmptyTypes).Invoke(CombatManager.Instance, null)!;
            await manager.ProceedFromTerminalRewardsScreen();
            await manager.EventSynchronizer.AwaitPendingOptionTasks();
            Check(native.CurrentRoom is EventRoom && model.CurrentOptions.Count == 2,
                "Actual native victory and Resume return the same event to intermission.");
            await model.CurrentOptions.Single(option => option.TextKey.EndsWith(".FIGHT")).Chosen();
            await Until(() => native.CurrentRoom is CombatRoom);
            var second = (CombatRoom)native.CurrentRoom!;
            Check(second.Encounter is not ColosseumFirstEncounter && FungusCount(second) == 1,
                "The independent second native elite battle receives ordinary event enhancement once.");
            string encounter = second.Encounter.Id.Entry;
            string opening = JigsawHuntingCombatTests.Opening(second);
            string parentRng = EventRng(model);
            string playerBefore = JsonSerializer.Serialize(native.Players[0].ToSerializable());
            string rngBefore = JsonSerializer.Serialize(native.Rng.ToSerializable());
            int historyRooms = native.CurrentMapPointHistoryEntry!.Rooms.Count;
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), native.Players[0].Creature,
                5, ValueProp.Unpowered, second.CombatState.Enemies[0]);
            await PlayerCmd.GainGold(23, native.Players[0]);
            native.Rng.Niche.NextInt(100);
            await JigsawNativeSaveHost.Save(null);
            var saved = saves.LoadRunSave();
            Check(saved.Success && saved.SaveData != null, "The actual second-battle native save parses.");
            CombatManager.Instance.Reset(true); manager.CleanUp(false);
            var restored = RunState.FromSerializable(saved.SaveData!);
            await manager.SetUpSavedSingleplayer(restored, saved.SaveData!);
            LocalContext.NetId = 1;
            await manager.GenerateMap(); await manager.LoadIntoLatestMapCoord(null);
            Check(restored.CurrentRoomCount == 2 && restored.BaseRoom is EventRoom &&
                restored.CurrentRoom is CombatRoom restoredBattle && restoredBattle.Encounter.Id.Entry == encounter && FungusCount(restoredBattle) == 1,
                "Real native load rebuilds this same second battle with its parent stack, rather than restarting the root event.");
            Check(JsonSerializer.Serialize(restored.Players[0].ToSerializable()) == playerBefore &&
                JsonSerializer.Serialize(restored.Rng.ToSerializable()) == rngBefore,
                "Second-battle rollback retains first-battle losses while discarding only second-battle effects and RNG.");
            Check(restored.CurrentMapPointHistoryEntry!.Rooms.Count == historyRooms && JigsawNativeSession.Current!.Map!.Steps == steps,
                "Second-battle restore appends no duplicate history and charges no exploration step.");
            var restoredSecond = (CombatRoom)restored.CurrentRoom!;
            Check(JigsawHuntingCombatTests.Opening(restoredSecond) == opening &&
                EventRng(manager.EventSynchronizer.GetLocalEvent()) == parentRng,
                "Second-battle reload reconstructs every initial enemy value and its same parent event RNG.");
            var resumedParent = manager.EventSynchronizer.GetLocalEvent();
            var staleFlee = resumedParent.CurrentOptions.FirstOrDefault(option => option.TextKey.EndsWith(".FLEE"));
            if (staleFlee != null) await staleFlee.Chosen();
            Check(!resumedParent.IsFinished && ReferenceEquals(restored.CurrentRoom, restoredSecond),
                "A restored parent cannot flee from an already accepted second battle.");
            if (victory)
            {
                await PlayerCmd.GainGold(31, restored.Players[0]);
                await (Task)AccessTools.Method(typeof(CombatManager), "EndCombatInternal", Type.EmptyTypes).Invoke(CombatManager.Instance, null)!;
                var won = saves.LoadRunSave();
                Check(won.Success && won.SaveData?.PreFinishedRoom is { IsPreFinished: true, ParentEventId: not null },
                    "Actual embedded victory saves the native prefinished reward room with its parent identity.");
                CombatManager.Instance.Reset(true); manager.CleanUp(false);
                var rewardRun = RunState.FromSerializable(won.SaveData!);
                Check(JigsawCheckpoint.Slot.TryGet(rewardRun, out var rewardPoint) && rewardPoint.EventCombat == null,
                    "The genuine victory save removes only the active event battle rollback descriptor.");
                await manager.SetUpSavedSingleplayer(rewardRun, won.SaveData!);
                LocalContext.NetId = 1;
                await manager.GenerateMap();
                await manager.LoadIntoLatestMapCoord(CombatRoom.FromSerializable(won.SaveData!.PreFinishedRoom!, rewardRun));
                Check(rewardRun.CurrentRoomCount == 2 && rewardRun.CurrentRoom is CombatRoom { IsPreFinished: true },
                    "Native embedded reward load keeps the parent stack and never restarts combat.");
                var rewardParent = manager.EventSynchronizer.GetLocalEvent();
                var rewardFlee = rewardParent.CurrentOptions.FirstOrDefault(option => option.TextKey.EndsWith(".FLEE"));
                if (rewardFlee != null) await rewardFlee.Chosen();
                Check(!rewardParent.IsFinished,
                    "The parent restored behind terminal victory rewards cannot flee as though the second battle never happened.");
            }
        }
        finally
        {
            profile.UnpatchAll(profile.Id); JigsawNativeSaveHost.Save = previousSave;
            CombatManager.Instance.Reset(true); manager.CleanUp(false);
        }
    }

    private static int FungusCount(CombatRoom room) => room.CombatState.Enemies.Count(creature => creature.Monster is AllMatters.Monsters.FungusBeast);
    private static string EventRng(EventModel model) =>
#if STS2_PUBLIC
        JsonSerializer.Serialize(new { model.Rng.Seed, model.Rng.Counter });
#else
        JsonSerializer.Serialize(model.Rng.ToSerializable());
#endif
    private static bool SkipProfile() => false;
    private static async Task Until(Func<bool> condition)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int frame = 0; frame < 120 && !condition(); frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        Check(condition(), "The actual asynchronous embedded battle entry finishes within the bounded host frame budget.");
    }
    private static JigsawRun ReadyRoot()
    {
        var map = JigsawRun.Create(75);
        Ok(map.ChooseEnd(1, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        Ok(map.CompleteAncient(1)); Ok(map.Continue(1));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var pending = new Queue<int>(); pending.Enqueue(0);
        while (pending.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (map.Nodes.Single(node => node.Id == next).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss) &&
                    previous.TryAdd(next, current)) pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == JigsawRoom.Unknown && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse(); Ok(map.SetRemainingSteps(0));
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!; Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        return map;
    }
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
