// THROWAWAY: isolated queen intent override and native enemy snapshot probe.
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class PrototypeHuntingQueenTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases = [("PrototypeHuntingQueen unconditional once override and native snapshot",()=>QueenLoop(null)),("PrototypeHuntingQueen built live DLL exact resource and native snapshot",LiveEntry)];
    private static async Task LiveEntry()
    {
#if STS2_PUBLIC
        const string target="public";
#else
        const string target="public-beta";
#endif
        string pack=Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="ModResourcePack").Value!;
        string path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(pack)!,target,"AllMattersHuntingQueenProbe","AllMattersHuntingQueenProbe.dll"));
        var assembly=System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(TestRunner).Assembly)!.LoadFromAssemblyPath(path);
        const string variable="ALLMATTERS_HUNTING_RESOURCE_PACK",id="AllMattersHuntingQueenProbe";
        var previous=System.Environment.GetEnvironmentVariable(variable);
        try
        {
            System.Environment.SetEnvironmentVariable(variable,Path.GetFullPath(Path.Combine(Path.GetDirectoryName(pack)!,"../.local/resource-search/ActsFromThePast.pck")));
            assembly.GetType("PrototypeHuntingQueenMod",true)!.GetMethod("Initialize")!.Invoke(null,null);
            await QueenLoop(assembly.GetType("PrototypeHuntingLiveTorch",true)!);
            string encounter=ModelDb.Encounter<QueenBoss>().Id.Entry;
            NativeEventTests.Check(ModelDb.GetById<EncounterModel>(new ModelId(ModelId.SlugifyCategory<EncounterModel>(),encounter)) is QueenBoss,"Native fight model lookup resolves queen encounter ID");
            Console.WriteLine($"LIVE_QUEEN_COMMAND target={target} actualNativeModelLookup=true manualCommand=fight {encounter}; Agent did not execute a room jump/game");
        }
        finally{new Harmony(id).UnpatchAll(id);System.Environment.SetEnvironmentVariable(variable,previous);}
    }
    private static readonly List<Creature> Visited=[];
    private static void Track(Creature __instance)=>Visited.Add(__instance);
    private static bool SkipTurnEnd(ref Task __result) {__result=Task.CompletedTask;return false;}
    private static async Task QueenLoop(Type? liveTorch)
    {
        PrototypeHuntingModelTests.Inject();
        var patch=new Harmony("allmatters.prototype70.queen");
        if(liveTorch==null)PrototypeHuntingQueenProbe.Install(patch);
        Type torchType=liveTorch??typeof(PrototypeHuntingTorch);
        patch.Patch(AccessTools.Method(typeof(Creature),nameof(Creature.TakeTurn)),prefix:new HarmonyMethod(typeof(PrototypeHuntingQueenTests),nameof(Track)));
        patch.Patch(AccessTools.Method(typeof(CombatManager),"EndEnemyTurn"),prefix:new HarmonyMethod(typeof(PrototypeHuntingQueenTests),nameof(SkipTurnEnd)));
        try
        {
            for(int n=1;n<=4;n++)
            foreach(string initial in new[]{"PUPPET_STRINGS_MOVE","YOU_ARE_MINE_MOVE","BURN_BRIGHT_FOR_ME_MOVE","OFF_WITH_YOUR_HEAD_MOVE","EXECUTION_MOVE","ENRAGE_MOVE"})
            {
                var players=Enumerable.Range(1,n).Select(i=>Player.CreateForNewRun(ModelDb.Character<Ironclad>(),UnlockState.all,(ulong)i)).ToArray();
                var run=RunState.CreateForTest(players,acts:[ModelDb.Act<Overgrowth>(),ModelDb.Act<Hive>(),ModelDb.Act<Glory>()],seed:"HUNTING70_QUEEN");run.CurrentActIndex=2;
                var previous=MegaCrit.Sts2.Core.Context.LocalContext.NetId;
                try
                {
                    var encounter=ModelDb.Encounter<QueenBoss>().ToMutable();
                    using(var scene=encounter.CreateScene()){NativeEventTests.Check(scene.HasNode("prototype_torch_left1")&&scene.HasNode("prototype_torch_left2"),"Actual native queen scene receives both left markers");scene.Free();}
                    var combat=new CombatState(encounter,run){RoundNumber=1};
                    foreach(var p in run.Players){combat.AddPlayer(p);p.ResetCombatState();p.Creature.SetMaxHpInternal(1000);p.Creature.SetCurrentHpInternal(1000);}
                    RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
                    MegaCrit.Sts2.Core.Context.LocalContext.NetId=run.Players[0].NetId;
                    CombatManager.Instance.Reset(true); CombatManager.Instance.SetUpCombat(combat);
                    encounter.GenerateMonstersWithSlots(run);
                    foreach(var (monster,slot) in encounter.MonstersWithSlots){var c=combat.CreateCreature(monster,CombatSide.Enemy,slot);combat.AddCreature(c);CombatManager.Instance.AddCreature(c);}
                    foreach(var c in combat.Enemies.ToArray())await c.AfterAddedToRoom();
                    using var native=HeadlessCombatInProgress.EnterNativeLifecycle();PrototypeHuntingNativeEndingTests.SupplyActiveInput();
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
                    var torches=combat.Enemies.Where(c=>c.Monster?.GetType()==torchType).ToArray();
                    NativeEventTests.Check(torches.All(t=>t.MaxHp==(int)Creature.ScaleHpForMultiplayer(50,encounter,n,2)),"Third-act torch HP has exactly one native encounter/act/player scaling");
                    NativeEventTests.Check(torches.Length==2 && torches.All(t=>t.IsSecondaryEnemy) && !Visited.Any(c=>c.Monster?.GetType()==torchType),"Native snapshot does not visit newly summoned torches");
                    NativeEventTests.Check(torches.All(t=>encounter.Slots.Contains(t.SlotName!)),"Summons are actually declared slots, not native unknown-slot negative-index fallback");
                    if(liveTorch!=null)
                    {
                        var tree=(Godot.SceneTree)Godot.Engine.GetMainLoop();await tree.ToSignal(tree,Godot.SceneTree.SignalName.ProcessFrame);
                        foreach(var torch in torches)
                        {
                            using var visual=torch.Monster!.CreateVisuals();tree.Root.AddChild(visual);
                            NativeEventTests.Check(visual.SpineBody?.GetSkeleton()?.GetData()?.HasAnimation("halberd_slam")==true&&visual.Bounds.Size.X==144,"Separately built live model uses exact author torch skeleton and bounds");
                            foreach(string language in new[]{"eng","zhs"})
                            {MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage(language);NativeEventTests.Check(torch.Monster.Title.GetFormattedText().Contains(language=="zhs"?"火炬头":"Torch Head"),"Actual current-locale native title renders for the live class");}
                            MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage("eng");tree.Root.RemoveChild(visual);visual.Free();
                        }
                    }
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
                    NativeEventTests.Check(combat.Enemies.Count(c=>c.Monster?.GetType()==torchType&&c.IsAlive)==1&&torches[1].GetPowerAmount<StrengthPower>()==4,"Later Enrage buffs survivor without replenishing");
                    await queen.AfterDeath(new ThrowingPlayerChoiceContext(),amalgam,false,0);
                    NativeEventTests.Check(queen.NextMove.Id!="PROTOTYPE_REKINDLE","Repeated death callback cannot rekindle twice");
                    Console.WriteLine($"QUEEN players={n} act=3 nativeGetters=true initial={initial} builtLiveDll={liveTorch!=null} torchHpOnce={torches[1].MaxHp} preventedIgnored=true override=true nativeSnapshot=true nextPhaseAttack=10-before-enrage enrageSurvivors=true noResummon=true; exact torch resources checked when builtLiveDll, EndEnemyTurn isolated, no UI/network/save");
                }
                finally{CombatManager.Instance.Reset(true);RunManager.Instance.CleanUp(false);MegaCrit.Sts2.Core.Context.LocalContext.NetId=previous;}
            }
        }
        finally{patch.UnpatchAll(patch.Id);if(liveTorch==null)PrototypeHuntingQueenProbe.Clear();Visited.Clear();MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage("eng");}
    }
}
