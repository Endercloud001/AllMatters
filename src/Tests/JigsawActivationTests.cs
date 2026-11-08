using AllMatters;
using AllMatters.Jigsaw;
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
using System.Diagnostics;

#if STS2_PUBLIC
using StartPlayer = MegaCrit.Sts2.Core.Entities.Multiplayer.LobbyPlayer;
#else
using StartPlayer = MegaCrit.Sts2.Core.Entities.Multiplayer.StartRunLobbyPlayer;
#endif
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;

internal static class JigsawActivationTests
{
    internal sealed class NewRunListener : IStartRunLobbyListener
    {
        public StartRunLobby Lobby = null!;
        public RunState? Native;
        public bool ShouldSave;
        // Substitutes the screen/NGame listener; lobby messages and RunManager entry stay native.
        public void BeginRun(string seed,List<ActModel> acts,IReadOnlyList<ModifierModel> modifiers)
        {
            if(modifiers.Count!=0)throw new Exception("Mode identity must not enter gameplay modifiers.");
            Native=RunState.CreateForTest(players:Lobby.Players.Select(p=>Player.CreateForNewRun(p.character,UnlockState.all,p.id)).ToList(),acts:[ModelDb.Act<Overgrowth>()],seed:seed);
            RunManager.Instance.SetUpNewMultiplayer(Native,Lobby,shouldSave:ShouldSave);
        }
        public void PlayerConnected(StartPlayer player){}
        public void PlayerChanged(StartPlayer player,bool isRandomCharacterResolution){}
        public void RemotePlayerDisconnected(StartPlayer player){}
        public void AscensionChanged(){}
        public void SeedChanged(){}
        public void ModifiersChanged(){}
        public void MaxAscensionChanged(){}
        public void LocalPlayerDisconnected(NetErrorInfo info){}
    }
    internal const string Name="Jigsaw native ENet new-run activation";
    public static async Task Run()
    {
        // Keep vanilla model-cache authentication independent of mutation by earlier tests.
        if (Environment.GetEnvironmentVariable("JIGSAW_NET_ROLE") == null)
        {
            foreach(bool disabled in new[]{false,true})
            {
                string logs = Path.Combine(Path.GetTempPath(), "allmatters-enet-" + Guid.NewGuid());
                Directory.CreateDirectory(logs);
                var start = new ProcessStartInfo(OS.GetExecutablePath()) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { "--headless", "--path", ProjectSettings.GlobalizePath("res://"), "--", Name }) start.ArgumentList.Add(arg);
                start.Environment["JIGSAW_NET_ROLE"] = "host";
                start.Environment["JIGSAW_NET_DIR"] = logs;
                start.Environment["JIGSAW_HOST_DISABLED"] = disabled.ToString();
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(50)); }
                finally { if (!process.HasExited) process.Kill(true); }
                File.WriteAllText(Path.Combine(logs, "host.log"), await output);
                File.WriteAllText(Path.Combine(logs, "host.err"), await errors);
                Console.WriteLine($"ENET isolated native host/client evidence: {logs}; host exit={process.ExitCode}");
                if (process.ExitCode != 0) throw new Exception("Native ENet host failed: " + logs);
            }
            return;
        }
        MegaCrit.Sts2.Core.Multiplayer.Serialization.MessageTypes.Initialize();
        var role=Environment.GetEnvironmentVariable("JIGSAW_NET_ROLE")??"host";
        var directory=Environment.GetEnvironmentVariable("JIGSAW_NET_DIR")??Path.Combine(Path.GetTempPath(),"allmatters-enet-"+Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var children=new List<Process>();
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
                if(deadline.Elapsed.TotalSeconds>20)throw new Exception($"ENet {role} timeout: {stage}; logs={directory}");
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
            bool enabled=Environment.GetEnvironmentVariable("JIGSAW_HOST_DISABLED")!="True";
            JigsawPreferences.Enabled=host!=null?enabled:!enabled;
            var listener=new NewRunListener();
            var lobby=new StartRunLobby(GameMode.Standard,net,listener,3);listener.Lobby=lobby;
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
            var session=JigsawNativeSession.Current;
            if((session!=null)!=enabled)throw new Exception("Host-selected Jigsaw must activate through native multiplayer new-run entry on every peer, regardless of client preference.");
            File.WriteAllText(Path.Combine(directory,role+".activated"),"ready");
            await Until(()=>new[]{"host","2","3"}.All(r=>File.Exists(Path.Combine(directory,r+".activated"))),"all peers checked mode identity");
            Console.WriteLine($"ENET ACTIVATED role={role} hostPreference={enabled} clientPreference={!enabled} modifiers={native.Modifiers.Count}");
            if(host!=null)
            {
                await Until(()=>children.All(p=>p.HasExited),"all clients finish activation");
                if(children.Any(p=>p.ExitCode!=0))throw new Exception("Activation client failed: "+directory);
            }
        }
        finally
        {
            foreach(var child in children){if(!child.HasExited)child.Kill(true);child.Dispose();}
            RunManager.Instance.CleanUp(false);
        }
    }
}
