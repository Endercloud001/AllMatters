// Minimal current-combat probe. The once flag is intentionally per in-memory
// Queen; it is NOT a save/network state design or a recovery claim.
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
#if HUNTING_QUEEN_MOD
using TorchForProbe=PrototypeHuntingLiveTorch;
#else
using TorchForProbe=PrototypeHuntingTorch;
#endif

public static class PrototypeHuntingQueenProbe
{
    private sealed class Once{public bool Rekindled;}
    private static ConditionalWeakTable<Queen,Once> Flags=new();
    public static void Clear()=>Flags=new();
    public static void Install(Harmony patch)
    {
        patch.Patch(AccessTools.Method(typeof(Queen),nameof(Queen.AfterDeath)),prefix:new HarmonyMethod(typeof(PrototypeHuntingQueenProbe),nameof(Prevented)),postfix:new HarmonyMethod(typeof(PrototypeHuntingQueenProbe),nameof(Override)));
        patch.Patch(AccessTools.Method(typeof(Queen),"EnrageMove"),postfix:new HarmonyMethod(typeof(PrototypeHuntingQueenProbe),nameof(Enrage)));
        patch.Patch(AccessTools.PropertyGetter(typeof(QueenBoss),nameof(EncounterModel.Slots)),postfix:new HarmonyMethod(typeof(PrototypeHuntingQueenProbe),nameof(Slots)));
        patch.Patch(AccessTools.Method(typeof(EncounterModel),nameof(EncounterModel.CreateScene)),postfix:new HarmonyMethod(typeof(PrototypeHuntingQueenProbe),nameof(Scene)));
    }
    private static bool Prevented(Creature creature,bool wasRemovalPrevented,ref Task __result)
    {if(!wasRemovalPrevented||creature.Monster is not TorchHeadAmalgam)return true;__result=Task.CompletedTask;return false;}
    private static void Override(Queen __instance,Creature creature,bool wasRemovalPrevented,ref Task __result)
    {
        var prior=__result;__result=Finish();
        async Task Finish()
        {
            await prior;
            if(wasRemovalPrevented||!creature.IsDead||creature.Monster is not TorchHeadAmalgam||!__instance.Creature.IsAlive)return;
            var flag=Flags.GetOrCreateValue(__instance);if(flag.Rekindled)return;flag.Rekindled=true;
            var rekindle=new MoveState("PROTOTYPE_REKINDLE",async _=>
            {
                foreach(var slot in new[]{"prototype_torch_left1","prototype_torch_left2"})
                {var torch=await CreatureCmd.Add<TorchForProbe>(__instance.CombatState,slot);await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(),torch,1,null,null);}
            },new BuffIntent());
            rekindle.FollowUpState=__instance.MoveStateMachine.States["ENRAGE_MOVE"];
            __instance.MoveStateMachine.States[rekindle.Id]=rekindle;__instance.SetMoveImmediate(rekindle,true);
        }
    }
    private static void Enrage(Queen __instance,ref Task __result)
    {
        var prior=__result;__result=Finish();
        async Task Finish(){await prior;foreach(var torch in __instance.CombatState.Enemies.Where(c=>c.IsAlive&&c.Monster is TorchForProbe))await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(),torch,2,__instance.Creature,null);}
    }
    private static void Slots(ref IReadOnlyList<string> __result)=>__result=["amalgam","prototype_torch_left1","prototype_torch_left2","queen"];
    private static void Scene(EncounterModel __instance,Control __result)
    {
        if(__instance is not QueenBoss)return;
        for(int i=1;i<=2;i++){string name="prototype_torch_left"+i;if(!__result.HasNode(name))__result.AddChild(new Marker2D{Name=name});__result.GetNode<Marker2D>(name).Position=new Vector2(1050+200*(i-1),695);}
        GD.Print("[AllMattersHuntingQueenProbe] native queen scene has two left torch markers (1050/1250,695); experimental positions.");
    }
}

#if HUNTING_QUEEN_MOD
[MegaCrit.Sts2.Core.Modding.ModInitializer(nameof(Initialize))]
public static class PrototypeHuntingQueenMod
{
    public static void Initialize()
    {
        string path=System.Environment.GetEnvironmentVariable("ALLMATTERS_HUNTING_RESOURCE_PACK")??Path.Combine(Path.GetDirectoryName(typeof(PrototypeHuntingQueenMod).Assembly.Location)!,"ActsFromThePast.pck");
        if(!ProjectSettings.LoadResourcePack(path,false))throw new FileNotFoundException("Place the author's original 0.13.0 ActsFromThePast.pck beside this probe DLL, or set ALLMATTERS_HUNTING_RESOURCE_PACK; no art is bundled.",path);
        STS2RitsuLib.RitsuLibFramework.EnsureGodotScriptsRegistered(typeof(MonsterModel).Assembly,STS2RitsuLib.RitsuLibFramework.CreateLogger("AllMattersHuntingQueenProbe"));
        ModelDb.Inject(typeof(TorchForProbe));PrototypeHuntingQueenProbe.Install(new Harmony("AllMattersHuntingQueenProbe"));
        GD.Print("[AllMattersHuntingQueenProbe] current-combat Queen/Torch resource+layout probe active. No save/recovery support claimed.");
    }
}
#endif
