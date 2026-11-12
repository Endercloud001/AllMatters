using AllMatters.Jigsaw;
using AllMatters.Monsters;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.TestSupport;

internal static class JigsawHuntingCombatTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("JigsawHuntingCombat actual forest entry appends one normal fungus", () => ForestEntry()),
        ("JigsawHuntingCombat active native save rebuilds complete before battle point", () => ForestEntry(restoreBattle: true)),
        ("JigsawHuntingCombat actual victory saves post battle reward point without reopening", () => ForestEntry(victory: true)),
        ("JigsawHuntingCombat last fungus spores settle with actual ending for one to four players", LastSpores),
        ("JigsawHuntingCombat prevented fungus death and Artifact preserve native settlement", PreventedSpores),
        ("JigsawHuntingCombat fungus native moves preserve exact random and forced branches", FungusMoves),
        .. JigsawHuntingActOneTests.Cases,
        .. JigsawHuntingEventCombatTests.Cases,
    ];

    private static async Task ForestEntry(bool restoreBattle = false, bool victory = false)
    {
        using var selector = CardSelectCmd.UseSelector(new TakeCardReward());
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        using var eventScope = HeadlessCombatInProgress.EnterEvent();
        var native = RunState.CreateForTest(
            [Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)],
            acts: [ModelDb.Act<Overgrowth>()], seed: "HUNTING84_FOREST_ENTRY");
        var manager = RunManager.Instance;
        manager.SetUpTest(native, new NetSingleplayerGameService());
        manager.CombatReplayWriter.IsEnabled = false;
        LocalContext.NetId = 1;
        var io = new MockGodotFileIo("hunting84-forest-memory-only");
        var saves = new RunSaveManager(84, io, new MigrationManager(io), forceSynchronous: true);
        JigsawNativeSaveHost.Save = room => saves.SaveRun(manager.ToSave(room), false);
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        var originalSize = viewport.Size;
        viewport.Size = new Vector2I(1920, 1080);
        var profileHost = new Harmony("AllMatters.Tests.Hunting84Profile");
        profileHost.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)),
            prefix: new HarmonyMethod(typeof(JigsawHuntingCombatTests), nameof(SkipProfile)));
        profileHost.Patch(AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay)),
            prefix: new HarmonyMethod(typeof(JigsawHuntingCombatTests), nameof(SkipProfile)));
        profileHost.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.UpdateProgressAfterCombatWon)),
            prefix: new HarmonyMethod(typeof(JigsawHuntingCombatTests), nameof(SkipProfile)));
        try
        {
            manager.GenerateRooms();
            await manager.GenerateMap();
            await manager.EnterRoom(new MapRoom());
            var map = JigsawRun.Create(75, players: [1UL]);
            Ok(map.ChooseEnd(1, EndChoice.Automatic));
            Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
            Ok(map.CompleteAncient(1));
            Ok(map.Continue(1));
            var previous = new Dictionary<int, int> { [0] = -1 };
            var pending = new Queue<int>();
            pending.Enqueue(0);
            while (pending.TryDequeue(out int current))
                foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
                {
                    int next = edge.First == current ? edge.Second : edge.First;
                    if (map.Nodes.Single(node => node.Id == next).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss) &&
                        previous.TryAdd(next, current)) pending.Enqueue(next);
                }
            int target = map.Nodes.First(node => node.Room == JigsawRoom.Combat && previous.ContainsKey(node.Id)).Id;
            var path = new List<int>();
            for (int node = target; node != 0; node = previous[node]) path.Add(node);
            path.Reverse();
            foreach (int node in path.SkipLast(1))
            {
                var entry = map.BeginEnter(node)!;
                Ok(map.ConfirmEnter(entry.Id));
                Ok(map.CompleteRoom(entry.Id));
            }
            int stepsBefore = map.Steps;
            Ok(map.SetRemainingSteps(0));
            JigsawCheckpoint.Capture(native, map);
            JigsawNativeSession.Restore(native);
            var session = JigsawNativeSession.Current!;
            await session.Enter(target);
            Check(native.CurrentRoom is CombatRoom && session.Map!.Steps == stepsBefore + 1 && session.Map.CurrentRoomHunted,
                "The production native room readiness confirms one hunted logical entry.");
            var room = (CombatRoom)native.CurrentRoom!;
            var fungus = room.CombatState.Enemies.Where(c => c.Monster?.GetType().Name == "FungusBeast").ToArray();
            Check(fungus.Length == 1, "The actual forest battle must append exactly one fungus at opening.");
            Check(fungus[0].MaxHp == 28 && fungus[0].CurrentHp == 28 &&
                !fungus[0].HasPower<MegaCrit.Sts2.Core.Models.Powers.MinionPower>(),
                "The appended fungus has its single-player base HP and remains a normal required enemy.");
            int enemiesBefore = room.Encounter.MonstersWithSlots.Count;
            bool refusedDuplicate = false;
            try { room.Encounter.GenerateMonstersWithSlots(native); }
            catch (InvalidOperationException) { refusedDuplicate = true; }
            Check(refusedDuplicate && room.Encounter.MonstersWithSlots.Count == enemiesBefore,
                "Native repeated generation rejects the call before another fungus can be appended.");
            if (victory)
            {
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), native.Players[0].Creature,
                    7, ValueProp.Unpowered, fungus[0]);
                await PlayerCmd.GainGold(19, native.Players[0]);
                native.Rng.Niche.NextInt(100);
                var endCombat = AccessTools.Method(typeof(CombatManager), "EndCombatInternal", Type.EmptyTypes);
                await (Task)endCombat.Invoke(CombatManager.Instance, null)!;
                Check(room.IsPreFinished, "The actual native EndCombat flow marks the current room prefinished.");
                var saved = saves.LoadRunSave();
                Check(saved.Success && saved.SaveData?.PreFinishedRoom is { IsPreFinished: true },
                    "The real awaited victory flow saves its native reward checkpoint instead of the before-roll point.");
                string postBattlePlayer = JsonSerializer.Serialize(native.Players[0].ToSerializable());
                string postBattleRng = JsonSerializer.Serialize(native.Rng.ToSerializable());
                CombatManager.Instance.Reset(true); manager.CleanUp(false);
                var restored = RunState.FromSerializable(saved.SaveData!);
                Check(JsonSerializer.Serialize(restored.Players[0].ToSerializable()) == postBattlePlayer &&
                    JsonSerializer.Serialize(restored.Rng.ToSerializable()) == postBattleRng,
                    "The saved victory point retains actual post-battle player effects and RNG together.");
                await manager.SetUpSavedSingleplayer(restored, saved.SaveData!);
                LocalContext.NetId = restored.Players[0].NetId;
                await manager.GenerateMap();
                await manager.LoadIntoLatestMapCoord(CombatRoom.FromSerializable(saved.SaveData!.PreFinishedRoom!, restored));
                Check(restored.CurrentRoom is CombatRoom { IsPreFinished: true } && JigsawNativeSession.Current!.Map is { } resumedMap &&
                    resumedMap.Steps == stepsBefore + 1 && resumedMap.CurrentNode == target,
                    "The real native reward-room reload does not start another combat or charge a second entry.");
                return;
            }
            if (restoreBattle)
            {
                string originalPlayer = JsonSerializer.Serialize(native.Players[0].ToSerializable());
                string originalEnemies = Opening(room);
                string originalRng = JsonSerializer.Serialize(native.Rng.ToSerializable());
                long originalEntry = session.Map.CreateSave().NextEntry;
                int originalRemaining = session.Map.RemainingSteps;
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), native.Players[0].Creature,
                    7, ValueProp.Unpowered, fungus[0]);
                await PlayerCmd.GainGold(19, native.Players[0]);
                native.Rng.Niche.NextInt(100);
                fungus[0].Monster!.Rng.NextInt(100);
                await JigsawNativeSaveHost.Save(null);
                var loaded = saves.LoadRunSave();
                Check(loaded.Success && loaded.SaveData != null, "A real active battle save parses through the native memory IO.");
                CombatManager.Instance.Reset(true);
                manager.CleanUp(false);
                var restored = RunState.FromSerializable(loaded.SaveData!);
                await manager.SetUpSavedSingleplayer(restored, loaded.SaveData!);
                LocalContext.NetId = restored.Players[0].NetId;
                await manager.GenerateMap();
                await manager.LoadIntoLatestMapCoord(null);
                var resumed = JigsawNativeSession.Current!;
                Console.WriteLine($"COMBAT_CHECKPOINT room={restored.CurrentRoom?.GetType().Name}; expected={originalEnemies}; actual={(restored.CurrentRoom is CombatRoom actualRoom ? Opening(actualRoom) : "no-combat")}");
                Check(restored.CurrentRoom is CombatRoom restoredRoom && Opening(restoredRoom) == originalEnemies,
                    "Native reload rebuilds the exact same enemies, initial HP, powers, intents and monster RNG once.");
                Check(JsonSerializer.Serialize(restored.Players[0].ToSerializable()) == originalPlayer &&
                    JsonSerializer.Serialize(restored.Rng.ToSerializable()) == originalRng,
                    "Native player effects and run RNG roll back together to the original battle opening.");
                Check(resumed.Map is { CurrentRoomHunted: true } restoredMap && restoredMap.CurrentNode == target &&
                    restoredMap.Steps == stepsBefore + 1 && restoredMap.RemainingSteps == originalRemaining &&
                    restoredMap.CreateSave().NextEntry == originalEntry,
                    "The complete native restore preserves the same hunted root and charges the logical entry exactly once.");
                await resumed.Enter(target);
                Check(resumed.Map!.Steps == stepsBefore + 1, "A duplicate restored destination cannot charge again.");
            }
        }
        finally
        {
            profileHost.UnpatchAll(profileHost.Id);
            JigsawNativeSaveHost.Save = null;
            CombatManager.Instance.Reset(true);
            manager.CleanUp(false);
            viewport.Size = originalSize;
        }
    }

    private static bool SkipProfile() => false;

    internal sealed class TakeCardReward : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect) =>
            Task.FromResult(options.Take(minSelect));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) => new() { card = options[0].Card };
    }

    internal static string Opening(CombatRoom room) => JsonSerializer.Serialize(room.CombatState.Enemies.Select(creature => new
    {
        Monster = creature.Monster!.Id.Entry,
        creature.MaxHp,
        creature.CurrentHp,
        Powers = creature.Powers.Select(power => new { power.Id.Entry, power.Amount }).ToArray(),
        Move = creature.Monster.NextMove.Id,
        Rng = HeadlessTestSetup.RngPosition(creature.Monster.Rng)
    }).ToArray());

    private static async Task LastSpores()
    {
        for (int count = 1; count <= 4; count++)
        {
            var run = HeadlessTestSetup.Run("HUNTING84_LAST_SPORES",
                Enumerable.Repeat(ModelDb.Character<Ironclad>(), count).ToArray());
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<VantomBoss>().ToMutable());
            var fungus = await CreatureCmd.Add<FungusBeast>(battle.Combat, "fungus");
            Check(fungus.Powers.Any(power => power.GetType().Name == "SporeCloudPower" && power.Amount == 2),
                "Native fungus addition applies exactly two fixed spores for every player count.");
            await CreatureCmd.Kill(battle.Monster.Creature);
            using var lifecycle = new NativeEndingScope();
            Check(CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsEnding,
                "The actual native getters begin with one living primary enemy.");
            await CreatureCmd.Kill(fungus);
            Check(run.Players.All(player => player.Creature.GetPowerAmount<VulnerablePower>() == 2),
                "Actual final-enemy death settles exact two Vulnerable for every living player.");
            Check(CombatManager.Instance.IsEnding,
                "The final death effect releases native victory detection after enemy removal.");
        }
    }

    private static async Task PreventedSpores()
    {
        ModelDb.Inject(typeof(FungusDeathShield));
        for (int count = 1; count <= 4; count++)
        {
            var run = HeadlessTestSetup.Run("HUNTING84_PREVENTED_SPORES",
                Enumerable.Repeat(ModelDb.Character<Ironclad>(), count).ToArray());
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<VantomBoss>().ToMutable());
            var fungus = await CreatureCmd.Add<FungusBeast>(battle.Combat, "fungus");
            await CreatureCmd.Kill(battle.Monster.Creature);
            var context = new ThrowingPlayerChoiceContext();
            await PowerCmd.Apply<FungusDeathShield>(context, fungus, 1, null, null);
            using var lifecycle = new NativeEndingScope();
            await CreatureCmd.Kill(fungus);
            Check(fungus.IsAlive && fungus.CurrentHp == 1 && !CombatManager.Instance.IsEnding &&
                run.Players.All(player => player.Creature.GetPowerAmount<VulnerablePower>() == 0),
                "Native prevented final death leaves the enemy alive and applies no spores.");
            var first = run.Players[0].Creature;
            await PowerCmd.Apply<ArtifactPower>(context, first, 1, null, null);
            Check(first.GetPowerAmount<ArtifactPower>() == 1, "The native immunity fixture supplies exactly one Artifact.");
            await CreatureCmd.Kill(fungus);
            Check(first.GetPowerAmount<ArtifactPower>() == 0 && first.GetPowerAmount<VulnerablePower>() == 0 &&
                run.Players.Skip(1).All(player => player.Creature.GetPowerAmount<VulnerablePower>() == 2) &&
                CombatManager.Instance.IsEnding,
                "Actual final spores consume Artifact once and settle other players before native ending.");
        }
    }

    public sealed class FungusDeathShield : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Single;
        public override bool ShouldDie(Creature creature) => creature != Owner;
        public override async Task AfterPreventingDeath(Creature creature)
        {
            if (creature != Owner) return;
            await CreatureCmd.Heal(creature, 1);
            await PowerCmd.Remove(this);
        }
    }

    private static async Task FungusMoves()
    {
        for (int count = 1; count <= 4; count++)
        {
            var run = HeadlessTestSetup.Run("HUNTING84_FUNGUS_MOVES",
                Enumerable.Repeat(ModelDb.Character<Ironclad>(), count).ToArray());
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<VantomBoss>().ToMutable());
            foreach (int roll in new[] { 39, 40 })
            {
                var creature = await CreatureCmd.Add<FungusBeast>(battle.Combat, "fungus" + roll);
                var fungus = creature.Monster!;
                fungus.Rng = new MegaCrit.Sts2.Core.Random.Rng((uint)SeedForRoll(roll));
                Check(fungus.NextMove.Id == "BITE", "Every new fungus starts with its native attack intent.");
                int healthBefore = run.Players.Sum(player => player.Creature.CurrentHp);
                await Perform(fungus);
                int lostHealth = healthBefore - run.Players.Sum(player => player.Creature.CurrentHp);
                Check(lostHealth == 6 * count,
                    $"The first native bite deals fixed six damage per player target; players={count}, actual={lostHealth}.");
                Check(fungus.NextMove.Id == (roll == 39 ? "GROW" : "BITE"),
                    "Native monster RNG preserves the exact forty percent grow threshold.");
                if (roll == 40)
                {
                    ulong beforeForced = HeadlessTestSetup.RngPosition(fungus.Rng);
                    await Perform(fungus);
                    Check(fungus.NextMove.Id == "GROW" && HeadlessTestSetup.RngPosition(fungus.Rng) == beforeForced,
                        "Two consecutive bites force growth without drawing another random branch.");
                }
                ulong beforeGrowth = HeadlessTestSetup.RngPosition(fungus.Rng);
                await Perform(fungus);
                Check(creature.GetPowerAmount<StrengthPower>() == 3 && fungus.NextMove.Id == "BITE" &&
                    HeadlessTestSetup.RngPosition(fungus.Rng) == beforeGrowth,
                    "Growth gives fixed three Strength and forces the following bite without a branch draw.");
            }
        }
    }

    private static async Task Perform(MonsterModel monster)
    {
        await monster.NextMove.PerformMove(monster.CombatState.PlayerCreatures);
        monster.MoveStateMachine.OnMovePerformed(monster.NextMove);
        monster.RollMove(monster.CombatState.PlayerCreatures);
    }

    private static int SeedForRoll(int wanted)
    {
        for (uint seed = 0; seed < 10000; seed++)
            if (new MegaCrit.Sts2.Core.Random.Rng(seed).NextInt(100) == wanted) return (int)seed;
        throw new Exception("No seed supplies the requested native branch input.");
    }

    // Temporarily remove only our host substitutes. Native death, powers and
    // ending detection run unchanged; no full turn-loop or victory-save claim.
    internal sealed class NativeEndingScope : IDisposable
    {
        private readonly Harmony host = new("AllMatters.TestHost");
        public NativeEndingScope()
        {
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Unpatch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
#if STS2_PUBLIC
            AccessTools.Property(typeof(CombatManager), nameof(CombatManager.IsInProgress)).SetValue(CombatManager.Instance, true);
#else
            var state = AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
            AccessTools.Property(state.GetType(), "IsInProgress").SetValue(state, true);
#endif
        }
        public void Dispose()
        {
            host.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                prefix: new HarmonyMethod(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Patch(AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                prefix: new HarmonyMethod(typeof(HeadlessCombatNotEnding), "Prefix"));
        }
    }

    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
