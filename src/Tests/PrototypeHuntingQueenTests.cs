// THROWAWAY: isolated queen intent override and native enemy snapshot probe.
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;

internal static class PrototypeHuntingQueenTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases = [("PrototypeHuntingQueen unconditional once override and native snapshot", QueenLoop)];
    private static readonly HashSet<Queen> Replaced=[];
    private static readonly List<Creature> Visited=[];
    private static bool NoPreventedDeath(bool wasRemovalPrevented, ref Task __result)
    { if (!wasRemovalPrevented) return true; __result=Task.CompletedTask; return false; }
    private static void Override(Queen __instance, Creature creature, bool wasRemovalPrevented, ref Task __result)
    {
        var prior=__result;
        __result=Finish();
        async Task Finish()
        {
            await prior;
            if(wasRemovalPrevented || !creature.IsDead || creature.Monster is not TorchHeadAmalgam || !__instance.Creature.IsAlive || !Replaced.Add(__instance)) return;
            var rekindle=new MoveState("PROTOTYPE_REKINDLE", async _ =>
            {
                foreach(var slot in new[]{"prototype_torch_left1","prototype_torch_left2"})
                {
                    var torch=await CreatureCmd.Add<PrototypeHuntingTorch>(__instance.CombatState,slot);
                    await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(),torch,1,null,null);
                }
            },new BuffIntent());
            rekindle.FollowUpState=__instance.MoveStateMachine.States["ENRAGE_MOVE"];
            __instance.MoveStateMachine.States[rekindle.Id]=rekindle;
            __instance.SetMoveImmediate(rekindle,true);
        }
    }
    private static void Enrage(Queen __instance,ref Task __result)
    {
        var prior=__result; __result=Finish();
        async Task Finish()
        {
            await prior;
            foreach(var torch in __instance.CombatState.Enemies.Where(c=>c.IsAlive&&c.Monster is PrototypeHuntingTorch))
                await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(),torch,2,__instance.Creature,null);
        }
    }
    private static void Track(Creature __instance)=>Visited.Add(__instance);
    private static void Slots(ref IReadOnlyList<string> __result)=>__result=["amalgam","prototype_torch_left1","prototype_torch_left2","queen"];
    private static bool SkipTurnEnd(ref Task __result) {__result=Task.CompletedTask;return false;}
    private static async Task QueenLoop()
    {
        PrototypeHuntingModelTests.Inject();
        var patch=new Harmony("allmatters.prototype70.queen");
        patch.Patch(AccessTools.Method(typeof(Queen),nameof(Queen.AfterDeath)),prefix:new HarmonyMethod(typeof(PrototypeHuntingQueenTests),nameof(NoPreventedDeath)),postfix:new HarmonyMethod(typeof(PrototypeHuntingQueenTests),nameof(Override)));
        patch.Patch(AccessTools.Method(typeof(Queen),"EnrageMove"),postfix:new HarmonyMethod(typeof(PrototypeHuntingQueenTests),nameof(Enrage)));
        patch.Patch(AccessTools.Method(typeof(Creature),nameof(Creature.TakeTurn)),prefix:new HarmonyMethod(typeof(PrototypeHuntingQueenTests),nameof(Track)));
        patch.Patch(AccessTools.Method(typeof(CombatManager),"EndEnemyTurn"),prefix:new HarmonyMethod(typeof(PrototypeHuntingQueenTests),nameof(SkipTurnEnd)));
        patch.Patch(AccessTools.PropertyGetter(typeof(QueenBoss),nameof(EncounterModel.Slots)),postfix:new HarmonyMethod(typeof(PrototypeHuntingQueenTests),nameof(Slots)));
        try
        {
            for(int n=1;n<=4;n++)
            foreach(string initial in new[]{"PUPPET_STRINGS_MOVE","YOU_ARE_MINE_MOVE","BURN_BRIGHT_FOR_ME_MOVE","OFF_WITH_YOUR_HEAD_MOVE","EXECUTION_MOVE","ENRAGE_MOVE"})
            {
                var run=HeadlessTestSetup.Run("HUNTING70_QUEEN",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
                var previous=MegaCrit.Sts2.Core.Context.LocalContext.NetId;
                try
                {
                    var encounter=ModelDb.Encounter<QueenBoss>().ToMutable();
                    var combat=new CombatState(encounter,run){RoundNumber=1};
                    foreach(var p in run.Players){combat.AddPlayer(p);p.ResetCombatState();p.Creature.SetMaxHpInternal(1000);p.Creature.SetCurrentHpInternal(1000);}
                    RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
                    MegaCrit.Sts2.Core.Context.LocalContext.NetId=run.Players[0].NetId;
                    CombatManager.Instance.Reset(true); CombatManager.Instance.SetUpCombat(combat);
                    encounter.GenerateMonstersWithSlots(run);
                    foreach(var (monster,slot) in encounter.MonstersWithSlots){var c=combat.CreateCreature(monster,CombatSide.Enemy,slot);combat.AddCreature(c);CombatManager.Instance.AddCreature(c);}
                    foreach(var c in combat.Enemies.ToArray())await c.AfterAddedToRoom();
                    var queen=(Queen)combat.Enemies.Single(c=>c.Monster is Queen).Monster!;
                    var amalgam=combat.Enemies.Single(c=>c.Monster is TorchHeadAmalgam);
                    queen.SetMoveImmediate((MoveState)queen.MoveStateMachine.States[initial],true);
                    await queen.AfterDeath(new ThrowingPlayerChoiceContext(),amalgam,true,0);
                    NativeEventTests.Check(queen.NextMove.Id==initial,"Prevented death must not override intent");
                    await CreatureCmd.Kill(amalgam);
                    NativeEventTests.Check(queen.NextMove.Id=="PROTOTYPE_REKINDLE","Actual death overrides every current intent");
                    combat.CurrentSide=CombatSide.Enemy;
                    queen.OnSideSwitch();
                    object? turnState=AccessTools.Field(typeof(CombatManager),"_turnState")?.GetValue(CombatManager.Instance);
                    if(turnState!=null) AccessTools.Property(turnState.GetType(),"IsInProgress").SetValue(turnState,true);
                    var execute=AccessTools.Method(typeof(CombatManager),"ExecuteEnemyTurn");
                    async Task EnemyPhase()
                    {
                        Visited.Clear();
                        var args=execute.GetParameters().Length==2?new object?[]{turnState,null}:new object?[]{null};
                        await (Task)execute.Invoke(CombatManager.Instance,args)!;
                    }
                    await EnemyPhase();
                    var torches=combat.Enemies.Where(c=>c.Monster is PrototypeHuntingTorch).ToArray();
                    NativeEventTests.Check(torches.Length==2 && torches.All(t=>t.IsSecondaryEnemy) && !Visited.Any(c=>c.Monster is PrototypeHuntingTorch),"Native snapshot does not visit newly summoned torches");
                    NativeEventTests.Check(torches.All(t=>encounter.Slots.Contains(t.SlotName!)),"Summons are actually declared slots, not native unknown-slot negative-index fallback");
                    foreach(var c in combat.Enemies){c.Monster!.OnSideSwitch();c.PrepareForNextTurn(combat.PlayerCreatures);}
                    int hp=run.Players.Sum(p=>p.Creature.CurrentHp);
                    await EnemyPhase();
                    NativeEventTests.Check(Visited.Select(c=>c.SlotName).SequenceEqual(new[]{"prototype_torch_left1","prototype_torch_left2","queen"}),"Expanded declared left slots really precede queen in native turn iteration");
                    NativeEventTests.Check(torches.All(t=>Visited.Contains(t)&&t.GetPowerAmount<StrengthPower>()==2),"Next native phase visits torches and Enrage adds fixed2");
                    int actualDamage=hp-run.Players.Sum(p=>p.Creature.CurrentHp);
                    Console.WriteLine($"QUEEN_DEBUG players={n} initial={initial} damage={actualDamage} visited={string.Join(',',Visited.Select(c=>c.Monster!.GetType().Name))} torchMoves={string.Join(',',torches.Select(c=>c.Monster!.NextMove.Id))}");
                    NativeEventTests.Check(actualDamage==20*n,$"Two left torches attack10 before Queen Enrage in native order; actual={actualDamage}");
                    await CreatureCmd.Kill(torches[0]);
                    queen.SetMoveImmediate((MoveState)queen.MoveStateMachine.States["ENRAGE_MOVE"],true);
                    await queen.NextMove.PerformMove(combat.PlayerCreatures);
                    NativeEventTests.Check(combat.Enemies.Count(c=>c.Monster is PrototypeHuntingTorch&&c.IsAlive)==1&&torches[1].GetPowerAmount<StrengthPower>()==4,"Later Enrage buffs survivor without replenishing");
                    await queen.AfterDeath(new ThrowingPlayerChoiceContext(),amalgam,false,0);
                    NativeEventTests.Check(queen.NextMove.Id!="PROTOTYPE_REKINDLE","Repeated death callback cannot rekindle twice");
                    Console.WriteLine($"QUEEN players={n} initial={initial} preventedIgnored=true override=true nativeSnapshot=true nextPhaseAttack=10-before-enrage enrageSurvivors=true noResummon=true; EndEnemyTurn isolated, no UI/network/save");
                }
                finally{CombatManager.Instance.Reset(true);RunManager.Instance.CleanUp(false);MegaCrit.Sts2.Core.Context.LocalContext.NetId=previous;}
            }
        }
        finally{patch.UnpatchAll(patch.Id);Replaced.Clear();Visited.Clear();}
    }
}


