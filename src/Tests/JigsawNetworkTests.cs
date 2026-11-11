using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.TestSupport;
using AllMatters;
using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using MegaCrit.Sts2.Core.ControllerInput;
using Godot;
using Environment = System.Environment;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Transport.ENet;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using System.Diagnostics;
using System.Text.Json.Nodes;

internal static class JigsawNetworkTests
{
#if STS2_PUBLIC
    private const string Reward="PRECISE_SCISSORS";
#else
    private const string Reward="POMANDER";
#endif
    internal static void ForceReward(ref Action<EventModel>? debugOnStart)
    {
        var original=debugOnStart;
        debugOnStart=e=>{original?.Invoke(e);if(e is AncientEventModel ancient)ancient.DebugOption=Reward;};
    }
    private static readonly TaskCompletionSource<IEnumerable<CardModel>> pendingSelection = new();
    private static CardModel[] pendingCards = [];
    internal sealed class LegalSelector : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options,int minSelect,int maxSelect) {
            var cards=options.Take(minSelect).ToArray();
            if(Environment.GetEnvironmentVariable("JIGSAW_PENDING_ANCIENT")=="1" && cards.First().Owner.NetId==3)
            {
                pendingCards=cards;
                return pendingSelection.Task;
            }
            return Task.FromResult<IEnumerable<CardModel>>(cards);
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,IReadOnlyList<CardRewardAlternative> alternatives) => throw new InvalidOperationException("Unexpected reward selector.");
    }
    private static readonly List<ulong> executed = [];
    private static RunSaveManager? isolatedSaveManager;
    private static bool SaveThroughIsolatedManager(ref Task __result)
    {
        if(isolatedSaveManager==null)return true;
        __result=isolatedSaveManager.SaveRun(RunManager.Instance.ToSave(null),true);
        return false;
    }
    private static void ObserveTake(ulong __0) { executed.Add(__0); Console.WriteLine("ENET CALLBACK sender="+__0); }
    internal const string Name="Jigsaw native ENet editor broadcast";
    public static async Task Run()
    {
        // Keep vanilla model-cache authentication independent of mutation by earlier tests.
        if (Environment.GetEnvironmentVariable("JIGSAW_NET_ROLE") == null)
        {
            string logs = Path.Combine(Path.GetTempPath(), "allmatters-enet-" + Guid.NewGuid());
            Directory.CreateDirectory(logs);
            var start = new ProcessStartInfo(OS.GetExecutablePath()) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "--headless", "--path", ProjectSettings.GlobalizePath("res://"), "--", Name }) start.ArgumentList.Add(arg);
            start.Environment["JIGSAW_NET_ROLE"] = "host";
            start.Environment["JIGSAW_NET_DIR"] = logs;
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(50)); }
            finally { if (!process.HasExited) process.Kill(true); }
            File.WriteAllText(Path.Combine(logs, "host.log"), await output);
            File.WriteAllText(Path.Combine(logs, "host.err"), await errors);
            Console.WriteLine($"ENET isolated native host/client evidence: {logs}; host exit={process.ExitCode}");
            if (process.ExitCode != 0) throw new Exception("Native ENet host failed: " + logs);
            return;
        }
        MegaCrit.Sts2.Core.Multiplayer.Serialization.MessageTypes.Initialize();
        var role=Environment.GetEnvironmentVariable("JIGSAW_NET_ROLE")??"host";
        var directory=Environment.GetEnvironmentVariable("JIGSAW_NET_DIR")??Path.Combine(Path.GetTempPath(),"allmatters-enet-"+Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var children=new List<Process>();
        var harmony=new HarmonyLib.Harmony("AllMatters.Tests.ENet80");
        harmony.Patch(HarmonyLib.AccessTools.Method(typeof(SaveManager),nameof(SaveManager.SaveRun)),
            prefix:new HarmonyLib.HarmonyMethod(typeof(JigsawNetworkTests),nameof(SaveThroughIsolatedManager)));
        harmony.Patch(HarmonyLib.AccessTools.Method(typeof(EventSynchronizer),nameof(EventSynchronizer.BeginEvent)),prefix:new HarmonyLib.HarmonyMethod(typeof(JigsawNetworkTests),nameof(ForceReward)));
        using var selector=CardSelectCmd.UseSelector(new LegalSelector());
        executed.Clear();
        harmony.Patch(HarmonyLib.AccessTools.Method(typeof(JigsawRun), nameof(JigsawRun.Take)), prefix:new HarmonyLib.HarmonyMethod(typeof(JigsawNetworkTests),nameof(ObserveTake)));
        var tree=(SceneTree)Engine.GetMainLoop();
        INetGameService net;
#if STS2_PUBLIC
        var host=role=="host"?new NetHostGameService():null;
        var client=role!="host"?new NetClientGameService():null;
#else
        var host=role=="host"?new NetHostGameService(PeerVersionInfo.LocalDefault()):null;
        var client=role!="host"?new NetClientGameService(PeerVersionInfo.LocalDefault()):null;
#endif
        ushort port;
        if (host != null)
        {
            using var socket = new System.Net.Sockets.UdpClient(0);
            port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint!).Port;
        }
        else port = ushort.Parse(Environment.GetEnvironmentVariable("JIGSAW_NET_PORT")!);
        net=(INetGameService?)host??client!;
        async Task Until(Func<bool> predicate,string stage)
        {
            var deadline=Stopwatch.StartNew();
            while(!predicate())
            {
                if(deadline.Elapsed.TotalSeconds>20)throw new Exception($"ENet {role} timeout: {stage}; executed={string.Join(",",executed)}; logs={directory}");
                net.Update();
                await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
            }
        }
        try
        {
            if(host!=null)
            {
                var error=host.StartENetHost(port,2);if(error.HasValue)throw new Exception("ENet bind failed");
                host.ClientConnected+=id=>host.SetPeerReadyForBroadcasting(id);
                foreach(var childRole in new[]{"2","3"})
                {
                    var start=new ProcessStartInfo(OS.GetExecutablePath()){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                    foreach(var arg in new[]{"--headless","--path",ProjectSettings.GlobalizePath("res://"),"--",Name})start.ArgumentList.Add(arg);
                    start.Environment["JIGSAW_NET_ROLE"]=childRole;start.Environment["JIGSAW_NET_DIR"]=directory;start.Environment["JIGSAW_NET_PORT"]=port.ToString();
                    var process=Process.Start(start)!;children.Add(process);
                    process.OutputDataReceived+=(_,e)=>{if(e.Data!=null)File.AppendAllText(Path.Combine(directory,childRole+".log"),e.Data+Environment.NewLine);};
                    process.ErrorDataReceived+=(_,e)=>{if(e.Data!=null)File.AppendAllText(Path.Combine(directory,childRole+".err"),e.Data+Environment.NewLine);};
                    process.BeginOutputReadLine();process.BeginErrorReadLine();
                }
                await Until(()=>host.ConnectedPeers.Count==2,"native authenticated clients");
            }
            else
            {
                var transport=new ENetClient(client!);client!.Initialize(transport,PlatformType.None);
                var connect=transport.ConnectToHost(ulong.Parse(role),"127.0.0.1",port,CancellationToken.None);
                await Until(()=>connect.IsCompleted,"transport handshake");
                if((await connect).HasValue)throw new Exception("ENet connect failed");
                await Until(()=>client.IsConnected,"native handshake");
            }
            Console.WriteLine($"ENET CONNECTED role={role} netId={net.NetId} type={net.Type} logs={directory}");
            var io=new MockGodotFileIo("jigsaw-enet-80-"+role+"-"+Guid.NewGuid());
            var saves=new RunSaveManager(80,io,new MigrationManager(io),forceSynchronous:true);
            isolatedSaveManager=saves;
            // Same native lobby entry as character selection; the listener substitutes NGame's scene host.
            JigsawPreferences.Enabled=host!=null;
            var listener=new JigsawActivationTests.NewRunListener{ShouldSave=true};
            var lobby=new MegaCrit.Sts2.Core.Multiplayer.Game.Lobby.StartRunLobby(GameMode.Standard,net,listener,3);
            listener.Lobby=lobby;
            if(host!=null)lobby.AddLocalHostPlayer(UnlockState.all,0);
            File.WriteAllText(Path.Combine(directory,role+".constructed"),"ready");
            await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".constructed"))),"all lobby handlers installed");
            if(host==null)
            {
                net.RegisterMessageHandler<MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby.ClientLobbyJoinResponseMessage>((message,_)=>lobby.InitializeFromMessage(message));
                net.SendMessage(new MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby.ClientLobbyJoinRequestMessage{unlockState=UnlockState.all.ToSerializable(),maxAscensionUnlocked=0});
            }
            await Until(()=>lobby.Players.Count==3,"native lobby roster");
            File.WriteAllText(Path.Combine(directory,role+".lobby"),"ready");
            await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".lobby"))),"all start lobbies ready");
            lobby.SetReady(true);
            await Until(()=>listener.Native!=null,"native begin-run callback");
            var native=listener.Native!;
            var session=JigsawNativeSession.Current??throw new Exception("Official new multiplayer entry must activate Host-selected Jigsaw.");
            // NGame.StartRun calls Launch after asset loading: assigns local owner and drains lobby buffering.
            RunManager.Instance.Launch();
            RunManager.Instance.GenerateRooms();await RunManager.Instance.GenerateMap();
            await session.StartAct();
            executed.Clear();
            RunManager.Instance.ActionExecutor.Unpause();
            File.WriteAllText(Path.Combine(directory,role+".ready"),"ready");
            await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".ready"))),"all sessions ready");
            // All three participants contend for the same piece via the production request path.
            await Until(()=>JigsawEditorAction.Submit(session,JigsawEditorOperation.Take,0).Success,"negotiated managed action capability");
            await Until(()=>executed.Count==3,"all three production callbacks including rejected contention");
            if(session.Map!.Holdings.Count!=1)throw new Exception("Contending native actions must accept exactly one shared holding.");
            var winner=session.Map!.Holdings.Keys.Single();
            File.WriteAllText(Path.Combine(directory,role+".winner.tmp"),winner+":"+string.Join(",",executed));
            File.Move(Path.Combine(directory,role+".winner.tmp"),Path.Combine(directory,role+".winner"));
            await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".winner"))),"all callbacks");
            if(new[]{"host","2","3"}.Select(r=>File.ReadAllText(Path.Combine(directory,r+".winner"))).Distinct().Count()!=1)throw new Exception("Native peers disagree on shared piece owner.");
            Console.WriteLine($"ENET ACCEPTED role={role} sender={net.NetId} winner={winner} holdings={session.Map.Holdings.Count} callbacks={string.Join(",",executed)}");
            // Release the race winner, then exercise a guaranteed non-host through engine input.
            if(net.NetId==winner)JigsawEditorAction.Submit(session,JigsawEditorOperation.Cancel);
            await Until(()=>session.Map.Holdings.Count==0,"race winner releases");
            if(role=="2")
            {
                tree.Root.Size=new(1280,720);
                HeadlessUiHost.EnsureInputActions();
                var view=session.CreateScreen(new MemoryJigsawTutorialProfile{Sentence=6});
                try
                {
                    await HeadlessUiHost.Mount(view);
                    async Task Press(string action)
                    {
                        Input.ParseInputEvent(new InputEventAction{Action=action,Pressed=true});
                        await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                    }
                    await Press(MegaInput.down);
                    int notifications=0;
                    session.Changed+=()=>notifications++;
                    string beforeTake=view.DialogText;
                    await Press(MegaInput.select);
                    if(notifications!=0 || view.DialogText!=beforeTake)throw new Exception("Queued client take must not save or announce success before native acceptance.");
                    await Until(()=>session.Map.Holdings.ContainsKey(2),"client UI take");
                    await Press(MegaInput.viewDiscardPile);
                    // Pump the actual action executor before asserting the user-visible result.
                    for(int i=0;i<30;i++){net.Update();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);}
                    if(session.Map.Holdings[2].Rotation!=1)throw new Exception("Client 2 engine rotation must edit its own held piece.");
                    await Press(MegaInput.cancel);
                    await Until(()=>!session.Map.Holdings.ContainsKey(2),"client UI cancel");
                    await Press(MegaInput.select);
                    await Until(()=>session.Map.Holdings.ContainsKey(2),"client UI retake");
                    File.WriteAllText(Path.Combine(directory,"client-return.taken"),"taken");
                    File.WriteAllText(Path.Combine(directory,role+".return-ready"),"ready");
                    await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".return-ready"))),"all peers observe retake");
                    await Press(MegaInput.viewDrawPile);
                    await Until(()=>session.Map.Holdings.Count==0,"client returns to pool");
                    await Press(MegaInput.viewExhaustPileAndTabRight);
                    await Press(MegaInput.viewExhaustPileAndTabRight);
                    await Press(MegaInput.select);
                    await Until(()=>session.Map.Choices.TryGetValue(2,out var choice) && choice==EndChoice.Manual,"client manual vote accepted");
                    File.WriteAllText(Path.Combine(directory,"client-ui.done"),"rotated cancelled returned");
                }
                finally{view.Free();}
            }
            if(role!="2")
            {
                await Until(()=>File.Exists(Path.Combine(directory,"client-return.taken")) && session.Map.Holdings.ContainsKey(2),"broadcast client retake");
                File.WriteAllText(Path.Combine(directory,role+".return-ready"),"ready");
            }
            await Until(()=>File.Exists(Path.Combine(directory,"client-ui.done")),"client UI operations");
            await Until(()=>session.Map.Holdings.Count==0,"client UI return converges on every peer");
            await Until(()=>session.Map.Choices.TryGetValue(2,out var choice) && choice==EndChoice.Manual,"client manual vote broadcast");
            var originalPool=session.Map.Pool;
            File.WriteAllText(Path.Combine(directory,role+".before-reset"),"ready");
            await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".before-reset"))),"all peers finish prior editing");
            if(role=="host")JigsawEditorAction.Submit(session,JigsawEditorOperation.Take,piece:1);
            if(role=="3")JigsawEditorAction.Submit(session,JigsawEditorOperation.Take,piece:2);
            await Until(()=>session.Map.Holdings.Count==2,"two different holdings before reset");
            if(role=="2")
            {
                if(new JigsawResetCommand().Process(native.Players.Single(p=>p.NetId==2),[]).success)throw new Exception("Non-host console reset must reject.");
                JigsawEditorAction.Submit(session,JigsawEditorOperation.Reset);
            }
            for(int i=0;i<60;i++){net.Update();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);}
            if(session.Map.Holdings.Count!=2 || !session.Map.Choices.ContainsKey(2))throw new Exception("Non-host reset cannot clear holdings or votes.");
            File.WriteAllText(Path.Combine(directory,role+".reset-ready"),"ready");
            await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".reset-ready"))),"non-host reset rejected");
            if(role=="host")
            {
                var reset=new JigsawResetCommand().Process(native.Players.Single(p=>p.NetId==1),[]);
                if(!reset.success || reset.task==null)throw new Exception("Host reset must submit an ordered task.");
                await Until(()=>reset.task.IsCompleted,"Host reset accepted and saved");await reset.task;
            }
            await Until(()=>session.Map.Holdings.Count==0 && session.Map.Choices.Count==0,"Host reset clears all holdings and votes");
            if(session.Map.Placements.Count!=0 || session.Map.SelectedEndpoints.Count!=0 || session.Map.Edges.Count!=0 || !ReferenceEquals(originalPool,session.Map.Pool))throw new Exception("Reset must preserve original pool and clear layout.");
            File.WriteAllText(Path.Combine(directory,role+".reset-done"),"done");
            await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".reset-done"))),"all peers observe reset");
            var blockedWorker=new TaskCompletionSource<JigsawLayout?>();
            bool workerStarted=false;
            if(role=="host" && Environment.GetEnvironmentVariable("JIGSAW_HOSTEXIT_WORKER")=="1")JigsawAutomaticWorker.TestOverride=(_,_)=>{workerStarted=true;return blockedWorker.Task;};
            if(role=="host" && Environment.GetEnvironmentVariable("JIGSAW_AUTOMATIC_FAILURE")=="1")JigsawAutomaticWorker.TestOverride=(_,_)=>throw new Exception("Host automatic worker failed.");
            if(role=="2")JigsawEditorAction.Submit(session,JigsawEditorOperation.ChooseEnd,choice:EndChoice.Manual);
            await Until(()=>session.Map.Choices.ContainsKey(2),"new legitimate vote after reset");
            Console.WriteLine("ENET HOST RESET cleared holdings/votes and retained original pool role="+role);
            if(role=="host")
            {
                var descriptor=(STS2RitsuLib.Networking.ManagedActions.RitsuLibManagedNetActionDescriptor<JigsawEditorIntent>)HarmonyLib.AccessTools.Field(typeof(JigsawEditorAction),"descriptor").GetValue(null)!;
                var intent=new JigsawEditorIntent(session.Native.CurrentActIndex,JigsawEditorOperation.ChooseEnd,Choice:EndChoice.Manual,Sequence:100,EditRevision:session.Map.EditRevision);
                STS2RitsuLib.Networking.ManagedActions.RitsuLibManagedNetActions.Request(RunManager.Instance,descriptor,intent);
                await Until(()=>session.Map.Choices.ContainsKey(1),"host original vote");
                STS2RitsuLib.Networking.ManagedActions.RitsuLibManagedNetActions.Request(RunManager.Instance,descriptor,intent);
                for(int i=0;i<60;i++){net.Update();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);}
                if(!session.Map.Choices.TryGetValue(1,out var originalVote)||originalVote!=EndChoice.Manual)throw new Exception("Replayed native request must not retract the accepted vote.");
                File.WriteAllText(Path.Combine(directory,"replay.checked"),"checked");
                STS2RitsuLib.Networking.ManagedActions.RitsuLibManagedNetActions.Request(RunManager.Instance,descriptor,intent with {Sequence=101,Choice=EndChoice.Automatic});
            }
            else await Until(()=>File.Exists(Path.Combine(directory,"replay.checked")),"host duplicate vote rejected");
            Console.WriteLine("ENET CLIENT UI rotation/cancel converged role="+role);
            if(role!="host")JigsawAutomaticWorker.TestOverride=(_,_)=>throw new Exception("Client must never compute automatic layout.");
            bool pendingAncient=Environment.GetEnvironmentVariable("JIGSAW_PENDING_ANCIENT")=="1";
            bool absentAncient=Environment.GetEnvironmentVariable("JIGSAW_ABSENT_ANCIENT")=="1";
            string[] remainingRoles=absentAncient ? ["host","2"] : ["host","2","3"];
            ulong[] remainingOwners=absentAncient ? [1,2] : [1,2,3];
            string absentState=System.Text.Json.JsonSerializer.Serialize(native.Players.Single(p=>p.NetId==3).ToSerializable());
            if(Environment.GetEnvironmentVariable("JIGSAW_DISCONNECT_CONSENSUS")=="1" || absentAncient)
            {
                if(role=="3")JigsawEditorAction.Submit(session,JigsawEditorOperation.Take,0);
                if(role=="2")JigsawEditorAction.Submit(session,JigsawEditorOperation.ChooseEnd,choice:EndChoice.Automatic);
                await Until(()=>session.Map.Holdings.ContainsKey(3) && session.Map.Choices.GetValueOrDefault(2UL)==EndChoice.Automatic && session.Map.Choices.GetValueOrDefault(1UL)==EndChoice.Automatic,"two automatic votes while departing player holds piece");
                File.WriteAllText(Path.Combine(directory,role+".disconnect-ready"),"ready");
                await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".disconnect-ready"))),"disconnect fixture ready");
                if(host!=null)host.DisconnectClient(3,NetError.Quit,true);
                if(role=="3") {await Until(()=>!client!.IsConnected,"native Host disconnects third player");return;}
                await Until(()=>session.Map.Phase==JigsawPhase.Ancient,"disconnect authorizes and starts remaining unanimous automatic worker");
                if(session.Map.ActivePlayers.Contains(3UL) || session.Map.Holdings.ContainsKey(3))throw new Exception("Ordered disconnect must cancel departed holding before consensus.");
                if(native.Players.Count!=3 || session.Map.OriginalPlayers.Count!=3)throw new Exception("Original native and map roster must survive disconnect.");
                Console.WriteLine("ENET DISCONNECT automatic worker entered Ancient with two active players role="+role);
                File.WriteAllText(Path.Combine(directory,role+".disconnect-passed"),"passed");
                await Until(()=>new[]{"host","2"}.All(r=>File.Exists(Path.Combine(directory,r+".disconnect-passed"))),"remaining peers converge");
                if(!absentAncient)
                {
                    if(host!=null) {await Until(()=>children.All(p=>p.HasExited),"disconnect clients finished");if(children.Any(p=>p.ExitCode!=0))throw new Exception("Disconnect client failed.");}
                    return;
                }
            }
            if(!absentAncient && role!="host")JigsawEditorAction.Submit(session,JigsawEditorOperation.ChooseEnd,choice:EndChoice.Automatic);
            if(Environment.GetEnvironmentVariable("JIGSAW_AUTOMATIC_FAILURE")=="1")
            {
                await Until(()=>session.Map.Phase==JigsawPhase.Editing && session.Map.Choices.Count==0,"Host failure returns every peer to editing");
                if(session.Map.Placements.Count!=0 || session.Map.Edges.Count!=0 || !ReferenceEquals(originalPool,session.Map.Pool))throw new Exception("Host failure must preserve the original graph and pool.");
                File.WriteAllText(Path.Combine(directory,role+".automatic-failed"),"editing, original pool and graph, no votes");
                await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".automatic-failed"))),"all peers accept Host failure");
                if(host!=null)await Until(()=>children.All(p=>p.HasExited),"failure clients finished");
                if(children.Any(p=>p.ExitCode!=0))throw new Exception("Failure scenario client failed.");
                Console.WriteLine("ENET Host automatic failure converged role="+role);
                return;
            }
            if(Environment.GetEnvironmentVariable("JIGSAW_HOSTEXIT_WORKER")=="1")
            {
                await Until(()=>session.Map.Phase==JigsawPhase.Generating && (host==null || workerStarted),"Host worker running");
                var generation=session.ResumeAutomatic();
                File.WriteAllText(Path.Combine(directory,role+".worker-ready"),"ready");
                await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".worker-ready"))),"all workers waiting");
                if(host!=null)host.Disconnect(NetError.Quit);
                else await Until(()=>!client!.IsConnected,"Host worker socket shutdown");
                if(JigsawNativeSession.Current!=null)throw new Exception("Worker session must close immediately.");
                await Until(()=>generation.IsCompleted,"closed generation resolves before blocked worker returns");
                await generation;
                blockedWorker.TrySetResult(null);
                for(int i=0;i<30;i++)await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                if(session.Map.Phase!=JigsawPhase.Generating || session.BeforeAncient!=null)throw new Exception("Late worker must not commit or enter Ancient.");
                if(host!=null){await Until(()=>children.All(p=>p.HasExited),"worker clients exit");if(children.Any(p=>p.ExitCode!=0))throw new Exception("Worker child failed.");}
                Console.WriteLine("ENET closed worker resolved without late commit role="+role);
                return;
            }
            await Until(()=>session.Map.Phase==JigsawPhase.Ancient && session.Native.CurrentRoom is MegaCrit.Sts2.Core.Rooms.EventRoom,"unanimous automatic consensus enters native Ancient");
            File.WriteAllText(Path.Combine(directory,role+".ancient-entered"),"entered");
            await Until(()=>remainingRoles.All(r=>File.Exists(Path.Combine(directory,r+".ancient-entered"))),"all players enter Ancient");
            JigsawEditorAction.Submit(session,JigsawEditorOperation.CompleteAncient);
            for(int i=0;i<60;i++){net.Update();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);}
            if(session.Map.CreateSave().AncientCompleted.Any())throw new Exception("Premature Ancient completion must reject without actual native reward.");
            File.WriteAllText(Path.Combine(directory,role+".premature-checked"),"checked");
            await Until(()=>remainingRoles.All(r=>File.Exists(Path.Combine(directory,r+".premature-checked"))),"all peers reject premature completion before choosing rewards");
            await saves.SaveRun(RunManager.Instance.ToSave(null),true);
            string partialPath=RunSaveManager.GetRunSavePath(80,"current_run_mp.save");
            var completeBefore=JsonNode.Parse(io.ReadFile(partialPath)!);
            var synchronizer=RunManager.Instance.EventSynchronizer;
            if(pendingAncient)
            {
                if(net.NetId==3)synchronizer.ChooseLocalOption(synchronizer.GetLocalEvent().CurrentOptions.ToList().FindIndex(o=>o.Relic?.Id.Entry==Reward));
                await Until(()=>pendingCards.Length>0,"third owner native reward blocked after relic obtain");
                if(!native.Players.Single(p=>p.NetId==3).Relics.Any(r=>r.Id.Entry==Reward))throw new Exception("Blocked native task must have already granted relic.");
                File.WriteAllText(Path.Combine(directory,role+".pending-ready"),"ready");
                await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".pending-ready"))),"blocked native replicas ready");
                if(host!=null)host.DisconnectClient(3,NetError.Quit,true);
                if(role=="3"){await Until(()=>!client!.IsConnected,"pending owner disconnected");return;}
                await Until(()=>session.Map.ActivePlayers.Count==2,"pending owner removed from active roster");
                remainingRoles=["host","2"];
            }
            foreach(ulong owner in (absentAncient || pendingAncient ? new ulong[]{2,1} : new ulong[]{2,1,3}))
            {
                var ownerPlayer=native.Players.Single(p=>p.NetId==owner);
                int cardCount=ownerPlayer.Deck.Cards.Count;
                int upgrades=ownerPlayer.Deck.Cards.Count(c=>c.IsUpgraded);
                if(net.NetId==owner)
                {
                    var local=synchronizer.GetLocalEvent();
                    int index=local.CurrentOptions.ToList().FindIndex(o=>o.Relic?.Id.Entry==Reward);
                    if(index<0)throw new Exception("Native fixture reward missing: "+Reward);
                    synchronizer.ChooseLocalOption(index);
                }
                await Until(()=>session.NativeAncientFinished(owner),"actual native reward owner="+owner);
                await Until(()=>session.Map.CreateSave().AncientCompleted.Contains(owner),"host accepts native owner="+owner);
                if(!native.Players.Single(p=>p.NetId==owner).Relics.Any(r=>r.Id.Entry==Reward))throw new Exception("Native relic absent after finished event.");
#if STS2_PUBLIC
                if(ownerPlayer.Deck.Cards.Count!=cardCount-1)throw new Exception("Actual scissors must remove one legal owner card.");
#else
                if(ownerPlayer.Deck.Cards.Count(c=>c.IsUpgraded)!=upgrades+1)throw new Exception("Actual pomander must upgrade one legal owner card.");
#endif
                if(owner!=(absentAncient || pendingAncient ? 1UL : 3UL))
                {
                    if(session.BeforeAncient==null)throw new Exception("Partial native reward must retain complete rollback checkpoint.");
                    await saves.SaveRun(RunManager.Instance.ToSave(null),true);
                    if(!JsonNode.DeepEquals(completeBefore,JsonNode.Parse(io.ReadFile(partialPath)!)))throw new Exception("Partial native reward serialization must preserve the WHOLE pre-Ancient native and Ritsu document.");
                }
                if(owner==2 && Environment.GetEnvironmentVariable("JIGSAW_HOSTEXIT")=="1")
                {
                    File.WriteAllText(Path.Combine(directory,role+".partial-ready"),"ready");
                    await Until(()=>remainingRoles.All(r=>File.Exists(Path.Combine(directory,r+".partial-ready"))),"partial reward peers ready");
                    if(host!=null)host.Disconnect(NetError.Quit);
                    else await Until(()=>!client!.IsConnected,"actual Host socket shutdown");
                    if(JigsawNativeSession.Current!=null)throw new Exception("Socket shutdown must immediately invalidate live Jigsaw session before native cleanup.");
                    await saves.SaveRun(RunManager.Instance.ToSave(null),true);
                    if(!JsonNode.DeepEquals(completeBefore,JsonNode.Parse(io.ReadFile(partialPath)!)))throw new Exception("Closed session must retain whole pre-Ancient native and Ritsu save protection.");
                    if(JigsawEditorAction.Submit(session,JigsawEditorOperation.ContinueAncient).Success)throw new Exception("Closed session must reject late actions.");
                    if(host!=null){await Until(()=>children.All(p=>p.HasExited),"Host exit clients verified");if(children.Any(p=>p.ExitCode!=0))throw new Exception("Host exit child failed.");}
                    Console.WriteLine("ENET immediate Host exit protected partial reward role="+role);
                    return;
                }
                Console.WriteLine($"ENET NATIVE REWARD role={role} owner={owner} reward={Reward} before={session.BeforeAncient!=null}");
                File.WriteAllText(Path.Combine(directory,role+".reward-"+owner),"done");
                await Until(()=>remainingRoles.All(r=>File.Exists(Path.Combine(directory,r+".reward-"+owner))),"all replicas native reward="+owner);
            }
            if(pendingAncient)
            {
                await session.Continue();
                await Until(()=>session.Map.Phase==JigsawPhase.Exploring,"active owners continue while original native effects remain pending");
                if(session.BeforeAncient==null)throw new Exception("Running absent native reward must retain whole prereward checkpoint.");
                await saves.SaveRun(RunManager.Instance.ToSave(null),true);
                if(!JsonNode.DeepEquals(completeBefore,JsonNode.Parse(io.ReadFile(partialPath)!)))throw new Exception("Running absent native reward must save WHOLE prereward native and Ritsu document.");
                if(Environment.GetEnvironmentVariable("JIGSAW_PENDING_ANCIENT_FAULT")=="1")
                {
                    pendingSelection.TrySetException(new InvalidOperationException("Interrupted native card selection fixture."));
                    for(int i=0;i<90;i++){net.Update();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);}
                    if(session.BeforeAncient==null || session.NativeAncientFinished(3))throw new Exception("Faulted absent native reward must retain whole rollback checkpoint without fake completion.");
                    await saves.SaveRun(RunManager.Instance.ToSave(null),true);
                    if(!JsonNode.DeepEquals(completeBefore,JsonNode.Parse(io.ReadFile(partialPath)!)))throw new Exception("Faulted absent reward must never persist partial native state.");
                    File.WriteAllText(Path.Combine(directory,role+".fault-checked"),"checked");
                    await Until(()=>remainingRoles.All(r=>File.Exists(Path.Combine(directory,r+".fault-checked"))),"remaining replicas retain failed reward rollback");
                    if(host!=null){await Until(()=>children.All(p=>p.HasExited),"fault scenario clients exit");if(children.Any(p=>p.ExitCode!=0))throw new Exception("Fault scenario child failed.");}
                    Console.WriteLine("ENET PENDING fault retained whole prereward checkpoint role="+role);
                    return;
                }
                pendingSelection.TrySetResult(pendingCards);
                await Until(()=>session.NativeAncientFinished(3) && session.Map.CreateSave().AncientCompleted.Contains(3UL) && session.BeforeAncient==null,"late original owner settles native effects and boundary");
#if STS2_PUBLIC
                if(native.Players.Single(p=>p.NetId==3).Deck.Cards.Contains(pendingCards[0]))throw new Exception("Late scissors must remove selected card.");
#else
                if(!pendingCards[0].IsUpgraded)throw new Exception("Late pomander must upgrade selected card.");
#endif
                Console.WriteLine("ENET PENDING absent native reward settled with whole rollback protection role="+role);
            }
            await session.WaitForSave();
            await Until(()=>remainingOwners.All(player=>session.Map.CreateSave().AncientCompleted.Contains(player)),"personal Ancient completions converge");
            var persisted=saves.LoadMultiplayerRunSave();
            if(!persisted.Success||persisted.SaveData==null)throw new Exception("Accepted Ancient completion must remain readable from the native multiplayer save.");
            string savePath=RunSaveManager.GetRunSavePath(80,"current_run_mp.save");
            var document=JsonNode.Parse(io.ReadFile(savePath)??throw new Exception("Native multiplayer save is missing."));
            JsonNode? FindJigsaw(JsonNode? node)
            {
                if(node is JsonObject obj)
                {
                    if(obj.TryGetPropertyValue("jigsaw",out var jigsaw))return jigsaw;
                    foreach(var value in obj.Select(property=>property.Value))if(FindJigsaw(value) is {} found)return found;
                }
                else if(node is JsonArray array)foreach(var value in array)if(FindJigsaw(value) is {} found)return found;
                return null;
            }
            var extension=FindJigsaw(document)?["data"]?["Map"]?["AncientCompleted"] as JsonArray;
            var completed=extension?.Select(item=>(ulong)item!).Order().ToArray();
            if(completed==null||!completed.SequenceEqual(remainingOwners))
                throw new Exception("Native multiplayer save must persist all accepted personal Ancient completions; got "+(extension?.ToJsonString()??"missing extension data"));
            if(absentAncient)
            {
                if(native.Players.Count!=3 || session.Map.OriginalPlayers.Count!=3 || !session.Map.ActivePlayers.Order().SequenceEqual(new ulong[]{1,2}) || session.Map.CreateSave().AncientCompleted.Contains(3UL))throw new Exception("Original roster and absent incomplete Ancient record must survive active-only completion.");
                if(session.NativeAncientFinished(3) || System.Text.Json.JsonSerializer.Serialize(native.Players.Single(p=>p.NetId==3).ToSerializable())!=absentState)throw new Exception("Absent owner must receive no rewards or synthetic native completion.");
                if(session.BeforeAncient!=null || JsonNode.DeepEquals(completeBefore,document))throw new Exception("Finished active owners must release whole native checkpoint and persist final effects.");
                if(persisted.SaveData!.Players.Count!=3)throw new Exception("Final save must retain all original native players.");
                Console.WriteLine("ENET ABSENT Ancient original=3 active=2 untouched=3 completed=1,2 final whole save role="+role);
            }
            if(!session.AncientReady)throw new Exception("Every connected player must finish their own Ancient reward before shared continuation.");
            await session.Continue();
            await Until(()=>session.Map.Phase==JigsawPhase.Exploring,"all-player continuation consensus enters exploration");
            if (!absentAncient)
            {
                HeadlessUiHost.EnsureInputActions();
                using var nativeUi = new JigsawNativeMapUiHost();
                var mapScreen = await nativeUi.Open(session);
                if (!mapScreen.IsProcessingInput() || session.Map.ActivePlayers.Count != 3)
                    throw new Exception("A fresh native lobby retains all three map voters and enables each production screen.");
                int target = session.Map.AvailableDestinations.First();
                if (role == "2")
                {
                    var position = mapScreen.MapControl.GlobalPosition + mapScreen.GridToCanvas(session.Map.Nodes.Single(node => node.Id == target).Center);
                    Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = position, GlobalPosition = position });
                    await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                    Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = position, GlobalPosition = position });
                }
                await Until(() => session.Map.DestinationVotes.ContainsKey(2), "a fresh Client casts its first exploration ballot through production ShowMap");
                if (session.Map.DestinationVotes.Count != 1 || session.Map.PendingEntry != null || session.Map.Steps != 0 ||
                    mapScreen.MapControl.GetParent().FindChild("DestinationVoters" + target, true, false) is not HBoxContainer row || row.GetChildCount() != 1)
                    throw new Exception("The fresh Client vote shows one portrait on every peer and never substitutes a Host ballot.");
                File.WriteAllText(Path.Combine(directory, role + ".fresh-map-vote"), "visible");
                await Until(() => remainingRoles.All(peer => File.Exists(Path.Combine(directory, peer + ".fresh-map-vote"))), "every fresh native endpoint observes the Client's first portrait");
            }
            File.WriteAllText(Path.Combine(directory,role+".ready-to-stop"),"ready");
            if(host!=null)
            {
                await Until(()=>remainingRoles.All(r=>File.Exists(Path.Combine(directory,r+".ready-to-stop"))),"all peers finish before Host exit");
                RunManager.Instance.CleanUp(false);
                File.WriteAllText(Path.Combine(directory,"host.stopped"),"stopped");
                await Until(()=>children.All(p=>p.HasExited),"clients exit after native Host shutdown");
            }
            else
            {
                await Until(()=>File.Exists(Path.Combine(directory,"host.stopped")),"native Host shutdown");
                await Until(()=>!client!.IsConnected,"native Host exit disconnects clients");
            }
            if(children.Any(p=>p.ExitCode!=0))throw new Exception("ENet child failed: "+directory);
        }
        finally
        {
            foreach(var child in children){if(!child.HasExited)child.Kill(true);child.Dispose();}
            harmony.UnpatchAll(harmony.Id);
            isolatedSaveManager=null;
            RunManager.Instance.CleanUp(false);
        }
    }
}
