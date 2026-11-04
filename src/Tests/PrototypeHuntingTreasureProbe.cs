// THROWAWAY forced punished treasure-room fixture, known native/list reward paths.
// Direct third-party RelicCmd.Obtain receipt compatibility is explicitly unresolved.
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

internal static class PrototypeHuntingTreasureProbe
{
    internal const string Id="AllMattersHuntingTreasureProbe";
    // One local opening only: this is the existing measured normal-call input,
    // not a chosen multiplayer/context/persistence design.
    private static bool NormalCall;
    internal static void Install(Harmony patch)
    {
        patch.Patch(AccessTools.Method(typeof(Hook),nameof(Hook.ShouldGenerateTreasure)),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureProbe),nameof(Generate)));
        patch.Patch(AccessTools.Method(typeof(PlayerCmd),nameof(PlayerCmd.GainGold)),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureProbe),nameof(Gold)));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),"DoTreasureRoomRewards"),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureProbe),nameof(BeginNormal)),postfix:new HarmonyMethod(typeof(PrototypeHuntingTreasureProbe),nameof(EndNormal)));
        patch.Patch(AccessTools.Method(typeof(RewardsSet),nameof(RewardsSet.GenerateWithoutOffering)),postfix:new HarmonyMethod(typeof(PrototypeHuntingTreasureProbe),nameof(Extras)));
    }
    private static bool Generate(Player player,ref bool __result)
    {if(player.RunState.CurrentRoom is not TreasureRoom)return true;__result=NormalCall;return false;}
    private static bool Gold(Player player,ref Task __result)
    {if(player.RunState.CurrentRoom is not TreasureRoom)return true;__result=Task.CompletedTask;return false;}
    private static void BeginNormal()=>NormalCall=true;
    private static void EndNormal(ref Task<int> __result)
    {
        var native=__result;__result=Finish();
        async Task<int> Finish(){try{await native;return 0;}finally{NormalCall=false;}}
    }
    private static void Extras(RewardsSet __instance,ref Task __result)
    {
        if(__instance.Room is not TreasureRoom)return;
        var native=__result;__result=Finish();
        async Task Finish(){await native;__instance.Rewards.Clear();}
    }
#if !HUNTING_TREASURE_MOD
    internal static void LoadStandalone()
    {
#if STS2_PUBLIC
        const string target="public";
#else
        const string target="public-beta";
#endif
        var pack=System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute),false).Cast<System.Reflection.AssemblyMetadataAttribute>().Single(a=>a.Key=="ModResourcePack").Value!;
        var path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(pack)!,target,Id,Id+".dll"));
        var assembly=System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(TestRunner).Assembly)!.LoadFromAssemblyPath(path);
        assembly.GetType("PrototypeHuntingTreasureMod",true)!.GetMethod("Initialize")!.Invoke(null,null);
        Console.WriteLine($"UI_TREASURE_ENTRY target={target} actualDllInitialized=true path={path}; known native/list reward fixture only, direct relic receipt compatibility unresolved");
    }
#endif
}

#if HUNTING_TREASURE_MOD
[MegaCrit.Sts2.Core.Modding.ModInitializer(nameof(Initialize))]
public static class PrototypeHuntingTreasureMod
{
    public static void Initialize()
    {
        PrototypeHuntingTreasureProbe.Install(new Harmony(PrototypeHuntingTreasureProbe.Id));
        Godot.GD.Print("[AllMattersHuntingTreasureProbe] Forced punished TreasureRoom fixture; native/list rewards only; direct third-party relic receipts unresolved; no quota/mask/save integration.");
    }
}
#endif
