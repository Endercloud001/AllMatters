using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;

internal static class PrototypeHuntingModelTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases = [("PrototypeHuntingModels original fungus mystic torch real commands", Models),("PrototypeHuntingModels exact random boundaries and all six status branches", RandomInputs),("PrototypeHuntingModels final fungus death and native player cleanup",LastDeath)];
    private static bool _injected;
    internal static void Inject()
    {
        if (_injected) return;
        foreach (var type in new[] { typeof(PrototypeHuntingFungus), typeof(PrototypeHuntingSpores), typeof(PrototypeHuntingMystic), typeof(PrototypeHuntingTorch),typeof(PrototypeHuntingDeathShield) }) ModelDb.Inject(type);
        _injected = true;
    }

    private static async Task Step(MonsterModel monster)
    {
        await monster.NextMove.PerformMove(monster.CombatState.PlayerCreatures);
        monster.MoveStateMachine.OnMovePerformed(monster.NextMove);
        monster.RollMove(monster.CombatState.PlayerCreatures);
    }

    private static int SeedFor(int bound,int wanted)
    {
        for(int seed=0;seed<10000;seed++)if(new MegaCrit.Sts2.Core.Random.Rng((uint)seed).NextInt(bound)==wanted)return seed;
        throw new Exception("Missing controlled RNG input");
    }
    private static async Task RandomInputs()
    {
        Inject();
        for(int n=1;n<=4;n++)
        {
            var run=HeadlessTestSetup.Run("HUNTING70_RANDOM",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
            using var battle=new HeadlessTestSetup.EncounterCombatScope(run,ModelDb.Encounter<VantomBoss>().ToMutable());
            foreach(var p in run.Players){p.Creature.SetMaxHpInternal(10000);p.Creature.SetCurrentHpInternal(10000);}
            foreach(int roll in new[]{39,40})
            {
                var c=await CreatureCmd.Add<PrototypeHuntingFungus>(battle.Combat,"prototype_rng_fungus"+roll);
                var m=c.Monster!;m.Rng=new MegaCrit.Sts2.Core.Random.Rng((uint)SeedFor(100,roll));
                await Step(m);
                NativeEventTests.Check(m.NextMove.Id==(roll==39?"GROW":"BITE"),"Exact40/60 threshold is preserved");
                if(roll==40){ulong pos=HeadlessTestSetup.RngPosition(m.Rng);await Step(m);NativeEventTests.Check(m.NextMove.Id=="GROW"&&HeadlessTestSetup.RngPosition(m.Rng)==pos,"Forced Growth after two bites consumes no new branch draw");}
                Console.WriteLine($"RANDOM players={n} fungusRoll={roll} exactBoundary=true forcedTwoBites=true");
            }
            Type[] types=[typeof(MegaCrit.Sts2.Core.Models.Cards.Dazed),typeof(MegaCrit.Sts2.Core.Models.Cards.Burn),typeof(MegaCrit.Sts2.Core.Models.Cards.Void),typeof(MegaCrit.Sts2.Core.Models.Cards.Toxic),typeof(MegaCrit.Sts2.Core.Models.Cards.Beckon),typeof(MegaCrit.Sts2.Core.Models.Cards.Slimed)];
            foreach(int wanted in Enumerable.Range(0,6))
            {
                var c=await CreatureCmd.Add<PrototypeHuntingMystic>(battle.Combat,"prototype_rng_mystic"+wanted);
                var m=(PrototypeHuntingMystic)c.Monster!;
                int seed=SeedFor(6,wanted);var expected=new MegaCrit.Sts2.Core.Random.Rng((uint)seed);
                m.Rng=new MegaCrit.Sts2.Core.Random.Rng((uint)seed);
                int[] offsets=run.Players.Select(p=>PileType.Discard.GetPile(p).Cards.Count).ToArray();
                await Step(m);
                for(int i=0;i<n;i++)
                {
                    var added=PileType.Discard.GetPile(run.Players[i]).Cards[offsets[i]];int roll=expected.NextInt(6);
                    NativeEventTests.Check(added.GetType()==types[roll]&&added.Owner==run.Players[i]&&added.CombatState==battle.Combat,"Each player consumes independent real RNG draw and owns correct combat status");
                }
                Console.WriteLine($"RANDOM players={n} mysticFirstRoll={wanted} allPlayerSequenceMatches=true seed={seed}");
            }
            var mixed=await CreatureCmd.Add<PrototypeHuntingMystic>(battle.Combat,"prototype_mixed_targets");
            var mystic=(PrototypeHuntingMystic)mixed.Monster!;
            var ally=battle.Monster.Creature;
            foreach(int wanted in new[]{0,1})
            {
                mixed.SetCurrentHpInternal(mixed.MaxHp-20);ally.SetCurrentHpInternal(ally.MaxHp-20);
                var injured=battle.Combat.Enemies.Where(c=>c.IsAlive&&c.CurrentHp<c.MaxHp).ToArray();
                NativeEventTests.Check(injured.Length==2,"Controlled mixed injured set contains self and ally");
                mystic.Rng=new MegaCrit.Sts2.Core.Random.Rng((uint)SeedFor(2,wanted));
                mystic.SetMoveImmediate((MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState)mystic.MoveStateMachine.States["HEAL"],true);
                await mystic.NextMove.PerformMove(battle.Combat.PlayerCreatures);
                NativeEventTests.Check(mystic.HealTargets.Last()==injured[wanted].CombatId&&injured[wanted].CurrentHp==injured[wanted].MaxHp-8&&injured[1-wanted].CurrentHp==injured[1-wanted].MaxHp-20,"Mixed injured target selection changes only chosen self/ally by12");
            }
            var living=battle.Combat.Enemies.Where(c=>c.IsAlive).ToArray();
            foreach(var target in new[]{mixed,ally})
            {
                int index=Array.IndexOf(living,target);int[] strengths=living.Select(c=>c.GetPowerAmount<StrengthPower>()).ToArray();
                mystic.Rng=new MegaCrit.Sts2.Core.Random.Rng((uint)SeedFor(living.Length,index));
                mystic.SetMoveImmediate((MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState)mystic.MoveStateMachine.States["BUFF"],true);
                await mystic.NextMove.PerformMove(battle.Combat.PlayerCreatures);
                NativeEventTests.Check(mystic.BuffTargets.Last()==target.CombatId&&living.Select((c,i)=>c.GetPowerAmount<StrengthPower>()==strengths[i]+(i==index?2:0)).All(x=>x),"Uniform living-target RNG can select self/ally and applies fixed2 only there");
            }
            if(n>1)
            {
                var dead=run.Players[^1];dead.Creature.SetCurrentHpInternal(0);
                int[] sizes=run.Players.Select(p=>PileType.Discard.GetPile(p).Cards.Count).ToArray();
                mystic.SetMoveImmediate((MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState)mystic.MoveStateMachine.States["CAST"],true);
                await mystic.NextMove.PerformMove(battle.Combat.PlayerCreatures);
                NativeEventTests.Check(run.Players.Select((p,i)=>PileType.Discard.GetPile(p).Cards.Count==sizes[i]+(p==dead?0:1)).All(x=>x),"Dead player gets no status or RNG-per-player action");
            }
            Console.WriteLine($"RANDOM_TARGETS players={n} mixedInjuredSelf/ally=true buffSelf/ally=true fixedHeal12/fixedStrength2=true deadPlayerSkipped={n>1}");
        }
    }

    private static async Task Models()
    {
        Inject();
        for (int n = 1; n <= 4; n++)
        {
            var run = HeadlessTestSetup.Run("HUNTING70_MODELS", Enumerable.Repeat(ModelDb.Character<Ironclad>(), n).ToArray());
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<VantomBoss>().ToMutable());
            var combat = battle.Combat;
            foreach (var p in run.Players) { p.Creature.SetMaxHpInternal(10000); p.Creature.SetCurrentHpInternal(10000); }
            var fungusCreature = await CreatureCmd.Add<PrototypeHuntingFungus>(combat, "prototype_fungus");
            var fungus = (PrototypeHuntingFungus)fungusCreature.Monster!;
            NativeEventTests.Check(fungus.NextMove.Id == "BITE" && fungusCreature.GetPowerAmount<PrototypeHuntingSpores>() == 2 && fungusCreature.IsPrimaryEnemy, "Fungus starts Bite with two spores, primary");
            NativeEventTests.Check(fungusCreature.MaxHp == (int)MegaCrit.Sts2.Core.Entities.Creatures.Creature.ScaleHpForMultiplayer(28,null,n,run.CurrentActIndex), "Fungus HP scales once");
            int consecutive = 0, grows = 0, bites = 0;
            bool afterGrow = false;
            for (int turn = 0; turn < 30; turn++)
            {
                string action = fungus.NextMove.Id;
                if (afterGrow) NativeEventTests.Check(action == "BITE", "Growth always followed by Bite");
                if (consecutive == 2) NativeEventTests.Check(action == "GROW", "Two bites force Growth");
                if (action == "BITE") { consecutive++; bites++; } else { consecutive = 0; grows++; }
                afterGrow = action == "GROW";
                await Step(fungus);
            }
            NativeEventTests.Check(grows > 0 && bites > 0 && fungusCreature.GetPowerAmount<StrengthPower>() == grows * 3, "Growth is fixed +3 independent of players");
            await PowerCmd.Apply<PrototypeHuntingDeathShield>(new ThrowingPlayerChoiceContext(),fungusCreature,1,null,null);
            await CreatureCmd.Kill(fungusCreature);
            NativeEventTests.Check(fungusCreature.IsAlive&&fungusCreature.CurrentHp==1&&run.Players.All(p => p.Creature.GetPowerAmount<VulnerablePower>() == 0), "Actually prevented death does not apply spores");
            await CreatureCmd.Kill(fungusCreature);
            NativeEventTests.Check(run.Players.All(p => p.Creature.GetPowerAmount<VulnerablePower>() == 2), "Actual fungus death applies spores to each living player");
            Console.WriteLine($"MODEL fungus players={n} hp={fungusCreature.MaxHp} turns=30 bites={bites} grows={grows} growth=3 spores=2 actualDeath=true; 40/60 authored threshold not statistical proof");
            foreach (var p in run.Players) await PowerCmd.Remove(p.Creature.GetPower<VulnerablePower>());

            var mysticCreature = await CreatureCmd.Add<PrototypeHuntingMystic>(combat, "prototype_mystic");
            var mystic = (PrototypeHuntingMystic)mysticCreature.Monster!;
            NativeEventTests.Check(mysticCreature.Powers.Count() == 0 && mystic.NextMove.Id == "CAST", "Mystic has no starting powers and starts Cast");
            int[] before = run.Players.Select(p => PileType.Discard.GetPile(p).Cards.Count).ToArray();
            await Step(mystic);
            for (int p = 0; p < n; p++) NativeEventTests.Check(PileType.Discard.GetPile(run.Players[p]).Cards.Count == before[p] + 1, "Independent status inserted once per living player");
            await Step(mystic);
            NativeEventTests.Check(mystic.HealTargets.Last() == null && mystic.NextMove.Id == "BUFF", "Empty injured set passes and advances");
            await Step(mystic);
            NativeEventTests.Check(mystic.NextMove.Id == "CAST", "Three-state loop returns to Cast");
            mysticCreature.SetCurrentHpInternal(mysticCreature.MaxHp - 20);
            await Step(mystic); await Step(mystic);
            NativeEventTests.Check(mystic.HealTargets.Last() == mysticCreature.CombatId && mysticCreature.CurrentHp == mysticCreature.MaxHp - 8, "Only injured Mystic can heal itself exactly12");
            await Step(mystic);
            int otherHp = battle.Monster.Creature.MaxHp;
            mysticCreature.SetCurrentHpInternal(mysticCreature.MaxHp);
            battle.Monster.Creature.SetCurrentHpInternal(otherHp - 20);
            await Step(mystic); await Step(mystic);
            NativeEventTests.Check(mystic.HealTargets.Last() == battle.Monster.Creature.CombatId && battle.Monster.Creature.CurrentHp == otherHp - 8, "Only injured ally gets exactly12");
            Console.WriteLine($"MODEL mystic players={n} hp={mysticCreature.MaxHp} rolls={string.Join(',',mystic.StatusRolls)} emptyHeal/self/ally=true; no resource/UI claim");

            var torch = await CreatureCmd.Add<PrototypeHuntingTorch>(combat, "prototype_torch");
            await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(), torch, 1, null, null);
            for (int turn=0; turn<2; turn++)
            {
                NativeEventTests.Check(torch.Monster!.NextMove.Id == "HIT", "Torch no spawn empty action");
                int hp = run.Players.Sum(p => p.Creature.CurrentHp);
                await Step(torch.Monster);
                NativeEventTests.Check(hp-run.Players.Sum(p => p.Creature.CurrentHp) == 10*n, "Torch fixed10 per native target player");
            }
            Console.WriteLine($"MODEL torch players={n} hp={torch.MaxHp} minion={torch.IsSecondaryEnemy} twoAttacks=10 each; queen snapshot NOT tested");
        }
    }

    private static async Task LastDeath()
    {
        Inject();
        for(int n=1;n<=4;n++)
        {
            var run=HeadlessTestSetup.Run("HUNTING70_LAST_SPORE",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
            using var battle=new HeadlessTestSetup.EncounterCombatScope(run,ModelDb.Encounter<VantomBoss>().ToMutable());
            var fungus=await CreatureCmd.Add<PrototypeHuntingFungus>(battle.Combat,"prototype_last_fungus");
            await CreatureCmd.Kill(battle.Monster.Creature);
            NativeEventTests.Check(battle.Combat.Enemies.Count(c=>c.IsAlive)==1,"Fungus really is final living enemy");
            await CreatureCmd.Kill(fungus);
            NativeEventTests.Check(run.Players.All(p=>p.Creature.GetPowerAmount<VulnerablePower>()==2),"Last enemy actual death still settles spores");
            foreach(var p in run.Players)p.AfterCombatEnd();
            NativeEventTests.Check(run.Players.All(p=>!p.Creature.HasPower<VulnerablePower>()),"Native player cleanup clears final death status");
            Console.WriteLine($"LAST_SPORE players={n} actualLastEnemy=true actualDeathSpore=2 nativePlayerCleanup=cleared; cleanup invoked explicitly, full EndCombat/rewards/save not exercised");
        }
    }
}
