using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Jigsaw;

internal static class JigsawHuntingServices
{
    internal static bool ShopBlocked(Player player) => JigsawHuntingNative.RootIsHunted(player) &&
        player.RunState.CurrentRoom is MerchantRoom && player.RunState.CurrentRoomCount == 1;
    internal static bool RestBlocked(Player player) => JigsawHuntingNative.RootIsHunted(player) &&
        player.RunState.CurrentRoom is RestSiteRoom && player.RunState.CurrentRoomCount == 1;
    internal static bool AllowedRest(RestSiteOption option) => option is HealRestSiteOption or MendRestSiteOption;
    internal static bool RestChoicesRemain(Player player) =>
        MegaCrit.Sts2.Core.Runs.RunManager.Instance.RestSiteSynchronizer.GetOptionsForPlayer(player)
            .Any(option => option.IsEnabled && (!RestBlocked(player) || AllowedRest(option)));
    internal static Player MerchantOwner(MerchantEntry entry) => (Player)AccessTools.Field(typeof(MerchantEntry), "_player").GetValue(entry)!;
}

[HarmonyPatch]
internal static class JigsawHuntingPurchase
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseWrapper));
        yield return AccessTools.Method(typeof(MerchantCardRemovalEntry), nameof(MerchantCardRemovalEntry.OnTryPurchaseWrapper), [typeof(MerchantInventory), typeof(bool), typeof(bool)]);
    }
    private static bool Prefix(MerchantEntry __instance, ref Task<bool> __result)
    {
        if (!JigsawHuntingServices.ShopBlocked(JigsawHuntingServices.MerchantOwner(__instance)))
            return true;
        __result = Task.FromResult(false);
        return false;
    }
}

[HarmonyPatch(typeof(OneOffSynchronizer), "DoMerchantCardRemoval")]
internal static class JigsawHuntingRemovalReceiver
{
    private static bool Prefix(Player player, ref Task<bool> __result)
    {
        if (!JigsawHuntingServices.ShopBlocked(player))
            return true;
        __result = Task.FromResult(false);
        return false;
    }
}

[HarmonyPatch(typeof(OneOffSynchronizer), nameof(OneOffSynchronizer.DoLocalMerchantCardRemoval))]
internal static class JigsawHuntingRemovalSender
{
    private static bool Prefix(OneOffSynchronizer __instance, ref Task<bool> __result)
    {
        var player = (Player)AccessTools.Property(typeof(OneOffSynchronizer), "LocalPlayer").GetValue(__instance)!;
        if (!JigsawHuntingServices.ShopBlocked(player))
            return true;
        __result = Task.FromResult(false);
        return false;
    }
}

[HarmonyPatch]
internal static class JigsawHuntingShopAppearance
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { typeof(NMerchantCard), typeof(NMerchantRelic), typeof(NMerchantPotion), typeof(NMerchantCardRemoval) }
        .Select(type => AccessTools.Method(type, "UpdateVisual"));
    private static bool Prefix(NMerchantSlot __instance)
    {
        if (__instance is NMerchantCardRemoval || __instance.Entry == null ||
            !JigsawHuntingServices.ShopBlocked(JigsawHuntingServices.MerchantOwner(__instance.Entry)))
            return true;
        __instance.Visible = false;
        __instance.FocusMode = Control.FocusModeEnum.None;
        __instance.MouseFilter = Control.MouseFilterEnum.Ignore;
        __instance.Hitbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        return false;
    }
    private static void Postfix(NMerchantSlot __instance)
    {
        if (__instance.Entry == null || !JigsawHuntingServices.ShopBlocked(JigsawHuntingServices.MerchantOwner(__instance.Entry)))
            return;
        __instance.FocusMode = Control.FocusModeEnum.None;
        __instance.MouseFilter = Control.MouseFilterEnum.Ignore;
        __instance.Hitbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        if (__instance is NMerchantCardRemoval)
        {
            var animation = __instance.GetNode<AnimationPlayer>("%Animation");
            animation.Play("Used");
            animation.Advance(0);
            __instance.GetNode<Control>("Cost").Visible = false;
        }
        else
            __instance.Visible = false;
    }
}

[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.DefaultFocusedControl), MethodType.Getter)]
internal static class JigsawHuntingShopFocus
{
    private static void Postfix(NMerchantInventory __instance, ref Control? __result)
    {
        if (__instance.Inventory?.AllEntries.FirstOrDefault() is { } entry &&
            JigsawHuntingServices.ShopBlocked(JigsawHuntingServices.MerchantOwner(entry)))
            __result = __instance.GetNode<Control>("%BackButton");
    }
}

[HarmonyPatch(typeof(RestSiteSynchronizer), "ChooseOption")]
internal static class JigsawHuntingRestChoice
{
    private static bool Prefix(RestSiteSynchronizer __instance, Player player, int optionIndex, ref Task<bool> __result)
    {
        var options = __instance.GetOptionsForPlayer(player);
        if (!JigsawHuntingServices.RestBlocked(player) || optionIndex >= 0 && optionIndex < options.Count && options[optionIndex].IsEnabled && JigsawHuntingServices.AllowedRest(options[optionIndex]))
            return true;
        __result = Task.FromResult(false);
        return false;
    }
}

[HarmonyPatch(typeof(NRestSiteButton), nameof(NRestSiteButton.Create))]
internal static class JigsawHuntingRestButton
{
    private static void Postfix(NRestSiteButton __result) => Restrict(__result);
    internal static void Restrict(NRestSiteButton button)
    {
        if (button.Option == null || !JigsawHuntingServices.RestBlocked((Player)AccessTools.Property(typeof(RestSiteOption), "Owner").GetValue(button.Option)!) || JigsawHuntingServices.AllowedRest(button.Option))
            return;
        AccessTools.Field(typeof(NRestSiteButton), "_isUnclickable").SetValue(button, true);
        if (button.IsNodeReady() && button.GetNodeOrNull<TextureRect>("%Icon")?.Material is ShaderMaterial material)
        { material.SetShaderParameter("s", 0f); material.SetShaderParameter("v", 0.6f); }
    }
}

[HarmonyPatch(typeof(NRestSiteButton), "Reload")]
internal static class JigsawHuntingRestButtonReload
{
    private static void Postfix(NRestSiteButton __instance) => JigsawHuntingRestButton.Restrict(__instance);
}

[HarmonyPatch(typeof(NRestSiteRoom), "UpdateRestSiteOptions")]
internal static class JigsawHuntingRestExit
{
    private static void Postfix(NRestSiteRoom __instance)
    {
        var native = MegaCrit.Sts2.Core.Runs.RunManager.Instance.DebugOnlyGetState();
        var player = native?.Players.FirstOrDefault(p => p.NetId == MegaCrit.Sts2.Core.Runs.RunManager.Instance.NetService.NetId);
        if (player == null || !JigsawHuntingServices.RestBlocked(player) || JigsawHuntingServices.RestChoicesRemain(player))
            return;
        // A native ban on healing must not trap a single player among forbidden actions.
        __instance.ProceedButton.Enable();
        NMapScreen.Instance?.SetTravelEnabled(true);
    }
}
