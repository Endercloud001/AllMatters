// THROWAWAY: every MerchantRoom is an explicit punished-room fixture. No quota,
// mask, inventory replacement, persistence or production integration architecture.
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;

internal static class PrototypeHuntingShopProbe
{
    internal static bool Restricted=true;
    private static bool Applies(Player player)=>Restricted&&player.RunState.CurrentRoom is MerchantRoom;
    internal static void Install(Harmony patch)
    {
        patch.Patch(AccessTools.Method(typeof(MerchantEntry),nameof(MerchantEntry.OnTryPurchaseWrapper)),prefix:new HarmonyMethod(typeof(PrototypeHuntingShopProbe),nameof(BlockShop)));
        patch.Patch(AccessTools.Method(typeof(MerchantCardRemovalEntry),nameof(MerchantCardRemovalEntry.OnTryPurchaseWrapper),[typeof(MerchantInventory),typeof(bool),typeof(bool)]),prefix:new HarmonyMethod(typeof(PrototypeHuntingShopProbe),nameof(BlockShop)));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),nameof(OneOffSynchronizer.DoLocalMerchantCardRemoval)),prefix:new HarmonyMethod(typeof(PrototypeHuntingShopProbe),nameof(BlockRemovalSender)));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),"DoMerchantCardRemoval"),prefix:new HarmonyMethod(typeof(PrototypeHuntingShopProbe),nameof(BlockRemovalReceiver)));
        foreach(var type in new[]{typeof(NMerchantCard),typeof(NMerchantRelic),typeof(NMerchantPotion),typeof(NMerchantCardRemoval)})
            patch.Patch(AccessTools.Method(type,"UpdateVisual"),postfix:new HarmonyMethod(typeof(PrototypeHuntingShopProbe),nameof(RestrictVisual)));
    }
    internal static bool BlockShop(MerchantEntry __instance,ref Task<bool> __result)
    {
        var player=(Player)AccessTools.Field(typeof(MerchantEntry),"_player").GetValue(__instance);
        if(!Applies(player))return true;
        __result=Task.FromResult(false);return false;
    }
    internal static bool BlockRemovalSender(OneOffSynchronizer __instance,ref Task<bool> __result)=>BlockRemovalReceiver((Player)AccessTools.Property(typeof(OneOffSynchronizer),"LocalPlayer").GetValue(__instance)!,ref __result);
    internal static bool BlockRemovalReceiver(Player player,ref Task<bool> __result)
    {
        if(!Applies(player))return true;
        __result=Task.FromResult(false);return false;
    }
    internal static void RestrictVisual(NMerchantSlot __instance)
    {
        if(__instance.Entry==null||!Applies((Player)AccessTools.Field(typeof(MerchantEntry),"_player").GetValue(__instance.Entry)))return;
        __instance.FocusMode=Control.FocusModeEnum.None;
        __instance.MouseFilter=Control.MouseFilterEnum.Ignore;
        __instance.Hitbox.MouseFilter=Control.MouseFilterEnum.Ignore;
        if(__instance is NMerchantCardRemoval)
        {
            var animator=__instance.GetNode<AnimationPlayer>("%Animation");animator.Play("Used");animator.Advance(0);
            __instance.GetNode<Control>("Cost").Visible=false;
        }
        else __instance.Visible=false;
    }
}

#if HUNTING_SHOP_MOD
[MegaCrit.Sts2.Core.Modding.ModInitializer(nameof(Initialize))]
public static class PrototypeHuntingShopMod
{
    public static void Initialize()
    {
        PrototypeHuntingShopProbe.Install(new Harmony("AllMattersHuntingShopProbe"));
        GD.Print("[AllMattersHuntingShopProbe] Forced punished MerchantRoom fixture; no quota, mask, inventory replacement or save state.");
    }
}
#endif
