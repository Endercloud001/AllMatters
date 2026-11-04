using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

public sealed class PrototypeHuntingRestCounter : RelicModel
{
    public int Count {get;private set;}
    public override RelicRarity Rarity=>RelicRarity.Event;
    public override Task AfterRestSiteHeal(Player player,bool isMimicked){if(player==Owner)Count++;return Task.CompletedTask;}
}
internal static class PrototypeHuntingRestExtrasTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingRestExtras MiniatureTent Pillow Shovel and native remote Mend",Extra)];
    private static ulong Target;
    private static bool Remote(ref Task<PlayerChoiceResult> __result){__result=Task.FromResult(PlayerChoiceResult.FromPlayerId(Target));return false;}
    private static bool Filter(RestSiteSynchronizer __instance,Player player,int optionIndex,ref Task<bool> __result)
    {
        var option=__instance.GetOptionsForPlayer(player)[optionIndex];
        if(option.IsEnabled&&option is HealRestSiteOption or MendRestSiteOption)return true;
        __result=Task.FromResult(false);return false;
    }
    private static async Task Extra()
    {
        using var outside=HeadlessCombatInProgress.EnterEvent();
        ModelDb.Inject(typeof(PrototypeHuntingRestCounter));
        var patch=new Harmony("allmatters.prototype70.rest-extras");
        patch.Patch(AccessTools.Method(typeof(RestSiteSynchronizer),"ChooseOption"),prefix:new HarmonyMethod(typeof(PrototypeHuntingRestExtrasTests),nameof(Filter)));
        try
        {
            for(int n=1;n<=4;n++)
            {
                var run=HeadlessTestSetup.Run("HUNTING70_REST_EXTRAS",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
                var previous=MegaCrit.Sts2.Core.Context.LocalContext.NetId;
                RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
                try
                {
                    patch.Patch(AccessTools.Method(RunManager.Instance.PlayerChoiceSynchronizer.GetType(),"WaitForRemoteChoice"),prefix:new HarmonyMethod(typeof(PrototypeHuntingRestExtrasTests),nameof(Remote)));
                    run.AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.RestSite,MegaCrit.Sts2.Core.Rooms.RoomType.RestSite,null);
                    foreach(var p in run.Players)
                    {
                        await RelicCmd.Obtain<MiniatureTent>(p);await RelicCmd.Obtain<RegalPillow>(p);await RelicCmd.Obtain<Shovel>(p);
                        var counter=(PrototypeHuntingRestCounter)ModelDb.Relic<PrototypeHuntingRestCounter>().ToMutable();p.AddRelicInternal(counter);
                        p.Creature.SetCurrentHpInternal(10);
                    }
                    var sync=RunManager.Instance.RestSiteSynchronizer;sync.BeginRestSite();
                    foreach(var p in run.Players)
                    {
                        var options=sync.GetOptionsForPlayer(p);
                        foreach(var forbidden in options.Where(o=>o is not HealRestSiteOption&&o is not MendRestSiteOption).ToArray())
                        {
                            int index=options.ToList().IndexOf(forbidden);
                            NativeEventTests.Check(!await (Task<bool>)AccessTools.Method(typeof(RestSiteSynchronizer),"ChooseOption").Invoke(sync,[p,index])!,"New Shovel/Smith options rejected before execution");
                        }
                        int heal=options.ToList().FindIndex(o=>o is HealRestSiteOption);int expected=10+(int)HealRestSiteOption.GetHealAmount(p);
                        NativeEventTests.Check(await (Task<bool>)AccessTools.Method(typeof(RestSiteSynchronizer),"ChooseOption").Invoke(sync,[p,heal])!,"Native Heal selected");
                        NativeEventTests.Check(p.Creature.CurrentHp==expected&&p.Relics.OfType<PrototypeHuntingRestCounter>().Single().Count==1,"Pillow modifier and native AfterRestSiteHeal retained");
                        NativeEventTests.Check(options.Any(o=>o is SmithRestSiteOption)&&options.All(o=>o is not HealRestSiteOption),"MiniatureTent leaves remaining options, removes only selected Heal");
                        NativeEventTests.Check(run.CurrentMapPointHistoryEntry!.GetEntry(p.NetId).RestSiteChoices.SequenceEqual(new[]{"HEAL"}),"Only allowed Heal recorded; no false Dig/Smith history");
                    }
                    if(n>1)
                    {
                        var owner=run.Players[0];var receiver=run.Players[1];receiver.Creature.SetCurrentHpInternal(10);
                        Target=receiver.NetId;MegaCrit.Sts2.Core.Context.LocalContext.NetId=receiver.NetId;
                        int mend=sync.GetOptionsForPlayer(owner).ToList().FindIndex(o=>o is MendRestSiteOption);
                        int expected=10+(int)MendRestSiteOption.GetHealAmount(receiver);
                        NativeEventTests.Check(await (Task<bool>)AccessTools.Method(typeof(RestSiteSynchronizer),"ChooseOption").Invoke(sync,[owner,mend])!,"Second allowed Tent action uses native remote Mend with supplied choice result");
                        NativeEventTests.Check(receiver.Creature.CurrentHp==expected&&receiver.Relics.OfType<PrototypeHuntingRestCounter>().Single().Count==2,"Mend retains receiver modifier and AfterRestSiteHeal");
                        var forbidden=sync.GetOptionsForPlayer(owner).ToList().FindIndex(o=>o is SmithRestSiteOption);
                        NativeEventTests.Check(!await (Task<bool>)AccessTools.Method(typeof(RestSiteSynchronizer),"ChooseOption").Invoke(sync,[owner,forbidden])!,"Filtering applies again after extra allowed action");
                    }
                    MegaCrit.Sts2.Core.Context.LocalContext.NetId=run.Players[0].NetId;
                    var room=new MegaCrit.Sts2.Core.Rooms.RestSiteRoom();
                    await room.EnterInternal(null,false); // independent completion-timing case; no preload/UI
                    var exiting=room.Exit(run);
#if STS2_PUBLIC
                    NativeEventTests.Check(exiting.IsCompleted&&sync.GetOptionsForPlayer(run.Players[^1]).Count>0,"Public native Exit returns while participant options remain");
                    await exiting;
                    Console.WriteLine($"REST_COMPLETION players={n} publicNativeExit=immediate pendingOptionsRemain=true needsExternalCompletionAggregation=true");
#else
                    if(n>1)NativeEventTests.Check(!exiting.IsCompleted,"Beta native Exit waits for other participants");
                    foreach(var p in run.Players.Skip(1)){MegaCrit.Sts2.Core.Context.LocalContext.NetId=p.NetId;sync.BeforeLocalRestSiteExited();}
                    await exiting;
                    NativeEventTests.Check(sync.AfterAllRestSitesCompleted().IsCompleted,"Beta native Exit completes after all controlled participant exits");
                    Console.WriteLine($"REST_COMPLETION players={n} betaNativeExit=await-all controlledParticipantExitCalls=true noNativeNetworkClaim=true");
#endif
                    Console.WriteLine($"REST_EXTRA players={n} Pillow=true AfterHeal=true ShovelDenied=true TentRemaining=true historyOnlyAllowed=true nativeMend={n>1}; supplied remote choice, no network/UI proof");
                }
                finally{RunManager.Instance.CleanUp(false);MegaCrit.Sts2.Core.Context.LocalContext.NetId=previous;}
            }
        }
        finally{patch.UnpatchAll(patch.Id);}
    }
}
