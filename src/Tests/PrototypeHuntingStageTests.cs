// THROWAWAY stage/draw experiments: explicit revival hooks and native turn methods;
// no automatic readiness coordinator, start-combat, checkpoint or network proof.
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

public sealed class PrototypeHuntingScrutiny : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override bool ShouldDraw(Player player, bool fromHandDraw) => fromHandDraw;
}

public sealed class PrototypeHuntingClock : PowerModel
{
    public bool Armed { get; set; }
    public int FullPlayerTurns { get; private set; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override async Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combat)
    {
        if (!Armed || side != CombatSide.Player) return;
        FullPlayerTurns++;
        if (FullPlayerTurns % 3 == 0) await PowerCmd.Apply<PrototypeHuntingScrutiny>(context, Owner, 1, Owner, null);
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player) await PowerCmd.Remove(Owner.GetPower<PrototypeHuntingScrutiny>());
    }
}

internal static class PrototypeHuntingStageTests
{
    private static bool HoldRevive;
    private static bool ReachedRevive;
    private static TaskCompletionSource? ReleaseRevive;
    private static bool PauseMaxHp(Creature creature,decimal amount,ref Task __result)
    {
        if(!HoldRevive||creature.Monster is not TestSubject)return true;
        HoldRevive=false;ReachedRevive=true;__result=Continue();return false;
        async Task Continue(){await ReleaseRevive!.Task;await CreatureCmd.SetMaxHp(creature,amount);}
    }
    internal static readonly (string Name, Func<Task> Test)[] Cases = [("PrototypeHuntingStage native respawns and real draw filtering at full turns 3 6 9", Stage),("PrototypeHuntingStage native player turn setup and both end phases",()=>NativePlayerTurns(false)),("PrototypeHuntingStage native alternating sides through scrutiny turns",()=>NativePlayerTurns(true)),("PrototypeHuntingStage continuous native start death revival and nine turns",Continuous)];
    private static PrototypeHuntingClock? ContinuousClock;
    private static TaskCompletionSource<Func<Task>?>? ParkedCoordinator;
    private static bool SkipFtue(ref bool __result){__result=true;return false;}
    private static bool ParkCoordinator(ref Task<Func<Task>?> __result)
    {ParkedCoordinator=new();__result=ParkedCoordinator.Task;return false;}
    private static void AfterRevive(TestSubject __instance,ref Task __result)
    {
        var native=__result;__result=Finish();
        async Task Finish()
        {
            await native;
            var ctx=new ThrowingPlayerChoiceContext();
            int phase=(int)HarmonyLib.AccessTools.Property(typeof(TestSubject),"Respawns").GetValue(__instance)!;
            if(phase==1)
            {
                await PowerCmd.Remove(__instance.Creature.GetPower<PainfulStabsPower>());
                var shy=await PowerCmd.Apply<SkittishPower>(ctx,__instance.Creature,10,null,null);
                if(shy!.Amount!=10)await PowerCmd.ModifyAmount(ctx,shy,10-shy.Amount,null,null);
                await PowerCmd.Apply<SuckPower>(ctx,__instance.Creature,3,null,null);
            }
            if(phase==2)
            {
                await PowerCmd.Remove(__instance.Creature.GetPower<SkittishPower>());
                await PowerCmd.Remove(__instance.Creature.GetPower<SuckPower>());
                ContinuousClock=await PowerCmd.Apply<PrototypeHuntingClock>(ctx,__instance.Creature,1,null,null);
                ContinuousClock!.Armed=true;
            }
        }
    }
    private static async Task Continuous()
    {
        ModelDb.Inject(typeof(PrototypeHuntingClock));ModelDb.Inject(typeof(PrototypeHuntingScrutiny));
        var patch=new HarmonyLib.Harmony("allmatters.prototype70.continuous-stage");
        patch.Patch(HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Saves.SaveManager),"SeenFtue"),prefix:new HarmonyLib.HarmonyMethod(typeof(PrototypeHuntingStageTests),nameof(SkipFtue)));
        patch.Patch(HarmonyLib.AccessTools.Method(typeof(TestSubject),"RespawnMove"),postfix:new HarmonyLib.HarmonyMethod(typeof(PrototypeHuntingStageTests),nameof(AfterRevive)));
#if !STS2_PUBLIC
        // Only the readiness coordinator is parked. Native start, side setup,
        // death interception, enemy moves and player end phases remain real.
        patch.Patch(HarmonyLib.AccessTools.Method(typeof(CombatManager),"AwaitTurnEndAndSwitchSides"),prefix:new HarmonyLib.HarmonyMethod(typeof(PrototypeHuntingStageTests),nameof(ParkCoordinator)));
#endif
        try
        {
            for(int n=1;n<=4;n++)
            {
                var run=HeadlessTestSetup.Run("HUNTING70_CONTINUOUS",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
                using var battle=new HeadlessTestSetup.EncounterCombatScope(run,ModelDb.Encounter<TestSubjectBoss>().ToMutable());
                using var lifecycle=HeadlessCombatInProgress.EnterNativeLifecycle();
                var subject=(TestSubject)battle.Monster;var ctx=new ThrowingPlayerChoiceContext();
                ContinuousClock=await PowerCmd.Apply<PrototypeHuntingClock>(ctx,subject.Creature,1,null,null);
                foreach(var p in run.Players)for(int i=0;i<90;i++)PileType.Draw.GetPile(p).AddInternal(battle.Combat.CreateCard<DefendIronclad>(p));
                Task start=InvokeNative("StartCombatInternal");
                try
                {
                    for(int frame=0;frame<120&&!start.IsCompleted&&run.Players.Any(p=>PileType.Hand.GetPile(p).Cards.Count!=5);frame++)
                        await Godot.Engine.GetMainLoop().ToSignal(Godot.Engine.GetMainLoop(),Godot.SceneTree.SignalName.ProcessFrame);
                    if(start.IsFaulted)await start;
                    NativeEventTests.Check(CombatManager.Instance.IsInProgress&&subject.Creature.HasPower<AdaptablePower>()&&run.Players.All(p=>PileType.Hand.GetPile(p).Cards.Count==5),"Native start reaches first player hand and actual AfterAdded powers, without supplied active flag");
                    for(int phase=1;phase<=2;phase++)
                    {
                        await CreatureCmd.Kill(subject.Creature);
                        NativeEventTests.Check(subject.NextMove.Id=="RESPAWN_MOVE"&&ContinuousClock!.FullPlayerTurns==0,"Actual native death interception selects revive without counting a half turn");
                        await InvokeNative("EndPlayerTurnPhaseOneInternal");await InvokeNative("EndPlayerTurnPhaseTwoInternal");
                        await InvokeNative("SwitchSides");await InvokeNative("StartTurn");
#if !STS2_PUBLIC
                        await InvokeNative("StartTurn");
#endif
                        NativeEventTests.Check((int)HarmonyLib.AccessTools.Property(typeof(TestSubject),"Respawns").GetValue(subject)! ==phase,"Native enemy turn completed exactly one actual revival");
                        Console.WriteLine($"STAGE_CONTINUOUS_TRANSITION players={n} phase={phase} armed={ContinuousClock!.Armed} count={ContinuousClock.FullPlayerTurns} attached={subject.Creature.HasPower<PrototypeHuntingClock>()} side={battle.Combat.CurrentSide}");
                        NativeEventTests.Check(ContinuousClock.FullPlayerTurns==(phase==2?1:0),"First complete player start after third-form completion is turn1");
                    }
                    NativeEventTests.Check(!subject.Creature.HasPower<SkittishPower>()&&!subject.Creature.HasPower<SuckPower>()&&subject.Creature.HasPower<NemesisPower>(),"Stage2 powers removed after native third-form completion");
                    for(int turn=1;turn<=9;turn++)
                    {
                        bool limited=turn%3==0;
                        NativeEventTests.Check(ContinuousClock!.FullPlayerTurns==turn&&subject.Creature.HasPower<PrototypeHuntingScrutiny>()==limited,"Continuous native player start applies exact3/6/9 scrutiny");
                        foreach(var p in run.Players)
                        {
                            NativeEventTests.Check(PileType.Hand.GetPile(p).Cards.Count==5,"Continuous native starting draw remains five");
                            await CardPileCmd.Draw(ctx,1,p,fromHandDraw:false);
                            NativeEventTests.Check(PileType.Hand.GetPile(p).Cards.Count==(limited?5:6),"Continuous extra draw respects scrutiny");
                        }
                        await InvokeNative("EndPlayerTurnPhaseOneInternal");await InvokeNative("EndPlayerTurnPhaseTwoInternal");
                        NativeEventTests.Check(!subject.Creature.HasPower<PrototypeHuntingScrutiny>(),"Continuous native end removes scrutiny");
                        Console.WriteLine($"STAGE_CONTINUOUS players={n} phase3Turn={turn} nativeStart=true nativeDeathsAndRevives=2 limited={limited} normalDraw=5 extraDraw={(limited?"blocked":"allowed")} endRemoved=true; FTUE query isolated before save access, readiness coordinator parked, explicit native turn dispatch, player HP reset before enemies, no network/checkpoint/production integration proof");
                        if(turn==9)break;
                        foreach(var p in run.Players)p.Creature.SetCurrentHpInternal(p.Creature.MaxHp);
                        await InvokeNative("SwitchSides");await InvokeNative("StartTurn");
#if !STS2_PUBLIC
                        await InvokeNative("StartTurn");
#endif
                    }
                }
                finally
                {
#if !STS2_PUBLIC
                    CombatManager.Instance.Reset(true);ParkedCoordinator?.TrySetResult(null);
                    try{await start.WaitAsync(TimeSpan.FromSeconds(5));}catch(OperationCanceledException){}
#endif
                    ContinuousClock=null;ParkedCoordinator=null;
                }
            }
        }
        finally{patch.UnpatchAll(patch.Id);}
    }
    private static async Task NativePlayerTurns(bool enemySides)
    {
        PrototypeHuntingModelTests.Inject();
        ModelDb.Inject(typeof(PrototypeHuntingFinalFungusEncounter));ModelDb.Inject(typeof(PrototypeHuntingClock));ModelDb.Inject(typeof(PrototypeHuntingScrutiny));
        for(int n=1;n<=4;n++)
        {
            var run=HeadlessTestSetup.Run("HUNTING70_NATIVE_PLAYER_TURNS",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
            using var battle=new HeadlessTestSetup.EncounterCombatScope(run,ModelDb.Encounter<PrototypeHuntingFinalFungusEncounter>().ToMutable());
            var clock=await PowerCmd.Apply<PrototypeHuntingClock>(new ThrowingPlayerChoiceContext(),battle.Monster.Creature,1,null,null);clock!.Armed=true;
            using var lifecycle=HeadlessCombatInProgress.EnterNativeLifecycle();
            PrototypeHuntingNativeEndingTests.SupplyActiveInput();
            foreach(var p in run.Players)for(int i=0;i<70;i++)await CardPileCmd.AddGeneratedCardToCombat(battle.Combat.CreateCard<DefendIronclad>(p),PileType.Draw,p);
            for(int turn=1;turn<=9;turn++)
            {
#if STS2_PUBLIC
                if(!enemySides||turn==1)
#endif
                await InvokeNative("StartTurn");
                bool limited=turn%3==0;
                NativeEventTests.Check(clock.FullPlayerTurns==turn&&battle.Monster.Creature.HasPower<PrototypeHuntingScrutiny>()==limited,"Native start dispatches one full-side counter before actual native hand draw");
                foreach(var p in run.Players)
                {
                    NativeEventTests.Check(PileType.Hand.GetPile(p).Cards.Count==5,"Native setup actually draws normal five on limited and ordinary turns");
                    await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(),1,p,fromHandDraw:false);
                    NativeEventTests.Check(PileType.Hand.GetPile(p).Cards.Count==(limited?5:6),"Extra draw uses actual scrutiny after native setup");
                }
                await InvokeNative("EndPlayerTurnPhaseOneInternal");
                await InvokeNative("EndPlayerTurnPhaseTwoInternal");
                NativeEventTests.Check(!battle.Monster.Creature.HasPower<PrototypeHuntingScrutiny>()&&run.Players.All(p=>PileType.Hand.GetPile(p).Cards.Count==0),"Native phase two clears scrutiny and flushes every hand");
                Console.WriteLine($"NATIVE_PLAYER_TURN players={n} fullTurn={turn} alternatingSides={enemySides} nativeStartAndSetup=true actualOpeningCardsPerPlayer=5 limited={limited} extraDraw={(limited?"blocked":"allowed")} nativeEndPhaseOne=true nativeEndPhaseTwo=true handsFlushed=true scrutinyRemoved=true; supplied already-armed phase input, native methods explicitly entered, no start-combat/automatic readiness-loop/network/save proof");
                if(enemySides&&turn<9)
                {
                    foreach(var p in run.Players)p.Creature.SetCurrentHpInternal(p.Creature.MaxHp);
                    await InvokeNative("SwitchSides");await InvokeNative("StartTurn");
                    NativeEventTests.Check(battle.Combat.CurrentSide==CombatSide.Player&&battle.Combat.RoundNumber==turn+1,"Native enemy execution/end switches side and advances native round");
                    Console.WriteLine($"NATIVE_ALTERNATING players={n} playerTurnCompleted={turn} nativeEnemyStartExecutionEnd=true nativeRound={battle.Combat.RoundNumber} currentSide=Player livingPrimary=true playerHpResetBeforeEnemy=true; prototype fungus encounter, native full start/end methods but readiness coordinator not run");
                }
            }
        }
    }
    private static Task InvokeNative(string name)
    {
        var methods=typeof(MegaCrit.Sts2.Core.Combat.CombatManager).GetMethods(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).Where(m=>m.Name==name).OrderByDescending(m=>m.GetParameters().Length);
        var method=methods.First();var args=method.GetParameters().Select(p=>p.ParameterType.Name=="CombatTurnState"?HarmonyLib.AccessTools.Field(typeof(MegaCrit.Sts2.Core.Combat.CombatManager),"_turnState").GetValue(MegaCrit.Sts2.Core.Combat.CombatManager.Instance):p.HasDefaultValue?p.DefaultValue:null).ToArray();
        return method.Invoke(MegaCrit.Sts2.Core.Combat.CombatManager.Instance,args) as Task??Task.CompletedTask;
    }
    private static async Task Stage()
    {
        ModelDb.Inject(typeof(PrototypeHuntingScrutiny)); ModelDb.Inject(typeof(PrototypeHuntingClock));
        for (int n=1; n<=4; n++)
        {
            var run = HeadlessTestSetup.Run("HUNTING70_STAGE", Enumerable.Repeat(ModelDb.Character<Ironclad>(), n).ToArray());
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<TestSubjectBoss>().ToMutable());
            var subject = (TestSubject)battle.Monster;
            var combat = battle.Combat;
            var ctx = new ThrowingPlayerChoiceContext();
            await subject.AfterAddedToRoom();
            var clock = await PowerCmd.Apply<PrototypeHuntingClock>(ctx, subject.Creature, 1, null, null);
            await clock!.BeforeSideTurnStart(ctx, CombatSide.Player, combat.PlayerCreatures, combat);
            NativeEventTests.Check(clock.FullPlayerTurns == 0, "No phase3 turn count before revival completes");
            subject.SetMoveImmediate((MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState)subject.MoveStateMachine.States["RESPAWN_MOVE"], true);
            await subject.NextMove.PerformMove(combat.PlayerCreatures);
            NativeEventTests.Check((int)HarmonyLib.AccessTools.Property(typeof(TestSubject), "Respawns").GetValue(subject)! == 1 && subject.Creature.HasPower<PainfulStabsPower>(), "Native second form really completed");
            await PowerCmd.Remove(subject.Creature.GetPower<PainfulStabsPower>());
            var shy = await PowerCmd.Apply<SkittishPower>(ctx, subject.Creature, 10, null, null);
            if (shy!.Amount != 10) await PowerCmd.ModifyAmount(ctx, shy, 10-shy.Amount, null, null);
            await PowerCmd.Apply<SuckPower>(ctx, subject.Creature, 3, null, null);
            NativeEventTests.Check(shy.Amount == 10 && subject.Creature.GetPowerAmount<SuckPower>() == 3, "Phase2 fixed powers at all player counts");
            var revivalPatch=new HarmonyLib.Harmony("allmatters.prototype70.revival-wait");
            revivalPatch.Patch(HarmonyLib.AccessTools.Method(typeof(CreatureCmd),nameof(CreatureCmd.SetMaxHp)),prefix:new HarmonyLib.HarmonyMethod(typeof(PrototypeHuntingStageTests),nameof(PauseMaxHp)));
            try
            {
                HoldRevive=true;ReachedRevive=false;ReleaseRevive=new TaskCompletionSource();
                var reviving=subject.NextMove.PerformMove(combat.PlayerCreatures);
                NativeEventTests.Check(ReachedRevive&&!reviving.IsCompleted&&(int)HarmonyLib.AccessTools.Property(typeof(TestSubject),"Respawns").GetValue(subject)! == 2,"Respawns reaches2 before actual asynchronous revival completion");
                NativeEventTests.Check(!clock.Armed&&clock.FullPlayerTurns==0&&subject.Creature.HasPower<SkittishPower>()&&subject.Creature.HasPower<SuckPower>(),"No premature phase clock or phase2 power clearing while revival awaits");
                ReleaseRevive.SetResult();await reviving;
                Console.WriteLine($"STAGE_REVIVAL players={n} nativeRespawnsAlready2=true actualRevivalPaused=true clockNotArmed=true phase2PowersRetainedUntilCompletion=true");
            }
            finally{HoldRevive=false;ReleaseRevive?.TrySetResult();revivalPatch.UnpatchAll(revivalPatch.Id);}
            NativeEventTests.Check((int)HarmonyLib.AccessTools.Property(typeof(TestSubject), "Respawns").GetValue(subject)! == 2 && subject.Creature.HasPower<NemesisPower>(), "Native third revival really completed");
            await PowerCmd.Remove(subject.Creature.GetPower<SkittishPower>());
            await PowerCmd.Remove(subject.Creature.GetPower<SuckPower>());
            clock.Armed = true;
            await clock.AfterSideTurnEnd(ctx, CombatSide.Player, combat.PlayerCreatures);
            NativeEventTests.Check(clock.FullPlayerTurns == 0, "Transition half turn end is not a full phase3 turn");
            for (int turn=1; turn<=9; turn++)
            {
                await clock.BeforeSideTurnStart(ctx, CombatSide.Player, combat.PlayerCreatures, combat);
                bool limited = turn%3 == 0;
                NativeEventTests.Check(subject.Creature.HasPower<PrototypeHuntingScrutiny>() == limited && clock.FullPlayerTurns==turn, "One shared full turn index");
                foreach (var p in run.Players)
                {
                    await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<DefendIronclad>(p),PileType.Draw,p);
                    await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<DefendIronclad>(p),PileType.Draw,p);
                    int hand=PileType.Hand.GetPile(p).Cards.Count;
                    await CardPileCmd.Draw(ctx,1,p,fromHandDraw:true);
                    NativeEventTests.Check(PileType.Hand.GetPile(p).Cards.Count==hand+1,"Normal opening draw is allowed");
                    await CardPileCmd.Draw(ctx,1,p,fromHandDraw:false);
                    NativeEventTests.Check(PileType.Hand.GetPile(p).Cards.Count==hand+(limited?1:2),"Only extra draw blocked on 3/6/9");
                    await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<DefendIronclad>(p),PileType.Hand,p);
                    NativeEventTests.Check(PileType.Hand.GetPile(p).Cards.Count==hand+(limited?2:3),"Non-draw entry is allowed");
                    await CardPileCmd.Add(PileType.Hand.GetPile(p).Cards.ToArray(),PileType.Discard);
                }
                await clock.AfterSideTurnEnd(ctx,CombatSide.Player,combat.PlayerCreatures);
                NativeEventTests.Check(!subject.Creature.HasPower<PrototypeHuntingScrutiny>(),"Scrutiny removed at this player-side end");
                Console.WriteLine($"STAGE players={n} fullTurn={turn} limited={limited} normalDraw=allowed extraDraw={(limited?"blocked":"allowed")} directAdd=allowed endRemoved=true; hooks explicitly driven");
            }
        }
    }
}
