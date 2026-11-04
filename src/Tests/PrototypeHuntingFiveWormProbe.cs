// Minimal live-scene experiment: only Phrog's fifth worm and five declared
// positions. No hunting trigger/state coordinator, saves, new art or full system.
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

public static class PrototypeHuntingFiveWormProbe
{
    private static readonly Type DeathMachine=typeof(InfestedPower).GetMethod(nameof(InfestedPower.AfterDeath))!.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
    public static void Install(Harmony patch)
    {
        patch.Patch(AccessTools.Method(DeathMachine,"MoveNext"),transpiler:new HarmonyMethod(typeof(PrototypeHuntingFiveWormProbe),nameof(FiveLoop)));
        patch.Patch(AccessTools.Method(typeof(Wriggler),"GenerateMoveStateMachine"),postfix:new HarmonyMethod(typeof(PrototypeHuntingFiveWormProbe),nameof(FifthBranch)));
        patch.Patch(AccessTools.PropertyGetter(typeof(PhrogParasiteElite),nameof(EncounterModel.Slots)),postfix:new HarmonyMethod(typeof(PrototypeHuntingFiveWormProbe),nameof(FiveSlots)));
        patch.Patch(AccessTools.Method(typeof(EncounterModel),nameof(EncounterModel.CreateScene)),postfix:new HarmonyMethod(typeof(PrototypeHuntingFiveWormProbe),nameof(Scene)));
    }
    private static int Count(InfestedPower power)=>power.Owner.Monster is PhrogParasite?5:4;
    private static IEnumerable<CodeInstruction> FiveLoop(IEnumerable<CodeInstruction> instructions)
    {
        var code=instructions.ToList();var bounds=code.Where(c=>c.opcode==OpCodes.Ldc_I4_4).ToArray();
        // Retain the existing experiment's narrow instruction check.
        if(bounds.Length!=1)throw new Exception($"Expected one loop bound, found {bounds.Length}");
        int index=code.IndexOf(bounds[0]);bounds[0].opcode=OpCodes.Ldarg_0;bounds[0].operand=null;
        var power=DeathMachine.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Single(f=>f.FieldType==typeof(InfestedPower));
        code.InsertRange(index+1,[new CodeInstruction(OpCodes.Ldfld,power),new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(PrototypeHuntingFiveWormProbe),nameof(Count)))]);
        return code;
    }
    private static void FifthBranch(Wriggler __instance,MonsterMoveStateMachine __result)=>((ConditionalBranchState)__result.States["INIT_MOVE"]).AddState(__result.States["NASTY_BITE_MOVE"],()=>__instance.Creature.SlotName=="wriggler5");
    private static void FiveSlots(ref IReadOnlyList<string> __result)=>__result=["phrog","wriggler1","wriggler2","wriggler3","wriggler4","wriggler5"];
    private static void Scene(EncounterModel __instance,Control __result)
    {
        if(__instance is not PhrogParasiteElite)return;
        if(!__result.HasNode("wriggler5"))__result.AddChild(new Marker2D{Name="wriggler5"});
        for(int i=1;i<=5;i++)__result.GetNode<Marker2D>("wriggler"+i).Position=new Vector2(840+210*(i-1),700);
        GD.Print("[AllMattersHuntingFiveWormProbe] native Phrog scene: five worm markers x=840/1050/1260/1470/1680 y=700 (210px equal spacing); wider user-requested trial, no layout acceptance yet.");
    }
}

#if HUNTING_FIVE_WORM_MOD
[MegaCrit.Sts2.Core.Modding.ModInitializer(nameof(Initialize))]
public static class PrototypeHuntingFiveWormMod
{
    public static void Initialize()
    {
        STS2RitsuLib.RitsuLibFramework.EnsureGodotScriptsRegistered(typeof(MonsterModel).Assembly,STS2RitsuLib.RitsuLibFramework.CreateLogger("AllMattersHuntingFiveWormProbe"));
        PrototypeHuntingFiveWormProbe.Install(new Harmony("AllMattersHuntingFiveWormProbe"));
        GD.Print("[AllMattersHuntingFiveWormProbe] Phrog death/slots/scene probe active; new disposable test run only. Other hunting rules are absent.");
    }
}
#endif
