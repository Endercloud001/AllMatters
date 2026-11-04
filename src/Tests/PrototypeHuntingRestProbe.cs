// THROWAWAY: explicit punished-rest input; no quota, persistence or coordinator.
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Rooms;

internal static class PrototypeHuntingRestProbe
{
    internal static bool Restricted=true;
    private static bool Applies(RestSiteOption option)=>Restricted&&((Player)AccessTools.Property(typeof(RestSiteOption),"Owner").GetValue(option)!).RunState.CurrentRoom is RestSiteRoom;
    private static bool Allowed(RestSiteOption option)=>option.IsEnabled&&option is HealRestSiteOption or MendRestSiteOption;
    internal static void Install(Harmony patch)
    {
        patch.Patch(AccessTools.Method(typeof(RestSiteSynchronizer),"ChooseOption"),prefix:new HarmonyMethod(typeof(PrototypeHuntingRestProbe),nameof(Choose)));
        patch.Patch(AccessTools.Method(typeof(NRestSiteButton),nameof(NRestSiteButton.Create)),postfix:new HarmonyMethod(typeof(PrototypeHuntingRestProbe),nameof(Created)));
        patch.Patch(AccessTools.Method(typeof(NRestSiteButton),"Reload"),postfix:new HarmonyMethod(typeof(PrototypeHuntingRestProbe),nameof(Reloaded)));
    }
    internal static bool Choose(RestSiteSynchronizer __instance,Player player,int optionIndex,ref Task<bool> __result)
    {
        var option=__instance.GetOptionsForPlayer(player)[optionIndex];
        if(!Applies(option)||Allowed(option))return true;
        __result=Task.FromResult(false);return false;
    }
    private static void Created(NRestSiteButton __result)
    {
        if(Applies(__result.Option)&&!Allowed(__result.Option))AccessTools.Field(typeof(NRestSiteButton),"_isUnclickable").SetValue(__result,true);
    }
    private static void Reloaded(NRestSiteButton __instance)
    {
        if(!__instance.IsNodeReady()||!Applies(__instance.Option)||Allowed(__instance.Option))return;
        AccessTools.Field(typeof(NRestSiteButton),"_isUnclickable").SetValue(__instance,true);
        var material=(ShaderMaterial)__instance.GetNode<TextureRect>("%Icon").Material;
        material.SetShaderParameter("s",0f);material.SetShaderParameter("v",0.6f);
    }
}

#if HUNTING_REST_MOD
[MegaCrit.Sts2.Core.Modding.ModInitializer(nameof(Initialize))]
public static class PrototypeHuntingRestMod
{
    public static void Initialize()
    {
        PrototypeHuntingRestProbe.Install(new Harmony("AllMattersHuntingRestProbe"));
        GD.Print("[AllMattersHuntingRestProbe] Forced punished-rest fixture: only native enabled Heal/Mend; no hunting budget, mask or save state.");
    }
}
#endif
