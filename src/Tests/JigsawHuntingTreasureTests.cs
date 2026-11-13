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
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Pooling;

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

    private static async Task WithTreasure(bool hunted, Func<JigsawNativeSession, TreasureRoom, Task> test, bool face = true, Action<RunState>? beforeEntry = null, bool shouldSave = false)
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
        JigsawNativeSaveHost.Save = room => SaveTreasurePoint(hostSaves, "entry/setup", room);
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
        var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>()], seed: "HUNTING_TREASURE_83");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService(), shouldSave: shouldSave);
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

    private static async Task SaveTreasurePoint(RunSaveManager saves, string phase, AbstractRoom? room)
    {
        try { await saves.SaveRun(RunManager.Instance.ToSave(room), false); }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Treasure native save failed during {phase}; room={room?.GetType().Name ?? "null"}, current={JigsawNativeSession.Current?.Map?.CurrentNode}: {error}");
            throw;
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
    private static NTreasureRoom? observedChest;
    private static Task? nativeChestOpening;
    private static int nativeChestOpens;
    private static NTreasureRoomRelicCollection? observedCollection;
    private static TaskCompletionSource? emptyAnimationStarted;
    private static int nativeProceedInputs;
    private static int nativeMapOpens;
    private static Task? nativeTerminalProceed;
    private static void ObserveChestOpening(NTreasureRoom __instance, Task __result)
    {
        nativeChestOpens++;
        if (ReferenceEquals(__instance, observedChest)) nativeChestOpening = __result;
    }
    private static void ObserveEmptyAnimation(NTreasureRoomRelicCollection __instance)
    {
        if (ReferenceEquals(__instance, observedCollection)) emptyAnimationStarted!.TrySetResult();
    }
    private static void ObserveProceedInput(NTreasureRoom __instance)
    {
        if (ReferenceEquals(__instance, observedChest)) nativeProceedInputs++;
    }
    private static void ObserveMapOpen(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen __instance)
    {
        if (ReferenceEquals(__instance, treasureMap)) nativeMapOpens++;
    }
    private static void ObserveTerminalProceed(Task __result) => nativeTerminalProceed = __result;

    private static async Task ClickTreasureControl(Control button)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        Vector2 at = button.GetGlobalRect().GetCenter();
        Check(button.IsVisibleInTree() && button.GetGlobalRect().HasArea(), "The native input target has a visible engine rectangle.");
        Check(tree.Root.GetVisibleRect().HasPoint(at), $"The native {button.Name} input rectangle {button.GetGlobalRect()} is inside viewport {tree.Root.GetVisibleRect()}.");
        tree.Root.PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        var hovered = tree.Root.GuiGetHoveredControl();
        Check(hovered == button || hovered != null && button.IsAncestorOf(hovered),
            $"Engine hover hits native {button.GetPath()} at {at}, rather than {hovered?.GetPath().ToString() ?? "no control"}; process={button.CanProcess()}, filter={button.MouseFilter}.");
        tree.Root.PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        tree.Root.PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private sealed class TreasureLoadListener : ILoadRunLobbyListener
    {
        internal LoadRunLobby Lobby = null!;
        internal Task? Loading;
        internal RunState? Native;
        public void BeginRun() => Loading = Load();
        private async Task Load()
        {
            Native = RunState.FromSerializable(Lobby.Run);
            await RunManager.Instance.SetUpSavedMultiplayer(Native, Lobby);
            RunManager.Instance.Launch();
            await RunManager.Instance.GenerateMap();
            await RunManager.Instance.LoadIntoLatestMapCoord(null);
        }
#if STS2_PUBLIC
        public void PlayerConnected(ulong playerId) { }
#else
        public void PlayerConnected(LoadRunLobbyPlayer player) { }
#endif
        public void PlayerReadyChanged(ulong playerId) { }
        public void RemotePlayerDisconnected(ulong playerId) { }
        public void LocalPlayerDisconnected(NetErrorInfo info) { }
        public Task<bool> ShouldAllowRunToBegin() => Task.FromResult(true);
    }

    private static Task EmptyTween() => WithTreasure(true, async (session, room) =>
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        Vector2I previousSize = tree.Root.Size;
        var consoleField = AccessTools.Field(typeof(MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole), "_instance");
        object? previousConsole = consoleField.GetValue(null);
        var previousSave = JigsawNativeSaveHost.Save;
        bool previousTesting = TestMode.IsOn;
        var previousAutoSlayer = NonInteractiveMode.AutoSlayerCheck;
        var previousFastMode = MegaCrit.Sts2.Core.Saves.SaveManager.Instance.PrefsSave.FastMode;
        var pools = (System.Collections.IDictionary)AccessTools.Field(typeof(NodePool), "_pools").GetValue(null)!;
        object? previousCardPool = pools[typeof(NCard)];
        bool replacedCardPool = false;
        NodePool<NCard>? cardPool = null;
        JigsawNativeMapUiHost? ui = null;
        NHotkeyManager? hotkeys = null;
        NControllerManager? controller = null;
        MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole? console = null;
        Harmony? host = null;
        NCursorManager? cursor = null;
        MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapDrawings? drawings = null;
        Control? previews = null;
        Control? cardVfx = null;
        NCapstoneContainer? capstone = null;
        NOverlayStack? overlays = null;
        NSceneContainer? rooms = null;
        NTreasureRoom? root = null;
        try
        {
            HeadlessUiHost.EnsureInputActions();
            tree.Root.Size = new Vector2I(1920, 1080);
            ui = new JigsawNativeMapUiHost();
            _ = await ui.Open(session); session.HideMap();
            hotkeys = new NHotkeyManager();
            AccessTools.Property(typeof(MegaCrit.Sts2.Core.Nodes.NGame), "HotkeyManager").SetValue(MegaCrit.Sts2.Core.Nodes.NGame.Instance, hotkeys);
            await HeadlessUiHost.Mount(hotkeys);
            controller = new NControllerManager();
            AccessTools.Property(typeof(NInputManager), "ControllerManager").SetValue(NGame.Instance.InputManager, controller);
            console = new MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole { Visible = false };
            consoleField.SetValue(null, console);
            host = new Harmony("AllMatters.Tests.TreasureScene");
            // Match the existing headless audio fixture: replace only the absent
            // FMOD output device while native button and room callbacks stay real.
            host.Patch(AccessTools.Method(typeof(SfxCmd), nameof(SfxCmd.Play), [typeof(string), typeof(float)]),
                prefix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(SkipHost)));
            host.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Saves.SaveManager), nameof(MegaCrit.Sts2.Core.Saves.SaveManager.SeenFtue)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(SeenTreasureFtue)));
            cursor = new NCursorManager();
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
            drawings = new MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapDrawings();
            drawings.Initialize(RunManager.Instance.NetService, session.Native, RunManager.Instance.InputSynchronizer);
            AccessTools.Property(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen), "Drawings").SetValue(treasureMap, drawings);
            treasureMap.AddChild(drawings);
            var runNode = NRun.Instance!;
            previews = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            runNode.GlobalUi.AddChild(previews);
            AccessTools.Property(typeof(NGlobalUi), "CardPreviewContainer").SetValue(runNode.GlobalUi, previews);
            cardVfx = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            runNode.GlobalUi.AddChild(cardVfx);
            cardVfx.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            AccessTools.Property(typeof(NGlobalUi), "AboveTopBarVfxContainer").SetValue(runNode.GlobalUi, cardVfx);
            capstone = new NCapstoneContainer();
            overlays = new NOverlayStack();
            AccessTools.Property(typeof(NGlobalUi), "MapScreen").SetValue(runNode.GlobalUi, treasureMap);
            AccessTools.Property(typeof(NGlobalUi), "CapstoneContainer").SetValue(runNode.GlobalUi, capstone);
            AccessTools.Property(typeof(NGlobalUi), "Overlays").SetValue(runNode.GlobalUi, overlays);
            AccessTools.Property(typeof(NRun), "ScreenStateTracker").SetValue(runNode, new ScreenStateTracker(treasureMap, capstone, overlays));
            rooms = new NSceneContainer();
            AccessTools.Field(typeof(NRun), "_roomContainer").SetValue(runNode, rooms);
            runNode.AddChild(rooms);
            rooms.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            root = ResourceLoader.Load<PackedScene>("res://scenes/rooms/treasure_room.tscn").Instantiate<NTreasureRoom>();
            AccessTools.Field(typeof(NTreasureRoom), "_room").SetValue(root, room);
            AccessTools.Field(typeof(NTreasureRoom), "_runState").SetValue(root, session.Native);
            root.ProcessMode = Node.ProcessModeEnum.Always;
            AccessTools.Property(typeof(NSceneContainer), "CurrentScene").SetValue(rooms, root);
            host.Patch(AccessTools.Method(typeof(NTreasureRoom), "OpenChest"),
                postfix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(ObserveChestOpening)) { priority = Priority.Last });
            host.Patch(AccessTools.Method(typeof(NTreasureRoomRelicCollection), "AnimIn"),
                postfix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(ObserveEmptyAnimation)));
            host.Patch(AccessTools.Method(typeof(NTreasureRoom),
#if STS2_PUBLIC
                "OnProceedButtonPressed"
#else
                "OnProceedButtonReleased"
#endif
                ), prefix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(ObserveProceedInput)));
            host.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen), "Open"),
                postfix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(ObserveMapOpen)));
            host.Patch(AccessTools.Method(typeof(RunManager),
#if STS2_PUBLIC
                nameof(RunManager.ProceedFromTerminalRewardsScreen)
#else
                nameof(RunManager.ProceedFromTerminalRewardsScreenAsync)
#endif
                ), postfix: new HarmonyMethod(typeof(JigsawHuntingTreasureTests), nameof(ObserveTerminalProceed)) { priority = Priority.Last });
            observedChest = root; nativeChestOpening = null; nativeChestOpens = 0;
            nativeProceedInputs = nativeMapOpens = 0; nativeTerminalProceed = null;
            emptyAnimationStarted = new();
            var io = new MockGodotFileIo("hunting-completed-treasure-input-83");
            var saves = new RunSaveManager(83, io, new MigrationManager(io), forceSynchronous: true);
            int completionSaves = 0;
            string saveStage = "scene setup";
            JigsawNativeSaveHost.Save = async finishedRoom =>
            {
                await SaveTreasurePoint(saves, saveStage, finishedRoom);
                completionSaves++;
            };
            // Use the actual native card scene and pool for Spoils removal;
            // isolate its one visual from any pool owned by another test.
            replacedCardPool = true;
            pools.Remove(typeof(NCard));
            cardPool = NodePool.Init<NCard>("res://scenes/cards/card.tscn", 0);
            rooms.AddChild(root);
            root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(ReferenceEquals(ActiveScreenContext.Instance.GetCurrentScreen(), root), "The native room owner makes the actual chest the active screen, including beta skip-delay checks.");
            var player = session.Native.Players[0];
            var spoils = session.Native.CreateCard<SpoilsMap>(player);
            player.Deck.AddInternal(spoils);
            spoils.SpoilsActIndex = session.Native.CurrentActIndex;
            AccessTools.Property(typeof(SpoilsMap), nameof(SpoilsMap.SpoilsCoord)).SetValue(spoils, session.Native.CurrentMapCoord);
            session.Native.CurrentMapPoint!.AddQuest(spoils);
            int gold = player.Gold;
            var collection = root.GetNode<NTreasureRoomRelicCollection>("%RelicCollection");
            observedCollection = collection;
            Task finished = collection.RelicPickingFinished();
            TestMode.IsOn = false;
            NonInteractiveMode.AutoSlayerCheck = () => false;
            MegaCrit.Sts2.Core.Saves.SaveManager.Instance.PrefsSave.FastMode = FastModeType.Normal;
            saveStage = "chest input/source";
            await ClickTreasureControl(root.GetNode<Control>("%Chest"));
            Check(nativeChestOpens == 1 && nativeChestOpening != null, "Engine chest input invokes the original native OpenChest exactly once.");
            Task open = nativeChestOpening!;
            if (open.IsFaulted) await open;
            await Task.WhenAny(emptyAnimationStarted.Task, open).WaitAsync(TimeSpan.FromSeconds(10));
            if (open.IsFaulted) await open;
            Check(emptyAnimationStarted.Task.IsCompletedSuccessfully, "The original collection AnimIn has actually created its empty Tween before the pending-input checks begin.");
            saveStage = "original empty Tween";
            Check(!finished.IsCompleted && !open.IsCompleted,
                $"The actual native open starts with unfinished empty collection notifications: collection={finished.Status}, open={open.Status}.");
            await tree.ToSignal(tree.CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
            Check(!finished.IsCompleted && !open.IsCompleted, "The empty chest cannot finish before its real delayed Tween callback.");
            Check(!JigsawNativeExploration.NativeFlowFinished(session, 1) && session.Map!.CurrentNode == 1 && !session.Map.Completed.Contains(1),
                "The actual pending OpenChest task keeps its puzzle root incomplete during the real Tween.");
            Check(!root.ProceedButton.IsEnabled, "Normal native waits keep Skip disabled until after the empty Tween, rather than exposing the noninteractive zero-delay branch.");
            int beforeEarlyInput = completionSaves;
            // The disabled native button slides outside the viewport and removes
            // its accept binding. Send the real engine accept action to that path.
            StringName proceedAction =
#if STS2_PUBLIC
                MegaInput.accept;
#else
                MegaInput.confirm;
#endif
            tree.Root.PushInput(new InputEventAction { Action = proceedAction, Pressed = true }, true);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            tree.Root.PushInput(new InputEventAction { Action = proceedAction, Pressed = false }, true);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(!finished.IsCompleted && !open.IsCompleted && session.Map!.CurrentNode == 1 &&
                !session.Map.Completed.Contains(1) && completionSaves == beforeEarlyInput,
                "A real early Proceed action cannot complete the root or write a completion save while the original empty Tween remains pending.");
            await open.WaitAsync(TimeSpan.FromSeconds(10));
            TestMode.IsOn = previousTesting;
            NonInteractiveMode.AutoSlayerCheck = previousAutoSlayer;
            MegaCrit.Sts2.Core.Saves.SaveManager.Instance.PrefsSave.FastMode = previousFastMode;
            Check(finished.IsCompletedSuccessfully && root.ProceedButton.IsEnabled,
                "The native Tween emits empty completion and enables the actual Proceed button.");
            Check(player.Gold == gold && player.Relics.OfType<NlothsHungryFace>().Single().Spent &&
                !player.Deck.Cards.Contains(spoils) && !session.Native.CurrentMapPoint.Quests.Contains(spoils) &&
                session.Native.CurrentMapPointHistoryEntry!.GetEntry(player.NetId).CompletedQuests.Count(id => id == spoils.Id) == 1,
                "The one input-driven chest consumes Face and completes the actual Spoils quest without paying gold.");
            await tree.ToSignal(tree.CreateTimer(0.55), SceneTreeTimer.SignalName.Timeout);
            saveStage = "native Proceed input";
            await ClickTreasureControl(root.ProceedButton);
            Check(nativeProceedInputs == 1 && nativeMapOpens > 0 && nativeTerminalProceed != null,
                $"Engine Proceed input reaches the original room callback, native map consumer and terminal task: callbacks={nativeProceedInputs}, mapOpens={nativeMapOpens}, terminal={nativeTerminalProceed?.Status}.");
            await nativeTerminalProceed!.WaitAsync(TimeSpan.FromSeconds(10));
            for (int frame = 0; frame < 120 && (session.Map!.CurrentNode != null || completionSaves == 0); frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await session.WaitForSave();
            Check(session.Map!.CurrentNode == null && session.Map.Completed.Contains(1) && session.Map.Steps == 1 && completionSaves > 0,
                $"Actual Proceed input naturally completes and saves the root: current={session.Map.CurrentNode}, completed=[{string.Join(',', session.Map.Completed)}], steps={session.Map.Steps}, saves={completionSaves}.");
            var saved = saves.LoadRunSave().SaveData!;
            Check(JigsawCheckpoint.Slot.TryGet(RunState.FromSerializable(saved), out var completedPoint) &&
                completedPoint.Map is { CurrentNode: null, PendingEntry: null, Steps: 1 } && completedPoint.Map.Completed.Contains(1),
                "The naturally written native save contains a completed root rather than a chest awaiting replay.");
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            root.Free();
            // The fresh lobby must resolve its own native screen context. The
            // old input host has finished its save and must stop owning lookup.
            host.Unpatch(AccessTools.PropertyGetter(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen), "Instance"), HarmonyPatchType.Prefix, host.Id);
            host.Unpatch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen), "_Ready"), HarmonyPatchType.Prefix, host.Id);
            host.Unpatch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen), "_Process"), HarmonyPatchType.Prefix, host.Id);
            treasureMap.Free(); treasureMap = null;
            ui.ReleaseSceneOwnership();
            RunManager.Instance.CleanUp(false);
            saveStage = "fresh lobby restore";
            var listener = new TreasureLoadListener();
            var lobby = new LoadRunLobby(new NetSingleplayerGameService(), listener, saved);
            listener.Lobby = lobby;
            try
            {
                lobby.AddLocalHostPlayer();
                lobby.SetReady(true);
                Check(listener.Loading != null, "The actual fresh load lobby begins restoring only through its ready flow.");
                await listener.Loading!.WaitAsync(TimeSpan.FromSeconds(10));
                var restored = listener.Native!;
                var restoredPlayer = restored.Players.Single();
                Check(restored.CurrentRoom is MapRoom && JigsawNativeSession.Current!.Map is { CurrentNode: null, PendingEntry: null, Steps: 1 } recovered && recovered.Completed.Contains(1),
                    "The actual new lobby and native saved-run entry restore exploration without reopening the chest or charging another step.");
                Check(nativeChestOpens == 1 && restoredPlayer.Gold == gold && restoredPlayer.Relics.OfType<NlothsHungryFace>().Single().Spent &&
                    !restoredPlayer.Deck.Cards.OfType<SpoilsMap>().Any() &&
                    restored.CurrentMapPointHistoryEntry!.GetEntry(restoredPlayer.NetId).CompletedQuests.Count(id => id == spoils.Id) == 1,
                    "Lobby restore retains consumed Face and exactly one Spoils completion with no second native chest opening.");
            }
            finally { lobby.CleanUp(disconnectSession: false); }
        }
        finally
        {
            TestMode.IsOn = previousTesting;
            NonInteractiveMode.AutoSlayerCheck = previousAutoSlayer;
            MegaCrit.Sts2.Core.Saves.SaveManager.Instance.PrefsSave.FastMode = previousFastMode;
            try
            {
                if (root != null && GodotObject.IsInstanceValid(root)) root.Free();
                if (cardPool != null)
                {
                    var usedCards = (IEnumerable<NCard>)AccessTools.Field(typeof(NodePool<NCard>), "_usedObjects").GetValue(cardPool)!;
                    foreach (var card in usedCards.Concat(cardPool.DebugFreeObjects).Distinct().ToArray())
                        if (GodotObject.IsInstanceValid(card)) card.Free();
                }
                foreach (Node? node in new Node?[] { drawings, capstone, overlays, treasureMap, rooms, previews, cardVfx, hotkeys, cursor, console, controller })
                    if (node != null && GodotObject.IsInstanceValid(node)) node.Free();
            }
            finally
            {
                if (replacedCardPool)
                {
                    pools.Remove(typeof(NCard));
                    if (previousCardPool != null) pools[typeof(NCard)] = previousCardPool;
                }
                JigsawNativeSaveHost.Save = previousSave;
                observedChest = null; nativeChestOpening = null;
                observedCollection = null; emptyAnimationStarted = null; nativeTerminalProceed = null;
                treasureMap = null;
                consoleField.SetValue(null, previousConsole);
                try { ui?.Dispose(); }
                finally
                {
                    try { if (host != null) host.UnpatchAll(host.Id); }
                    finally { tree.Root.Size = previousSize; }
                }
            }
        }
    }, shouldSave: true);

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
