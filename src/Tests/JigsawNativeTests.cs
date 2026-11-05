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
    private enum EntryFailureKind { None, BeforeReady, RestoredBeforeReady, AfterReady, RetryAfterReady, BeforeSave }
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw native saves serialize pending native writes and exit waits latest", SerializedWrites),
      ("Jigsaw native queued successful save recovers prior write failure", ()=>SerializedWrites(true)),
      ("Jigsaw native pending edits merge while in-flight checkpoint stays intact", CoalescedEdits),
      ("Jigsaw native pending editor retry survives earlier edit write failure", ()=>CoalescedEdits(false, failFirst:true)),
      ("Jigsaw native successful native retry supersedes failed editor save", ()=>CoalescedEdits(false, failFirst:true, nativeRetry:true)),
      ("Jigsaw native old pending edits cannot save a replacement run", ()=>CoalescedEdits(true)),
      ("Jigsaw native checkpoint recovery fault matrix", CheckpointFaultMatrix),
      ("Jigsaw native backup restores the exact pool and native player from one source", BackupSource),
      ("Jigsaw native act enters real map room before Ancient rewards", EditingBeforeAncient),
      ("Jigsaw native automatic enters actual Ancient from saved authorized operation", Automatic),
      ("Jigsaw native automatic routed input remains responsive and recovers worker exception", ()=>AutomaticUi()),
      ("Jigsaw native automatic late old session cannot commit", ()=>AutomaticUi(stale:true)),
      ("Jigsaw native automatic late final success save cannot enter replacement run", ()=>LateFinalSave(false)),
      ("Jigsaw native automatic late final failure save cannot alter replacement run", ()=>LateFinalSave(true)),
      ("Jigsaw native automatic failed final save preserves accepted map and retries", ()=>LateFinalSave(false,failWrite:true)),
      ("Jigsaw native authorized process producer", AuthorizedProducer),
      ("Jigsaw native authorized process consumer", AuthorizedConsumer),
      ("Jigsaw native manual map reaches actual Ancient and first room checkpoint", FullFirstRoom),
      ("Jigsaw native treasure rewards complete before map checkpoint", CompletedTreasure),
      ("Jigsaw native partial Ancient restores complete prereward player and map", PartialAncient),
      ("Jigsaw native actual Ancient reward callback rolls back then replays once", ()=>FirstRoomFlow(false, actualPartial:true)),
      ("Jigsaw native failed real room load retries same destination and content once", ()=>FirstRoomFlow(false, entryFailure:EntryFailureKind.BeforeReady)),
      ("Jigsaw native failed room save and repeated restored failure retain entry", ()=>FirstRoomFlow(false, entryFailure:EntryFailureKind.RestoredBeforeReady)),
      ("Jigsaw native failure after readiness retains confirmed entry once", ()=>FirstRoomFlow(false, entryFailure:EntryFailureKind.AfterReady)),
      ("Jigsaw native retried readiness survives a later native callback failure", ()=>FirstRoomFlow(false, entryFailure:EntryFailureKind.RetryAfterReady)),
      ("Jigsaw native entry failure before native save retries uncharged destination", ()=>FirstRoomFlow(false, entryFailure:EntryFailureKind.BeforeSave)),
      ("Jigsaw native finished Ancient restore preserves rewards without replay", FinishedAncient),
      ("Jigsaw native failed memory save prevents successful wait until retry", FailedSave),
      ("Jigsaw native missing mode payload rejects a marked native save", ()=>MissingModeData()),
      ("Jigsaw native missing phase rejects incomplete checkpoint", ()=>MissingModeData("Phase")),
      ("Jigsaw native missing checkpoint fields reject partial documents", MissingCheckpointFields),
      ("Jigsaw native console discovery and editable phase reset", ConsoleReset),
      ("Jigsaw native later acts start a fresh pool before preserved Ancient and Boss", LaterAct) ];

    private static async Task Automatic()
    {
        using var atlases=HeadlessTestSetup.LoadAtlases("card_atlas","relic_atlas");
        var native=RunState.CreateForTest(players:[MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(),MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1)],acts:[ModelDb.Act<Overgrowth>()],seed:"AUTOMATIC_75");
        RunManager.Instance.SetUpTest(native,new NetSingleplayerGameService(),shouldSave:true);
        var io=new MockGodotFileIo("automatic-75-memory-only");
        var manager=new RunSaveManager(75,io,new MigrationManager(io),forceSynchronous:true);
        JigsawNativeSaveHost.Save=room=>manager.SaveRun(RunManager.Instance.ToSave(room),false);
        try
        {
            native.ExtraFields.StartedWithNeow=true;RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            var session=JigsawNativeSession.Enable(native);
            await RunManager.Instance.EnterMapCoord(native.Map.StartingMapPoint.coord);
            var original=System.Text.Json.JsonSerializer.Serialize(session.Map!.Pool);
            var watch=System.Diagnostics.Stopwatch.StartNew();
            var generation=session.Automatic();int frames=0;
            var tree=(Godot.SceneTree)Godot.Engine.GetMainLoop();
            while(!generation.IsCompleted) {await tree.ToSignal(tree,Godot.SceneTree.SignalName.ProcessFrame);frames++;}
            await generation;
            Require(frames>0,"The Godot scene tree continues processing frames during actual C# generation.");
            Require(session.Map.Phase==JigsawPhase.Ancient && native.CurrentRoom is EventRoom,"Automatic accepted map reaches the actual native Ancient callback.");
            Require(System.Text.Json.JsonSerializer.Serialize(session.Map.Pool)==original && session.Map.Steps==0,"Automatic neither rerolls the original pool nor charges entry.");
            var loaded=manager.LoadRunSave();
            Require(loaded.Success && loaded.SaveData!=null,"Automatic native save parses.");
            Console.WriteLine($"AUTOMATIC_75 native elapsed={watch.ElapsedMilliseconds}ms; frames={frames}; pieces={session.Map.Placements.Count}; algorithm={session.Map.AutomaticOperation?.Algorithm}; budget={session.Map.AutomaticOperation?.ExpansionBudget}; host=Godot/shared-beta-resources");
        }
        finally { JigsawNativeSaveHost.Save=null;RunManager.Instance.CleanUp(false); }
    }
    private static string RecoveryExchange => Environment.GetEnvironmentVariable("ALLMATTERS_75_RECOVERY")??Path.Combine(Path.GetTempPath(),$"AllMatters-75-{Environment.ProcessId}.json");
    private static async Task LateFinalSave(bool failure,bool failWrite=false)
    {
        var native=RunState.CreateForTest(players:[MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(),MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1)],acts:[ModelDb.Act<Overgrowth>()],seed:"FINAL_SAVE_75");
        RunManager.Instance.SetUpTest(native,new NetSingleplayerGameService(),shouldSave:true);
        var io=new MockGodotFileIo("final-save-memory-only");
        var manager=new RunSaveManager(75,io,new MigrationManager(io),forceSynchronous:true);
        var reached=new TaskCompletionSource();var release=new TaskCompletionSource();
        var session=JigsawNativeSession.Enable(native);
        JigsawAutomaticWorker.TestOverride=(map,_)=>Task.FromResult(failure?null:map.AutomaticWitness);
        JigsawNativeSaveHost.Save=async room=>
        {
            var captured=RunManager.Instance.ToSave(room);
            if(session.Map is { AutomaticOperation:not null, Phase:not JigsawPhase.Generating })
            {reached.TrySetResult();await release.Task;}
            await manager.SaveRun(captured,false);
        };
        try
        {
            native.ExtraFields.StartedWithNeow=true;RunManager.Instance.GenerateRooms();await RunManager.Instance.GenerateMap();
            await RunManager.Instance.EnterMapCoord(native.Map.StartingMapPoint.coord);
            var generation=session.Automatic();await reached.Task;
            if(failWrite)
            {
                io.ShouldFailWrites=true;release.SetResult();
                bool rejected=false;try {await generation;} catch {rejected=true;}
                Require(rejected && session.Map!.Phase==JigsawPhase.Ancient && native.CurrentRoom is MapRoom,"Failed final save retains the accepted map and does not enter rewards.");
                var layout=session.Map!.Placements.ToArray();io.ShouldFailWrites=false;
                await session.Continue();
                Require(native.CurrentRoom is EventRoom && session.Map.Placements.SequenceEqual(layout),"Retry saves the same accepted map and enters actual Ancient without regenerating.");
                return;
            }
            RunManager.Instance.CleanUp(false);
            var replacement=(RunState)HeadlessTestSetup.PlayerWithRun("REPLACEMENT_FINAL_SAVE_75").RunState;
            RunManager.Instance.SetUpTest(replacement,new NetSingleplayerGameService());
            var current=JigsawNativeSession.Enable(replacement);
            release.SetResult();await generation;
            Require(current.Map==null&&replacement.CurrentRoom==null,"A late final save cannot enter Ancient or alter the replacement run.");
        }
        finally {release.TrySetResult();JigsawAutomaticWorker.TestOverride=null;JigsawNativeSaveHost.Save=null;RunManager.Instance.CleanUp(false);}
    }
    private static async Task AuthorizedProducer()
    {
        var native=RunState.CreateForTest(players:[MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(),MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1)],acts:[ModelDb.Act<Overgrowth>()],seed:"PROCESS_RECOVERY_75");
        RunManager.Instance.SetUpTest(native,new NetSingleplayerGameService());
        var io=new MockGodotFileIo("authorized-process-memory-only");
        var manager=new RunSaveManager(75,io,new MigrationManager(io),forceSynchronous:true);
        try
        {
            native.ExtraFields.StartedWithNeow=true;RunManager.Instance.GenerateRooms();await RunManager.Instance.GenerateMap();
            var session=JigsawNativeSession.Enable(native);await RunManager.Instance.EnterMapCoord(native.Map.StartingMapPoint.coord);
            Require(session.Map!.ChooseEnd(1,EndChoice.Automatic).Success,"Authorize before the process boundary.");
            JigsawCheckpoint.Capture(native,session.Map);
            await manager.SaveRun(RunManager.Instance.ToSave(null),false);
            File.WriteAllText(RecoveryExchange,io.ReadFile(RunSaveManager.GetRunSavePath(75,"current_run.save"))!);
            Console.WriteLine($"AUTHORIZED_PRODUCER process={Environment.ProcessId}; target={RecoveryExchange}");
        }
        finally {RunManager.Instance.CleanUp(false);}
    }
    private static async Task AuthorizedConsumer()
    {
        using var atlases=HeadlessTestSetup.LoadAtlases("card_atlas","relic_atlas");
        var io=new MockGodotFileIo("authorized-process-memory-only");
        var manager=new RunSaveManager(75,io,new MigrationManager(io),forceSynchronous:true);
        var nativePath=RunSaveManager.GetRunSavePath(75,"current_run.save");
        io.WriteFile(nativePath,File.ReadAllText(RecoveryExchange));
        JigsawNativeSaveHost.Save=room=>manager.SaveRun(RunManager.Instance.ToSave(room),false);
        try
        {
            var save=manager.LoadRunSave();Require(save.Success && save.SaveData!=null,"New process parses the actual native container.");
            var restored=RunState.FromSerializable(save.SaveData!);
            await RunManager.Instance.SetUpSavedSingleplayer(restored,save.SaveData!);
            MegaCrit.Sts2.Core.Context.LocalContext.NetId=restored.Players[0].NetId;
            var session=JigsawNativeSession.Current!;var map=session.Map!;
            Require(map.Phase==JigsawPhase.Generating && map.AutomaticOperation!=null,"Saved authorization is restored without another vote.");
            var expected=JigsawAutomatic.Solve(map,map.AutomaticOperation!.ExpansionBudget)!;
            await RunManager.Instance.GenerateMap();await RunManager.Instance.LoadIntoLatestMapCoord(null);
            Require(map.Phase==JigsawPhase.Ancient && map.Placements.SequenceEqual(expected.Placements) && restored.CurrentRoom is EventRoom,"Actual native load reruns the exact saved operation and enters Ancient.");
            Console.WriteLine($"AUTHORIZED_CONSUMER process={Environment.ProcessId}; pieces={map.Placements.Count}; algorithm={map.AutomaticOperation.Algorithm}");
        }
        finally {JigsawNativeSaveHost.Save=null;RunManager.Instance.CleanUp(false);File.Delete(RecoveryExchange);}
    }
    private static async Task AutomaticUi(bool stale=false)
    {
        using var atlases=HeadlessTestSetup.LoadAtlases("card_atlas","relic_atlas","ui_atlas");
        var native=RunState.CreateForTest(players:[MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(),MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1)],acts:[ModelDb.Act<Overgrowth>()],seed:"AUTOMATIC_UI_75");
        RunManager.Instance.SetUpTest(native,new NetSingleplayerGameService());
        var worker=new TaskCompletionSource<JigsawLayout?>();
        var tree=(Godot.SceneTree)Godot.Engine.GetMainLoop();
        AllMatters.Jigsaw.Presentation.JigsawScreen? view=null;
        var probe=new JigsawUnhandledInputProbe();
        var priorModal=MegaCrit.Sts2.Core.Nodes.CommonUi.NModalContainer.Instance;
        var modal=new MegaCrit.Sts2.Core.Nodes.CommonUi.NModalContainer();
        modal.AddChild(new Godot.ColorRect{Name="Backstop"});
        try
        {
            var session=JigsawNativeSession.Enable(native);
            native.ExtraFields.StartedWithNeow=true;RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();await RunManager.Instance.EnterMapCoord(native.Map.StartingMapPoint.coord);
            var map=session.Map!;
            var original=map.CreateSave();
            JigsawAutomaticWorker.TestOverride=(_,_)=>worker.Task;
            HeadlessUiHost.EnsureInputActions();
            view=session.CreateScreen(new AllMatters.Jigsaw.Presentation.MemoryJigsawTutorialProfile{Sentence=6});
            await HeadlessUiHost.Mount(modal,probe,view);
            view.SwitchZone(-1);view.Navigate(1);
            Godot.Input.ParseInputEvent(new Godot.InputEventAction{Action=MegaCrit.Sts2.Core.ControllerInput.MegaInput.select,Pressed=true});
            for(int i=0;i<4;i++)await tree.ToSignal(tree,Godot.SceneTree.SignalName.ProcessFrame);
            Require(map.Phase==JigsawPhase.Generating && view.IsGenerating,"Engine input enters the production automatic callback and shows the waiting screen.");
            var pending=session.ResumeAutomatic();
            int before=probe.Events;
            Godot.Input.ParseInputEvent(new Godot.InputEventAction{Action=MegaCrit.Sts2.Core.ControllerInput.MegaInput.cancel,Pressed=true});
            for(int i=0;i<4;i++)await tree.ToSignal(tree,Godot.SceneTree.SignalName.ProcessFrame);
            Require(map.Phase==JigsawPhase.Generating && probe.Events==before,"Frames and engine input continue while the worker waits, with no underlying input leak.");
            if(stale)
            {
                RunManager.Instance.CleanUp(false);
                var replacement=(RunState)HeadlessTestSetup.PlayerWithRun("NEW_SESSION_75").RunState;
                RunManager.Instance.SetUpTest(replacement,new NetSingleplayerGameService());
                var current=JigsawNativeSession.Enable(replacement);
                worker.SetResult(JigsawAutomatic.Solve(map,0));await pending;
                Require(current.Map==null && map.Phase==JigsawPhase.Generating && map.Placements.SequenceEqual(original.Placements),"Late old-session success cannot alter either the current session or the old original map.");
                return;
            }
            worker.SetException(new InvalidOperationException("injected worker failure"));
            await pending;
            Require(map.Phase==JigsawPhase.Editing && map.Choices.Count==0 && !view.IsGenerating && view.DialogText.Contains("已保留原布局"),"Exception clears the waiting cover and votes and presents the recovery sentence.");
            Require(map.Placements.SequenceEqual(original.Placements)&&map.Edges.SequenceEqual(original.ExternalEdges),"Worker exception preserves the original layout.");
        }
        finally
        {
            JigsawAutomaticWorker.TestOverride=null;RunManager.Instance.CleanUp(false);view?.Free();probe.Free();modal.Free();
            AccessTools.Property(typeof(MegaCrit.Sts2.Core.Nodes.CommonUi.NModalContainer),"Instance").SetValue(null,priorModal);
        }
    }

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

    private static async Task MissingCheckpointFields()
    {
        foreach (string field in new[] { "Budget", "Steps", "FirstProgress", "CurrentNode", "PendingEntry", "NextEntry", "AncientCompleted", "Players", "Pool", "Placements", "ExternalEdges", "Visited", "Completed", "GenerationRandomState" })
            await MissingModeData(field);
        foreach (string field in new[] { "Version", "Act", "Map" }) await MissingModeData(field, header: true);
        await MissingModeData("Version", header: true, unsupportedVersion: true);
    }

    private static async Task MissingModeData(string? missingField=null, bool header=false, bool unsupportedVersion=false)
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
                        if (property.Key == "jigsaw")
                        {
                            if (missingField == null) obj.Remove(property.Key);
                            else
                            {
                                var data = property.Value!["data"]!.AsObject();
                                if (unsupportedVersion) data["Version"] = 999;
                                else Require((header ? data : data["Map"]!.AsObject()).Remove(missingField), "Preparation removes actual persisted field.");
                            }
                            removed = true;
                        }
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
            try { RunState.FromSerializable(loaded.SaveData!); } catch(Exception error) when(error is InvalidDataException or ArgumentException) { rejected=true; }
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

    private static async Task FirstRoomFlow(bool completeTreasure,bool partialAncient=false,bool finishedAncient=false,bool actualPartial=false,EntryFailureKind entryFailure=EntryFailureKind.None)
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
            string beforeReward = System.Text.Json.JsonSerializer.Serialize(native.Players[0].ToSerializable());
            MegaCrit.Sts2.Core.Saves.SerializableRun? interrupted = null;
            if (actualPartial) JigsawAncientRewardBoundaryHost.BeforeDone = current =>
            {
                if (!ReferenceEquals(current, ancient)) return;
                Require(!current.IsFinished && !session.AncientReady &&
                    System.Text.Json.JsonSerializer.Serialize(native.Players[0].ToSerializable()) != beforeReward,
                    "The actual native option has applied effects before event completion.");
                interrupted = RunManager.Instance.ToSave(null);
            };
            var option=ancient.CurrentOptions.Single(o=>o.Relic?.Id.Entry==
#if STS2_PUBLIC
                "FISHING_ROD"
#else
                "POMANDER"
#endif
            );
            using(MegaCrit.Sts2.Core.Commands.CardSelectCmd.UseSelector(new NativeEventTests.Select(native.Players[0].Deck.Cards.First()))) await option.Chosen();
            JigsawAncientRewardBoundaryHost.BeforeDone = null;
            Require(ancient.IsFinished && session.AncientReady,"Real native option completion, rather than a direct domain completion call, authorizes Continue.");
            if (actualPartial)
            {
                Require(interrupted != null, "The real Ancient Done callback exposes the partial reward checkpoint.");
                string completeReward = System.Text.Json.JsonSerializer.Serialize(native.Players[0].ToSerializable());
                string choice = option.Relic!.Id.Entry;
                await manager.SaveRun(interrupted!, false);
                var partial = manager.LoadRunSave();
                RunManager.Instance.CleanUp(false);
                var rollback = RunState.FromSerializable(partial.SaveData!);
                Require(System.Text.Json.JsonSerializer.Serialize(rollback.Players[0].ToSerializable()) == beforeReward,
                    "Actual option relic and card side effects roll back to the complete prereward player.");
                await RunManager.Instance.SetUpSavedSingleplayer(rollback, partial.SaveData!);
                MegaCrit.Sts2.Core.Context.LocalContext.NetId = rollback.Players[0].NetId;
                await RunManager.Instance.GenerateMap(); await RunManager.Instance.LoadIntoLatestMapCoord(null);
                var recovered = JigsawNativeSession.Current!;
                Require(recovered.Map!.Placements.SequenceEqual(map.Placements) && !recovered.AncientReady,
                    "Native reload preserves the accepted map and reopens the unfinished Ancient choice.");
                var restoredAncient = RunManager.Instance.EventSynchronizer.GetLocalEvent();
                using (MegaCrit.Sts2.Core.Commands.CardSelectCmd.UseSelector(new NativeEventTests.Select(rollback.Players[0].Deck.Cards.First())))
                    await restoredAncient.CurrentOptions.Single(o => o.Relic?.Id.Entry == choice).Chosen();
                var actualPlayer = System.Text.Json.JsonSerializer.Serialize(rollback.Players[0].ToSerializable());
                var expected = JsonNode.Parse(completeReward)!.AsObject(); var actual = JsonNode.Parse(actualPlayer)!.AsObject();
                // User clarification of #67: profile discoveries remain native and monotonic.
                // This run's newly-discovered list can differ after rollback/reselection.
                Require(expected.Remove("discovered_relics") && actual.Remove("discovered_relics"), "Compare the actual native discovery field separately.");
                Require(MegaCrit.Sts2.Core.Saves.SaveManager.Instance.IsRelicSeen(option.Relic!), "Native profile discovery survives reward rollback.");
                Require(recovered.AncientReady && JsonNode.DeepEquals(expected, actual),
                    "Replaying the real choice applies all remaining native player effects exactly once.");
                return;
            }
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
            if (entryFailure != EntryFailureKind.None)
            {
                JigsawEntryFailureHost.Early = entryFailure == EntryFailureKind.BeforeSave;
                JigsawEntryFailureHost.Fail = entryFailure is not (EntryFailureKind.AfterReady or EntryFailureKind.BeforeSave);
                JigsawEntryFailureHost.Late = entryFailure == EntryFailureKind.AfterReady;
                bool failed = false;
                try { await session.Enter(1); } catch (IOException) { failed = true; }
                if (entryFailure == EntryFailureKind.AfterReady)
                {
                    Require(failed && map.PendingEntry == null && map.Steps == 1 && map.FirstProgress == 1 && map.CurrentNode == 1,
                        "A failure after actual RoomEntered cannot roll back the confirmed entry.");
                    await session.Enter(1);
                    Require(map.Steps == 1 && native.VisitedMapCoords.Count == 2, "A retry after confirmation cannot re-enter or charge twice.");
                    return;
                }
                Require(failed && (entryFailure==EntryFailureKind.BeforeSave || JigsawEntryFailureHost.Relics != null) && map.PendingEntry?.NodeId == 1 && map.Steps == 0 && map.FirstProgress == 0,
                    "A real native room has rolled contents but failed before readiness, retaining the uncharged destination.");
                long identity = map.PendingEntry!.Id;
                if (entryFailure == EntryFailureKind.RetryAfterReady)
                {
                    JigsawEntryFailureHost.Late = true;
                    failed = false; try { await session.Enter(1); } catch (IOException) { failed = true; }
                    var confirmed = JigsawNativeSession.Current!;
                    Require(failed && confirmed.Map is { Steps:1, FirstProgress:1, CurrentNode:1, PendingEntry:null },
                        "Restored native RoomEntered confirms before a later callback fails, without rolling back to pending.");
                    await confirmed.Enter(1);
                    Require(confirmed.Map.Steps == 1 && confirmed.Native.VisitedMapCoords.Count == 2 && confirmed.Native.MapPointHistory[0].Count == 2,
                        "A later callback failure after restored readiness cannot re-enter or duplicate native history.");
                    return;
                }
                if (entryFailure == EntryFailureKind.RestoredBeforeReady)
                {
                    await manager.SaveRun(RunManager.Instance.ToSave(null), false);
                    var savedFailure = manager.LoadRunSave();
                    RunManager.Instance.CleanUp(false);
                    var recovered = RunState.FromSerializable(savedFailure.SaveData!);
                    await RunManager.Instance.SetUpSavedSingleplayer(recovered, savedFailure.SaveData!);
                    MegaCrit.Sts2.Core.Context.LocalContext.NetId = recovered.Players[0].NetId;
                    await RunManager.Instance.GenerateMap();
                    JigsawEntryFailureHost.Fail = true;
                    failed = false; try { await RunManager.Instance.LoadIntoLatestMapCoord(null); } catch (IOException) { failed = true; }
                    Require(failed && JigsawNativeSession.Current!.Map!.PendingEntry?.Id == identity && JigsawNativeSession.Current.Map.Steps == 0,
                        "A saved failed entry retains its original identity and can fail again without charging.");
                    session = JigsawNativeSession.Current!;
                }
                await session.Enter(1);
                var retry = JigsawNativeSession.Current!;
                var recoveredMap = retry.Map!;
                Require(recoveredMap.PendingEntry == null && recoveredMap.CurrentNode == 1 && recoveredMap.Steps == 1 &&
                    recoveredMap.FirstProgress == 1 && recoveredMap.CreateSave().NextEntry == identity && retry.Native.CurrentRoom is TreasureRoom,
                    "Retry loads the same accepted destination through real native readiness, charging exactly once.");
                Require(JigsawEntryFailureHost.Relics == null || RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics!.Select(r => r.Id.Entry).SequenceEqual(JigsawEntryFailureHost.Relics),
                    "Retry restores pre-roll native content state instead of rerolling the treasure.");
                Require(retry.Native.VisitedMapCoords.Count == 2 && retry.Native.MapPointHistory[0].Count == 2,
                    "Retry retains only the Ancient and one first-room visit/history entry.");
                await session.Enter(1); await retry.Enter(1);
                Require(retry.Map!.Steps == 1 && retry.Native.VisitedMapCoords.Count == 2, "Repeated old or current session requests do not re-enter or charge twice.");
                return;
            }
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
        finally { JigsawEntryFailureHost.Reset(); JigsawAncientRewardBoundaryHost.BeforeDone = null; JigsawNativeSaveHost.Save=null; RunManager.Instance.CleanUp(false); }
    }

    private static async Task CheckpointFaultMatrix()
    {
        foreach (bool multiplayer in new[] { false, true })
        foreach (bool synchronous in new[] { false, true })
        foreach (string scenario in new[] { "normal", "missing", "corrupt", "rename-fails", "write-fails" })
        {
            var native = (RunState)HeadlessTestSetup.PlayerWithRun("CHECKPOINT_76").RunState;
            RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
            try
            {
                var io = new MockGodotFileIo("checkpoint-76-isolated");
                var manager = new RunSaveManager(76, io, new MigrationManager(io), forceSynchronous: synchronous);
                string path = RunSaveManager.GetRunSavePath(76, multiplayer ? "current_run_mp.save" : "current_run.save");
                var first = JigsawRun.Create(76);
                native.Players[0].Gold = 111;
                JigsawCheckpoint.Capture(native, first);
                await manager.SaveRun(RunManager.Instance.ToSave(null), multiplayer);
                var second = JigsawRun.Create(77);
                native.Players[0].Gold = 222;
                JigsawCheckpoint.Capture(native, second);
                await manager.SaveRun(RunManager.Instance.ToSave(null), multiplayer);
                string main = io.ReadFile(path), backup = io.ReadFile(path + ".backup");
                if (scenario == "missing") io.DeleteFile(path);
                if (scenario is "corrupt" or "rename-fails")
                {
                    var invalid = JsonNode.Parse(main)!;
                    invalid["schema_version"] = new JsonObject { ["invalid"] = true };
                    io.SetFileContent(path, invalid.ToJsonString());
                    if (scenario == "rename-fails") io.RenameFileAction = (_, _) => throw new IOException("Injected preservation failure");
                }
                if (scenario == "write-fails")
                {
                    native.Players[0].Gold = 333;
                    JigsawCheckpoint.Capture(native, JigsawRun.Create(78));
                    int saved = 0;
                    manager.Saved += () => saved++;
                    io.ShouldFailWrites = true;
                    bool rejected = false;
                    try { await manager.SaveRun(RunManager.Instance.ToSave(null), multiplayer); }
                    catch (Exception) { rejected = true; }
                    finally { io.ShouldFailWrites = false; }
                    Require(rejected && saved == 0 && io.ReadFile(path) == main && io.ReadFile(path + ".backup") == backup,
                        "Failed native write cannot signal success or change either complete checkpoint.");
                }
                var loaded = multiplayer ? manager.LoadMultiplayerRunSave() : manager.LoadRunSave();
                Require(loaded.Success && loaded.SaveData != null, "Native recovery must return an actual parsed object.");
                var restored = RunState.FromSerializable(loaded.SaveData!);
                bool fromBackup = scenario is "missing" or "corrupt" or "rename-fails";
                var expected = fromBackup ? first : second;
                Require(restored.Players[0].Gold == (fromBackup ? 111 : 222) &&
                    JigsawCheckpoint.Slot.TryGet(restored, out var data) &&
                    System.Text.Json.JsonSerializer.Serialize(data.Map) == System.Text.Json.JsonSerializer.Serialize(expected.CreateSave()),
                    "Native player and complete production map must come from the same successfully parsed checkpoint.");
                Console.WriteLine($"CHECKPOINT_76 scenario={scenario}; multiplayerPath={multiplayer}; synchronous={synchronous}; source={(fromBackup ? "backup" : "main")}; nativePlayers=1; host=isolated-IO");
            }
            finally { RunManager.Instance.CleanUp(false); }
        }
    }
    private static Task CoalescedEdits() => CoalescedEdits(false);
    private static async Task CoalescedEdits(bool stale, bool failFirst=false, bool nativeRetry=false)
    {
        var native = RunState.CreateForTest(players: [MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(), MegaCrit.Sts2.Core.Unlocks.UnlockState.all, 1)], acts: [ModelDb.Act<Overgrowth>()], seed: "COALESCED_EDITS_76");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService(), shouldSave: true);
        var release = new TaskCompletionSource();
        var io = new MockGodotFileIo("coalesced-76-memory-only");
        var manager = new RunSaveManager(76, io, new MigrationManager(io));
        int writes = 0;
        try
        {
            RunManager.Instance.GenerateRooms(); await RunManager.Instance.GenerateMap();
            var session = JigsawNativeSession.Enable(native); await session.StartAct();
            JigsawNativeSaveHost.Save = room => manager.SaveRun(RunManager.Instance.ToSave(room), false);
            JigsawDelayedIoHost.Write = async (target, path, bytes) =>
            {
                if (++writes == 1)
                {
                    await release.Task;
                    if (failFirst) throw new IOException("Injected editor write failure");
                }
                target.WriteFile(path, bytes);
            };
            session.OnEdited();
            Require(writes == 1, "First edit has entered actual native IO.");
            var exit = session.WaitForSave();
            if (nativeRetry)
            {
                release.SetResult();
                bool failed = false; try { await exit; } catch (IOException) { failed = true; }
                Require(failed, "The latest failed editor point must refuse exit before retry.");
                await manager.SaveRun(RunManager.Instance.ToSave(null), false);
                await session.WaitForSave();
                Require(writes == 2, "A later successful native complete point supersedes the old editor failure.");
                return;
            }
            Require(session.Map!.Take(1, 0).Success && session.Map.Drop(1, new(3, 0)).Success, "Prepare a later accepted placement.");
            session.OnEdited();
            native.Players[0].Gold = 222;
            session.OnEdited();
            Require(!exit.IsCompleted && writes == 1, "Exit waits while newer edits remain pending.");
            if (stale)
            {
                RunManager.Instance.CleanUp(false);
                var replacement = (RunState)HeadlessTestSetup.PlayerWithRun("REPLACEMENT_PENDING_SAVE_76").RunState;
                RunManager.Instance.SetUpTest(replacement, new NetSingleplayerGameService(), shouldSave: true);
                JigsawNativeSession.Enable(replacement);
                release.SetResult(); await exit;
                Require(writes == 1, "Old queued edits cannot capture or write a replacement run after the in-flight save completes.");
                return;
            }
            release.SetResult();
            await exit;
            Require(writes == 2, "Only not-yet-started edits merge into one latest complete checkpoint.");
            var loaded = manager.LoadRunSave();
            var restored = RunState.FromSerializable(loaded.SaveData!);
            Require(restored.Players[0].Gold == 222 && JigsawCheckpoint.Slot.TryGet(restored, out var data) && data.Map!.Placements.Length == 1,
                "Exit includes the newest accepted native and map state.");
            if (failFirst) return;
            string path = RunSaveManager.GetRunSavePath(76, "current_run.save");
            io.DeleteFile(path);
            var backup = RunState.FromSerializable(manager.LoadRunSave().SaveData!);
            Require(JigsawCheckpoint.Slot.TryGet(backup, out var old) && old.Map!.Placements.Length == 0,
                "A checkpoint already being written retains its original complete map.");
        }
        finally { release.TrySetResult(); JigsawDelayedIoHost.Write = null; JigsawNativeSaveHost.Save = null; RunManager.Instance.CleanUp(false); }
    }

    private static Task SerializedWrites() => SerializedWrites(false);
    private static async Task SerializedWrites(bool failFirst)
    {
        var native = (RunState)HeadlessTestSetup.PlayerWithRun("SERIAL_SAVE_76").RunState;
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        var release = new TaskCompletionSource();
        var reached = new TaskCompletionSource();
        var io = new MockGodotFileIo("serial-76-memory-only");
        var manager = new RunSaveManager(76, io, new MigrationManager(io));
        int writes = 0;
        JigsawDelayedIoHost.Write = async (target, path, bytes) =>
        {
            if (!ReferenceEquals(target, io)) throw new InvalidOperationException("Unexpected IO owner");
            if (++writes == 1)
            {
                reached.SetResult(); await release.Task;
                if (failFirst) throw new IOException("Injected failure of the first queued write");
            }
            target.WriteFile(path, bytes);
        };
        try
        {
            var session = JigsawNativeSession.Enable(native);
            native.Players[0].Gold = 111;
            JigsawCheckpoint.Capture(native, JigsawRun.Create(76));
            var first = manager.SaveRun(RunManager.Instance.ToSave(null), false);
            await reached.Task;
            Task? earlyExit = failFirst ? session.WaitForSave() : null;
            native.Players[0].Gold = 222;
            var secondMap = JigsawRun.Create(77);
            JigsawCheckpoint.Capture(native, secondMap);
            var second = manager.SaveRun(RunManager.Instance.ToSave(null), false);
            var exit = earlyExit ?? session.WaitForSave();
            bool serialized = writes == 1 && !second.IsCompleted && !exit.IsCompleted;
            release.SetResult();
            bool failed = false;
            try { await first; } catch (IOException) { failed = true; }
            await Task.WhenAll(second, exit);
            Require(failed == failFirst, "Native caller observes the real write outcome while the later complete save can retry.");
            Require(serialized, "Only one native write may start, and exit must wait for both accepted saves.");
            var loaded = manager.LoadRunSave();
            var restored = RunState.FromSerializable(loaded.SaveData!);
            Require(restored.Players[0].Gold == 222 && JigsawCheckpoint.Slot.TryGet(restored, out var saved) &&
                System.Text.Json.JsonSerializer.Serialize(saved.Map) == System.Text.Json.JsonSerializer.Serialize(secondMap.CreateSave()),
                "A delayed older write cannot replace the latest complete native and map checkpoint.");
        }
        finally { release.TrySetResult(); JigsawDelayedIoHost.Write = null; RunManager.Instance.CleanUp(false); }
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

[HarmonyPatch(typeof(MockGodotFileIo), nameof(MockGodotFileIo.WriteFileAsync), [typeof(string), typeof(byte[])])]
internal static class JigsawDelayedIoHost
{
    internal static Func<MockGodotFileIo, string, byte[], Task>? Write;
    private static bool Prefix(MockGodotFileIo __instance, string path, byte[] bytes, ref Task __result)
    {
        if (Write == null) return true;
        __result = Write(__instance, path, bytes);
        return false;
    }
}

[HarmonyPatch(typeof(TreasureRoom), nameof(TreasureRoom.EnterInternal))]
internal static class JigsawEntryFailureHost
{
    internal static bool Early;
    internal static bool Fail;
    internal static bool Late;
    internal static string[]? Relics;
    private static void Postfix(ref Task __result)
    {
        if (Late) { Late = false; __result = InstallLateFailure(__result); return; }
        if (!Fail) return;
        Fail = false;
        __result = FailAfterContents(__result);
    }
    private static async Task InstallLateFailure(Task original)
    {
        await original;
        RunManager.Instance.RoomEntered += ThrowAfterReady;
    }
    private static void ThrowAfterReady()
    {
        RunManager.Instance.RoomEntered -= ThrowAfterReady;
        throw new IOException("Injected failure after RoomEntered confirmation");
    }
    internal static void Reset()
    {
        Early = Fail = Late = false; Relics = null;
        RunManager.Instance.RoomEntered -= ThrowAfterReady;
    }
    private static async Task FailAfterContents(Task original)
    {
        await original;
        Relics = RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics!.Select(r => r.Id.Entry).ToArray();
        throw new IOException("Injected real treasure load failure before RoomEntered");
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterMapPointInternal))]
internal static class JigsawEarlyEntryFailureHost
{
    private static bool Prefix(ref Task __result)
    {
        if (!JigsawEntryFailureHost.Early || JigsawNativeSession.Current?.Map?.PendingEntry == null) return true;
        JigsawEntryFailureHost.Early = false;
        __result = Task.FromException(new IOException("Injected failure after AddVisitedMapCoord but before native save"));
        return false;
    }
}

// Observe a real option after Obtain/AfterObtained but before native completion.
// The callback captures a save; it neither suppresses nor synthesizes rewards.
[HarmonyPatch(typeof(AncientEventModel), "Done")]
internal static class JigsawAncientRewardBoundaryHost
{
    internal static Action<AncientEventModel>? BeforeDone;
    private static void Prefix(AncientEventModel __instance) => BeforeDone?.Invoke(__instance);
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
