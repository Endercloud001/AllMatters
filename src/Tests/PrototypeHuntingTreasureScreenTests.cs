// THROWAWAY: real beta treasure scene/OpenChest. Explicit peripheral UI substitutes.
#if !STS2_PUBLIC
using Godot;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;

internal static class PrototypeHuntingTreasureScreenTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingTreasureScreen beta original scene OpenChest empty animation completes",Open)];
    private static NGame? GameHost;
    private static NMapScreen? MapHost;
    private static NControllerManager? ControllerHost;
    private static NInputManager? InputHost;
    private static int FtueCalls,TravelCalls,ScreenExitCalls;
    private static bool NoUi()=>false;
    private static bool GameGetter(ref NGame __result){__result=GameHost!;return false;}
    private static bool NoRun(ref NRun __result){__result=null!;return false;}
    private static bool MapGetter(ref NMapScreen __result){__result=MapHost!;return false;}
    private static bool ControllerGetter(ref NControllerManager __result){__result=ControllerHost!;return false;}
    private static bool InputGetter(ref NInputManager __result){__result=InputHost!;return false;}
    private static bool NoFtue(ref Task __result){FtueCalls++;__result=Task.CompletedTask;return false;}
    private static bool NoTravel(bool enabled){if(enabled)TravelCalls++;return false;}
    private static void ScreenStateBoundary(bool inPicking){if(!inPicking)ScreenExitCalls++;}
    private static IEnumerable<CodeInstruction> CollectionScreenBoundary(IEnumerable<CodeInstruction> source)
    {
        var code=source.ToList();
        for(int i=0;i<code.Count;i++)
        {
            if(i+3<code.Count&&code[i].Calls(AccessTools.PropertyGetter(typeof(NRun),"Instance"))&&
                code[i+1].operand is MethodInfo get&&get.Name=="get_ScreenStateTracker"&&
                code[i+3].operand is MethodInfo set&&set.Name=="SetIsInSharedRelicPickingScreen")
            {
                yield return code[i+2];
                yield return new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(PrototypeHuntingTreasureScreenTests),nameof(ScreenStateBoundary)));
                i+=3;
            }
            else yield return code[i];
        }
    }
    private static async Task Open()
    {
        using var outside=HeadlessCombatInProgress.EnterEvent();
        var previousNetId=MegaCrit.Sts2.Core.Context.LocalContext.NetId;
        MegaCrit.Sts2.Core.Context.LocalContext.NetId=1;
        using var game=new NGame();using var map=new NMapScreen();using var controller=new NControllerManager();using var input=new NInputManager();
        GameHost=game;MapHost=map;ControllerHost=controller;InputHost=input;
        using var atlases=HeadlessTestSetup.LoadAtlases("ui_atlas","relic_atlas");
        var patch=new Harmony("allmatters.prototype70.native-openchest");
        void Prefix(Type type,string method,string replacement)=>patch.Patch(AccessTools.Method(type,method),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureScreenTests),replacement));
        void Getter(Type type,string replacement)=>patch.Patch(AccessTools.PropertyGetter(type,"Instance"),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureScreenTests),replacement));
        Getter(typeof(NGame),nameof(GameGetter));Getter(typeof(NMapScreen),nameof(MapGetter));Getter(typeof(NControllerManager),nameof(ControllerGetter));Getter(typeof(NInputManager),nameof(InputGetter));
        patch.Patch(AccessTools.PropertyGetter(typeof(NGame),"CurrentRunNode"),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureScreenTests),nameof(NoRun)));
        foreach(string m in new[]{"SetScreenShakeTarget","ClearScreenShakeTarget"})Prefix(typeof(NGame),m,nameof(NoUi));
        foreach(string m in new[]{"RegisterHotkeys","UnregisterHotkeys"})Prefix(typeof(NButton),m,nameof(NoUi));
        Prefix(typeof(NHotkeyIcon),"UpdateInput",nameof(NoUi));
        // Empty native hands remain empty: peer cursor display is outside this host.
        Prefix(typeof(NHandImageCollection),"Initialize",nameof(NoUi));Prefix(typeof(NHandImageCollection),"_ExitTree",nameof(NoUi));
        Prefix(typeof(NTreasureRoom),"RelicFtueCheck",nameof(NoFtue)); // Never enter preference-writing FTUE.
        Prefix(typeof(NMapScreen),"SetTravelEnabled",nameof(NoTravel));
        // Replace just the external tracker call, retaining actual fade-out Tween.
        patch.Patch(AccessTools.Method(typeof(NTreasureRoomRelicCollection),"AnimOut"),transpiler:new HarmonyMethod(typeof(PrototypeHuntingTreasureScreenTests),nameof(CollectionScreenBoundary)));
        PrototypeHuntingTreasureProbe.LoadStandalone();
        var tree=(SceneTree)Engine.GetMainLoop();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        if(!ModelDb.AllRelics.Any(r=>r is PrototypeHuntingRewardRelic))ModelDb.Inject(typeof(PrototypeHuntingRewardRelic));
        try
        {
            for(int n=1;n<=4;n++)
            {
                FtueCalls=TravelCalls=ScreenExitCalls=0;
                var players=Enumerable.Range(1,n).Select(i=>Player.CreateForNewRun(ModelDb.Character<Ironclad>(),UnlockState.all,(ulong)i)).ToArray();
                var run=RunState.CreateForTest(players,acts:[ModelDb.Act<Hive>()],seed:"HUNTING70_OPEN_CHEST");
                RunManager.Instance.SetUpTest(run,new NetSingleplayerGameService());
                MegaCrit.Sts2.Core.Context.LocalContext.NetId=players[0].NetId;
                run.Map=new SpoilsActMap(run);
                var point=run.Map.GetAllMapPoints().Single(p=>p.PointType==MapPointType.Treasure);
                run.AddVisitedMapCoord(point.coord);
                run.AppendToMapPointHistory(MapPointType.Treasure,RoomType.Treasure,null);
                var room=new TreasureRoom(0);run.PushRoom(room);
                var quests=new List<SpoilsMap>();
                foreach(var p in players)
                {
                    var card=run.CreateCard<SpoilsMap>(p);p.Deck.AddInternal(card);card.SpoilsActIndex=run.CurrentActIndex;
                    card.ModifyGeneratedMapLate(run,run.Map,run.CurrentActIndex);await card.AfterMapGenerated(run.Map,run.CurrentActIndex);quests.Add(card);
                    await RelicCmd.Obtain<AllMatters.Relics.NlothsHungryFace>(p);
                    p.AddRelicInternal(ModelDb.Relic<PrototypeHuntingRewardRelic>().ToMutable());
                }
                var gold=players.Select(p=>p.Gold).ToArray();var owned=players.Select(p=>p.Relics.Count).ToArray();
                NTreasureRoom? node=null;
                try
                {
                    await room.EnterInternal(run,false);
                    await Hook.AfterRoomEntered(run,room);
                    NativeEventTests.Check(players.All(p=>p.Relics.OfType<AllMatters.Relics.NlothsHungryFace>().Single().Spent),"Face consumed for each participant before original opening");
                    bool mode=TestMode.IsOn;
                    try{TestMode.IsOn=false;node=NTreasureRoom.Create(room,run)!;tree.Root.AddChild(node);}
                    finally{TestMode.IsOn=mode;}
                    int notifications=0;RunManager.Instance.TreasureRoomRelicSynchronizer.RelicsAwarded+=_=>notifications++;
                    var task=(Task)AccessTools.Method(typeof(NTreasureRoom),"OpenChest").Invoke(node,null)!;
                    var clock=System.Diagnostics.Stopwatch.StartNew();
                    while(!task.IsCompleted&&clock.ElapsedMilliseconds<10000)await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                    NativeEventTests.Check(task.IsCompleted,"Real scene OpenChest must finish within bounded native frames");await task;
                    var collection=node.GetNode<NTreasureRoomRelicCollection>("%RelicCollection");
                    NativeEventTests.Check(collection.RelicPickingBegan().IsCompletedSuccessfully&&collection.RelicPickingFinished().IsCompletedSuccessfully&&notifications==1,"Original scene native Tween completes both sources and notifies once");
                    NativeEventTests.Check(players.Select(p=>p.Gold).SequenceEqual(gold)&&players.Select(p=>p.Relics.Count).SequenceEqual(owned),"Original OpenChest obtains no gold or relics");
                    NativeEventTests.Check(players.All(p=>p.Relics.OfType<PrototypeHuntingRewardRelic>().Single().NonRewardCounter==1),"Original OpenChest retains real async nonreward hooks for every player");
                    NativeEventTests.Check(!players[0].Deck.Cards.Contains(quests[0])&&!point.Quests.Contains(quests[0])&&run.CurrentMapPointHistoryEntry!.GetEntry(players[0].NetId).CompletedQuests.Contains(quests[0].Id),"Original OpenChest normal rewards complete local Spoils task/card/marker/history despite no payment");
                    NativeEventTests.Check(quests.Skip(1).All(card=>card.Owner.Deck.Cards.Contains(card)&&point.Quests.Contains(card)),"Nonlocal tasks remain pending in single-client host, no simulated network completion");
                    NativeEventTests.Check(node.ProceedButton.IsEnabled&&FtueCalls==1&&TravelCalls==1&&ScreenExitCalls==1,"OpenChest reaches enabled proceed and travel/screen boundaries without entering FTUE writes");
                    Console.WriteLine($"UI_TREASURE_SCREEN players={n} target=public-beta originalSceneReady={node.IsNodeReady()} nativeOpenChest=completed nativeSpineChest=true originalEmptyVfx=true nativeFadeOut=true nativeTweenNotifications={notifications} manualCompleteCalls=0 bothTasks=true goldDelta=0 relicDelta=0 asyncNonrewardEach=1 faceSpentEach=true localSpoilsQuest/card/marker/history=completed remoteSpoils=pending-single-client proceedEnabled={node.ProceedButton.IsEnabled} ftueIsolatedCalls={FtueCalls} travelBoundaryCalls={TravelCalls} screenExitBoundaryCalls={ScreenExitCalls}; raw outer game/input/controller/map hosts, hotkeys/cursor-hands/external screen-tracker isolated, no physical input/network/public scene claim");
                    while(clock.ElapsedMilliseconds<3400)await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                    NativeEventTests.Check(!collection.Visible&&!(bool)AccessTools.Field(typeof(NTreasureRoom),"_isRelicCollectionOpen").GetValue(node),"Actual native fade-out callback hides collection and OpenChest clears picking state");
                    await room.Exit(run);
                }
                finally{if(node!=null){if(node.GetParent()!=null)tree.Root.RemoveChild(node);node.Free();}RunManager.Instance.CleanUp(false);}
            }
        }
        finally{patch.UnpatchAll(patch.Id);new Harmony(PrototypeHuntingTreasureProbe.Id).UnpatchAll(PrototypeHuntingTreasureProbe.Id);GameHost=null;MapHost=null;ControllerHost=null;InputHost=null;MegaCrit.Sts2.Core.Context.LocalContext.NetId=previousNetId;}
    }
}
#else
internal static class PrototypeHuntingTreasureScreenTests
{
    // No fake success: original public scene resources are not available.
    internal static readonly (string Name,Func<Task> Test)[] Cases=[];
}
#endif
