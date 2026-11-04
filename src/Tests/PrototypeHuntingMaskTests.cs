// THROWAWAY native relic/hook boundary. Does not replace a real victory, room vote or save test.
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

public sealed class PrototypeHuntingMask : RelicModel
{
    public override RelicRarity Rarity=>RelicRarity.Event;
    public override async Task AfterCombatVictory(CombatRoom room)
    {
        var run=Owner.RunState;
        var last=run.Map.SecondBossMapPoint??run.Map.BossMapPoint;
        if(room.RoomType==RoomType.Boss&&room.ParentEventId==null&&run.CurrentMapCoord==last.coord)
            await RelicCmd.Remove(this);
    }
}
public sealed class PrototypeHuntingRewardObserver : RelicModel
{
    public bool? MaskAtRewards{get;private set;}
    public override RelicRarity Rarity=>RelicRarity.Event;
    public override bool TryModifyRewards(Player player,List<Reward> rewards,AbstractRoom? room)
    {if(player==Owner)MaskAtRewards=player.Relics.OfType<PrototypeHuntingMask>().Any();return false;}
}
internal sealed class PrototypeHuntingBossMap(bool dual):ActMap
{
    public override MapPoint StartingMapPoint{get;}=new(0,0);
    public override MapPoint BossMapPoint{get;}=new(0,1){PointType=MapPointType.Boss};
    private readonly MapPoint _second=new(0,2){PointType=MapPointType.Boss};
    public override MapPoint? SecondBossMapPoint=>dual?_second:null;
    protected override MapPoint?[,] Grid{get;}=new MapPoint?[1,1];
}
internal static class PrototypeHuntingMaskTests
{
    private static bool Injected;
    internal static void Inject()
    {
        if(Injected)return;
        ModelDb.Inject(typeof(PrototypeHuntingMask));ModelDb.Inject(typeof(PrototypeHuntingRewardObserver));Injected=true;
    }
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingMask native unique grant tradability and final boss hook before rewards",Mask),("PrototypeHuntingMask ordinary native front back pulls exclude event bucket",NormalDrops)];
    private static Task NormalDrops()
    {
        Inject();
        foreach(int n in new[]{1,2,3,4})
        {
            var run=HeadlessTestSetup.Run("HUNTING70_MASK_DROPS",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
            foreach(var p in run.Players)
            {
                // Deliberately include the Event candidate in a real native bag: this
                // checks native draw semantics, not automatic pool registration.
                var save=p.RelicGrabBag.ToSerializable();foreach(var list in save.RelicIdLists.Values)list.Clear();
                var mask=ModelDb.Relic<PrototypeHuntingMask>();save.RelicIdLists[RelicRarity.Event]=[mask.Id];
                p.RelicGrabBag.LoadFromSerializable(save);
                foreach(var rarity in new[]{RelicRarity.Common,RelicRarity.Uncommon,RelicRarity.Rare,RelicRarity.Shop})
                {
                    var front=MegaCrit.Sts2.Core.Factories.RelicFactory.PullNextRelicFromFront(p,rarity);
                    var back=MegaCrit.Sts2.Core.Factories.RelicFactory.PullNextRelicFromBack(p,rarity,_=>true);
                    NativeEventTests.Check(front.Id!=mask.Id&&back.Id!=mask.Id&&p.RelicGrabBag.Contains(mask),"Usual reward/shop rarity pulls leave deliberately supplied Event mask untouched");
                }
            }
            Console.WriteLine($"MASK_DROPS players={n} eventCandidateInNativeBag=true front/backCommonUncommonRareShop=excluded eventCandidateRetained=true; automatic mod pool registration not asserted");
        }
        return Task.CompletedTask;
    }
    internal static async Task Once(Player p)
    {if(!p.Relics.OfType<PrototypeHuntingMask>().Any())await RelicCmd.Obtain<PrototypeHuntingMask>(p);}
    private static async Task Mask()
    {
        using var outside=HeadlessCombatInProgress.EnterEvent();
        Inject();
        for(int n=1;n<=4;n++)foreach(bool dual in new[]{false,true})
        {
            var players=Enumerable.Range(1,n).Select(i=>Player.CreateForNewRun(ModelDb.Character<Ironclad>(),UnlockState.all,(ulong)i)).ToArray();
            var run=RunState.CreateForTest(players,acts:[ModelDb.Act<Overgrowth>(),ModelDb.Act<Hive>(),ModelDb.Act<Glory>()],seed:"HUNTING70_MASK");
            var previous=MegaCrit.Sts2.Core.Context.LocalContext.NetId;
            RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
            try
            {
                run.Map=new PrototypeHuntingBossMap(dual);run.AddVisitedMapCoord(run.Map.BossMapPoint.coord);
                foreach(var p in players)
                {
                    await Once(p);await Once(p);
                    NativeEventTests.Check(p.Relics.OfType<PrototypeHuntingMask>().Count()==1&&!p.Relics.OfType<PrototypeHuntingMask>().Single().IsTradable,"Local idempotence adapter grants one nontradable Event relic");
                }
                var eventId=ModelDb.AllEvents.First().Id;
                var eventBoss=new CombatRoom(ModelDb.Encounter<TestSubjectBoss>().ToMutable(),run){ParentEventId=eventId};
                foreach(var p in players)eventBoss.CombatState.AddPlayer(p);
                await Hook.AfterCombatVictory(run,eventBoss.CombatState,eventBoss);
                NativeEventTests.Check(players.All(p=>p.Relics.OfType<PrototypeHuntingMask>().Any()),"Event uses same boss model but does not clear masks");
                var firstBoss=new CombatRoom(ModelDb.Encounter<TestSubjectBoss>().ToMutable(),run);
                foreach(var p in players)firstBoss.CombatState.AddPlayer(p);
                if(dual)
                {
                    await Hook.AfterCombatVictory(run,firstBoss.CombatState,firstBoss);
                    NativeEventTests.Check(players.All(p=>p.Relics.OfType<PrototypeHuntingMask>().Any()),"First of two bosses retains mask");
                    run.AddVisitedMapCoord(run.Map.SecondBossMapPoint!.coord);
                }
                foreach(var p in players)p.AddRelicInternal((PrototypeHuntingRewardObserver)ModelDb.Relic<PrototypeHuntingRewardObserver>().ToMutable());
                var finalBoss=new CombatRoom(ModelDb.Encounter<TestSubjectBoss>().ToMutable(),run);
                foreach(var p in players)finalBoss.CombatState.AddPlayer(p);
                await Hook.AfterCombatVictory(run,finalBoss.CombatState,finalBoss);
                NativeEventTests.Check(players.All(p=>!p.Relics.OfType<PrototypeHuntingMask>().Any()),"Final true map boss native hook removes all masks");
                foreach(var p in players)
                {
                    await new RewardsSet(p).EmptyForRoom(finalBoss).WithCustomRewards([new GoldReward(20,p)]).GenerateWithoutOffering();
                    NativeEventTests.Check(p.Relics.OfType<PrototypeHuntingRewardObserver>().Single().MaskAtRewards==false,"Real reward modifier observes mask already absent");
                }
                Console.WriteLine($"MASK players={n} dual={dual} duplicateGuard=true nontradable=true eventBossRetains=true firstBossRetains={dual} finalHookRemovesBeforeRewards=true; Hook driven explicitly, no actual victory/room completion/save proof");
            }
            finally{RunManager.Instance.CleanUp(false);MegaCrit.Sts2.Core.Context.LocalContext.NetId=previous;}
        }
    }
}
