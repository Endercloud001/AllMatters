using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using HarmonyLib;
using System.Text.Json.Nodes;

internal static class JigsawNativeTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw native backup restores the exact pool and native player from one source", BackupSource),
      ("Jigsaw native act enters real map room before Ancient rewards", EditingBeforeAncient),
      ("Jigsaw native manual map reaches actual Ancient and first room checkpoint", FullFirstRoom),
      ("Jigsaw native treasure rewards complete before map checkpoint", CompletedTreasure),
      ("Jigsaw native partial Ancient restores complete prereward player and map", PartialAncient),
      ("Jigsaw native finished Ancient restore preserves rewards without replay", FinishedAncient),
      ("Jigsaw native failed memory save prevents successful wait until retry", FailedSave),
      ("Jigsaw native missing mode payload rejects a marked native save", MissingModeData),
      ("Jigsaw native console discovery and editable phase reset", ConsoleReset),
      ("Jigsaw native later acts start a fresh pool before preserved Ancient and Boss", LaterAct) ];

    private static async Task LaterAct()
    {
        using var atlases=HeadlessTestSetup.LoadAtlases("card_atlas","relic_atlas");
        var native=RunState.CreateForTest(players:[MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(
            ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(),MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1)],
            seed:"JIGSAW_THREE_NATIVE_ACTS");
        RunManager.Instance.SetUpTest(native,new NetSingleplayerGameService());
        var io=new MockGodotFileIo("Jigsaw-later-acts-memory-only");
        var manager=new RunSaveManager(74,io,new MigrationManager(io),forceSynchronous:true);
        JigsawNativeSaveHost.Save=room=>manager.SaveRun(RunManager.Instance.ToSave(room),false);
        try
        {
            native.ExtraFields.StartedWithNeow=true;
            RunManager.Instance.GenerateRooms();
            Require(native.Acts.Count==3,"Fixture retains all three native default acts.");
            var ancients=native.Acts.Select(a=>a.Ancient.Id).ToArray();
            var bosses=native.Acts.Select(a=>a.BossEncounter.Id).ToArray();
            var session=JigsawNativeSession.Enable(native);
            await RunManager.Instance.EnterAct(0,false);
            var first=session.Map!;
            Require(native.CurrentRoom is MapRoom && first.Phase==JigsawPhase.Editing,"First native EnterAct is intercepted before Neow reward.");
            for(int act=1;act<3;act++)
            {
                await RunManager.Instance.EnterAct(act,false);
                var map=session.Map!;
                Require(native.CurrentActIndex==act && native.CurrentRoom is MapRoom && map.Phase==JigsawPhase.Editing && map.Steps==0 && map.Placements.Count==0,"Later native EnterAct starts an empty editable puzzle before Ancient rewards.");
                var expected=JigsawRun.Create(unchecked((int)native.Rng.Seed+act*7919),15-act,players:[1UL]);
                Require(map.Budget==15-act && !ReferenceEquals(first,map) && map.Pool.SelectMany(p=>p.Nodes).SequenceEqual(expected.Pool.SelectMany(p=>p.Nodes)),"Later act uses its own deterministic full pool and act budget.");
                Require(native.Act.Ancient.Id==ancients[act] && native.Act.BossEncounter.Id==bosses[act] && native.Map.StartingMapPoint.PointType==MegaCrit.Sts2.Core.Map.MapPointType.Ancient,"Native generated Ancient and Boss content remain intact.");
                Require(native.VisitedMapCoords.Count==0 && !session.AncientReady,"Later editing does not award Ancient or enter native map coordinates.");
                await session.Enter(999);
                Require(map.Steps==0 && map.PendingEntry==null,"Rejected native destination in editing cannot charge a step.");
                first=map;
            }
        }
        finally { JigsawNativeSaveHost.Save=null; RunManager.Instance.CleanUp(false); }
    }

    private static async Task MissingModeData()
    {
        var native=RunState.CreateForTest(players:[MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(
            ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(),MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1)],
            acts:[ModelDb.Act<Overgrowth>()],seed:"JIGSAW_MISSING_DATA");
        RunManager.Instance.SetUpTest(native,new NetSingleplayerGameService());
        try
        {
            RunManager.Instance.GenerateRooms(); await RunManager.Instance.GenerateMap();
            await JigsawNativeSession.Enable(native).StartAct();
            var io=new MockGodotFileIo("Jigsaw-missing-extension-memory-only");
            var manager=new RunSaveManager(74,io,new MigrationManager(io),forceSynchronous:true);
            await manager.SaveRun(RunManager.Instance.ToSave(null),false);
            string path=RunSaveManager.GetRunSavePath(74,"current_run.save");
            var json=JsonNode.Parse(io.ReadFile(path) ?? throw new InvalidOperationException("Expected native saved JSON."))!;
            bool RemoveJigsaw(JsonNode node)
            {
                bool removed=false;
                if(node is JsonObject obj)
                {
                    foreach(var property in obj.ToArray())
                        if(property.Key=="jigsaw") { obj.Remove(property.Key); removed=true; }
                        else if(property.Value!=null) removed|=RemoveJigsaw(property.Value);
                }
                else if(node is JsonArray array) foreach(var item in array) if(item!=null) removed|=RemoveJigsaw(item);
                return removed;
            }
            Require(RemoveJigsaw(json),"Preparation must remove the actual jigsaw extension entry from a real native save.");
            io.SetFileContent(path,json.ToJsonString());
            var loaded=manager.LoadRunSave();
            Require(loaded.Success && loaded.SaveData!=null,"A missing extension still leaves structurally valid native JSON.");
            bool rejected=false;
            try { RunState.FromSerializable(loaded.SaveData!); } catch(InvalidDataException) { rejected=true; }
            Require(rejected,"A marked jigsaw run missing its map payload must reject restoration, never silently become a vanilla run.");
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static async Task ConsoleReset()
    {
        JigsawConsoleHistoryHost.Suppress=true;
        // ModManager.Initialize(null,...) in this host skips loading mod metadata.
        // Supply the native loaded-assembly entry, then exercise native discovery.
        var mods=(List<MegaCrit.Sts2.Core.Modding.Mod>)AccessTools.Field(typeof(MegaCrit.Sts2.Core.Modding.ModManager),"_mods").GetValue(null)!;
        var loadedMod=new MegaCrit.Sts2.Core.Modding.Mod
        {
            path=typeof(JigsawResetCommand).Assembly.Location,
            state=MegaCrit.Sts2.Core.Modding.ModLoadState.Loaded,
#if STS2_PUBLIC
            assembly=typeof(JigsawResetCommand).Assembly
#else
            assemblies=[typeof(JigsawResetCommand).Assembly]
#endif
        };
        var cache=AccessTools.Field(typeof(MegaCrit.Sts2.Core.Helpers.ReflectionHelper),"_modTypes");
        object? previousCache=cache.GetValue(null);
        mods.Add(loadedMod); cache.SetValue(null,null);
        try
        {
            var console=new MegaCrit.Sts2.Core.DevConsole.DevConsole(shouldAllowDebugCommands:true);
            Require(console.GetCompletionResults("am_jigsaw_reset").Candidates.Contains("am_jigsaw_reset"),"Actual native console discovery includes the mod reset command.");
        }
        finally { JigsawConsoleHistoryHost.Suppress=false; mods.Remove(loadedMod); cache.SetValue(null,previousCache); }
        var native=RunState.CreateForTest(players:[MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(
            ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(),MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1)],
            acts:[ModelDb.Act<Overgrowth>()],seed:"JIGSAW_CONSOLE_RESET");
        RunManager.Instance.SetUpTest(native,new NetSingleplayerGameService());
        try
        {
            var command=new JigsawResetCommand();
            Require(command.DebugOnly && !command.Process(native.Players[0],[]).success,"Debug restrictions remain native and a vanilla run rejects the command.");
            RunManager.Instance.GenerateRooms(); await RunManager.Instance.GenerateMap();
            var session=JigsawNativeSession.Enable(native); await session.StartAct(); var map=session.Map!;
            var pool=map.Pool;
            Require(map.Take(1,0).Success && map.Drop(1,new(3,0)).Success && map.AddEdge(1,0,1).Success && map.Take(1,1).Success,"Prepare committed layout, retained edge and held preview.");
            Require(!command.Process(native.Players[0],["unexpected"]).success && map.Holdings.Count==1,"Invalid command arguments preserve state.");
            var reset=command.Process(native.Players[0],[]); if(reset.task!=null) await reset.task;
            Require(reset.success && map.Nodes.Count==1 && map.Edges.Count==0 && map.Holdings.Count==0 && ReferenceEquals(pool,map.Pool),"Actual command resets only editable MapRoom and keeps the original pool.");
            Require(command.Process(native.Players[0],[]).success,"Repeated reset is harmless.");
            Require(map.Take(1,0).Success && map.Drop(1,new(3,0)).Success && map.AddEdge(1,0,1).Success && map.ChooseEnd(1,EndChoice.Manual).Success,"Map remains editable after reset.");
            Require(!command.Process(native.Players[0],[]).success && map.Placements.Count==1,"Authorized inspection rejects command without changing the layout.");
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static async Task EditingBeforeAncient()
    {
        var native = RunState.CreateForTest(
            players: [MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(
                ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(), MegaCrit.Sts2.Core.Unlocks.UnlockState.all, 1)],
            acts: [ModelDb.Act<Overgrowth>()], seed: "JIGSAW_NATIVE_ENTRY");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        try
        {
            var session = JigsawNativeSession.Enable(native);
            native.ExtraFields.StartedWithNeow = true;
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            await RunManager.Instance.EnterMapCoord(native.Map.StartingMapPoint.coord);
            Require(native.CurrentRoom is MapRoom && session.Map?.Phase == JigsawPhase.Editing,
                "A real native map room must hold editing before any Ancient reward.");
            Require(native.VisitedMapCoords.Count == 0 && session.Map!.Steps == 0,
                "Starting the editing phase cannot enter a reward room or consume a step.");
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static Task FullFirstRoom()=>FirstRoomFlow(completeTreasure:false);
    private static Task CompletedTreasure()=>FirstRoomFlow(completeTreasure:true);
    private static Task PartialAncient()=>FirstRoomFlow(false,partialAncient:true);
    private static Task FinishedAncient()=>FirstRoomFlow(false,finishedAncient:true);

    private static async Task FailedSave()
    {
        var native=(RunState)HeadlessTestSetup.PlayerWithRun("JIGSAW_FAILED_SAVE").RunState;
        RunManager.Instance.SetUpTest(native,new NetSingleplayerGameService());
        try
        {
            var session=JigsawNativeSession.Enable(native);
            var io=new MockGodotFileIo("Jigsaw-failed-save-memory-only");
            var manager=new RunSaveManager(74,io,new MigrationManager(io),forceSynchronous:true);
            io.ShouldFailWrites=true;
            bool writeFailed=false;
            try { await manager.SaveRun(RunManager.Instance.ToSave(null),false); }
            catch(InvalidOperationException error) when(error.Message=="Simulated write failure") { writeFailed=true; }
            Require(writeFailed,"The native memory IO must actually reject the injected write.");
            bool rejected=false;
            try { await session.WaitForSave(); } catch(IOException) { rejected=true; }
            Require(rejected,"A real failed native memory write must make WaitForSave reject exit success.");
            io.ShouldFailWrites=false;
            await manager.SaveRun(RunManager.Instance.ToSave(null),false);
            await session.WaitForSave();
            Require(manager.LoadRunSave().Success,"A later successful native write clears the failure and remains loadable.");
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static async Task FirstRoomFlow(bool completeTreasure,bool partialAncient=false,bool finishedAncient=false)
    {
        using var atlases=HeadlessTestSetup.LoadAtlases("card_atlas","relic_atlas");
        using var eventScope=HeadlessCombatInProgress.EnterEvent();
        var native=RunState.CreateForTest(players:[MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(
            ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(),MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1)],
            acts:[ModelDb.Act<Overgrowth>()],seed:
#if STS2_PUBLIC
            "JIGSAW_FIRST_CHEST_0"
#else
            "JIGSAW_FIRST_CHEST_6"
#endif
            );
        RunManager.Instance.SetUpTest(native,new NetSingleplayerGameService());
        var io=new MockGodotFileIo("Jigsaw-first-room-memory-only");
        var manager=new RunSaveManager(74,io,new MigrationManager(io),forceSynchronous:true);
        // The real EventRoom completion requests SaveManager directly. Redirect only
        // filesystem ownership to an isolated native manager; serialization stays real.
        JigsawNativeSaveHost.Save=room=>manager.SaveRun(RunManager.Instance.ToSave(room),false);
        try
        {
            native.ExtraFields.StartedWithNeow=true;
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            var session=JigsawNativeSession.Enable(native);
            await RunManager.Instance.EnterMapCoord(native.Map.StartingMapPoint.coord);
            var map=session.Map!;
            Require(native.CurrentRoom is MapRoom && map.Phase==JigsawPhase.Editing,"Real native Ancient entry is intercepted before rewards.");
            Require(map.Take(1,0).Success && map.Drop(1,new(3,0)).Success && map.AddEdge(1,0,1).Success && map.ChooseEnd(1,EndChoice.Manual).Success,"The actual generated pool admits its partial manual witness.");
            await session.Inspect();
            Require(native.CurrentRoom is EventRoom && map.Phase==JigsawPhase.Ancient && !session.AncientReady,
                $"Manual success enters the real native Ancient reward room. Actual room={native.CurrentRoom?.GetType().Name}, phase={map.Phase}, ready={session.AncientReady}, eventFinished={RunManager.Instance.EventSynchronizer.GetLocalEvent()?.IsFinished}, options={RunManager.Instance.EventSynchronizer.GetLocalEvent()?.CurrentOptions.Count}.");
            var ancient=RunManager.Instance.EventSynchronizer.GetLocalEvent();
            Require(ancient is AncientEventModel && ancient.CurrentOptions.Count>0,"Native Ancient exposes real personal reward options.");
            Console.WriteLine("JIGSAW Ancient options: "+string.Join(",",ancient.CurrentOptions.Select(o=>o.Relic?.Id.Entry ?? o.TextKey)));
            if(partialAncient)
            {
                var player=native.Players[0];
                int originalGold=player.Gold;
                string originalDeck=System.Text.Json.JsonSerializer.Serialize(player.ToSerializable());
                var originalRng=HeadlessTestSetup.RngPosition(native.Rng.Niche);
                // Emulate a save between real side effects and event completion;
                // no EventModel completion or domain completion is synthesized.
                await MegaCrit.Sts2.Core.Commands.PlayerCmd.GainGold(27,player);
                MegaCrit.Sts2.Core.Commands.CardCmd.Upgrade(player.Deck.Cards[0]);
                await MegaCrit.Sts2.Core.Commands.RelicCmd.Obtain(ModelDb.Relic<MegaCrit.Sts2.Core.Models.Relics.GoldenPearl>().ToMutable(),player);
                native.Rng.Niche.NextInt(100);
                await JigsawNativeSaveHost.Save(null);
                var partial=manager.LoadRunSave();
                Require(partial.Success && partial.SaveData!=null,"Partial Ancient native checkpoint parses.");
                var rollback=RunState.FromSerializable(partial.SaveData!);
                Require(rollback.Players[0].Gold==originalGold && System.Text.Json.JsonSerializer.Serialize(rollback.Players[0].ToSerializable())==originalDeck && HeadlessTestSetup.RngPosition(rollback.Rng.Niche)==originalRng,"Partial Ancient restores original native player gold/deck/relics and RNG together.");
                Require(JigsawCheckpoint.Slot.TryGet(rollback,out var partialMap) && partialMap.Map is {Phase:JigsawPhase.Ancient,Steps:0} && partialMap.Map.AncientCompleted.Length==0 && partialMap.Map.Placements.Length==1,"Partial Ancient rollback keeps the successful layout and incomplete reward phase.");
                return;
            }
            var option=ancient.CurrentOptions.Single(o=>o.Relic?.Id.Entry==
#if STS2_PUBLIC
                "FISHING_ROD"
#else
                "POMANDER"
#endif
            );
            using(MegaCrit.Sts2.Core.Commands.CardSelectCmd.UseSelector(new NativeEventTests.Select(native.Players[0].Deck.Cards.First()))) await option.Chosen();
            Require(ancient.IsFinished && session.AncientReady,"Real native option completion, rather than a direct domain completion call, authorizes Continue.");
            if(finishedAncient)
            {
                string expectedPlayer=System.Text.Json.JsonSerializer.Serialize(native.Players[0].ToSerializable());
                await JigsawNativeSaveHost.Save(native.CurrentRoom);
                var finished=manager.LoadRunSave();
                Require(finished.Success && finished.SaveData!=null,"Finished Ancient checkpoint parses.");
                RunManager.Instance.CleanUp(false);
                var finishedRun=RunState.FromSerializable(finished.SaveData!);
                await RunManager.Instance.SetUpSavedSingleplayer(finishedRun,finished.SaveData!);
                MegaCrit.Sts2.Core.Context.LocalContext.NetId=finishedRun.Players[0].NetId;
                await RunManager.Instance.GenerateMap();
                await RunManager.Instance.LoadIntoLatestMapCoord(null);
                var finishedSession=JigsawNativeSession.Current!;
                Require(finishedSession.AncientReady && finishedSession.Map?.Phase==JigsawPhase.Ancient && System.Text.Json.JsonSerializer.Serialize(finishedRun.Players[0].ToSerializable())==expectedPlayer,"Actual finished Ancient restore keeps native rewards and does not replay them.");
                Require(finishedRun.CurrentRoom is not EventRoom finishedEvent || finishedEvent.LocalMutableEvent.IsFinished,"Finished Ancient restore cannot expose fresh reward choices.");
                await finishedSession.Continue();
                Require(finishedSession.Map?.Phase==JigsawPhase.Exploring,"Restored Ancient rewards require a fresh Continue then allow exploration.");
                return;
            }
            await session.Continue();
            Require(map.Phase==JigsawPhase.Exploring && map.Steps==0,"Continue begins exploration without charging Ancient.");
            await session.Enter(1);
            Console.WriteLine("JIGSAW first native room: "+native.CurrentRoom?.GetType().Name+"; logical room: "+map.Nodes.Single(n=>n.Id==1).Room);
            Require(native.CurrentRoom is not (null or MapRoom or EventRoom { CanonicalEvent:AncientEventModel }) && map.Steps==1 && map.FirstProgress==1 && map.CurrentNode==1,"Real native readiness confirms exactly one first-room entry.");
            var loaded=manager.LoadRunSave();
            Require(loaded.Success && loaded.SaveData!=null,"The actual isolated native first-room save must parse.");
            // Use native's own before-roll save, not a post-readiness snapshot with
            // already-consumed room RNG. Restart through the production load path.
            var expectedRelics=RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics!.Select(r=>r.Id).ToArray();
            int expectedGold=native.Players[0].Gold;
            RunManager.Instance.CleanUp(false);
            var restored=RunState.FromSerializable(loaded.SaveData!);
            Require(JigsawNativeSession.Current==null,"Deserialization alone does not replace the active native run.");
            await RunManager.Instance.SetUpSavedSingleplayer(restored,loaded.SaveData!);
            MegaCrit.Sts2.Core.Context.LocalContext.NetId=restored.Players[0].NetId;
            await RunManager.Instance.GenerateMap();
            await RunManager.Instance.LoadIntoLatestMapCoord(null);
            var resumed=JigsawNativeSession.Current!; var resumedMap=resumed.Map!;
            Require(restored.CurrentRoom is TreasureRoom && resumedMap.Steps==1 && resumedMap.FirstProgress==1 && resumedMap.CurrentNode==1 && resumedMap.PendingEntry==null,"Actual native reload confirms the pending first room exactly once.");
            Require(restored.Players[0].Gold==expectedGold && RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics!.Select(r=>r.Id).SequenceEqual(expectedRelics),"Reload preserves the exact original native room roll and Ancient reward state.");
            resumed.ConfirmRestoredEntry();
            Require(resumedMap.Steps==1,"Repeated restored readiness cannot charge another step.");
            resumed.OnEdited(); await resumed.WaitForSave();
            var unchangedEntrySave=manager.LoadRunSave();
            var unchangedEntryRun=RunState.FromSerializable(unchangedEntrySave.SaveData!);
            Require(JigsawCheckpoint.Slot.TryGet(unchangedEntryRun,out var entryCheckpoint) && entryCheckpoint.Map is {CurrentNode:null,PendingEntry:not null},"Opening help during an active room must preserve native's pre-roll entry checkpoint, not save consumed room RNG.");
            if(completeTreasure)
            {
                using var mapScreen=new MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen();
                mapScreen.Open();
                Require(resumedMap.CurrentNode==1 && !resumedMap.Completed.Contains(1),"Opening the map before native chest rewards are finished cannot complete the room.");
                var treasure=(TreasureRoom)restored.CurrentRoom!;
                int gold=restored.Players[0].Gold;
                int rewardGold=await treasure.DoNormalRewards();
                await treasure.DoExtraRewardsIfNeeded();
                Require(restored.Players[0].Gold==gold+rewardGold,"Real native chest reward commands apply the rolled gold exactly once.");
                // Deliver the actual accepted native skip action. The headless host
                // omits the UI/action-loop pump, so execute this queued action directly.
                var skip=new MegaCrit.Sts2.Core.GameActions.PickRelicAction(restored.Players[0],null);
                skip.OnEnqueued(_=>{},1);
                await skip.Execute();
                await RunManager.Instance.ProceedFromTerminalRewardsScreen();
                mapScreen.Open();
                Require(resumedMap.CurrentNode==null && resumedMap.Completed.Contains(1) && resumedMap.Steps==1,"Native skip/proceed/map completion marks the first room complete without another step.");
                await resumed.WaitForSave();
                await JigsawNativeSaveHost.Save(null);
                var completedSave=manager.LoadRunSave();
                Require(completedSave.Success && completedSave.SaveData!=null,"Completed native room checkpoint parses.");
                var completedRun=RunState.FromSerializable(completedSave.SaveData!);
                Require(JigsawCheckpoint.Slot.TryGet(completedRun,out var checkpoint) && checkpoint.Map is {Steps:1,FirstProgress:1,CurrentNode:null} && checkpoint.Map.Completed.Contains(1),"Serialized completed-room checkpoint preserves completion and exactly one entry.");
                mapScreen.Free();
                RunManager.Instance.CleanUp(false);
                await RunManager.Instance.SetUpSavedSingleplayer(completedRun,completedSave.SaveData!);
                MegaCrit.Sts2.Core.Context.LocalContext.NetId=completedRun.Players[0].NetId;
                await RunManager.Instance.GenerateMap();
                await RunManager.Instance.LoadIntoLatestMapCoord(null);
                Require(completedRun.CurrentRoom is MapRoom && JigsawNativeSession.Current?.Map is {Steps:1,CurrentNode:null},"Actual completed-room reload returns to MapRoom without rerolling treasure or rewarding twice.");
            }
        }
        finally { JigsawNativeSaveHost.Save=null; RunManager.Instance.CleanUp(false); }
    }

    private static async Task BackupSource()
    {
        var native = (RunState)HeadlessTestSetup.PlayerWithRun("JIGSAW_CHECKPOINT").RunState;
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        try
        {
            var io = new MockGodotFileIo("Jigsaw-isolated-memory-only");
            var manager = new RunSaveManager(74, io, new MigrationManager(io), forceSynchronous: true);
            string path = RunSaveManager.GetRunSavePath(74, "current_run.save");
            native.Players[0].Gold = 111;
            var first = JigsawRun.Create(74);
            JigsawCheckpoint.Capture(native, first);
            await manager.SaveRun(RunManager.Instance.ToSave(null), false);
            native.Players[0].Gold = 222;
            JigsawCheckpoint.Capture(native, JigsawRun.Create(75));
            await manager.SaveRun(RunManager.Instance.ToSave(null), false);
            io.DeleteFile(path);
            var loaded = manager.LoadRunSave();
            Require(loaded.Success && loaded.SaveData != null, "Native backup must load.");
            var restored = RunState.FromSerializable(loaded.SaveData!);
            Require(restored.Players[0].Gold == 111, "Native selected the backup player.");
            Require(JigsawCheckpoint.Slot.TryGet(restored, out var saved) && saved.Map != null,
                "The actual backup's jigsaw payload must survive native loading.");
            Require(saved.Map!.Pool.SequenceEqual(first.CreateSave().Pool,
                System.Collections.Generic.EqualityComparer<JigsawPiece>.Create((a,b) =>
                    a!.Id == b!.Id && a.Nodes.SequenceEqual(b.Nodes), p => p.Id)),
                "The backup must restore its original pool, not the current primary or a new pool.");
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new Exception(message); }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Saves.SaveManager),nameof(MegaCrit.Sts2.Core.Saves.SaveManager.SaveRun))]
internal static class JigsawNativeSaveHost
{
    internal static Func<AbstractRoom?,Task>? Save;
    private static bool Prefix(AbstractRoom? preFinishedRoom,ref Task __result)
    {
        if(Save==null) return true;
        __result=Save(preFinishedRoom); return false;
    }
}

// Console discovery is real; reading an account console-history file is unrelated
// to command registration and remains outside the isolated test's IO boundary.
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.DevConsole.DevConsole),"LoadCommandHistory")]
internal static class JigsawConsoleHistoryHost
{
    internal static bool Suppress;
    private static bool Prefix()=>!Suppress;
}
