using AllMatters.Jigsaw;
using AllMatters.Monsters;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;
using System.Text.Json;
using VoidCard = MegaCrit.Sts2.Core.Models.Cards.Void;

internal static class JigsawMysticTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw Mystic hunted Glory normal entry adds one native monster before combat creation", NativeEntry),
     ("Jigsaw Mystic generation respects roots and scales native HP once for one to four combat roles", Generation),
     ("Jigsaw Mystic native CAST HEAL BUFF cycle records real cards and fixed Strength", Cycle),
     ("Jigsaw Mystic natural CAST adds discard statuses without healing injured enemies", CastWithInjuredEnemies),
     ("Jigsaw Mystic predicts all six independent native status branches and skips dead owners", CastBranches),
     ("Jigsaw Mystic native healing and buff targeting include self and exclude dead enemies", Targets),
     ("Jigsaw Mystic real resource factory plays complete Spine actions and bilingual text", Presentation)];

    // The initial red used this real entry before the model existed.
    private static async Task NativeEntry()
    {
        foreach (bool hunted in new[] { false, true })
        {
            using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
            using var eventScope = HeadlessCombatInProgress.EnterEvent();
            var native = RunState.CreateForTest(
                [Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)],
                acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING86_MYSTIC_ENTRY");
            native.CurrentActIndex = 2;
            var manager = RunManager.Instance;
            ulong? previousNetId = LocalContext.NetId;
            var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
            var previousSize = viewport.Size;
            var host = new Harmony("AllMatters.Tests.Mystic86EntryProfile");
            try
            {
                manager.SetUpTest(native, new NetSingleplayerGameService());
                manager.CombatReplayWriter.IsEnabled = false;
                LocalContext.NetId = 1;
                viewport.Size = new Vector2I(1920, 1080);
                // Entry is not a persistence test. Prevent writing real profiles,
                // replays or saves, while retaining native room/combat generation.
                JigsawNativeSaveHost.Save = _ => Task.CompletedTask;
                host.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)),
                    prefix: new HarmonyMethod(typeof(JigsawMysticTests), nameof(SkipProfile)));
                host.Patch(AccessTools.Method(typeof(RunManager), nameof(RunManager.WriteReplay)),
                    prefix: new HarmonyMethod(typeof(JigsawMysticTests), nameof(SkipProfile)));
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
                NativeEventTests.Check(native.Act is Glory && native.CurrentRoom is CombatRoom &&
                    session.Map!.Steps == stepsBefore + 1 && session.Map.CurrentRoomHunted == hunted,
                    "Real native Glory entry confirms exactly one logical safe/hunted room.");
                var room = (CombatRoom)native.CurrentRoom!;
                var mystics = room.CombatState.Enemies.Where(creature =>
                    creature.Monster?.GetType().FullName == "AllMatters.Monsters.Mystic").ToArray();
                NativeEventTests.Check(mystics.Length == (hunted ? 1 : 0),
                    $"Actual native Glory ordinary entry appends Mystic only under its hunted root: hunted={hunted}, expected={(hunted ? 1 : 0)}, actual={mystics.Length}.");
                if (hunted)
                    Check(mystics[0].MaxHp == 56 && mystics[0].CurrentHp == 56 && mystics[0].Powers.Count == 0 &&
                        mystics[0].Monster!.NextMove.Id == "CAST", "Real entry starts at HP56 with CAST and no initial powers, including no Minion.");
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
        (ModelId Id, string? Slot)[] originals;
        using (var safe = new MysticBattle(1, scope: "safe"))
            originals = safe.Encounter.MonstersWithSlots.Select(pair => (pair.Item1.Id, pair.Item2)).ToArray();
        for (int count = 1; count <= 4; count++)
        {
            using var battle = new MysticBattle(count);
            var mystic = battle.Mystic!;
            int expected = (int)Creature.ScaleHpForMultiplayer(56, battle.Encounter, count, 2);
            Check(mystic.Creature.MaxHp == expected && mystic.Creature.CurrentHp == expected &&
                mystic.Creature.Powers.Count == 0 && mystic.NextMove.Id == "CAST",
                $"Native creation applies HP scaling exactly once, with no powers or Minion: roles={count}, expected={expected}, actual={mystic.Creature.MaxHp}.");
            Check(battle.Encounter.MonstersWithSlots.Last().Item1 == mystic &&
                battle.Encounter.MonstersWithSlots.Where(pair => pair.Item1 is not Mystic).Select(pair => (pair.Item1.Id, pair.Item2)).SequenceEqual(originals) &&
                battle.Combat.Enemies.Count == battle.Encounter.MonstersWithSlots.Count,
                "The addition follows every original generated enemy and all native creatures are retained.");
            bool rejected = false;
            try { battle.Encounter.GenerateMonstersWithSlots(battle.Run); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && battle.Encounter.MonstersWithSlots.Count(pair => pair.Item1 is Mystic) == 1,
                "Native duplicate generation rejects before a second Mystic can be appended.");
        }
        foreach (string scope in new[] { "safe", "non-jigsaw", "other-act", "elite", "boss", "event" })
        {
            using var battle = new MysticBattle(1, scope: scope);
            Check(battle.Encounter.MonstersWithSlots.Count(pair => pair.Item1 is Mystic) == (scope == "event" ? 1 : 0),
                $"Generation respects the actual accepted root: scope={scope}.");
        }
        return Task.CompletedTask;
    }

    private static async Task Cycle()
    {
        for (int count = 1; count <= 4; count++)
        {
            using var battle = new MysticBattle(count);
            var mystic = battle.Mystic!;
            var expected = Clone(mystic.Rng);
            var cards = battle.Players.Select(_ => StatusIds[expected.NextInt(6)]).ToArray();
            var drawCounts = battle.Players.Select(player => player.PlayerCombatState!.DrawPile.Cards.Count).ToArray();
            var handCounts = battle.Players.Select(player => player.PlayerCombatState!.Hand.Cards.Count).ToArray();
            var deckCounts = battle.Players.Select(player => player.Deck.Cards.Count).ToArray();
            await Perform(mystic);
            Check(mystic.NextMove.Id == "HEAL" && battle.Players.Select((player, index) =>
                player.PlayerCombatState!.DiscardPile.Cards is var pile && pile.Count == 1 &&
                pile[0].Id == cards[index] && ReferenceEquals(pile[0].Owner, player) &&
                player.PlayerCombatState.Hand.Cards.Count == handCounts[index] &&
                player.PlayerCombatState.DrawPile.Cards.Count == drawCounts[index] && player.Deck.Cards.Count == deckCounts[index]).All(value => value),
                "Native CAST generates exactly one independently predicted owned card in each Discard, without Hand or Draw leakage.");
            Check(CombatManager.Instance.History.Entries.OfType<CardGeneratedEntry>().Count() == count && SameRng(mystic.Rng, expected),
                "Real generation history and the complete native RNG state account for one draw per living combat role.");
            var hp = battle.Combat.Enemies.Select(enemy => enemy.CurrentHp).ToArray();
            await Perform(mystic);
            Check(mystic.NextMove.Id == "BUFF" && battle.Combat.Enemies.Select(enemy => enemy.CurrentHp).SequenceEqual(hp) && SameRng(mystic.Rng, expected),
                "Full-health HEAL draws no RNG and still advances to BUFF.");
            var receiver = battle.Combat.Enemies[expected.NextInt(battle.Combat.Enemies.Count)];
            await Perform(mystic);
            var entries = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>().Where(entry => entry.Power is StrengthPower).ToArray();
            Check(mystic.NextMove.Id == "CAST" && entries is [{ Amount: 2 }] &&
                ReferenceEquals(entries[0].Actor, receiver) && ReferenceEquals(entries[0].Applier, mystic.Creature) &&
                receiver.GetPowerAmount<StrengthPower>() == 2 && SameRng(mystic.Rng, expected),
                $"BUFF applies fixed Strength2 once to the predicted native living enemy, including in {count}-role combat.");
            Check(CombatManager.Instance.History.Entries.OfType<MonsterPerformedMoveEntry>().Where(entry => ReferenceEquals(entry.Monster, mystic))
                .Select(entry => entry.Move.Id).SequenceEqual(new[] { "CAST", "HEAL", "BUFF" }),
                "Native monster history records the full three-action cycle in order.");
        }
    }

    private static async Task CastWithInjuredEnemies()
    {
        for (int count = 1; count <= 4; count++)
        {
            using var battle = new MysticBattle(count);
            var mystic = battle.Mystic!;
            foreach (var enemy in battle.Combat.Enemies) enemy.SetCurrentHpInternal(enemy.MaxHp - 20);
            for (int cast = 0; cast < 2; cast++)
            {
                Check(mystic.NextMove.Id == "CAST", "The actual native initial roll or completed HEAL/BUFF cycle selects CAST.");
                var hp = battle.Combat.Enemies.Select(enemy => enemy.CurrentHp).ToArray();
                var expected = Clone(mystic.Rng);
                var cards = battle.Players.Select(_ => StatusIds[expected.NextInt(6)]).ToArray();
                await Perform(mystic);
                Check(battle.Combat.Enemies.Select(enemy => enemy.CurrentHp).SequenceEqual(hp) &&
                    battle.Players.Select((player, index) => player.PlayerCombatState!.DiscardPile.Cards is var pile &&
                        pile.Count == cast + 1 && pile.Last().Id == cards[index] && ReferenceEquals(pile.Last().Owner, player)).All(value => value) &&
                    SameRng(mystic.Rng, expected),
                    "Each natural CAST generates one predicted six-way status per owner while injured enemies retain their exact HP.");
                if (cast == 0) { await Perform(mystic); await Perform(mystic); }
            }
        }
    }

    private static ModelId[] StatusIds => [ModelDb.Card<Dazed>().Id, ModelDb.Card<Burn>().Id,
        ModelDb.Card<VoidCard>().Id, ModelDb.Card<Toxic>().Id, ModelDb.Card<Beckon>().Id, ModelDb.Card<Slimed>().Id];

    private static async Task CastBranches()
    {
        var seen = new HashSet<ModelId>();
        var nativeStates = new HashSet<string>();
        var initialization = new List<string>();
        bool equal = false, different = false;
        for (int seed = 0; seed < 64 && (seen.Count < 6 || !equal || !different); seed++)
        {
            using var battle = new MysticBattle(3, seed: "MYSTIC_STATUS_BRANCH_" + seed);
            var mystic = battle.Mystic!;
            string state = RngDescription(mystic.Rng);
            nativeStates.Add(state);
            if (initialization.Count < 8) initialization.Add($"input={seed},run={battle.Run.Rng.Seed},coord={battle.Run.CurrentMapCoord},rng={state}");
            battle.Players[2].Creature.SetCurrentHpInternal(0);
            var expected = Clone(mystic.Rng);
            ModelId first = StatusIds[expected.NextInt(6)], second = StatusIds[expected.NextInt(6)];
            await Perform(mystic);
            var one = battle.Players[0].PlayerCombatState!.DiscardPile.Cards.Single();
            var two = battle.Players[1].PlayerCombatState!.DiscardPile.Cards.Single();
            Check(one.Id == first && two.Id == second && !ReferenceEquals(one, two) &&
                ReferenceEquals(one.Owner, battle.Players[0]) && ReferenceEquals(two.Owner, battle.Players[1]) &&
                battle.Players[2].PlayerCombatState!.DiscardPile.Cards.Count == 0 &&
                CombatManager.Instance.History.Entries.OfType<CardGeneratedEntry>().Count() == 2 && SameRng(mystic.Rng, expected),
                $"Native seed {seed} yields the exact two independently predicted status outcomes; dead owner receives neither card nor RNG draw.");
            seen.Add(first); seen.Add(second);
            equal |= first == second;
            different |= first != second;
        }
        Check(seen.SetEquals(StatusIds) && equal && different,
            $"Deterministic native-seed coverage predicts all six status branches: seen=[{string.Join(',', seen)}], equal={equal}, different={different}, distinctNativeRngStates={nativeStates.Count}; {string.Join("; ", initialization)}.");
    }

    private static async Task Targets()
    {
        foreach (bool self in new[] { true, false })
        {
            using var battle = new MysticBattle(1);
            var mystic = battle.Mystic!;
            var target = self ? mystic.Creature : battle.Combat.Enemies.First(enemy => enemy.Monster is not Mystic);
            target.SetCurrentHpInternal(target.MaxHp - 20);
            var expected = Clone(mystic.Rng);
            expected.NextInt(1);
            Force(mystic, "HEAL");
            await Perform(mystic);
            Check(target.CurrentHp == target.MaxHp - 8 && SameRng(mystic.Rng, expected) && mystic.NextMove.Id == "BUFF",
                $"HEAL restores native twelve HP to the sole injured living {(self ? "self" : "companion")} and advances once.");
        }
        using (var battle = new MysticBattle(1))
        {
            var mystic = battle.Mystic!;
            foreach (var enemy in battle.Combat.Enemies.Where(enemy => !ReferenceEquals(enemy, mystic.Creature))) enemy.SetCurrentHpInternal(0);
            var expected = Clone(mystic.Rng);
            Force(mystic, "HEAL");
            await Perform(mystic);
            Check(SameRng(mystic.Rng, expected) && battle.Combat.Enemies.Where(enemy => !ReferenceEquals(enemy, mystic.Creature)).All(enemy => enemy.IsDead),
                "Dead injured companions are not revived and do not turn an empty HEAL into a random draw.");
            expected.NextInt(1);
            await Perform(mystic);
            Check(mystic.Creature.GetPowerAmount<StrengthPower>() == 2 &&
                battle.Combat.Enemies.Where(enemy => !ReferenceEquals(enemy, mystic.Creature)).All(enemy => enemy.GetPowerAmount<StrengthPower>() == 0) && SameRng(mystic.Rng, expected),
                "BUFF includes the sole living self and never grants Strength to dead companions.");
        }
    }

    private static async Task Presentation()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var model = ModelDb.Monster<Mystic>();
        using var visuals = model.CreateVisuals();
        await HeadlessUiHost.Mount(visuals);
        try
        {
            var sprite = visuals.SpineBody!;
            for (int frame = 0; frame < 30 && !sprite.IsAnimationStateReady(); frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(sprite.IsAnimationStateReady() && new[] { "Idle", "Attack", "Hit" }.All(sprite.GetSkeleton()!.GetData()!.HasAnimation),
                "The real registered visual factory loads the complete native Spine skeleton and all three actions.");
            var animator = model.GenerateAnimator(sprite);
            foreach (string trigger in new[] { "Attack", "Hit" })
            {
                Check(animator.HasTrigger(trigger), "The gameplay animation trigger is registered: " + trigger);
                animator.SetTrigger(trigger);
                var state = sprite.GetAnimationState();
#if STS2_PUBLIC
                var track = state.GetCurrent(0);
#else
                using var track = state.GetCurrent(0);
#endif
                Check(track?.GetAnimationName() == trigger, "The actual Spine track plays " + trigger);
                state.Update(track!.GetAnimationDuration() + 0.1f);
                state.Apply(sprite.GetSkeleton()!);
                state.Update(0.01f);
                state.Apply(sprite.GetSkeleton()!);
#if STS2_PUBLIC
                var next = state.GetCurrent(0);
#else
                using var next = state.GetCurrent(0);
#endif
                Check(next?.GetAnimationName() == "Idle", "Spine completion returns " + trigger + " to the looping Idle action.");
            }
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                Check(!model.Title.GetFormattedText().Contains("ALL_MATTERS"), "The monster title resolves in " + language);
                foreach (string move in new[] { "CAST", "HEAL", "BUFF" })
                {
                    string key = $"ALL_MATTERS_MONSTER_MYSTIC.moves.{move}.title";
                    string text = new LocString("monsters", key).GetRawText();
                    Check(!string.IsNullOrWhiteSpace(text) && !text.Contains("ALL_MATTERS"), "The move title resolves: " + language + "/" + move);
                }
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); tree.Root.RemoveChild(visuals); visuals.Free(); }
    }

    private static async Task Perform(MonsterModel monster)
    {
        await monster.PerformMove();
        monster.RollMove(monster.CombatState.PlayerCreatures);
    }

    private static void Force(MonsterModel monster, string move) =>
        monster.SetMoveImmediate((MoveState)monster.MoveStateMachine!.States[move], true);

    private static Rng Clone(Rng rng) =>
#if STS2_PUBLIC
        new(rng.Seed, rng.Counter);
#else
        new(rng.ToSerializable());
#endif

    private static bool SameRng(Rng actual, Rng expected) =>
#if STS2_PUBLIC
        actual.Seed == expected.Seed && actual.Counter == expected.Counter;
#else
        JsonSerializer.Serialize(actual.ToSerializable(), new JsonSerializerOptions { IncludeFields = true }) ==
        JsonSerializer.Serialize(expected.ToSerializable(), new JsonSerializerOptions { IncludeFields = true });
#endif

    private static string RngDescription(Rng rng) =>
#if STS2_PUBLIC
        $"seed={rng.Seed}/counter={rng.Counter}";
#else
        JsonSerializer.Serialize(rng.ToSerializable(), new JsonSerializerOptions { IncludeFields = true });
#endif

    // Single native run owner plus real combat roles; no fabricated remote roster.
    private sealed class MysticBattle : IDisposable
    {
        private readonly ulong? previousNetId = LocalContext.NetId;
        internal Player[] Players { get; }
        internal RunState Run { get; }
        internal EncounterModel Encounter { get; }
        internal CombatState Combat { get; }
        internal Mystic? Mystic { get; }

        internal MysticBattle(int count, string scope = "hunted", string seed = "MYSTIC_NATIVE_ACTIONS")
        {
            Players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
            Run = RunState.CreateForTest([Players[0]], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: seed);
            Run.CurrentActIndex = scope == "other-act" ? 1 : 2;
            Encounter = scope == "event" ? ModelDb.Encounter<AllMatters.Encounters.HolesEncounter>().ToMutable() :
                ModelDb.Encounter<AxebotsNormal>().ToMutable();
            Combat = new CombatState(Encounter, Run) { RoundNumber = 1 };
            try
            {
                RunManager.Instance.SetUpTest(Run, new NetSingleplayerGameService());
                LocalContext.NetId = Players[0].NetId;
                CombatManager.Instance.Reset(true);
                Combat.AddPlayer(Players[0]);
                Players[0].ResetCombatState();
                CombatManager.Instance.SetUpCombat(Combat);
                // Match the existing legal single-owner combat-role host: only
                // the run owner participates in native deck initialization.
                foreach (var player in Players.Skip(1))
                { player.RunState = Run; Combat.AddPlayer(player); player.ResetCombatState(); }
                var parent = ModelDb.Event<FieldOfManSizedHoles>();
                if (scope == "event") Run.PushRoom(new EventRoom(parent));
                Run.PushRoom(new CombatRoom(Combat) { ParentEventId = scope == "event" ? parent.Id : null });
                if (scope != "non-jigsaw")
                {
                    var root = scope switch { "event" => JigsawRoom.Unknown, "elite" => JigsawRoom.Elite, "boss" => JigsawRoom.Boss, _ => JigsawRoom.Combat };
                    JigsawCheckpoint.Capture(Run, AcceptedRoot(Players[0].NetId, scope != "safe", root));
                    JigsawNativeSession.Restore(Run);
                }
                // Use the same real chronological map projection as native
                // Jigsaw entry. Public CreateCreature otherwise falls back to
                // act+combatId when CurrentMapCoord is null, ignoring run seed.
                var projection = JigsawEntryProjection.Append(Run.Map, 1, scope == "event" ? JigsawRoom.Unknown : JigsawRoom.Combat);
                Run.Map = projection.Map;
                Check(Run.AddVisitedMapCoord(projection.Point.coord) && Run.CurrentMapPoint == projection.Point,
                    "The fixture enters an actual native map coordinate before native monster RNG initialization.");
                Encounter.GenerateMonstersWithSlots(Run);
                foreach (var (monster, slot) in Encounter.MonstersWithSlots)
                {
                    Combat.AddCreature(Combat.CreateCreature(monster, CombatSide.Enemy, slot));
                    monster.SetUpForCombat();
                    monster.RollMove(Combat.PlayerCreatures);
                }
                Mystic = Combat.Enemies.Select(enemy => enemy.Monster).OfType<Mystic>().SingleOrDefault();
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            JigsawNativeSession.TryGet(Run)?.Close();
            CombatManager.Instance.Reset(true);
            RunManager.Instance.CleanUp(false);
            LocalContext.NetId = previousNetId;
        }
    }

    private static JigsawRun AcceptedRoot(ulong owner, bool hunted, JigsawRoom roomType)
    {
        var map = JigsawRun.Create(75, players: [owner]);
        Ok(map.ChooseEnd(owner, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        Ok(map.CompleteAncient(owner));
        Ok(map.Continue(owner));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var pending = new Queue<int>();
        pending.Enqueue(0);
        while (pending.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (previous.TryAdd(next, current)) pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == roomType && previous.ContainsKey(node.Id)).Id;
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
        Check(map.CurrentNode == target && map.CurrentRoomHunted == hunted, "Real room-entry transitions establish the requested root.");
        return map;
    }

    private static bool SkipProfile() => false;
    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
    private static void Ok(JigsawResult result) => NativeEventTests.Check(result.Success, result.Reason);
}
