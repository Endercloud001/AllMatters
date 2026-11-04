// THROWAWAY stage/draw experiment. Explicit hook driving, not a full turn loop or checkpoint.
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
    internal static readonly (string Name, Func<Task> Test)[] Cases = [("PrototypeHuntingStage native respawns and real draw filtering at full turns 3 6 9", Stage)];
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



