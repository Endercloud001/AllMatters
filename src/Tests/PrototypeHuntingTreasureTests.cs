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
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingTreasure native Spoils quest and async extras without rewards",Treasure),("PrototypeHuntingTreasure NEGATIVE direct async native grant bypass measurement",DirectCommands),("PrototypeHuntingTreasure primitive cancellation and caller contract measurements",CancellationContracts)];
    private static bool DirectInjected,ContextualCarrier;
    private static void InjectDirect(){if(DirectInjected)return;ModelDb.Inject(typeof(PrototypeHuntingDirectRewardRelic));DirectInjected=true;}
    private static bool BlockAllTreasureGold(Player player,ref Task __result)
    {if(player.RunState.CurrentRoom is not TreasureRoom)return true;__result=Task.CompletedTask;return false;}
    private static bool ReturnWithoutReceipt(RelicModel relic,Player player,ref Task<RelicModel> __result)
    {
        if(player.RunState.CurrentRoom is not TreasureRoom)return true;
        // Experimental carriers only, never selected as the production API.
        if(ContextualCarrier)relic.Owner=player;
        __result=Task.FromResult(relic);return false;
    }
    private static async Task CancellationContracts()
    {
        using var outside=HeadlessCombatInProgress.EnterEvent();InjectDirect();
        var patch=new Harmony("allmatters.prototype70.treasure-return-contract");
        patch.Patch(AccessTools.Method(typeof(PlayerCmd),nameof(PlayerCmd.GainGold)),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureTests),nameof(BlockAllTreasureGold)));
        patch.Patch(AccessTools.Method(typeof(RelicCmd),nameof(RelicCmd.Obtain),[typeof(RelicModel),typeof(Player),typeof(int)]),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureTests),nameof(ReturnWithoutReceipt)));
        patch.Patch(AccessTools.Method(typeof(RewardsSet),nameof(RewardsSet.GenerateWithoutOffering)),postfix:new HarmonyMethod(typeof(PrototypeHuntingTreasureTests),nameof(ClearFinal)));
        try
        {
            foreach(int mode in new[]{0,1,2})for(int n=1;n<=4;n++)
            {
                ContextualCarrier=mode!=0;var run=HeadlessTestSetup.Run("HUNTING70_REWARD_RETURN",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
                RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
                try
                {
                    run.AppendToMapPointHistory(MapPointType.Treasure,RoomType.Treasure,null);var room=new TreasureRoom(0);run.PushRoom(room);
                    foreach(var p in run.Players)
                    {
                        var extra=(PrototypeHuntingDirectRewardRelic)ModelDb.Relic<PrototypeHuntingDirectRewardRelic>().ToMutable();extra.UseReturnedOwner=true;extra.RequireInventoryReceipt=mode==2;p.AddRelicInternal(extra);
                        int gold=p.Gold,owned=p.Relics.Count,history=run.CurrentMapPointHistoryEntry!.GetEntry(p.NetId).RelicChoices.Count;
                        bool bag=p.RelicGrabBag.Contains(ModelDb.Relic<MegaCrit.Sts2.Core.Models.Relics.Anchor>());
                        var rewards=new RewardsSet(p).EmptyForRoom(room).WithCustomRewards([new GoldReward(20,p)]);Exception? error=null;
                        try{await rewards.GenerateWithoutOffering();}catch(Exception e){error=e;}
                        NativeEventTests.Check(p.Gold==gold&&p.Relics.Count==owned&&run.CurrentMapPointHistoryEntry.GetEntry(p.NetId).RelicChoices.Count==history&&p.RelicGrabBag.Contains(ModelDb.Relic<MegaCrit.Sts2.Core.Models.Relics.Anchor>())==bag,"Primitive denial causes no actual gold/relic/history/bag receipt");
                        NativeEventTests.Check(extra.NonRewardCounter==(mode==1?1:0),"Carrier compatibility is measured: bare Owner access fails; contextual carrier permits Owner-only caller, actual-receipt assertion still fails");
                        Console.WriteLine($"TREASURE_RETURN players={n} player={p.NetId} mode={(mode==0?"bare":mode==1?"contextual-owner":"contextual-requires-receipt")} nativeGoldDelta=0 nativeRelicDelta=0 nativeHistoryDelta=0 bagUnchanged=true carrierInInventory=false hookCounterExpected=1 hookCounterActual={extra.NonRewardCounter} error={error?.GetType().Name??"none"} rewardListCount={rewards.Rewards.Count} productionStrategySelected=false; contextual carrier is only a probe, caller compatibility is NOT universally proven");
                    }
                    run.PopCurrentRoom();var eventModel=ModelDb.Event<AllMatters.Events.Cleric>();run.PushRoom(new EventRoom(eventModel));run.AppendToMapPointHistory(MapPointType.Unknown,RoomType.Event,eventModel.Id);
                    var normal=run.Players[0];int normalGold=normal.Gold,normalRelics=normal.Relics.Count;
                    await PlayerCmd.GainGold(11,normal);await RelicCmd.Obtain<MegaCrit.Sts2.Core.Models.Relics.Anchor>(normal);
                    NativeEventTests.Check(normal.Gold==normalGold+11&&normal.Relics.Count==normalRelics+1,"Same primitive guards allow ordinary event native gold/relic receipt");
                    Console.WriteLine($"TREASURE_RETURN_EVENT players={n} mode={mode} ordinaryEvent=Cleric nativeGainGold11=allowed nativeObtainAnchor=allowed; guards remain installed, no room reward-policy false positive");
                }
                finally{RunManager.Instance.CleanUp(false);}
            }
        }
        finally{patch.UnpatchAll(patch.Id);ContextualCarrier=false;}
    }
    private static async Task DirectCommands()
    {
        using var outside=HeadlessCombatInProgress.EnterEvent();InjectDirect();
        var patch=new Harmony("allmatters.prototype70.treasure-direct-negative");
        patch.Patch(AccessTools.Method(typeof(Hook),nameof(Hook.ShouldGenerateTreasure)),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureTests),nameof(TreasureGate)));
        patch.Patch(AccessTools.Method(typeof(PlayerCmd),nameof(PlayerCmd.GainGold)),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureTests),nameof(NoGold)));
        patch.Patch(AccessTools.Method(typeof(RewardsSet),nameof(RewardsSet.GenerateWithoutOffering)),postfix:new HarmonyMethod(typeof(PrototypeHuntingTreasureTests),nameof(ClearFinal)));
        try
        {
            for(int n=1;n<=4;n++)
            {
                var run=HeadlessTestSetup.Run("HUNTING70_DIRECT_REWARDS",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
                RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
                try
                {
                    run.AppendToMapPointHistory(MapPointType.Treasure,RoomType.Treasure,null);var room=new TreasureRoom(0);run.PushRoom(room);
                    foreach(var p in run.Players)
                    {
                        var extra=(PrototypeHuntingDirectRewardRelic)ModelDb.Relic<PrototypeHuntingDirectRewardRelic>().ToMutable();p.AddRelicInternal(extra);
                        int gold=p.Gold,owned=p.Relics.Count,history=run.CurrentMapPointHistoryEntry!.GetEntry(p.NetId).RelicChoices.Count;
                        var rewards=new RewardsSet(p).EmptyForRoom(room).WithCustomRewards([new GoldReward(20,p)]);await rewards.GenerateWithoutOffering();
                        bool bypass=p.Gold==gold+11&&p.Relics.Count==owned+1&&run.CurrentMapPointHistoryEntry.GetEntry(p.NetId).RelicChoices.Count==history+1;
                        NativeEventTests.Check(bypass&&rewards.Rewards.Count==0&&extra.NonRewardCounter==1,"NEGATIVE measurement: clearing the list after hooks does not undo actual direct native grants; nonreward hook still completes");
                        Console.WriteLine($"TREASURE_DIRECT_NEGATIVE players={n} player={p.NetId} hook=asyncAfterModifyingRewards input=GainGold11+ObtainAnchor expectedGoldDelta=0 actualGoldDelta={p.Gold-gold} expectedRelicDelta=0 actualRelicDelta={p.Relics.Count-owned} actualPickedHistoryDelta=1 clearedRewardList=0 nonRewardCounter=1 requirementSatisfied=false; measurement passed means bypass reproduced, NOT treasure rule passed");
                    }
                }
                finally{RunManager.Instance.CleanUp(false);}
            }
        }
        finally{patch.UnpatchAll(patch.Id);}
    }
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

public sealed class PrototypeHuntingDirectRewardRelic : RelicModel
{
    public int NonRewardCounter{get;private set;}
    public bool UseReturnedOwner{get;set;}
    public bool RequireInventoryReceipt{get;set;}
    public override MegaCrit.Sts2.Core.Entities.Relics.RelicRarity Rarity=>MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Event;
    public override bool TryModifyRewards(Player player,List<Reward> rewards,AbstractRoom? room)=>player==Owner&&room is TreasureRoom;
    public override async Task AfterModifyingRewards()
    {
        await Task.Yield();await PlayerCmd.GainGold(11,Owner);var returned=await RelicCmd.Obtain<MegaCrit.Sts2.Core.Models.Relics.Anchor>(Owner);
        if(UseReturnedOwner)_=returned.Owner.NetId;
        if(RequireInventoryReceipt&&!Owner.Relics.Contains(returned))throw new InvalidOperationException("Emitter assumes actual relic receipt before continuing its nonreward work.");
        NonRewardCounter++;
    }
}
