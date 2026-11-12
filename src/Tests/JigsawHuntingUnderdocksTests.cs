using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawHuntingUnderdocksTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw hunting Underdocks Gardeners open with fixed nine and preserve slots moves and other Skittish sources", Gardeners),
      ("Jigsaw hunting Underdocks Skulking Colony keeps fixed shell twenty adds plating six and preserves native health and later sources", Colony),
      ("Jigsaw hunting Underdocks Terror Eel adds weakness after original vulnerable to exactly the native targets", Eel),
      ("Jigsaw hunting Underdocks Matriarch halves native scaled maximum and current health without waking", MatriarchHealth) ];

    private static async Task MatriarchHealth()
    {
        var ordinary = new Dictionary<(int Players, int Ascension), (int Health, int Plating)>();
        foreach (int ascension in new[] { 0, 7, 8 })
        for (int count = 1; count <= 4; count++)
        foreach (bool hunted in new[] { false, true })
        {
            var players = Enumerable.Range(1, count).Select(id =>
                Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            var native = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Underdocks>()],
                ascensionLevel: ascension, seed: "HUNTING84_MATRIARCH_HEALTH");
            var manager = RunManager.Instance;
            ulong? previousId = LocalContext.NetId;
            manager.SetUpTest(native, new NetSingleplayerGameService()); LocalContext.NetId = 1;
            try
            {
                var encounter = ModelDb.AllEncounters.Single(model =>
                    model.AllPossibleMonsters.Any(monster => monster is LagavulinMatriarch)).ToMutable();
                Check(encounter.RoomType == RoomType.Boss, "Use the actual native Matriarch Boss identity.");
                var map = Root(hunted, JigsawRoom.Boss);
                JigsawCheckpoint.Capture(native, map); JigsawNativeSession.Restore(native);
                native.AppendToMapPointHistory(MapPointType.Boss, RoomType.Boss, encounter.Id);
                for (int index = 1; index < players.Length; index++) native.AddPlayerDebug(players[index], index);
                int historyStart = CombatManager.Instance.History.Entries.Count();
                using (HeadlessCombatInProgress.EnterEvent())
                    await manager.EnterRoom(new CombatRoom(encounter, native));
                var room = (CombatRoom)native.CurrentRoom!;
                var creature = room.CombatState.Enemies.Single(enemy => enemy.Monster is LagavulinMatriarch);
                var matriarch = (LagavulinMatriarch)creature.Monster!;
                int nativeBase = ascension < 8 ? 222 : 233;
                Check(matriarch.MinInitialHp == nativeBase && matriarch.MaxInitialHp == nativeBase,
                    "The native A0/A7/A8 health threshold remains unchanged before multiplayer creature scaling.");
                if (!hunted)
                {
                    Check(creature.MaxHp == (int)MegaCrit.Sts2.Core.Entities.Creatures.Creature.ScaleHpForMultiplayer(
                        nativeBase, encounter, count, native.CurrentActIndex),
                        "The ordinary control uses the original native multiplayer-scaled health once.");
                    ordinary[(count, ascension)] = (creature.MaxHp, creature.GetPowerAmount<PlatingPower>());
                }
                var baseline = ordinary[(count, ascension)];
                int expected = hunted ? baseline.Health / 2 : baseline.Health;
                Check(creature.MaxHp == expected && creature.CurrentHp == expected,
                    $"players={count}, A{ascension}, hunted={hunted}: halve native scaled maximum/current HP with floor, expected={expected}, actual={creature.MaxHp}/{creature.CurrentHp}.");
                Check(!matriarch.IsAwake && !matriarch.IsShellAwake && matriarch.NextMove.Id == "SLEEP_MOVE" &&
                    creature.GetPowerAmount<AsleepPower>() == 3 && creature.GetPowerAmount<PlatingPower>() == baseline.Plating,
                    "Health adjustment causes no wake-up and preserves the native sleeping move, Asleep and normal Plating scaling.");
                Check(!CombatManager.Instance.History.Entries.Skip(historyStart).OfType<DamageReceivedEntry>().Any(),
                    "The initial health adjustment never executes a damage command or records a damage event.");
            }
            finally
            {
                CombatManager.Instance.Reset(true); manager.CleanUp(false); LocalContext.NetId = previousId;
            }
        }
    }

    private static async Task Eel()
    {
        for (int count = 1; count <= 4; count++)
        foreach (bool hunted in new[] { false, true })
        {
            var players = Enumerable.Range(1, count).Select(id =>
                Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            var native = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Underdocks>()], seed: "HUNTING84_EEL");
            var manager = RunManager.Instance;
            ulong? previousId = LocalContext.NetId;
            manager.SetUpTest(native, new NetSingleplayerGameService());
            LocalContext.NetId = 1;
            try
            {
                var encounter = ModelDb.AllEncounters.First(model =>
                    model.AllPossibleMonsters.Any(monster => monster is TerrorEel)).ToMutable();
                var map = Root(hunted, encounter.RoomType == RoomType.Elite ? JigsawRoom.Elite : JigsawRoom.Combat);
                JigsawCheckpoint.Capture(native, map); JigsawNativeSession.Restore(native);
                native.AppendToMapPointHistory(encounter.RoomType == RoomType.Elite ? MapPointType.Elite : MapPointType.Monster,
                    encounter.RoomType, encounter.Id);
                for (int index = 1; index < players.Length; index++) native.AddPlayerDebug(players[index], index);
                using (HeadlessCombatInProgress.EnterEvent())
                    await manager.EnterRoom(new CombatRoom(encounter, native));
                var room = (CombatRoom)native.CurrentRoom!;
                var eel = (TerrorEel)room.CombatState.Enemies.First(enemy => enemy.Monster is TerrorEel).Monster!;
                var target = players[^1].Creature;
                foreach (bool artifact in new[] { false, true })
                {
                    if (target.HasPower<VulnerablePower>()) await PowerCmd.Remove(target.GetPower<VulnerablePower>());
                    if (target.HasPower<WeakPower>()) await PowerCmd.Remove(target.GetPower<WeakPower>());
                    if (artifact) await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), target, 1, target, null);
                    int historyStart = CombatManager.Instance.History.Entries.Count();
                    await eel.TerrorState.PerformMove([target]);
                    Check(target.GetPowerAmount<VulnerablePower>() == (artifact ? 0 : 99) &&
                        target.GetPowerAmount<WeakPower>() == (hunted ? 99 : 0) && !target.HasPower<ArtifactPower>(),
                        $"players={count}, hunted={hunted}, artifact={artifact}: original Vulnerable99 resolves before hunted Weak99; actual={target.GetPowerAmount<VulnerablePower>()}/{target.GetPowerAmount<WeakPower>()}.");
                    var received = CombatManager.Instance.History.Entries.Skip(historyStart).OfType<PowerReceivedEntry>()
                        .Where(entry => entry.Power is VulnerablePower or WeakPower).ToArray();
                    Check(received.All(entry => ReferenceEquals(entry.Power.Owner, target) && entry.Amount == 99) &&
                        received.Select(entry => entry.Power.GetType()).SequenceEqual(
                            (artifact ? Array.Empty<Type>() : new[] { typeof(VulnerablePower) })
                            .Concat(hunted ? new[] { typeof(WeakPower) } : Array.Empty<Type>())),
                        "Actual history keeps the same explicit native target subset and original-then-added command order.");
                    Check(players.Take(players.Length - 1).All(player =>
                        !player.Creature.HasPower<VulnerablePower>() && !player.Creature.HasPower<WeakPower>()),
                        "Players outside the original target subset receive neither debuff.");
                }
            }
            finally
            {
                CombatManager.Instance.Reset(true); manager.CleanUp(false); LocalContext.NetId = previousId;
            }
        }
    }

    private static async Task Colony()
    {
        var safeHealth = new Dictionary<int, int>();
        var safeLater = new Dictionary<int, (int Shell, int Plating, decimal ShellHistory, decimal PlatingHistory)>();
        for (int count = 1; count <= 4; count++)
        foreach (bool hunted in new[] { false, true })
        {
            var players = Enumerable.Range(1, count).Select(id =>
                Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            var native = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Underdocks>()], seed: "HUNTING84_COLONY");
            var manager = RunManager.Instance;
            ulong? previousId = LocalContext.NetId;
            manager.SetUpTest(native, new NetSingleplayerGameService());
            LocalContext.NetId = 1;
            try
            {
                var encounter = ModelDb.AllEncounters.First(model =>
                    model.AllPossibleMonsters.Any(monster => monster is SkulkingColony)).ToMutable();
                Check(encounter.RoomType is RoomType.Monster or RoomType.Elite,
                    "Use the native Colony encounter's ordinary or elite room identity.");
                var map = Root(hunted, encounter.RoomType == RoomType.Elite ? JigsawRoom.Elite : JigsawRoom.Combat);
                JigsawCheckpoint.Capture(native, map);
                JigsawNativeSession.Restore(native);
                native.AppendToMapPointHistory(encounter.RoomType == RoomType.Elite ? MapPointType.Elite : MapPointType.Monster,
                    encounter.RoomType, encounter.Id);
                for (int index = 1; index < players.Length; index++) native.AddPlayerDebug(players[index], index);
                int historyStart = CombatManager.Instance.History.Entries.Count();
                using (HeadlessCombatInProgress.EnterEvent())
                    await manager.EnterRoom(new CombatRoom(encounter, native));
                var room = (CombatRoom)native.CurrentRoom!;
                var colony = room.CombatState.Enemies.Single(enemy => enemy.Monster is SkulkingColony);
                var opening = CombatManager.Instance.History.Entries.Skip(historyStart).OfType<PowerReceivedEntry>()
                    .Where(entry => ReferenceEquals(entry.Power.Owner, colony)).ToArray();
                if (!hunted) safeHealth[count] = colony.MaxHp;
                Check(colony.MaxHp == safeHealth[count] && colony.CurrentHp == colony.MaxHp &&
                    colony.Monster!.MinInitialHp == 75 && colony.Monster.NextMove.Id == "ZOOM_MOVE",
                    "The unmodified native A0 health, multiplayer HP scaling and first Zoom move survive the hunted opening; twenty is shell, not HP.");
                if (hunted)
                {
                    Check(colony.GetPowerAmount<HardenedShellPower>() == 20 && colony.GetPowerAmount<PlatingPower>() == 6,
                        $"players={count}: hunted Colony opens with fixed shell20/plating6; actual={colony.GetPowerAmount<HardenedShellPower>()}/{colony.GetPowerAmount<PlatingPower>()}.");
                    Check(opening.Count(entry => entry.Power is HardenedShellPower && entry.Amount == 20) == 1 &&
                        opening.Count(entry => entry.Power is PlatingPower && entry.Amount == 6) == 1 &&
                        opening.Count(entry => entry.Power is HardenedShellPower or PlatingPower) == 2,
                        "The two fixed opening amounts are each one actual native grant, never a scaled grant followed by a correction.");
                }
                else
                    Check(colony.GetPowerAmount<PlatingPower>() == 0 && opening.Count(entry => entry.Power is HardenedShellPower) == 1,
                        "The safe native initializer retains its shell grant and does not gain hunted Plating.");

                await PowerCmd.Remove(colony.GetPower<HardenedShellPower>());
                if (colony.HasPower<PlatingPower>()) await PowerCmd.Remove(colony.GetPower<PlatingPower>());
                historyStart = CombatManager.Instance.History.Entries.Count();
                await PowerCmd.Apply<HardenedShellPower>(new ThrowingPlayerChoiceContext(), colony, 3, players[0].Creature, null);
                await PowerCmd.Apply<PlatingPower>(new ThrowingPlayerChoiceContext(), colony, 3, players[0].Creature, null);
                var later = CombatManager.Instance.History.Entries.Skip(historyStart).OfType<PowerReceivedEntry>()
                    .Where(entry => ReferenceEquals(entry.Power.Owner, colony)).ToArray();
                var actual = (colony.GetPowerAmount<HardenedShellPower>(), colony.GetPowerAmount<PlatingPower>(),
                    later.Single(entry => entry.Power is HardenedShellPower).Amount,
                    later.Single(entry => entry.Power is PlatingPower).Amount);
                if (!hunted) safeLater[count] = actual;
                Check(actual == safeLater[count],
                    "Separate shell and Plating sources to the same hunted target retain exactly their ordinary multiplayer amounts and native history.");
            }
            finally
            {
                CombatManager.Instance.Reset(true);
                manager.CleanUp(false);
                LocalContext.NetId = previousId;
            }
        }
    }

    private static async Task Gardeners()
    {
        int[] nativeOpening = [6, 9, 12, 15];
        int[] nativeLaterAmount = [3, 4, 6, 7];
        decimal[] nativeLaterHistory = [3m, 4.5m, 6m, 7.5m];
        var moves = new Dictionary<string, string>
        {
            ["first"] = "FLAIL_MOVE", ["second"] = "BITE_MOVE",
            ["third"] = "LASH_MOVE", ["fourth"] = "ENLARGE_MOVE"
        };
        for (int count = 1; count <= 4; count++)
        foreach (bool hunted in new[] { false, true })
        {
            var players = Enumerable.Range(1, count).Select(id =>
                Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            var native = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Underdocks>()], seed: "HUNTING84_GARDENERS");
            var manager = RunManager.Instance;
            ulong? previousId = LocalContext.NetId;
            manager.SetUpTest(native, new NetSingleplayerGameService());
            LocalContext.NetId = 1;
            try
            {
                var encounter = ModelDb.AllEncounters.First(model => model.RoomType == RoomType.Elite &&
                    model.AllPossibleMonsters.Any(monster => monster is PhantasmalGardener)).ToMutable();
                var map = Root(hunted, JigsawRoom.Elite);
                JigsawCheckpoint.Capture(native, map);
                JigsawNativeSession.Restore(native);
                // A legal single-owner session is established before adding native
                // combat participants. This is 1–4 command input, not networking.
                native.AppendToMapPointHistory(MapPointType.Elite, RoomType.Elite, encounter.Id);
                for (int index = 1; index < players.Length; index++) native.AddPlayerDebug(players[index], index);
                int historyStart = CombatManager.Instance.History.Entries.Count();
                using (HeadlessCombatInProgress.EnterEvent())
                    await manager.EnterRoom(new CombatRoom(encounter, native));
                var room = (CombatRoom)native.CurrentRoom!;
                var gardeners = room.CombatState.Enemies.Where(enemy => enemy.Monster is PhantasmalGardener).ToArray();
                Check(gardeners.Length == 4 && gardeners.Select(enemy => enemy.SlotName).ToHashSet().SetEquals(moves.Keys),
                    "The native elite encounter creates its four original Gardeners in their original named slots.");
                var openingEntries = CombatManager.Instance.History.Entries.Skip(historyStart)
                    .OfType<PowerReceivedEntry>().Where(entry => entry.Power is SkittishPower).ToArray();
                foreach (var gardener in gardeners)
                {
                    int expected = hunted ? 9 : nativeOpening[count - 1];
                    Check(gardener.GetPowerAmount<SkittishPower>() == expected,
                        $"players={count}, hunted={hunted}: native Gardener opening Skittish must be {expected}; actual={gardener.GetPowerAmount<SkittishPower>()}.");
                    Check(gardener.Monster!.NextMove.Id == moves[gardener.SlotName!],
                        "Each native Gardener retains the first move selected by its original slot.");
                    var received = openingEntries.Where(entry => ReferenceEquals(entry.Power.Owner, gardener)).ToArray();
                    Check(received.Length == 1 && received[0].Amount == expected,
                        "Each Gardener receives its opening amount in one native grant, with no larger grant then correction.");
                }

                var target = gardeners[0];
                await PowerCmd.Remove(target.GetPower<SkittishPower>());
                historyStart = CombatManager.Instance.History.Entries.Count();
                await PowerCmd.Apply<SkittishPower>(new ThrowingPlayerChoiceContext(), target, 3, players[0].Creature, null);
                var later = CombatManager.Instance.History.Entries.Skip(historyStart)
                    .OfType<PowerReceivedEntry>().Where(entry => entry.Power is SkittishPower && ReferenceEquals(entry.Power.Owner, target)).ToArray();
                Check(target.GetPowerAmount<SkittishPower>() == nativeLaterAmount[count - 1] &&
                    later.Length == 1 && later[0].Amount == nativeLaterHistory[count - 1],
                    "A separate native Skittish source to the same hunted creature retains normal multiplayer scaling and history.");
            }
            finally
            {
                CombatManager.Instance.Reset(true);
                manager.CleanUp(false);
                LocalContext.NetId = previousId;
            }
        }
    }

    private static JigsawRun Root(bool hunted, JigsawRoom kind)
    {
        var map = JigsawRun.Create(75, players: [1UL]);
        Ok(map.ChooseEnd(1, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        Ok(map.CompleteAncient(1)); Ok(map.Continue(1));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var pending = new Queue<int>(); pending.Enqueue(0);
        while (pending.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if ((map.Nodes.Single(node => node.Id == next).Room == kind ||
                     map.Nodes.Single(node => node.Id == next).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss)) &&
                    previous.TryAdd(next, current)) pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == kind && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        Ok(map.SetRemainingSteps(hunted ? 0 : 100));
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!; Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        Check(map.CurrentRoomHunted == hunted, "Legal domain entry establishes the safe or hunted root snapshot.");
        return map;
    }

    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Check(bool condition, string message) => NativeEventTests.Check(condition, message);
}
