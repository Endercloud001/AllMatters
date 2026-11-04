// THROWAWAY: keep original task chain, suppress its rewards in a narrow experiment.
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

public sealed class PrototypeHuntingRewardRelic : RelicModel
{
    public override MegaCrit.Sts2.Core.Entities.Relics.RelicRarity Rarity => MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Event;
    private List<Reward>? _rewards;
    public int NonRewardCounter { get; private set; }
    public override bool TryModifyRewards(Player player,List<Reward> rewards,AbstractRoom? room)
    {
        if(player!=Owner||room is not TreasureRoom)return false;
        _rewards=rewards;rewards.Add(new GoldReward(7,player));return true;
    }
    public override async Task AfterModifyingRewards()
    {
        await Task.Yield();
        NonRewardCounter++;
        _rewards!.Add(new GoldReward(9,Owner));
    }
}

internal static class PrototypeHuntingTreasureTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingTreasure native Spoils quest and async extras without rewards",Treasure)];
    private static bool NormalPath;
    private static bool TreasureGate(Player player,ref bool __result)
    {
        if(player.RunState.CurrentRoom is not TreasureRoom)return true;
        __result=NormalPath;return false;
    }
    private static bool NoGold(Player player,ref Task __result)
    {
        if(!NormalPath||player.RunState.CurrentRoom is not TreasureRoom)return true;
        __result=Task.CompletedTask;return false;
    }
    private static void EnterNormal()=>NormalPath=true;
    private static void ExitNormal(ref Task<int> __result)
    {
        var original=__result;__result=Finish();
        async Task<int> Finish(){try{await original;return 0;}finally{NormalPath=false;}}
    }
    private static void ClearFinal(RewardsSet __instance,ref Task __result)
    {
        if(__instance.Room is not TreasureRoom)return;
        var original=__result;__result=Finish();
        async Task Finish(){await original;__instance.Rewards.Clear();}
    }
    private static async Task Treasure()
    {
        using var outside=HeadlessCombatInProgress.EnterEvent();
        ModelDb.Inject(typeof(PrototypeHuntingRewardRelic));
        var patch=new Harmony("allmatters.prototype70.treasure");
        patch.Patch(AccessTools.Method(typeof(Hook),nameof(Hook.ShouldGenerateTreasure)),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureTests),nameof(TreasureGate)));
        patch.Patch(AccessTools.Method(typeof(PlayerCmd),nameof(PlayerCmd.GainGold)),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureTests),nameof(NoGold)));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),"DoTreasureRoomRewards"),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureTests),nameof(EnterNormal)),postfix:new HarmonyMethod(typeof(PrototypeHuntingTreasureTests),nameof(ExitNormal)));
        patch.Patch(AccessTools.Method(typeof(RewardsSet),nameof(RewardsSet.GenerateWithoutOffering)),postfix:new HarmonyMethod(typeof(PrototypeHuntingTreasureTests),nameof(ClearFinal)));
        try
        {
            for(int n=1;n<=4;n++)
            {
                var players=Enumerable.Range(1,n).Select(i=>Player.CreateForNewRun(ModelDb.Character<Ironclad>(),UnlockState.all,(ulong)i)).ToArray();
                var run=RunState.CreateForTest(players,acts:[ModelDb.Act<Hive>()],seed:"HUNTING70_TREASURE");
                var prior=MegaCrit.Sts2.Core.Context.LocalContext.NetId;
                RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
                try
                {
                    run.Map=new SpoilsActMap(run);
                    var point=run.Map.GetAllMapPoints().Single(p=>p.PointType==MapPointType.Treasure);
                    run.AddVisitedMapCoord(point.coord);
                    run.AppendToMapPointHistory(MapPointType.Treasure,RoomType.Treasure,null);
                    var room=new TreasureRoom(0);run.PushRoom(room);
                    foreach(var p in players)
                    {
                        var card=run.CreateCard<SpoilsMap>(p);p.Deck.AddInternal(card);
                        card.SpoilsActIndex=run.CurrentActIndex;
                        card.ModifyGeneratedMapLate(run,run.Map,run.CurrentActIndex);
                        await card.AfterMapGenerated(run.Map,run.CurrentActIndex);
                        NativeEventTests.Check(point.Quests.Contains(card),"Native Spoils map marker attached");
                        var face=await RelicCmd.Obtain<AllMatters.Relics.NlothsHungryFace>(p);
                        await Hook.AfterRoomEntered(run,room);
                        NativeEventTests.Check(face.Spent,"Face consumed despite empty hunting chest");
                        var extra=(PrototypeHuntingRewardRelic)ModelDb.Relic<PrototypeHuntingRewardRelic>().ToMutable();p.AddRelicInternal(extra);
                        int gold=p.Gold;
                        await (Task<int>)AccessTools.Method(typeof(OneOffSynchronizer),"DoTreasureRoomRewards").Invoke(RunManager.Instance.OneOffSynchronizer,[p])!;
                        NativeEventTests.Check(p.Gold==gold&&!p.Deck.Cards.Contains(card)&&!point.Quests.Contains(card),"No ordinary/600 gold; native task removes card and marker");
                        NativeEventTests.Check(run.CurrentMapPointHistoryEntry!.GetEntry(p.NetId).CompletedQuests.Contains(card.Id),"Native task completion still recorded");
                        var rewards=new RewardsSet(p).EmptyForRoom(room).WithCustomRewards([new GoldReward(20,p)]);
                        await rewards.GenerateWithoutOffering();
                        NativeEventTests.Check(rewards.Rewards.Count==0&&extra.NonRewardCounter==1,"Sync/async rewards cancelled after real async hook; nonreward counter retained");
                        Console.WriteLine($"TREASURE players={n} player={p.NetId} nativeSpoilsMap=true gold=0 questComplete=true cardRemoved=true markerRemoved=true faceSpent=true asyncCounter=1 rewards=0; open animation NOT tested");
                    }
                    var sync=new TreasureRoomRelicSynchronizer(run,1,RunManager.Instance.ActionQueueSynchronizer,run.SharedRelicGrabBag,run.Rng.Niche);
                    bool finished=false;sync.RelicsAwarded+=_=>finished=true;
                    sync.BeginRelicPicking();
                    NativeEventTests.Check(sync.CurrentRelics==null||sync.CurrentRelics.Count==0,"No relic rewards generated");
                    sync.CompleteWithNoRelics();
                    NativeEventTests.Check(finished,"All-empty native relic voting finishes without waiting for votes");
                    // Continue through the actual room entry/reward wrappers used by
                    // OpenChest. The full screen/animation is deliberately not replaced.
                    var roomSync=RunManager.Instance.TreasureRoomRelicSynchronizer;
                    bool roomFinished=false;roomSync.RelicsAwarded+=_=>roomFinished=true;
                    await room.EnterInternal(run,false);
                    NativeEventTests.Check(roomSync.CurrentRelics==null||roomSync.CurrentRelics.Count==0,"Actual room entry begins empty native relic selection");
                    roomSync.CompleteWithNoRelics();
                    foreach(var p in players)
                    {
                        MegaCrit.Sts2.Core.Context.LocalContext.NetId=p.NetId;int gold=p.Gold;
                        int displayed=await room.DoNormalRewards();
                        NativeEventTests.Check(displayed==0&&p.Gold==gold,"Actual public normal reward wrapper returns zero gold for particle count and gives no gold");
                    }
                    MegaCrit.Sts2.Core.Context.LocalContext.NetId=1;
                    await room.DoExtraRewardsIfNeeded();
                    NativeEventTests.Check(players.All(p=>p.Relics.OfType<PrototypeHuntingRewardRelic>().Single().NonRewardCounter==2),"Actual room extra-reward generation and Offer complete for every player, retaining async nonreward hook exactly once more");
                    NativeEventTests.Check(roomFinished,"Actual room synchronizer reports completion for empty selection");
                    await room.Exit(run);
                    Console.WriteLine($"TREASURE_ROOM players={n} nativeEnter/normal/extra/offer/exit=true normalGoldAndParticleCount=0 asyncCounterPerPlayer=2 emptyVoteNotified=true; no native network or OpenChest visual animation claim");
                }
                finally{NormalPath=false;RunManager.Instance.CleanUp(false);MegaCrit.Sts2.Core.Context.LocalContext.NetId=prior;}
            }
        }
        finally{patch.UnpatchAll(patch.Id);NormalPath=false;}
    }
}



