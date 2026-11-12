using AllMatters.Jigsaw;
using AllMatters.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;

internal static class JigsawHuntingTreasureTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw hunting treasure opens without gold while consuming Face and completing Spoils", Spoils),
      ("Jigsaw hunting treasure without Face cancels native gold and shared relics only in punished roots", NativeRewards),
      ("Jigsaw hunting treasure waits for asynchronous extra reward hooks and preserves nonreward work", Extras),
      ("Jigsaw hunting treasure saves the whole before-open point while quest removal is suspended", Checkpoint),
      ("Jigsaw hunting treasure completion waits for the actual quest task on this endpoint", Completion),
      ("Jigsaw hunting treasure completion received before the native source waits for that source", SourceNotStarted),
      ("Jigsaw hunting treasure recovery drains original tasks before replacing the native run", RecoveryDrain),
      ("Jigsaw hunting treasure direct third party payments are a concrete unsupported boundary", DirectPayments),
      ("Jigsaw hunting treasure actual pause and closed session menu entries drain native work", MenuDrain),
      ("Jigsaw hunting treasure native empty Tween finishes before proceeding", EmptyTween),
      ("Jigsaw hunting treasure completed owners checkpoint does not replay the chest", CompletedCheckpoint) ];

    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);

    private static JigsawRun Ready(bool hunted)
    {
        var map = Enumerable.Range(0, 100).Select(seed => JigsawRun.Create(seed))
            .First(run => run.Pool[0].Nodes[0].Room == JigsawRoom.Treasure);
        Ok(map.Take(1, 0)); Ok(map.Drop(1, new(3, 0))); Ok(map.AddEdge(1, 0, 1));
        Ok(map.ChooseEnd(1, EndChoice.Manual)); Ok(map.Inspect());
        Ok(map.CompleteAncient(1)); Ok(map.Continue(1));
        if (hunted) Ok(map.SetRemainingSteps(0));
        return map;
    }

    private static async Task WithTreasure(bool hunted, Func<JigsawNativeSession, TreasureRoom, Task> test, bool face = true, Action<RunState>? beforeEntry = null)
    {
        JigsawHuntingNativeTests.RegisterScripts();
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        using var events = HeadlessCombatInProgress.EnterEvent();
        var persistence = new Harmony("AllMatters.Tests.TreasureProfilePersistence");
        persistence.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Saves.SaveManager), nameof(MegaCrit.Sts2.Core.Saves.SaveManager.SaveProgressFile)),
            prefix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(SkipHost)));
        var hostIo = new MockGodotFileIo("hunting-treasure-host-83");
        var hostSaves = new RunSaveManager(83, hostIo, new MigrationManager(hostIo), forceSynchronous: true);
        var previousSave = JigsawNativeSaveHost.Save;
        JigsawNativeSaveHost.Save = room => hostSaves.SaveRun(RunManager.Instance.ToSave(room), false);
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
        var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>()], seed: "HUNTING_TREASURE_83");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        MegaCrit.Sts2.Core.Context.LocalContext.NetId = 1;
        try
        {
            RunManager.Instance.GenerateRooms(); await RunManager.Instance.GenerateMap();
            native.AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.Ancient, RoomType.Event, null);
            native.AddVisitedMapCoord(native.Map.StartingMapPoint.coord);
            JigsawCheckpoint.Capture(native, Ready(hunted));
            JigsawNativeSession.Restore(native);
            var session = JigsawNativeSession.Current!;
            if (face) await RelicCmd.Obtain<NlothsHungryFace>(player);
            beforeEntry?.Invoke(native);
            await session.Enter(1);
            Check(native.CurrentRoom is TreasureRoom && session.Map!.CurrentRoomHunted == hunted,
                "The native exploration entry confirms the real treasure room and its authoritative punishment snapshot.");
            await test(session, (TreasureRoom)native.CurrentRoom!);
        }
        finally
        {
            MegaCrit.Sts2.Core.Combat.CombatManager.Instance.Reset(true); RunManager.Instance.CleanUp(false);
            JigsawNativeSaveHost.Save = previousSave;
            persistence.UnpatchAll(persistence.Id);
        }
    }

    private static async Task NativeRewards()
    {
        await WithTreasure(true, async (session, room) =>
        {
            var player = session.Native.Players[0]; int gold = player.Gold;
            Check(!player.Relics.OfType<NlothsHungryFace>().Any(), "No Face can supply the empty chest behavior in this scenario.");
            int particles = await room.DoNormalRewards();
            Check(particles == 0 && player.Gold == gold, "Punishment itself prevents ordinary chest gold.");
            Check(RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics?.Count is not > 0,
                "The actual native shared relic generation produces an empty chest.");
        }, face: false);
        await WithTreasure(false, async (session, room) =>
        {
            var player = session.Native.Players[0]; int gold = player.Gold;
            int particles = await room.DoNormalRewards();
            Check(particles is >= 42 and <= 52 && player.Gold - gold == particles,
                "A safe root keeps the native chest payment even in a puzzle run.");
            Check(RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics?.Count > 0,
                "A safe root still generates native shared relics.");
        }, face: false);
        await WithTreasure(false, async (session, room) =>
        {
            var player = session.Native.Players[0]; int gold = player.Gold;
            int particles = await room.DoNormalRewards();
            Check(particles == 0 && player.Gold == gold && player.Relics.OfType<NlothsHungryFace>().Single().Spent,
                "A non-hunted Face chest retains its original consumed empty behavior.");
        });
    }

    private static RewardsSet? extraSet;
    private static TaskCompletionSource? releaseExtra;
    private static int nonrewardCount;
    private static void AddSynchronousExtra(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (extraSet?.Player == player && ReferenceEquals(extraSet.Room, room)) rewards.Add(new GoldReward(13, player));
    }
    private static void AddAsynchronousExtra(ref Task __result)
    {
        if (extraSet != null) __result = Finish(__result, extraSet);
        static async Task Finish(Task original, RewardsSet set)
        {
            await original;
            nonrewardCount++;
            await releaseExtra!.Task;
            set.Rewards.Add(new GoldReward(19, set.Player));
        }
    }

    private static Task Extras() => WithTreasure(true, async (session, room) =>
    {
        await room.DoNormalRewards();
        var player = session.Native.Players[0]; int gold = player.Gold;
        var fixture = new Harmony("AllMatters.Tests.HuntingTreasureExtras");
        fixture.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.ModifyRewards)), postfix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(AddSynchronousExtra)));
        fixture.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.AfterModifyingRewards)), postfix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(AddAsynchronousExtra)));
        extraSet = new RewardsSet(player).EmptyForRoom(room);
        releaseExtra = new(TaskCreationOptions.RunContinuationsAsynchronously); nonrewardCount = 0;
        try
        {
            Task generating = extraSet.GenerateWithoutOffering();
            Check(nonrewardCount == 1 && !generating.IsCompleted, "The actual generation task awaits the asynchronous nonreward hook.");
            var completing = JigsawNativeExploration.ReceiveRoomCompletion(session, player.NetId);
            Check(!completing.IsCompleted && !JigsawNativeExploration.NativeFlowFinished(session, player.NetId),
                "Empty relics do not complete the room while the actual asynchronous reward generation is running.");
            releaseExtra.SetResult(); await generating;
            Check(await completing, "The existing completion request resumes when the actual extra task ends.");
            Check(nonrewardCount == 1 && extraSet.Rewards.Count == 0 && player.Gold == gold,
                "Both synchronous and asynchronous list rewards disappear only after nonreward work finishes once.");
            await extraSet.Offer();
            Check(player.Gold == gold, "Offering the generated empty set cannot acquire any of its extra rewards.");
        }
        finally { releaseExtra.TrySetResult(); extraSet = null; releaseExtra = null; fixture.UnpatchAll(fixture.Id); }
    }, face: false);

    private static SpoilsMap? removing;
    private static TaskCompletionSource? releaseRemoval;
    private static RunState? removalRunAfterResume;
    private static void SuspendRemoval(CardModel card, ref Task __result)
    {
        if (card == removing) __result = Wait(__result);
        static async Task Wait(Task original)
        {
            await original;
            await releaseRemoval!.Task;
            removalRunAfterResume = RunManager.Instance.DebugOnlyGetState();
        }
    }

    private static async Task Checkpoint()
    {
        SpoilsMap? quest = null;
        await WithTreasure(true, async (session, room) =>
        {
            var native = session.Native; var player = native.Players[0];
            AccessTools.Property(typeof(SpoilsMap), nameof(SpoilsMap.SpoilsCoord)).SetValue(quest!, native.CurrentMapCoord);
            native.CurrentMapPoint!.AddQuest(quest!);
            removing = quest; releaseRemoval = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var fixture = new Harmony("AllMatters.Tests.HuntingTreasureRemoval");
            fixture.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCardRemoved)), postfix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(SuspendRemoval)));
            Task<int>? opening = null;
            try
            {
                opening = room.DoNormalRewards();
                Check(!opening.IsCompleted && player.Deck.Cards.Contains(quest!) && native.CurrentMapPointHistoryEntry!.GetEntry(1).CompletedQuests.Contains(quest!.Id),
                    "The real quest chain is paused after its completion history but before removing the real deck card.");
                var io = new MockGodotFileIo("hunting-treasure-83-memory-only");
                var saves = new RunSaveManager(83, io, new MigrationManager(io), forceSynchronous: true);
                await saves.SaveRun(RunManager.Instance.ToSave(null), false);
                var loaded = saves.LoadRunSave(); Check(loaded.Success, "The native IO path writes a valid whole point during an unfinished chest.");
                var restored = RunState.FromSerializable(loaded.SaveData!);
                JigsawCheckpoint.Slot.TryGet(restored, out var observed);
                Console.WriteLine($"TREASURE_CHECKPOINT cards={restored.Players[0].Deck.Cards.OfType<SpoilsMap>().Count()} quests={restored.MapPointHistory.SelectMany(act => act).Sum(history => history.GetEntry(1).CompletedQuests.Count)} pending={observed?.Map?.PendingEntry} steps={observed?.Map?.Steps} retained={session.UnfinishedTreasureCheckpoint != null}");
                Check(restored.Players[0].Deck.Cards.OfType<SpoilsMap>().Count() == 1 &&
                    restored.MapPointHistory.SelectMany(act => act).All(history => !history.GetEntry(1).CompletedQuests.Contains(quest.Id)) &&
                    JigsawCheckpoint.Slot.TryGet(restored, out var data) && data.Map!.PendingEntry?.NodeId == 1 && data.Map.Steps == 0,
                    "Saved native player, quest history, RNG and pending puzzle entry all return to the same before-open point rather than a half-removed quest.");
            }
            finally
            {
                releaseRemoval.TrySetResult(); if (opening != null) await opening;
                removing = null; releaseRemoval = null; fixture.UnpatchAll(fixture.Id);
            }
        }, beforeEntry: native =>
        {
            quest = native.CreateCard<SpoilsMap>(native.Players[0]); quest.SpoilsActIndex = native.CurrentActIndex;
            native.Players[0].Deck.AddInternal(quest);
        });
    }

    private static Task Completion() => WithTreasure(true, async (session, room) =>
    {
        var player = session.Native.Players[0];
        removing = session.Native.CreateCard<SpoilsMap>(player); player.Deck.AddInternal(removing);
        removing.SpoilsActIndex = session.Native.CurrentActIndex;
        AccessTools.Property(typeof(SpoilsMap), nameof(SpoilsMap.SpoilsCoord)).SetValue(removing, session.Native.CurrentMapCoord);
        session.Native.CurrentMapPoint!.AddQuest(removing);
        releaseRemoval = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = new Harmony("AllMatters.Tests.HuntingTreasureCompletion");
        fixture.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCardRemoved)), postfix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(SuspendRemoval)));
        Task<int>? opening = null;
        try
        {
            opening = room.DoNormalRewards();
            var completed = JigsawNativeExploration.ReceiveRoomCompletion(session, player.NetId);
            Check(!completed.IsCompleted && !JigsawNativeExploration.NativeFlowFinished(session, player.NetId),
                "An empty shared relic list cannot authenticate completion while the actual quest task is still running.");
            releaseRemoval.SetResult(); await opening;
            Check(await completed && !player.Deck.Cards.Contains(removing), "After the real quest task finishes, the same completion request can proceed without a second click.");
        }
        finally { releaseRemoval.TrySetResult(); if (opening != null) await opening; removing = null; releaseRemoval = null; fixture.UnpatchAll(fixture.Id); }
    });

    private static Task SourceNotStarted() => WithTreasure(true, async (session, room) =>
    {
        var completion = JigsawNativeExploration.ReceiveRoomCompletion(session, 1);
        Check(!completion.IsCompleted && !JigsawNativeExploration.NativeFlowFinished(session, 1),
            "A completion intent arriving before its native source cannot treat an empty task list as finished.");
        await room.DoNormalRewards();
        Check(await completion, "The same completion intent resumes after the real native source arrives and finishes.");
    });

    private static Task RecoveryDrain() => WithTreasure(true, async (session, room) =>
    {
        var native = session.Native; var player = native.Players[0];
        removing = native.CreateCard<SpoilsMap>(player); player.Deck.AddInternal(removing);
        removing.SpoilsActIndex = native.CurrentActIndex;
        AccessTools.Property(typeof(SpoilsMap), nameof(SpoilsMap.SpoilsCoord)).SetValue(removing, native.CurrentMapCoord);
        native.CurrentMapPoint!.AddQuest(removing);
        releaseRemoval = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = new Harmony("AllMatters.Tests.HuntingTreasureDrain");
        fixture.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCardRemoved)), postfix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(SuspendRemoval)));
        fixture.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.AfterModifyingRewards)), postfix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(FailExtra)));
        Task? reload = null; Task<int>? opening = null;
        try
        {
            opening = room.DoNormalRewards();
            extraSet = new RewardsSet(player).EmptyForRoom(room);
            try { await extraSet.GenerateWithoutOffering(); throw new Exception("The real extra hook must propagate its failure."); }
            catch (InvalidOperationException error) when (error.Message == "Injected treasure nonreward hook failure.") { }
            var checkpoint = RunManager.Instance.ToSave(null);
            var restored = RunState.FromSerializable(checkpoint);
            reload = (Task)AccessTools.Method(typeof(JigsawNativeSession), "LoadCheckpoint").Invoke(null, [checkpoint, restored, null, 0L])!;
            Check(!reload.IsCompleted && ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), native),
                "Recovery keeps the original native run installed while the uncancellable real quest removal is pending.");
            releaseRemoval.SetResult(); await opening; await reload;
            Check(ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), restored) && !player.Deck.Cards.Contains(removing) && ReferenceEquals(removalRunAfterResume, native),
                "A faulted sibling cannot release drain early: the old hook still sees its original global run until it actually finishes, then recovery installs the checkpoint.");
        }
        finally
        {
            releaseRemoval.TrySetResult(); if (opening != null) await opening;
            if (reload != null) await reload;
            removing = null; releaseRemoval = null; extraSet = null; removalRunAfterResume = null; fixture.UnpatchAll(fixture.Id);
        }
    });

    private static void FailExtra(ref Task __result)
    {
        if (extraSet != null) __result = Fail(__result);
        static async Task Fail(Task original) { await original; throw new InvalidOperationException("Injected treasure nonreward hook failure."); }
    }

    private static RelicModel? directRelic;
    private static void DirectPayment(ref Task __result)
    {
        if (extraSet != null) __result = Pay(__result, extraSet);
        static async Task Pay(Task original, RewardsSet set)
        {
            await original;
            await PlayerCmd.GainGold(7, set.Player);
            directRelic = await RelicCmd.Obtain<MegaCrit.Sts2.Core.Models.Relics.Lantern>(set.Player);
            set.Rewards.Add(new GoldReward(13, set.Player));
        }
    }

    private static Task DirectPayments() => WithTreasure(true, async (session, room) =>
    {
        var player = session.Native.Players[0]; int gold = player.Gold;
        var fixture = new Harmony("AllMatters.Tests.TreasureDirectPayments");
        fixture.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.AfterModifyingRewards)),
            postfix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(DirectPayment)));
        extraSet = new RewardsSet(player).EmptyForRoom(room);
        try
        {
            await extraSet.GenerateWithoutOffering();
            Check(extraSet.Rewards.Count == 0 && player.Gold == gold + 7,
                "The supported list award is removed, while a third party direct GainGold command concretely bypasses that list boundary.");
            Check(directRelic?.Owner == player && player.Relics.Contains(directRelic),
                "Direct Obtain is concretely unsupported; its returned owned relic really exists in inventory, rather than being a fabricated Owner-only object.");
        }
        finally { extraSet = null; directRelic = null; fixture.UnpatchAll(fixture.Id); }
    }, face: false);

    private static int nativeExits;
    private static bool NativeMenuExit(ref Task __result)
    {
        nativeExits++;
        RunManager.Instance.CleanUp(false);
        __result = Task.CompletedTask;
        return false;
    }

    private static async Task MenuDrain()
    {
        foreach (bool pause in new[] { true, false })
            await WithTreasure(true, async (session, room) =>
            {
                var native = session.Native; var player = native.Players[0];
                removing = native.CreateCard<SpoilsMap>(player); player.Deck.AddInternal(removing);
                removing.SpoilsActIndex = native.CurrentActIndex;
                AccessTools.Property(typeof(SpoilsMap), nameof(SpoilsMap.SpoilsCoord)).SetValue(removing, native.CurrentMapCoord);
                native.CurrentMapPoint!.AddQuest(removing);
                releaseRemoval = new(TaskCreationOptions.RunContinuationsAsynchronously);
                var fixture = new Harmony("AllMatters.Tests.TreasureMenuDrain");
                fixture.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCardRemoved)), postfix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(SuspendRemoval)));
                using var ui = new JigsawNativeMapUiHost();
                _ = await ui.Open(session); session.HideMap();
                var menu = new MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu.NPauseMenu();
                var target = pause ? AccessTools.Method(menu.GetType(), "CloseToMenu") :
                    AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.NGame), nameof(MegaCrit.Sts2.Core.Nodes.NGame.ReturnToMainMenu));
                // Replace only native fade/preload/scene ownership after production
                // exit prefixes have run. Keep real CleanUp as the observed effect.
                fixture.Patch(target, prefix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(NativeMenuExit)) { priority = Priority.Last });
                nativeExits = 0; Task? exiting = null; Task<int>? opening = null;
                try
                {
                    opening = room.DoNormalRewards();
                    if (!pause) session.Close();
                    exiting = (Task)target.Invoke(pause ? menu : MegaCrit.Sts2.Core.Nodes.NGame.Instance, null)!;
                    Check(!exiting.IsCompleted && nativeExits == 0 && ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), native),
                        "The actual pause/menu entry reaches production drain and retains the old global run, including after session Close.");
                    releaseRemoval.SetResult(); await opening; await exiting;
                    Check(nativeExits == 1 && RunManager.Instance.DebugOnlyGetState() == null && ReferenceEquals(removalRunAfterResume, native),
                        "Only after the original native hook resumes against its own run may the actual menu continuation clean it up once.");
                }
                finally
                {
                    releaseRemoval.TrySetResult(); if (opening != null) await opening; if (exiting != null) await exiting;
                    removing = null; releaseRemoval = null; removalRunAfterResume = null; fixture.UnpatchAll(fixture.Id);
                    menu.Free();
                }
            });
    }

    private static bool SkipHost() => false;
    private static bool SeenTreasureFtue(string ftueKey, ref bool __result)
    {
        if (ftueKey != "obtain_relic_ftue") return true;
        __result = true;
        return false;
    }
    private static MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen? treasureMap;
    private static bool MapInstance(ref MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen __result) { __result = treasureMap!; return false; }
#if STS2_PUBLIC
    private static Control PublicCollectionHost()
    {
        // The public DLL cannot instantiate beta's chest/proceed scripts. Keep
        // the native collection subtree and its actual sibling dependencies.
        string scene = Godot.FileAccess.GetFileAsString("res://scenes/rooms/treasure_room.tscn");
        var sections = System.Text.RegularExpressions.Regex.Split(scene, @"(?=\[node )");
        var kept = sections.Where(section => !System.Text.RegularExpressions.Regex.IsMatch(section,
            "^\\[node name=\"(?:Chest|ProceedButton|SkipMultiplayerVoteContainer|Banner|ColorRect|GoldExplosion|GoldExplosionCollider)\""));
        string collectionScene = string.Concat(kept).Replace("script = ExtResource(\"1_ywu15\")", "");
        string path = ProjectSettings.GlobalizePath("res://../../.local/issue83/public-collection-host.tscn");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, collectionScene);
        return ResourceLoader.Load<PackedScene>(path, cacheMode: ResourceLoader.CacheMode.Ignore).Instantiate<Control>();
    }
#endif
    private static Task EmptyTween() => WithTreasure(true, async (session, room) =>
    {
        HeadlessUiHost.EnsureInputActions();
        using var ui = new JigsawNativeMapUiHost();
        _ = await ui.Open(session); session.HideMap();
        var hotkeys = new MegaCrit.Sts2.Core.Nodes.CommonUi.NHotkeyManager();
        AccessTools.Property(typeof(MegaCrit.Sts2.Core.Nodes.NGame), "HotkeyManager").SetValue(MegaCrit.Sts2.Core.Nodes.NGame.Instance, hotkeys);
        await HeadlessUiHost.Mount(hotkeys);
        var host = new Harmony("AllMatters.Tests.TreasureScene");
        host.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Saves.SaveManager), nameof(MegaCrit.Sts2.Core.Saves.SaveManager.SeenFtue)),
            prefix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(SeenTreasureFtue)));
        var cursor = new MegaCrit.Sts2.Core.Nodes.CommonUi.NCursorManager();
        AccessTools.Property(typeof(MegaCrit.Sts2.Core.Nodes.NGame), "CursorManager").SetValue(MegaCrit.Sts2.Core.Nodes.NGame.Instance, cursor);
        host.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.NGame), "SetScreenShakeTarget"), prefix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(SkipHost)));
        host.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.NGame), "ClearScreenShakeTarget"), prefix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(SkipHost)));
        treasureMap = new MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen();
        AccessTools.Field(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen), "_runState").SetValue(treasureMap, session.Native);
        host.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen), "_Ready"), prefix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(SkipHost)));
        host.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen), "_Process"), prefix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(SkipHost)));
        host.Patch(AccessTools.PropertyGetter(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen), "Instance"), prefix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(MapInstance)));
        await HeadlessUiHost.Mount(treasureMap);
        treasureMap.SetProcess(false);
        treasureMap.Hide();
        var trackerProperty = AccessTools.Property(typeof(MegaCrit.Sts2.Core.Nodes.NRun), "ScreenStateTracker");
        trackerProperty.SetValue(MegaCrit.Sts2.Core.Nodes.NRun.Instance, System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(trackerProperty.PropertyType));
#if STS2_PUBLIC
        var root = PublicCollectionHost();
        var chestVisual = new Control();
#else
        var root = ResourceLoader.Load<PackedScene>("res://scenes/rooms/treasure_room.tscn").Instantiate<NTreasureRoom>();
        AccessTools.Field(typeof(NTreasureRoom), "_room").SetValue(root, room);
        AccessTools.Field(typeof(NTreasureRoom), "_runState").SetValue(root, session.Native);
#endif
        try
        {
            await HeadlessUiHost.Mount(root);
            var collection = root.GetNode<NTreasureRoomRelicCollection>("%RelicCollection");
#if STS2_PUBLIC
            collection.Initialize(session.Native);
#endif
            Task began = collection.RelicPickingBegan(), finished = collection.RelicPickingFinished();
#if STS2_PUBLIC
            // The bundled host has beta room art. Exercise public's real collection
            // contract, without calling its incompatible whole-room chest skin code.
            collection.InitializeRelics();
            collection.AnimIn(chestVisual);
            Task open = finished;
#else
            Task open = (Task)AccessTools.Method(typeof(NTreasureRoom), "OpenChest").Invoke(root, null)!;
#endif
            if (open.IsFaulted) await open;
            Check(!began.IsCompleted && !finished.IsCompleted && !open.IsCompleted,
                "The actual native open starts with unfinished empty collection notifications.");
            var tree = (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree.CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
            Check(!finished.IsCompleted && !open.IsCompleted, "The empty chest cannot finish before its real delayed Tween callback.");
#if !STS2_PUBLIC
            Check(!JigsawNativeExploration.NativeFlowFinished(session, 1), "The actual pending OpenChest task prevents an early local completion during the real Tween.");
#endif
            await open.WaitAsync(TimeSpan.FromSeconds(10));
            Check(began.IsCompletedSuccessfully && finished.IsCompletedSuccessfully
#if !STS2_PUBLIC
                && root.ProceedButton.IsEnabled
#endif
                ,
                "The native Tween emits empty completion and enables the actual Proceed button.");
            await room.DoNormalRewards();
            await room.DoExtraRewardsIfNeeded();
            session.CompleteCurrentRoom(true);
            await session.WaitForSave();
            Check(session.Map!.CurrentNode == null, "The finished native local chest can complete its puzzle root.");
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            root.Free();
            ui.ReleaseSceneOwnership();
            var io = new MockGodotFileIo("hunting-completed-treasure-83");
            var saves = new RunSaveManager(83, io, new MigrationManager(io), forceSynchronous: true);
            await saves.SaveRun(RunManager.Instance.ToSave(null), false);
            var restored = RunState.FromSerializable(saves.LoadRunSave().SaveData!);
            Check(JigsawCheckpoint.Slot.TryGet(restored, out var completedPoint) && completedPoint.Map!.CurrentNode == null && completedPoint.Map.Completed.Contains(1),
                "The native completed point retains the fully completed root, without an open chest to replay.");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(root)) root.Free();
#if STS2_PUBLIC
            chestVisual.Free();
#endif
            treasureMap.Free(); treasureMap = null; hotkeys.Free(); cursor.Free(); host.UnpatchAll(host.Id);
        }
    }, face: false);

    private static Task CompletedCheckpoint() => WithTreasure(true, async (session, room) =>
    {
        await room.DoNormalRewards(); await room.DoExtraRewardsIfNeeded();
        var map = session.Map!;
        Ok(map.RecordRoomCompletion(1));
        Check(map.CurrentNode == 1 && map.RoomCompletedPlayers.Count == 1, "Every active owner has completed this root while destination ballots are still outstanding.");
        var io = new MockGodotFileIo("hunting-treasure-all-completed-83");
        var saves = new RunSaveManager(83, io, new MigrationManager(io), forceSynchronous: true);
        await saves.SaveRun(RunManager.Instance.ToSave(null), false);
        var restored = RunState.FromSerializable(saves.LoadRunSave().SaveData!);
        Check(JigsawCheckpoint.Slot.TryGet(restored, out var completed) && completed.Map!.CurrentNode == null &&
            completed.Map.PendingEntry == null && completed.Map.Completed.Contains(1) && completed.Map.Steps == 1,
            "The whole completed checkpoint restores exploration, not an opened chest that would rerun its hooks.");
        await (Task)AccessTools.Method(typeof(JigsawNativeSession), "LoadCheckpoint").Invoke(null, [saves.LoadRunSave().SaveData!, restored, null, 0L])!;
        Check(restored.CurrentRoom is MapRoom && JigsawNativeSession.Current?.Map is { CurrentNode: null, PendingEntry: null, Steps: 1 } recovered && recovered.Completed.Contains(1),
            "Actual SetUpSaved and LoadIntoLatestMapCoord mount exploration without reopening the completed chest or charging another step.");
    });

    private static async Task Spoils()
    {
        await WithTreasure(true, async (session, room) =>
        {
            var native = session.Native; var player = native.Players[0];
            var card = native.CreateCard<SpoilsMap>(player);
            player.Deck.AddInternal(card);
            card.SpoilsActIndex = native.CurrentActIndex;
            AccessTools.Property(typeof(SpoilsMap), nameof(SpoilsMap.SpoilsCoord)).SetValue(card, native.CurrentMapCoord);
            native.CurrentMapPoint!.AddQuest(card);
            int gold = player.Gold;
            int particles = await room.DoNormalRewards();
            Check(particles == 0 && player.Gold == gold, "A hunted chest grants neither native chest nor Spoils gold.");
            Check(player.Relics.OfType<NlothsHungryFace>().Single().Spent, "The real room-entered hook consumes Face normally.");
            Check(!player.Deck.Cards.Contains(card) && !native.CurrentMapPoint.Quests.Contains(card) &&
                native.CurrentMapPointHistoryEntry!.GetEntry(player.NetId).CompletedQuests.Count(id => id == card.Id) == 1,
                "The actual chest source completes Spoils history, removes its real card, and removes the map quest once.");
        });
    }
}
