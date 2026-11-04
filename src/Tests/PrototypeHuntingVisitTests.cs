// THROWAWAY room-boundary inputs. Budget and entry penalty are in-memory inputs,
// native events/room stacks/relic commands run; no integrated map coordinator or save.
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

internal static class PrototypeHuntingVisitTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingVisit safe last-step event waits for native participant completion and shop exit",Completion),("PrototypeHuntingVisit real Colosseum two nested combat rooms retain safe parent",Nested)];
    private static async Task Begin(EventModel model,MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
#if STS2_PUBLIC
        await model.BeginEvent(player,false);
#else
        await model.BeginEvent(player,null,false);
#endif
    }
    private static async Task Completion()
    {
        using var outside=HeadlessCombatInProgress.EnterEvent();PrototypeHuntingMaskTests.Inject();
        for(int n=1;n<=4;n++)
        {
            var run=HeadlessTestSetup.Run("HUNTING70_SAFE_EVENT",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
            var previous=MegaCrit.Sts2.Core.Context.LocalContext.NetId;
            RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
            try
            {
                int steps=1;bool entryPunished=run.Players.Any(p=>p.Relics.OfType<PrototypeHuntingMask>().Any());
                var canonical=ModelDb.AllEvents.Single(e=>e.Id.Entry=="ALL_MATTERS_EVENT_CLERIC");
                var root=new EventRoom(canonical);run.PushRoom(root);steps--;
                var models=new List<EventModel>();
                foreach(var p in run.Players){p.Gold=100;p.Creature.SetCurrentHpInternal(10);var model=canonical.ToMutable();models.Add(model);await Begin(model,p);}
                for(int i=0;i<n;i++)
                {
                    NativeEventTests.Check(!entryPunished&&steps==0&&run.Players.All(p=>!p.Relics.OfType<PrototypeHuntingMask>().Any()),"Safe captured entry must not gain mask before last native event completion");
                    await models[i].CurrentOptions.Single(o=>o.TextKey.EndsWith(".HEAL")).Chosen();
                    NativeEventTests.Check(models[i].IsFinished,"Native service finishes participant event");
                    if(models.All(m=>m.IsFinished))foreach(var p in run.Players)await PrototypeHuntingMaskTests.Once(p);
                }
                NativeEventTests.Check(run.Players.All(p=>p.Relics.OfType<PrototypeHuntingMask>().Count()==1)&&!entryPunished,"All native finishes grant once without retroactively punishing this entry");
                run.PopCurrentRoom();
                NativeEventTests.Check(run.Players.All(p=>p.Relics.OfType<PrototypeHuntingMask>().Any()),"Next entry can consume actual owned masks");
                Console.WriteLine($"VISIT players={n} nativeClericAllFinished=true lastStepInput=1->0 entrySnapshotSafe=true masksOnlyAfterAll=true; in-memory aggregation, no unified-enter/network/save proof");
            }
            finally{RunManager.Instance.CleanUp(false);MegaCrit.Sts2.Core.Context.LocalContext.NetId=previous;}

            var shopRun=HeadlessTestSetup.Run("HUNTING70_SAFE_SHOP",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
            RunManager.Instance.SetUpTest(shopRun,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
            try
            {
                var room=new MerchantRoom();shopRun.PushRoom(room);
                foreach(var p in shopRun.Players)
                {
                    p.Gold=10000;var inventory=MerchantInventory.CreateForNormalMerchant(p);room.Inventories.Add(inventory);
                    NativeEventTests.Check(await inventory.CardEntries.First().OnTryPurchaseWrapper(inventory),"Safe last-step merchant purchase is actually allowed");
                    NativeEventTests.Check(!p.Relics.OfType<PrototypeHuntingMask>().Any(),"Shop services are not closed just because leaving is possible");
                }
                await room.Exit(shopRun);
                foreach(var p in shopRun.Players)await PrototypeHuntingMaskTests.Once(p);
                NativeEventTests.Check(shopRun.Players.All(p=>p.Relics.OfType<PrototypeHuntingMask>().Count()==1),"Grant occurs after actual native shop Exit call");
                Console.WriteLine($"VISIT_SHOP players={n} safeNativePurchase=true maskBeforeExit=false maskAfterNativeExit=true; all-team leave consensus belongs to multiplayer integration");
            }
            finally{RunManager.Instance.CleanUp(false);MegaCrit.Sts2.Core.Context.LocalContext.NetId=previous;}
        }
    }
    private static async Task Nested()
    {
        using var outside=HeadlessCombatInProgress.EnterEvent();
        for(int n=1;n<=4;n++)
        {
            var run=HeadlessTestSetup.Run("HUNTING70_SAFE_NESTED",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());run.CurrentActIndex=2;
            var previous=MegaCrit.Sts2.Core.Context.LocalContext.NetId;
            RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
            RunManager.Instance.CombatReplayWriter.IsEnabled=false;
            MegaCrit.Sts2.Core.Context.LocalContext.NetId=1;
            HeadlessBanditRoomEntrance.Active=true;HeadlessBanditRoomEntrance.Rooms.Clear();
            try
            {
                var canonical=ModelDb.Event<AllMatters.Events.Colosseum>();var root=new EventRoom(canonical);run.PushRoom(root);
                run.AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.Unknown,RoomType.Event,canonical.Id);
                int steps=0;bool entryPunished=false; // supplied last-step entry snapshot, not inferred from zero
                var sync=RunManager.Instance.EventSynchronizer;sync.BeginEvent(canonical,false);await sync.AwaitPendingOptionTasks();
#if !STS2_PUBLIC
                sync.GenerateInternalCombatStateIfNecessary(sync.GetLocalEvent());
#endif
                var models=run.Players.Select(sync.GetEventForPlayer).ToArray();
                foreach(var model in models)await model.CurrentOptions.Single().Chosen();
                foreach(var model in models)await model.CurrentOptions.Single().Chosen();
                var first=HeadlessBanditRoomEntrance.Rooms.Single();
                run.PushRoom(first);
                NativeEventTests.Check(ReferenceEquals(run.BaseRoom,root)&&first.ParentEventId==canonical.Id&&!entryPunished&&steps==0,"Native child combat retains safe parent identity and supplied entry state");
                run.PopCurrentRoom();first.MarkPreFinished();sync.ResumeEvents(first);
                foreach(var model in models)await model.CurrentOptions.Single(o=>o.TextKey.EndsWith(".FIGHT")).Chosen();
                var second=HeadlessBanditRoomEntrance.Rooms.Last();run.PushRoom(second);
                NativeEventTests.Check(HeadlessBanditRoomEntrance.Rooms.Count==2&&ReferenceEquals(run.BaseRoom,root)&&second.ParentEventId==canonical.Id&&!entryPunished&&steps==0&&run.Players.All(p=>!p.Relics.OfType<PrototypeHuntingMask>().Any()),"Second native internal combat stays safe with no premature mask");
                Console.WriteLine($"VISIT_NESTED players={n} realColosseumHandlers=true nativeChildRooms=2 safeParentSnapshot=true stepsInputUnchanged=0 masksBeforeTerminalCompletion=0; UI scheduling substituted, actual combat victory/terminal rewards not proven");
            }
            finally{HeadlessBanditRoomEntrance.Active=false;HeadlessBanditRoomEntrance.Rooms.Clear();RunManager.Instance.CleanUp(false);MegaCrit.Sts2.Core.Context.LocalContext.NetId=previous;}
        }
    }
}
