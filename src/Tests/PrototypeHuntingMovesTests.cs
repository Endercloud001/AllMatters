// THROWAWAY local move adapters; real native piles, powers, damage and intents.
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class PrototypeHuntingMovesTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingMoves Insatiable sandpit5 four per pile and TestSubject growing attacks",Moves)];
    private static int Ascension;
    private static void Claw(ref int __result)=>__result=Ascension>=9?10:9;
    private static async Task Moves()
    {
        var patch=new Harmony("allmatters.prototype70.moves");
        patch.Patch(AccessTools.PropertyGetter(typeof(TestSubject),"MultiClawDamage"),postfix:new HarmonyMethod(typeof(PrototypeHuntingMovesTests),nameof(Claw)));
        try
        {
            foreach(int asc in new[]{0,9})for(int n=1;n<=4;n++)
            {
                Ascension=asc;
                var players=Enumerable.Range(1,n).Select(i=>Player.CreateForNewRun(ModelDb.Character<Ironclad>(),UnlockState.all,(ulong)i)).ToArray();
                var run=RunState.CreateForTest(players,acts:[],ascensionLevel:asc,seed:"HUNTING70_MOVES");
                using var battle=new HeadlessTestSetup.EncounterCombatScope(run,ModelDb.Encounter<VantomBoss>().ToMutable());
                var combat=battle.Combat;var ctx=new ThrowingPlayerChoiceContext();
                foreach(var p in players){p.Creature.SetMaxHpInternal(10000);p.Creature.SetCurrentHpInternal(10000);}
                var worm=await CreatureCmd.Add<TheInsatiable>(combat,"prototype_insatiable");
                NativeEventTests.Check(worm.Monster!.NextMove.Id=="LIQUIFY_GROUND_MOVE","Original first action remains Liquify");
                var modified=new MoveState("PROTOTYPE_LIQUIFY",async targets=>
                {
                    foreach(var target in targets)
                    {
                        var sand=(SandpitPower)ModelDb.Power<SandpitPower>().ToMutable();sand.Target=target;
                        await PowerCmd.Apply(ctx,sand,worm,5,worm,null);
                        NativeEventTests.Check(sand.Amount==5,"Sandpit fixed5 per target");
                        var p=target.Player??target.PetOwner;
                        for(int i=0;i<4;i++)await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<FranticEscape>(p),PileType.Draw,null,CardPilePosition.Random);
                        for(int i=0;i<4;i++)await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<FranticEscape>(p),PileType.Discard,null,CardPilePosition.Random);
                    }
                    AccessTools.Property(typeof(TheInsatiable),"HasLiquified").SetValue(worm.Monster,true);
                },new StatusIntent(8));
                worm.Monster.SetMoveImmediate(modified,true);await worm.Monster.NextMove.PerformMove(combat.PlayerCreatures);
                NativeEventTests.Check(worm.Powers.OfType<SandpitPower>().Count()==n&&players.All(p=>PileType.Draw.GetPile(p).Cards.Count(c=>c is FranticEscape)==4&&PileType.Discard.GetPile(p).Cards.Count(c=>c is FranticEscape)==4),"Every original player target gets4+4 and own sandpit");
                Console.WriteLine($"MOVES Insatiable players={n} asc={asc} sandpit=5 draw=4 discard=4 first=Liquify localStatusIntent=8; animation/complete original followup NOT tested");

                var creature=await CreatureCmd.Add<TestSubject>(combat,"prototype_subject");var subject=(TestSubject)creature.Monster!;
                subject.SetMoveImmediate((MoveState)subject.MoveStateMachine.States["RESPAWN_MOVE"],true);
                await subject.NextMove.PerformMove(combat.PlayerCreatures);
                await PowerCmd.Remove(creature.GetPower<PainfulStabsPower>());
                var claw=(MoveState)subject.MoveStateMachine.States["MULTI_CLAW_MOVE"];
                for(int use=0;use<3;use++)
                {
                    int hp=players.Sum(p=>p.Creature.CurrentHp);
                    int intentDamage=((MultiAttackIntent)claw.Intents.Single()).GetTotalDamage(combat.PlayerCreatures,creature);
                    await claw.PerformMove(combat.PlayerCreatures);
                    int expected=(3+use)*(asc==0?9:10);
                    NativeEventTests.Check(hp-players.Sum(p=>p.Creature.CurrentHp)==expected*n&&intentDamage==expected,"Native 3+N uses and exact9/10 match intent and real damage");
                    Console.WriteLine($"MOVES TestSubject players={n} asc={asc} use={use} hits={3+use} perHit={(asc==0?9:10)} intent={intentDamage} damagePerPlayer={expected}");
                }
            }
        }
        finally{patch.UnpatchAll(patch.Id);}
    }
}
