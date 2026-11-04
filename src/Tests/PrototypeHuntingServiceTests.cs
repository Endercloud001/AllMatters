// THROWAWAY service execution boundary experiments; no production/UI patches.
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

internal static class PrototypeHuntingServiceTests
{
    private static bool Restricted=true;
    internal static readonly (string Name, Func<Task> Test)[] Cases = [("PrototypeHuntingServices merchant execution rejects without inventory mutation", Shop),("PrototypeHuntingServices rest execution filtering and native Heal", Rest),("PrototypeHuntingServices direct removal sender and receiver protected",DirectRemoval),("PrototypeHuntingServices actual second store removal and retained first store pricing",CrossStore)];
    private static async Task CrossStore()
    {
        using var outside=HeadlessCombatInProgress.EnterEvent();
        var patch=new Harmony("allmatters.prototype70.cross-store");
        patch.Patch(AccessTools.Method(typeof(MerchantEntry),nameof(MerchantEntry.OnTryPurchaseWrapper)),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),nameof(BlockShop)));
        patch.Patch(AccessTools.Method(typeof(MerchantCardRemovalEntry),nameof(MerchantCardRemovalEntry.OnTryPurchaseWrapper),[typeof(MerchantInventory),typeof(bool),typeof(bool)]),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),nameof(BlockShop)));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),nameof(OneOffSynchronizer.DoLocalMerchantCardRemoval)),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),nameof(BlockRemovalSender)));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),"DoMerchantCardRemoval"),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),nameof(BlockRemovalReceiver)));
        try
        {
            for(int n=1;n<=4;n++)
            {
                var run=HeadlessTestSetup.Run("HUNTING70_CROSS_STORE",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());var p=run.Players[0];
                RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
                try
                {
                    Restricted=false;p.Gold=1000;
                    var firstRoom=new MerchantRoom();run.PushRoom(firstRoom);var a=MerchantInventory.CreateForNormalMerchant(p);
                    var originalEntries=a.AllEntries.ToArray();int before=a.CardRemovalEntry!.Cost;
                    run.PopCurrentRoom();run.PushRoom(new MerchantRoom());var b=MerchantInventory.CreateForNormalMerchant(p);
                    var removed=p.Deck.Cards.First();int cards=p.Deck.Cards.Count;
                    using(CardSelectCmd.UseSelector(new NativeEventTests.Select(removed)))NativeEventTests.Check(await b.CardRemovalEntry!.OnTryPurchaseWrapper(b),"Pre-hunting second-store transaction actually removes a card");
                    NativeEventTests.Check(!p.Deck.Cards.Contains(removed)&&p.Deck.Cards.Count==cards-1&&p.ExtraFields.CardShopRemovalsUsed==1,"Native removal count increments only for successful real deletion");
                    run.PopCurrentRoom();run.PushRoom(firstRoom);Restricted=true;
                    a.CardRemovalEntry.CalcCost();
                    NativeEventTests.Check(a.CardRemovalEntry.Cost==before+MerchantCardRemovalEntry.PriceIncrease,"Retained first-store entry uses native cross-store price increment");
                    int gold=p.Gold;ulong rng=HeadlessTestSetup.RngPosition(p.PlayerRng.Shops);
                    NativeEventTests.Check(!await a.CardRemovalEntry.OnTryPurchaseWrapper(a,true,false)&&!await RunManager.Instance.OneOffSynchronizer.DoLocalMerchantCardRemoval(0,false),"Revisited first-store restriction denies free removal at wrapper and direct sender");
                    NativeEventTests.Check(p.Gold==gold&&p.ExtraFields.CardShopRemovalsUsed==1&&!a.CardRemovalEntry.Used&&originalEntries.SequenceEqual(a.AllEntries)&&rng==HeadlessTestSetup.RngPosition(p.PlayerRng.Shops),"No false first-store Used, additional count, reroll or money change");
                    Console.WriteLine($"SHOP_CROSS players={n} actualRemovalInSecondStore=true removalCount=1 retainedFirstStorePrice={a.CardRemovalEntry.Cost} originalPrice={before} deniedFreeAfterReturn=true firstStoreUsed=false; inventory retained in memory, not native re-enter/save/UI Used callback proof");
                }
                finally{Restricted=true;RunManager.Instance.CleanUp(false);}
            }
        }
        finally{Restricted=true;patch.UnpatchAll(patch.Id);}
    }
    private static async Task DirectRemoval()
    {
        using var outside=HeadlessCombatInProgress.EnterEvent();
        var run=HeadlessTestSetup.Run("HUNTING70_DIRECT_REMOVAL",ModelDb.Character<Ironclad>());var p=run.Players[0];
        var patch=new Harmony("allmatters.prototype70.direct-removal");
        patch.Patch(AccessTools.Method(typeof(MerchantEntry),nameof(MerchantEntry.OnTryPurchaseWrapper)),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),nameof(BlockShop)));
        patch.Patch(AccessTools.Method(typeof(MerchantCardRemovalEntry),nameof(MerchantCardRemovalEntry.OnTryPurchaseWrapper),[typeof(MerchantInventory),typeof(bool),typeof(bool)]),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),nameof(BlockShop)));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),nameof(OneOffSynchronizer.DoLocalMerchantCardRemoval)),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),nameof(BlockRemovalSender)));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),"DoMerchantCardRemoval"),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),nameof(BlockRemovalReceiver)));
        RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
        try
        {
            run.PushRoom(new MerchantRoom());p.Gold=1000;var target=p.Deck.Cards.First();int cards=p.Deck.Cards.Count;
            bool bought;
            using(CardSelectCmd.UseSelector(new NativeEventTests.Select(target)))bought=await RunManager.Instance.OneOffSynchronizer.DoLocalMerchantCardRemoval(75,true);
            Console.WriteLine($"SHOP_DIRECT nativeSenderResult={bought} gold={p.Gold} cardsBefore={cards} cardsAfter={p.Deck.Cards.Count} removalUses={p.ExtraFields.CardShopRemovalsUsed}");
            NativeEventTests.Check(!bought&&p.Gold==1000&&p.Deck.Cards.Count==cards&&p.ExtraFields.CardShopRemovalsUsed==0,"Public direct removal sender must be denied before selection/payment/mutation");
            bool received=await (Task<bool>)AccessTools.Method(typeof(OneOffSynchronizer),"DoMerchantCardRemoval").Invoke(RunManager.Instance.OneOffSynchronizer,[p,75,true])!;
            NativeEventTests.Check(!received&&p.Gold==1000&&p.Deck.Cards.Count==cards&&p.ExtraFields.CardShopRemovalsUsed==0,"Direct receiver path also denies before mutation");
            Console.WriteLine("SHOP_DIRECT receiverResult=false fieldsUnchanged=true");
        }
        finally{patch.UnpatchAll(patch.Id);RunManager.Instance.CleanUp(false);}
    }
    private static bool BlockShop(MerchantEntry __instance,ref Task<bool> __result)
    {
        var p=(Player)AccessTools.Field(typeof(MerchantEntry),"_player").GetValue(__instance);
        if(!Restricted||p.RunState.CurrentRoom is not MerchantRoom)return true;
        __result=Task.FromResult(false);return false;
    }
    private static bool BlockRemovalSender(OneOffSynchronizer __instance,ref Task<bool> __result)
    {
        var player=(Player)AccessTools.Property(typeof(OneOffSynchronizer),"LocalPlayer").GetValue(__instance)!;
        return BlockRemovalReceiver(player,ref __result);
    }
    private static bool BlockRemovalReceiver(Player player,ref Task<bool> __result)
    {
        if(!Restricted||player.RunState.CurrentRoom is not MerchantRoom)return true;
        __result=Task.FromResult(false);return false;
    }
    private static bool BlockRest(RestSiteSynchronizer __instance,Player player,int optionIndex,ref Task<bool> __result)
    {
        var option=__instance.GetOptionsForPlayer(player)[optionIndex];
        if(option.IsEnabled && option is HealRestSiteOption or MendRestSiteOption)return true;
        __result=Task.FromResult(false);return false;
    }
    private static async Task Shop()
    {
        using var outside=HeadlessCombatInProgress.EnterEvent();
        var patch=new Harmony("allmatters.prototype70.shop");
        patch.Patch(AccessTools.Method(typeof(MerchantEntry),nameof(MerchantEntry.OnTryPurchaseWrapper)),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),nameof(BlockShop)));
        patch.Patch(AccessTools.Method(typeof(MerchantCardRemovalEntry),nameof(MerchantCardRemovalEntry.OnTryPurchaseWrapper),[typeof(MerchantInventory),typeof(bool),typeof(bool)]),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),nameof(BlockShop)));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),nameof(OneOffSynchronizer.DoLocalMerchantCardRemoval)),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),nameof(BlockRemovalSender)));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),"DoMerchantCardRemoval"),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),nameof(BlockRemovalReceiver)));
        try
        {
            for(int n=1;n<=4;n++)
            {
                var run=HeadlessTestSetup.Run("HUNTING70_SHOP",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
                var prior=MegaCrit.Sts2.Core.Context.LocalContext.NetId;
                RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
                try
                {
                    run.PushRoom(new MerchantRoom());
                    foreach(var p in run.Players)
                    {
                        var inv=MerchantInventory.CreateForNormalMerchant(p);
                        var entries=inv.AllEntries.ToArray();
                        int events=0;
                        foreach(var e in entries)e.PurchaseCompleted+=(_,_)=>events++;
                        int gold=p.Gold,used=p.ExtraFields.CardShopRemovalsUsed;
                        ulong rng=HeadlessTestSetup.RngPosition(p.PlayerRng.Shops);
                        var costs=entries.Select(e=>e.Cost).ToArray();var stocked=entries.Select(e=>e.IsStocked).ToArray();
                        for(int reopen=0;reopen<2;reopen++)
                        foreach(var e in entries)
                        {
                            NativeEventTests.Check(!await e.OnTryPurchaseWrapper(inv),"Paid entry rejected");
                            NativeEventTests.Check(!await e.OnTryPurchaseWrapper(inv,true),"Free/ignoreCost entry rejected");
                        }
                        var free=entries.OfType<MerchantCardEntry>().First();var costField=AccessTools.Field(typeof(MerchantEntry),"_cost");int fixturePrice=(int)costField.GetValue(free)!;
                        costField.SetValue(free,0);
                        try{NativeEventTests.Check(free.Cost==0&&!await free.OnTryPurchaseWrapper(inv,false),"Actual zero-cost fixture rejected even without ignoreCost");}
                        finally{costField.SetValue(free,fixturePrice);}
                        NativeEventTests.Check(!await inv.CardRemovalEntry!.OnTryPurchaseWrapper(inv,true,false),"Independent removal wrapper rejected");
                        NativeEventTests.Check(p.Gold==gold&&p.ExtraFields.CardShopRemovalsUsed==used&&!inv.CardRemovalEntry.Used&&events==0,"No money, removal count, Used, or purchase callbacks mutated");
                        NativeEventTests.Check(rng==HeadlessTestSetup.RngPosition(p.PlayerRng.Shops)&&entries.SequenceEqual(inv.AllEntries)&&costs.SequenceEqual(entries.Select(e=>e.Cost))&&stocked.SequenceEqual(entries.Select(e=>e.IsStocked)),"Inventory references, RNG, prices and stock preserved");
                        await RelicCmd.Obtain<TheCourier>(p);var courierCosts=entries.Select(e=>e.Cost).ToArray();
                        var membership=await RelicCmd.Obtain<MembershipCard>(p);
                        var discounted=entries.Select(e=>e.Cost).ToArray();
                        foreach(var e in entries)NativeEventTests.Check(!await e.OnTryPurchaseWrapper(inv,true),"Courier/discount do not bypass restriction");
                        NativeEventTests.Check(discounted.SequenceEqual(entries.Select(e=>e.Cost))&&rng==HeadlessTestSetup.RngPosition(p.PlayerRng.Shops),"Dynamic price and Courier RNG unaffected by denied attempts");
                        await RelicCmd.Remove(membership);
                        NativeEventTests.Check(courierCosts.SequenceEqual(entries.Select(e=>e.Cost))&&rng==HeadlessTestSetup.RngPosition(p.PlayerRng.Shops),"Losing dynamic discount recalculates native Cost without rebuilding inventory/RNG");
                        var nextStore=new MerchantCardRemovalEntry(p);
                        NativeEventTests.Check(nextStore.Cost==inv.CardRemovalEntry.Cost,"Denied removal does not increase cross-store price");
                        Console.WriteLine($"SHOP players={n} player={p.NetId} entries={entries.Length} paid/free/removal=denied reopenAttempts=2 gold/stock/rng/callbacks/removal=unchanged discount/Courier=unchanged; actual UI/revisit-save NOT tested");
                    }
                    run.PopCurrentRoom();
                    var visitor=run.Players[0];visitor.Gold=100;visitor.Creature.SetCurrentHpInternal(10);
                    var service=ModelDb.AllEvents.Single(e=>e.Id.Entry=="ALL_MATTERS_EVENT_CLERIC").ToMutable();
#if STS2_PUBLIC
                    await service.BeginEvent(visitor,false);
#else
                    await service.BeginEvent(visitor,null,false);
#endif
                    await service.CurrentOptions.Single(o=>o.TextKey.EndsWith(".HEAL")).Chosen();
                    NativeEventTests.Check(visitor.Gold==65&&visitor.Creature.CurrentHp==30&&service.IsFinished,"Ordinary event purchase/heal is not affected while merchant execution filter remains installed");
                    Console.WriteLine($"SHOP_EVENT players={n} nativeClericHeal=true paid35=true eventFinished=true merchantFilterStillInstalled=true");
                }
                finally{RunManager.Instance.CleanUp(false);MegaCrit.Sts2.Core.Context.LocalContext.NetId=prior;}
            }
        }
        finally{patch.UnpatchAll(patch.Id);}
    }
    private static async Task Rest()
    {
        using var outside=HeadlessCombatInProgress.EnterEvent();
        var patch=new Harmony("allmatters.prototype70.rest");
        patch.Patch(AccessTools.Method(typeof(RestSiteSynchronizer),"ChooseOption"),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),nameof(BlockRest)));
        try
        {
            for(int n=1;n<=4;n++)
            {
                var run=HeadlessTestSetup.Run("HUNTING70_REST",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
                var prior=MegaCrit.Sts2.Core.Context.LocalContext.NetId;
                RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
                try
                {
                    var sync=RunManager.Instance.RestSiteSynchronizer;
                    sync.BeginRestSite();
                    foreach(var p in run.Players)
                    {
                        p.Creature.SetCurrentHpInternal(10);
                        var options=sync.GetOptionsForPlayer(p);
                        int smith=options.ToList().FindIndex(o=>o is SmithRestSiteOption);
                        int before=options.Count;
                        bool rejected=!(bool)await (Task<bool>)AccessTools.Method(typeof(RestSiteSynchronizer),"ChooseOption").Invoke(sync,[p,smith])!;
                        NativeEventTests.Check(rejected&&options.Count==before&&sync.GetChosenOptionIndex(p.NetId)==null,"Rejected Smith not executed, removed or recorded");
                        NativeEventTests.Check(options.Count(o=>o is MendRestSiteOption)==(n==1?0:1),"No solo Mend invented");
                        int heal=options.ToList().FindIndex(o=>o is HealRestSiteOption);
                        int expected=Math.Min(p.Creature.MaxHp,10+(int)HealRestSiteOption.GetHealAmount(p));
                        bool selected=await (Task<bool>)AccessTools.Method(typeof(RestSiteSynchronizer),"ChooseOption").Invoke(sync,[p,heal])!;
                        NativeEventTests.Check(selected&&p.Creature.CurrentHp==expected,"Allowed Heal uses native effect");
                        Console.WriteLine($"REST players={n} player={p.NetId} smith=denied noChosenRecord=true heal={expected} native=true mendPresent={n>1}; icon/extra-use/hooks/completion still require more experiments");
                    }
#if !STS2_PUBLIC
                    NativeEventTests.Check(RunManager.Instance.RestSiteSynchronizer.AfterAllRestSitesCompleted().IsCompleted,"Beta completes after every player native selection");
#else
                    NativeEventTests.Check(AccessTools.Method(typeof(RestSiteSynchronizer),"AfterAllRestSitesCompleted")==null,"Public lacks beta completion API");
#endif
                }
                finally{RunManager.Instance.CleanUp(false);MegaCrit.Sts2.Core.Context.LocalContext.NetId=prior;}
            }
        }
        finally{patch.UnpatchAll(patch.Id);}
    }
}

