// THROWAWAY: real dependency experiments for issue 70; no production integration.
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

internal static class PrototypeHuntingTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("PrototypeHunting native HP and fixed Artifact boundary 1-4 players", Scaling),
     ("PrototypeHunting matriarch full HP halving and dynamic debuff classification", NativeBoundaries),
     ("PrototypeHunting packed encounter marker audit", SceneAudit),
     ("PrototypeHunting native Wriggler four slots and fifth negative control", Worms)];

    private static async Task Worms()
    {
        var run = HeadlessTestSetup.Run("HUNTING70_WORMS", ModelDb.Character<Ironclad>());
        var combat = HeadlessTestSetup.Combat(run);
        for (int i = 1; i <= 5; i++)
        {
            var worm = (Wriggler)ModelDb.Monster<Wriggler>().ToMutable();
            worm.StartStunned = true;
            var creature = combat.CreateCreature(worm, CombatSide.Enemy, "wriggler" + i);
            combat.AddCreature(creature);
            worm.SetUpForCombat();
            worm.RollMove(combat.PlayerCreatures);
            NativeEventTests.Check(worm.NextMove.Id == "SPAWNED_MOVE", "Native spawn retains initial empty action");
            await worm.NextMove.PerformMove(combat.PlayerCreatures);
            worm.MoveStateMachine.OnMovePerformed(worm.NextMove);
            if (i < 5)
            {
                worm.RollMove(combat.PlayerCreatures);
                string expected = i % 2 == 1 ? "NASTY_BITE_MOVE" : "WRIGGLE_MOVE";
                NativeEventTests.Check(worm.NextMove.Id == expected, "Existing slot parity follows native state machine");
                Console.WriteLine($"EVIDENCE Wriggler slot={i} spawn performed then intent={worm.NextMove.Id}");
            }
            else
            {
                Exception? failure = null;
                try { worm.RollMove(combat.PlayerCreatures); } catch (Exception ex) { failure = ex; }
                NativeEventTests.Check(failure != null, "Native fifth slot should expose missing branch, not silently succeed");
                Console.WriteLine($"EVIDENCE Wriggler slot=5 negative control failure={failure!.GetType().Name}: {failure.Message}; adapted fifth action NOT implemented or passed");
                var branch = (MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.ConditionalBranchState)worm.MoveStateMachine.States["INIT_MOVE"];
                branch.AddState(worm.MoveStateMachine.States["NASTY_BITE_MOVE"], () => worm.Creature.SlotName == "wriggler5");
                worm.RollMove(combat.PlayerCreatures);
                NativeEventTests.Check(worm.NextMove.Id == "NASTY_BITE_MOVE", "Local fifth-slot branch resolves to bite after actual spawn empty action");
                Console.WriteLine("EVIDENCE Wriggler fifth local state adaptation intent=NASTY_BITE_MOVE; attack damage and actual fifth scene position NOT exercised");
            }
        }
    }

    private static Task SceneAudit()
    {
        var factory = typeof(ModelDb).GetMethods().Single(m => m.Name == "Encounter" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
        int scenes = 0;
        foreach (var type in typeof(MonsterModel).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(EncounterModel).IsAssignableFrom(t)))
        {
            var encounter = (EncounterModel)factory.MakeGenericMethod(type).Invoke(null, null)!;
            if (!encounter.HasScene) { Console.WriteLine($"SCENE {type.Name} auto-position slots={string.Join(',', encounter.Slots)}; actual widths NOT measured"); continue; }
            string path = (string)typeof(EncounterModel).GetProperty("ScenePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(encounter)!;
            var scene = Godot.ResourceLoader.Load<Godot.PackedScene>(path);
            NativeEventTests.Check(scene != null, $"Encounter scene loads: {path}");
            using var state = scene!.GetState();
            var markers = new List<string>();
            for (int i = 0; i < state.GetNodeCount(); i++)
            {
                if (state.GetNodeType(i).ToString() != "Marker2D") continue;
                string position = "default";
                for (int j = 0; j < state.GetNodePropertyCount(i); j++)
                    if (state.GetNodePropertyName(i,j).ToString() == "position") position = state.GetNodePropertyValue(i,j).ToString();
                markers.Add($"{state.GetNodePath(i)}:{state.GetNodeName(i)}@{position}");
            }
            scenes++;
            Console.WriteLine($"SCENE {type.Name} path={path} declared={string.Join(',', encounter.Slots)} markers={string.Join(';', markers)}; no added markers and no visual capacity claim");
        }
        NativeEventTests.Check(scenes > 0, "Audit must load actual dedicated encounter scenes");
        return Task.CompletedTask;
    }

    private static async Task NativeBoundaries()
    {
        for (int n = 1; n <= 4; n++)
        {
            var run = HeadlessTestSetup.Run("HUNTING70_NATIVE", Enumerable.Repeat(ModelDb.Character<Ironclad>(), n).ToArray());
            var combat = HeadlessTestSetup.Combat(run);
            var context = new ThrowingPlayerChoiceContext();
            var matriarch = (LagavulinMatriarch)ModelDb.Monster<LagavulinMatriarch>().ToMutable();
            var enemy = combat.CreateCreature(matriarch, CombatSide.Enemy, "matriarch");
            combat.AddCreature(enemy);
            await matriarch.AfterAddedToRoom();
            int original = enemy.MaxHp;
            await CreatureCmd.SetMaxAndCurrentHp(enemy, original / 2);
            NativeEventTests.Check(enemy.CurrentHp == original / 2 && enemy.MaxHp == original / 2 && !matriarch.IsAwake && !matriarch.IsShellAwake, "Halving starts full and preserves sleep");
            NativeEventTests.Check(enemy.GetPower<AsleepPower>() != null, "Original sleep power remains");
            Console.WriteLine($"EVIDENCE matriarch players={n} scaled={original} max={enemy.MaxHp} current={enemy.CurrentHp} awake={matriarch.IsAwake} shell={matriarch.IsShellAwake}");
            var strength = await PowerCmd.Apply<StrengthPower>(context, enemy, -3, null, null);
            var dexterity = await PowerCmd.Apply<DexterityPower>(context, enemy, -2, null, null);
            NativeEventTests.Check(strength!.TypeForCurrentAmount == MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff && dexterity!.TypeForCurrentAmount == MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff, "Negative Strength/Dexterity use dynamic classification");
            var toRemove = enemy.Powers.Where(p => p.IsVisible && p.TypeForCurrentAmount == MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff).ToArray();
            foreach (var power in toRemove) await PowerCmd.Remove(power);
            NativeEventTests.Check(enemy.GetPower<StrengthPower>() == null && enemy.GetPower<DexterityPower>() == null && enemy.GetPower<PlatingPower>() != null, "Visible debuffs removed; positive Plating retained");
            var noDraw = await PowerCmd.Apply<NoDrawPower>(context, enemy, 1, null, null);
            foreach (var player in run.Players)
            {
                NativeEventTests.Check(noDraw!.ShouldDraw(player, true) && noDraw.ShouldDraw(player, false), "Native NoDraw on monster does NOT implement global Scrutiny");
            }
            Console.WriteLine($"EVIDENCE dynamic cleanse players={n} removed={toRemove.Length}; native monster NoDraw allows all player draws; Waterfall hidden state and full draw pipeline NOT exercised");
        }
    }

    private static async Task Scaling()
    {
        for (int n = 1; n <= 4; n++)
        {
            var run = HeadlessTestSetup.Run("HUNTING70_HP", Enumerable.Repeat(ModelDb.Character<Ironclad>(), n).ToArray());
            var combat = HeadlessTestSetup.Combat(run);
            var context = new ThrowingPlayerChoiceContext();
            var enemy = combat.CreateCreature(ModelDb.Monster<Wriggler>().ToMutable(), CombatSide.Enemy, "probe");
            combat.AddCreature(enemy);
            int firstHp = enemy.MaxHp;
            NativeEventTests.Check(firstHp == enemy.CurrentHp, "Native creation starts at scaled full HP");
            var rawRun = HeadlessTestSetup.Run("HUNTING70_HP", ModelDb.Character<Ironclad>());
            var raw = HeadlessTestSetup.Combat(rawRun).CreateCreature(ModelDb.Monster<Wriggler>().ToMutable(), CombatSide.Enemy, "probe");
            int expected = (int)MegaCrit.Sts2.Core.Entities.Creatures.Creature.ScaleHpForMultiplayer(raw.MaxHp, null, n, run.CurrentActIndex);
            NativeEventTests.Check(firstHp == expected, $"CreateCreature scales once: {firstHp} vs {expected}");
            enemy.ScaleMonsterHpForMultiplayer(null, n, run.CurrentActIndex);
            Console.WriteLine($"EVIDENCE HP players={n} raw={raw.MaxHp} once={firstHp} twice={enemy.MaxHp}; second call is NOT safe initialization");
            for (int fixedAmount = 1; fixedAmount <= 5; fixedAmount += 2)
            {
                var target = combat.CreateCreature(ModelDb.Monster<Wriggler>().ToMutable(), CombatSide.Enemy, "artifact");
                combat.AddCreature(target);
                var power = await PowerCmd.Apply<ArtifactPower>(context, target, fixedAmount, null, null);
                NativeEventTests.Check(power!.Amount == fixedAmount + n - 1, "Native Artifact grows with players");
                int grown = power.Amount;
                await PowerCmd.ModifyAmount(context, power, fixedAmount - grown, null, null);
                NativeEventTests.Check(power.Amount == fixedAmount, "Local post-application adjustment reaches requested amount");
                Console.WriteLine($"EVIDENCE Artifact players={n} input={fixedAmount} native={grown} adjusted={power.Amount}; adjustment is experimental, not architecture decision");
            }
            NativeEventTests.Check(enemy.IsPrimaryEnemy, "Ordinary enemy remains primary");
            await PowerCmd.Apply<MinionPower>(context, enemy, 1, null, null);
            NativeEventTests.Check(enemy.IsSecondaryEnemy && !enemy.IsPrimaryEnemy, "Minion changes enemy classification");
            NativeEventTests.Check(!enemy.GetPower<MinionPower>()!.ShouldOwnerDeathTriggerFatal(), "Minion disallows Fatal hook");
            Console.WriteLine($"EVIDENCE Minion players={n} primary={enemy.IsPrimaryEnemy} secondary={enemy.IsSecondaryEnemy}; actual victory and kill relics NOT exercised");
        }
    }
}
