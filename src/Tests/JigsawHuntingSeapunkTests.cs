using AllMatters.Jigsaw;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;
using System.Text.Json;

internal static class JigsawHuntingSeapunkTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw Seapunk actual Underdocks entry appends one normal native enemy", NativeEntry),
     ("Jigsaw Seapunk generation preserves native pool slots RNG and scales once for one to four roles", Generation),
     ("Jigsaw Seapunk Unknown event battles add once per native encounter", EventBattles),
     ("Jigsaw Seapunk excludes safe unrelated acts and top level elite Boss roots", Boundaries),
     ("Jigsaw Seapunk native cycle retains ordinary commands and must be defeated", Cycle)];

    private static async Task NativeEntry()
    {
        ModelId[] originals = [];
        Type? originalEncounter = null;
        foreach (bool hunted in new[] { false, true })
        {
            using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
            using var eventScope = HeadlessCombatInProgress.EnterEvent();
            var native = RunState.CreateForTest(
                [Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)],
                acts: [ModelDb.Act<Underdocks>()], seed: "HUNTING84_SEAPUNK_ENTRY");
            var manager = RunManager.Instance;
            ulong? previousNetId = LocalContext.NetId;
            var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
            var previousSize = viewport.Size;
            var host = new Harmony("AllMatters.Tests.Seapunk84EntryProfile");
            try
            {
                manager.SetUpTest(native, new NetSingleplayerGameService());
                manager.CombatReplayWriter.IsEnabled = false;
                LocalContext.NetId = 1;
                viewport.Size = new Vector2I(1920, 1080);
                // Retain real room entry, while keeping profiles and saves local to the test.
                JigsawNativeSaveHost.Save = _ => Task.CompletedTask;
                host.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)),
                    prefix: new HarmonyMethod(typeof(JigsawHuntingSeapunkTests), nameof(SkipProfile)));
                host.Patch(AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay)),
                    prefix: new HarmonyMethod(typeof(JigsawHuntingSeapunkTests), nameof(SkipProfile)));
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
                Ok(map.SetRemainingSteps(hunted ? 0 : 100));
                JigsawCheckpoint.Capture(native, map);
                JigsawNativeSession.Restore(native);
                var session = JigsawNativeSession.Current!;
                await session.Enter(target);
                Check(native.Act is Underdocks && native.CurrentRoom is CombatRoom &&
                    session.Map!.Steps == stepsBefore + 1 && session.Map.CurrentRoomHunted == hunted,
                    "Real native Underdocks entry confirms exactly one logical safe/hunted room.");
                var room = (CombatRoom)native.CurrentRoom!;
                var enemies = room.CombatState.Enemies.ToArray();
                if (!hunted)
                {
                    originalEncounter = room.Encounter.GetType();
                    originals = enemies.Select(creature => creature.Monster!.Id).ToArray();
                    continue;
                }
                int expected = originals.Count(id => id == ModelDb.Monster<Seapunk>().Id) + 1;
                int actual = enemies.Count(creature => creature.Monster is Seapunk);
                Check(room.Encounter.GetType() == originalEncounter && enemies.Length == originals.Length + 1 &&
                    enemies.Take(originals.Length).Select(creature => creature.Monster!.Id).SequenceEqual(originals) && actual == expected,
                    $"Actual hunted entry retains the safe native encounter and appends one Seapunk: expected={expected}, actual={actual}, originalEnemies={originals.Length}, actualEnemies={enemies.Length}.");
                var addition = enemies.Last();
                Check(addition.Monster is Seapunk && addition.CurrentHp == addition.MaxHp && addition.Powers.Count == 0 &&
                    addition.Monster.NextMove.Id == "SEA_KICK_MOVE",
                    "The appended native Seapunk begins at full native HP with Sea Kick and no initial powers or Minion.");
            }
            finally
            {
                host.UnpatchAll(host.Id);
                JigsawNativeSaveHost.Save = null;
                JigsawNativeSession.Current?.Close();
                CombatManager.Instance.Reset(true);
                manager.CleanUp(false);
                LocalContext.NetId = previousNetId;
                viewport.Size = previousSize;
            }
        }
    }

    private static Task Generation()
    {
        var pool = ModelDb.Act<Underdocks>().GenerateAllEncounters().Where(encounter => encounter.RoomType == RoomType.Monster).ToArray();
        Check(pool.Length == 14, "The current native Underdocks ordinary pool contains all ten regular and four weak encounters.");
        foreach (var canonical in pool)
            for (int count = 1; count <= 4; count++)
            {
                (ModelId Id, string? Slot)[] originals;
                string opening;
                Dictionary<string, Vector2> markers;
                using (var safe = new SeapunkBattle(canonical, count, scope: "safe"))
                {
                    originals = safe.Encounter.MonstersWithSlots.Select(pair => (pair.Item1.Id, pair.Item2)).ToArray();
                    opening = Opening(safe.Combat.Enemies);
                    markers = ReadMarkers(safe.Encounter);
                }
                using var battle = new SeapunkBattle(canonical, count);
                string context = $"{canonical.GetType().Name}, roles={count}";
                var generated = battle.Encounter.MonstersWithSlots;
                var addition = battle.Combat.Enemies.Last();
                Check(generated.Count == originals.Length + 1 && generated.Take(originals.Length)
                    .Select(pair => (pair.Item1.Id, pair.Item2)).SequenceEqual(originals) &&
                    Opening(battle.Combat.Enemies.Take(originals.Length)) == opening,
                    context + ": native original order, slots, initial HP, moves, powers and complete monster RNG are unchanged.");
                Check(generated.Last().Item1 is Seapunk && ReferenceEquals(generated.Last().Item1, addition.Monster) &&
                    generated.Count(pair => pair.Item1 is Seapunk) == originals.Count(pair => pair.Id == ModelDb.Monster<Seapunk>().Id) + 1 &&
                    generated.Last().Item2 == (battle.Encounter.HasScene ? JigsawHuntingCombat.AdditionSlot : null),
                    context + ": exactly one extra original Seapunk follows the native roster, even when the roster already includes Seapunk.");
                Check(addition.CurrentHp == addition.MaxHp && addition.Monster!.MinInitialHp == 44 && addition.Monster.MaxInitialHp == 46 &&
                    Enumerable.Range(44, 3).Any(hp => addition.MaxHp == (int)Creature.ScaleHpForMultiplayer(hp, battle.Encounter, count, 0)) &&
                    addition.IsPrimaryEnemy && !addition.IsSecondaryEnemy && !addition.HasPower<MinionPower>() && addition.Powers.Count == 0 &&
                    addition.Monster.NextMove.Id == "SEA_KICK_MOVE",
                    context + $": native A0 HP44..46 scales exactly once, starts with Sea Kick, and remains a required normal enemy; actual HP={addition.MaxHp}.");
                if (battle.Encounter.HasScene)
                {
                    JigsawHuntingNativeTests.RegisterScripts();
                    var scene = battle.Encounter.CreateScene();
                    try
                    {
                        var actualMarkers = SceneMarkers(scene);
                        Check(markers.All(pair => actualMarkers.TryGetValue(pair.Key, out var position) && position == pair.Value) &&
                            actualMarkers.Count == markers.Count + 1 && generated.All(pair => pair.Item2 != null &&
                                scene.GetNodeOrNull<Marker2D>(pair.Item2) != null),
                            context + ": actual authored scene markers remain fixed and every generated enemy has exactly one valid native slot.");
                        var occupied = originals.Select(pair => scene.GetNode<Marker2D>(pair.Slot!)).ToArray();
                        var marker = scene.GetNode<Marker2D>(JigsawHuntingCombat.AdditionSlot);
                        Check(float.IsFinite(marker.Position.X) && float.IsFinite(marker.Position.Y) &&
                            marker.Position.X < occupied.Min(original => original.Position.X) &&
                            marker.Position.Y == occupied.Max(original => original.Position.Y),
                            context + ": the actual addition marker is grounded alongside the original occupied markers, including scenes with empty Slots declarations.");
                    }
                    finally { scene.Free(); }
                }
                int before = generated.Count;
                bool rejected = false;
                try { battle.Encounter.GenerateMonstersWithSlots(battle.Run); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && battle.Encounter.MonstersWithSlots.Count == before && battle.Combat.Enemies.Count == before,
                    context + ": native duplicate generation rejects before a second addition or creature can appear.");
            }
        return Task.CompletedTask;
    }

    private static Task EventBattles()
    {
        for (int count = 1; count <= 4; count++)
        {
            using var battle = new SeapunkBattle(ModelDb.Encounter<CorpseSlugsWeak>(), count, scope: "event");
            var map = JigsawNativeSession.TryGet(battle.Run)!.Map!;
            int steps = map.Steps;
            var additions = new HashSet<MonsterModel>();
            foreach (var canonical in new EncounterModel[] { ModelDb.Encounter<CorpseSlugsWeak>(),
                ModelDb.Encounter<PhrogParasiteElite>(), ModelDb.Encounter<SoulFyshBoss>() })
            {
                if (battle.Encounter.GetType() != canonical.GetType()) battle.Next(canonical);
                var addition = battle.Encounter.MonstersWithSlots.Last().Item1;
                Check(battle.Run.CurrentRoomCount == 2 && battle.Run.BaseRoom is EventRoom &&
                    ((CombatRoom)battle.Run.CurrentRoom!).ParentEventId != null && map.CurrentRoomHunted &&
                    map.Nodes.Single(node => node.Id == map.CurrentNode).Room == JigsawRoom.Unknown && map.Steps == steps,
                    $"roles={count}, {canonical.GetType().Name}: consecutive native children retain the same accepted hunted Unknown root and exploration charge.");
                Check(addition is Seapunk && additions.Add(addition) &&
                    battle.Encounter.MonstersWithSlots.Count(pair => pair.Item1 is Seapunk) == 1 &&
                    !addition.Creature.HasPower<MinionPower>() && addition.Creature.IsPrimaryEnemy,
                    "Each independent ordinary, named Elite or named Boss event encounter gets one distinct normal Seapunk, using the current act ordinary enhancement.");
                int before = battle.Encounter.MonstersWithSlots.Count;
                bool rejected = false;
                try { battle.Encounter.GenerateMonstersWithSlots(battle.Run); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && battle.Encounter.MonstersWithSlots.Count == before,
                    "A repeat generation inside one event child cannot duplicate that child's opening addition.");
            }
        }
        return Task.CompletedTask;
    }

    private static Task Boundaries()
    {
        foreach (string scope in new[] { "safe", "non-jigsaw", "other-act", "elite", "boss", "second-boss" })
        {
            using var battle = new SeapunkBattle(ModelDb.Encounter<CorpseSlugsWeak>(), 1, scope);
            Check(battle.Encounter.MonstersWithSlots.All(pair => pair.Item1 is not Seapunk),
                $"scope={scope}: only a hunted current-act Underdocks ordinary or Unknown root can append Seapunk.");
            Check(scope != "non-jigsaw" || JigsawNativeSession.TryGet(battle.Run) == null,
                "The standalone counterexample has no Jigsaw session, rather than merely a safe room.");
        }
        var run = HeadlessTestSetup.Run("SEAPUNK_NO_ACTS", ModelDb.Character<Ironclad>());
        var encounter = ModelDb.Encounter<CorpseSlugsWeak>().ToMutable();
        encounter.GenerateMonstersWithSlots(run);
        Check(JigsawNativeSession.TryGet(run) == null && encounter.MonstersWithSlots.All(pair => pair.Item1 is not Seapunk),
            "An unrelated native encounter without any acts generates normally before the patch attempts to inspect act data.");
        return Task.CompletedTask;
    }

    private static async Task Cycle()
    {
        for (int count = 1; count <= 4; count++)
        {
            using var battle = new SeapunkBattle(ModelDb.Encounter<CorpseSlugsWeak>(), count);
            var addition = battle.Combat.Enemies.Last();
            var seapunk = addition.Monster!;
            int hp = battle.Players.Sum(player => player.Creature.CurrentHp);
            await Perform(seapunk);
            Check(hp - battle.Players.Sum(player => player.Creature.CurrentHp) == 11 * count && seapunk.NextMove.Id == "SPINNING_KICK_MOVE",
                $"roles={count}: native Sea Kick deals unchanged eleven to each original native target and advances to Spinning Kick.");
            hp = battle.Players.Sum(player => player.Creature.CurrentHp);
            int historyStart = CombatManager.Instance.History.Entries.Count();
            await Perform(seapunk);
            Check(hp - battle.Players.Sum(player => player.Creature.CurrentHp) == 8 * count && seapunk.NextMove.Id == "BUBBLE_BURP_MOVE" &&
                CombatManager.Instance.History.Entries.Skip(historyStart).OfType<DamageReceivedEntry>().Count() == 4 * count,
                "Native Spinning Kick retains original four hits of two for every native player target.");
            await Perform(seapunk);
            Check(addition.Block == 7 && addition.GetPowerAmount<StrengthPower>() == 1 && seapunk.NextMove.Id == "SEA_KICK_MOVE",
                $"roles={count}: native Bubble Burp grants original Block7/Strength1 then returns to Sea Kick; actual Block={addition.Block}, Strength={addition.GetPowerAmount<StrengthPower>()}, next={seapunk.NextMove.Id}.");
            var moves = CombatManager.Instance.History.Entries.OfType<MonsterPerformedMoveEntry>()
                .Where(entry => ReferenceEquals(entry.Monster, seapunk)).Select(entry => entry.Move.Id).ToArray();
            Check(moves.SequenceEqual(new[] { "SEA_KICK_MOVE", "SPINNING_KICK_MOVE", "BUBBLE_BURP_MOVE" }),
                $"roles={count}: original MonsterModel.PerformMove records the complete native three-command cycle; actual=[{string.Join(',', moves)}].");
            using var ending = new JigsawHuntingCombatTests.NativeEndingScope();
            foreach (var original in battle.Combat.Enemies.Where(enemy => !ReferenceEquals(enemy, addition)).ToArray())
                await CreatureCmd.Kill(original);
            Check(addition.IsAlive && battle.Combat.Enemies.Contains(addition) && !CombatManager.Instance.IsEnding &&
                addition.IsPrimaryEnemy && !addition.HasPower<MinionPower>(),
                "Killing every original enemy leaves the normal added Seapunk alive and blocks native combat completion.");
            await CreatureCmd.Kill(addition);
            Check(addition.IsDead && CombatManager.Instance.IsEnding,
                "Native combat completion is released only after the added required Seapunk also dies.");
        }
    }

    private static async Task Perform(MonsterModel monster)
    {
        await monster.PerformMove();
        monster.RollMove(monster.CombatState.PlayerCreatures);
    }

    private static string Opening(IEnumerable<Creature> enemies) => JsonSerializer.Serialize(enemies.Select(creature => new
    {
        creature.Monster!.Id,
        creature.MaxHp,
        creature.CurrentHp,
        Powers = creature.Powers.Select(power => new { power.Id, power.Amount }),
        Move = creature.Monster.NextMove.Id,
        Rng = RngDescription(creature.Monster)
    }));

    private static string RngDescription(MonsterModel monster) =>
#if STS2_PUBLIC
        $"{monster.Rng.Seed}/{monster.Rng.Counter}";
#else
        JsonSerializer.Serialize(monster.Rng.ToSerializable(), new JsonSerializerOptions { IncludeFields = true });
#endif

    private static Dictionary<string, Vector2> SceneMarkers(Control scene) => scene.FindChildren("*", "Marker2D", true, false)
        .OfType<Marker2D>().ToDictionary(marker => scene.GetPathTo(marker).ToString(), marker => marker.Position);

    private static Dictionary<string, Vector2> ReadMarkers(EncounterModel encounter)
    {
        if (!encounter.HasScene) return [];
        JigsawHuntingNativeTests.RegisterScripts();
        var scene = encounter.CreateScene();
        try { return SceneMarkers(scene); }
        finally { scene.Free(); }
    }

    private sealed class SeapunkBattle : IDisposable
    {
        private readonly ulong? previousNetId = LocalContext.NetId;
        private readonly bool eventBattle;
        internal Player[] Players { get; }
        internal RunState Run { get; }
        internal EncounterModel Encounter { get; private set; } = null!;
        internal CombatState Combat { get; private set; } = null!;

        internal SeapunkBattle(EncounterModel canonical, int count, string scope = "hunted")
        {
            Players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
            Run = RunState.CreateForTest([Players[0]], acts: [scope == "other-act" ? ModelDb.Act<Hive>() : ModelDb.Act<Underdocks>()],
                seed: "SEAPUNK_NATIVE_MATRIX");
            eventBattle = scope == "event";
            try
            {
                RunManager.Instance.SetUpTest(Run, new NetSingleplayerGameService());
                LocalContext.NetId = Players[0].NetId;
                if (eventBattle) Run.PushRoom(new EventRoom(ModelDb.Event<FieldOfManSizedHoles>()));
                if (scope != "non-jigsaw")
                {
                    var root = scope switch { "event" => JigsawRoom.Unknown, "elite" => JigsawRoom.Elite,
                        "boss" => JigsawRoom.Boss, "second-boss" => JigsawRoom.SecondBoss, _ => JigsawRoom.Combat };
                    JigsawCheckpoint.Capture(Run, AcceptedRoot(scope != "safe", root));
                    JigsawNativeSession.Restore(Run);
                }
                var projection = JigsawEntryProjection.Append(Run.Map, 1, eventBattle ? JigsawRoom.Unknown : JigsawRoom.Combat);
                Run.Map = projection.Map;
                Check(Run.AddVisitedMapCoord(projection.Point.coord) && Run.CurrentMapPoint == projection.Point,
                    "The native fixture enters a chronological map coordinate before original monster RNG and HP generation.");
                Next(canonical);
            }
            catch { Dispose(); throw; }
        }

        internal void Next(EncounterModel canonical)
        {
            bool firstCombat = Encounter == null;
            if (Run.CurrentRoom is CombatRoom) Run.PopCurrentRoom();
            CombatManager.Instance.Reset(true);
            Encounter = canonical.ToMutable();
            Combat = new CombatState(Encounter, Run) { RoundNumber = 1 };
            Combat.AddPlayer(Players[0]);
            Players[0].ResetCombatState();
            CombatManager.Instance.SetUpCombat(Combat);
            foreach (var player in Players.Skip(1))
            {
                if (firstCombat) player.RunState = Run;
                Combat.AddPlayer(player);
                player.ResetCombatState();
            }
            Run.PushRoom(new CombatRoom(Combat) { ParentEventId = eventBattle ? ModelDb.Event<FieldOfManSizedHoles>().Id : null });
            Encounter.GenerateMonstersWithSlots(Run);
            foreach (var (monster, slot) in Encounter.MonstersWithSlots)
            {
                Combat.AddCreature(Combat.CreateCreature(monster, CombatSide.Enemy, slot));
                monster.SetUpForCombat();
                monster.RollMove(Combat.PlayerCreatures);
            }
        }

        public void Dispose()
        {
            JigsawNativeSession.TryGet(Run)?.Close();
            CombatManager.Instance.Reset(true);
            RunManager.Instance.CleanUp(false);
            LocalContext.NetId = previousNetId;
        }
    }

    private static JigsawRun AcceptedRoot(bool hunted, JigsawRoom kind)
    {
        var map = JigsawRun.Create(75, doubleBoss: kind == JigsawRoom.SecondBoss, players: [1UL]);
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
                if ((kind == JigsawRoom.SecondBoss || map.Nodes.Single(node => node.Id == next).Room != JigsawRoom.SecondBoss) &&
                    previous.TryAdd(next, current)) pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == kind && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        Ok(map.SetRemainingSteps(hunted ? 0 : 100));
        foreach (int node in path)
        {
            if (!hunted && node == target) Ok(map.SetRemainingSteps(1));
            var entry = map.BeginEnter(node)!;
            Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        Check(map.CurrentNode == target && map.CurrentRoomHunted == hunted, "Accepted native fixture root retains its actual safe/hunted snapshot.");
        return map;
    }

    private static bool SkipProfile() => false;
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Check(bool condition, string message) => NativeEventTests.Check(condition, message);
}
