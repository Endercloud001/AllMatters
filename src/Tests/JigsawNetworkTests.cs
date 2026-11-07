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
            var native=RunState.CreateForTest(players:new ulong[]{1,2,3}.Select(id=>Player.CreateForNewRun(ModelDb.Character<Ironclad>(),UnlockState.all,id)).ToList(),acts:[ModelDb.Act<Overgrowth>()],seed:"ENET_EDITOR_80");
            RunManager.Instance.SetUpTest(native,net,shouldSave:true);
            RunManager.Instance.GenerateRooms();await RunManager.Instance.GenerateMap();
            var session=JigsawNativeSession.Enable(native);await session.StartAct();
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
            Console.WriteLine("ENET CLIENT UI rotation/cancel converged role="+role);
            JigsawEditorAction.Submit(session,JigsawEditorOperation.ChooseEnd,choice:EndChoice.Automatic);
            await Until(()=>session.Map.Phase==JigsawPhase.Ancient && session.Native.CurrentRoom is MegaCrit.Sts2.Core.Rooms.EventRoom,"unanimous automatic consensus enters native Ancient");
            File.WriteAllText(Path.Combine(directory,role+".ancient-entered"),"entered");
            await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".ancient-entered"))),"all players enter Ancient");
            session.AncientFinished(net.NetId);
            await session.WaitForSave();
            await Until(()=>new ulong[]{1,2,3}.All(player=>session.Map.CreateSave().AncientCompleted.Contains(player)),"personal Ancient completions converge");
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
            if(completed==null||!completed.SequenceEqual(new ulong[]{1,2,3}))
                throw new Exception("Native multiplayer save must persist all accepted personal Ancient completions; got "+(extension?.ToJsonString()??"missing extension data"));
            if(!session.AncientReady)throw new Exception("Every connected player must finish their own Ancient reward before shared continuation.");
            await session.Continue();
            await Until(()=>session.Map.Phase==JigsawPhase.Exploring,"all-player continuation consensus enters exploration");
            File.WriteAllText(Path.Combine(directory,role+".ready-to-stop"),"ready");
            if(host!=null)
            {
                await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".ready-to-stop"))),"all peers finish before Host exit");
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
