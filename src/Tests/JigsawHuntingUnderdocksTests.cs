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
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;
using System.Text.Json;

internal static class JigsawHuntingUnderdocksTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw hunting Underdocks Gardeners open with fixed nine and preserve slots moves and other Skittish sources", Gardeners),
      ("Jigsaw hunting Underdocks Skulking Colony keeps fixed shell twenty adds plating six and preserves native health and later sources", Colony),
      ("Jigsaw hunting Underdocks Terror Eel adds weakness after original vulnerable to exactly the native targets", Eel),
      ("Jigsaw hunting Underdocks Matriarch halves native scaled maximum and current health without waking", MatriarchHealth),
      ("Jigsaw hunting Underdocks Matriarch adds one sleeping Lagavulin minion on the left", MatriarchMinion),
      ("Jigsaw hunting Underdocks Matriarch minion excludes safe event non Jigsaw and independent Lagavulin sources", MatriarchMinionBoundaries),
      ("Jigsaw hunting Underdocks Matriarch no acts native generation keeps the original single Boss", MatriarchNoActs) ];

    private static Task MatriarchNoActs()
    {
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
        var native = RunState.CreateForTest([player], acts: [], seed: "HUNTING84_MATRIARCH_NO_ACTS");
        var manager = RunManager.Instance;
        manager.SetUpTest(native, new NetSingleplayerGameService());
        try
        {
            var encounter = ModelDb.AllEncounters.Single(model =>
                model.AllPossibleMonsters.Any(monster => monster is LagavulinMatriarch)).ToMutable();
            native.PushRoom(new CombatRoom(encounter, native));
            Check(JigsawNativeSession.TryGet(native) == null, "The native generator has no Jigsaw session and no acts.");
            encounter.GenerateMonstersWithSlots(native);
            Check(encounter.MonstersWithSlots.Count == 1 && encounter.MonstersWithSlots[0].Item1 is LagavulinMatriarch &&
                encounter.MonstersWithSlots[0].Item2 == null,
                "A genuine no-act native Matriarch generation remains one original unslotted Boss, without accessing a nonexistent act.");
            return Task.CompletedTask;
        }
        finally
        {
            CombatManager.Instance.Reset(true); manager.CleanUp(false);
        }
    }

    private static async Task MatriarchMinion()
    {
        var openings = new Dictionary<int, string>();
        for (int count = 1; count <= 4; count++)
        for (int replay = 0; replay <= 1; replay++)
        {
            var players = Enumerable.Range(1, count).Select(id =>
                Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            var native = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Underdocks>()], seed: "HUNTING84_MATRIARCH_MINION");
            var manager = RunManager.Instance;
            ulong? previousId = LocalContext.NetId;
            manager.SetUpTest(native, new NetSingleplayerGameService()); LocalContext.NetId = 1;
            try
            {
                var encounter = ModelDb.AllEncounters.Single(model =>
                    model.AllPossibleMonsters.Any(monster => monster is LagavulinMatriarch)).ToMutable();
                JigsawCheckpoint.Capture(native, Root(true, JigsawRoom.Boss)); JigsawNativeSession.Restore(native);
                native.AppendToMapPointHistory(MapPointType.Boss, RoomType.Boss, encounter.Id);
                for (int index = 1; index < players.Length; index++) native.AddPlayerDebug(players[index], index);
                int historyStart = CombatManager.Instance.History.Entries.Count();
                using (HeadlessCombatInProgress.EnterEvent())
                    await manager.EnterRoom(new CombatRoom(encounter, native));
                var room = (CombatRoom)native.CurrentRoom!;
                Check(room.CombatState.Enemies.Count == 2,
                    $"The actual hunted Matriarch opening contains its original Boss and exactly one added Lagavulin; expected=2, actual={room.CombatState.Enemies.Count}.");
                var minion = room.CombatState.Enemies[0];
                var boss = room.CombatState.Enemies[1];
                Check(minion.Monster is AllMatters.Monsters.Lagavulin { StartsAwake: false, IsAwake: false } &&
                    boss.Monster is LagavulinMatriarch { IsAwake: false, IsShellAwake: false } &&
                    minion.GetPowerAmount<MinionPower>() == 1 && minion.IsSecondaryEnemy && boss.IsPrimaryEnemy &&
                    minion.GetPowerAmount<AllMatters.Powers.AsleepLagavulinPower>() == 3 &&
                    minion.Monster.NextMove.Id == AllMatters.Monsters.Lagavulin.SLEEP &&
                    boss.GetPowerAmount<AsleepPower>() == 3 && boss.Monster.NextMove.Id == "SLEEP_MOVE",
                    "The original Mod Lagavulin opens asleep with Minion one before the original sleeping Matriarch in native left-to-right order.");
                Check(encounter.MonstersWithSlots.Select(pair => pair.Item1).SequenceEqual(new[] { minion.Monster, boss.Monster }) &&
                    encounter.MonstersWithSlots.All(pair => pair.Item2 == null) && !encounter.HasScene &&
                    minion.SlotName == null && boss.SlotName == null,
                    "The unslotted native Boss encounter preserves the original Matriarch and uses list ordering for its left companion, without a synthetic scene marker.");
                Check(ReferenceEquals(minion.Monster!.RunRng, native.Rng) && ReferenceEquals(boss.Monster!.RunRng, native.Rng) &&
                    !ReferenceEquals(minion.Monster.Rng, boss.Monster.Rng) &&
                    room.CombatState.Players.Count == count,
                    "Native creature creation binds the shared run RNG and distinct monster RNGs for the actual one to four combat roles.");
                Check(minion.Monster.MinInitialHp == 109 && minion.Monster.MaxInitialHp == 111 &&
                    Enumerable.Range(109, 3).Any(hp => minion.MaxHp == (int)MegaCrit.Sts2.Core.Entities.Creatures.Creature.ScaleHpForMultiplayer(
                        hp, encounter, count, native.CurrentActIndex)) && minion.CurrentHp == minion.MaxHp,
                    $"roles={count}: the existing Lagavulin keeps its original HP range and exactly one native multiplayer scaling; actual={minion.MaxHp}.");
                int bossExpected = (int)MegaCrit.Sts2.Core.Entities.Creatures.Creature.ScaleHpForMultiplayer(
                    222, encounter, count, native.CurrentActIndex) / 2;
                Check(boss.MaxHp == bossExpected && boss.CurrentHp == bossExpected,
                    $"roles={count}: adding a minion preserves the Matriarch's native scaled and then halved full health, expected={bossExpected}, actual={boss.CurrentHp}/{boss.MaxHp}.");
                var grants = CombatManager.Instance.History.Entries.Skip(historyStart).OfType<PowerReceivedEntry>()
                    .Where(entry => entry.Power is MinionPower).ToArray();
                Check(grants.Length == 1 && ReferenceEquals(grants[0].Actor, minion) && grants[0].Amount == 1 &&
                    minion.HasPower<AllMatters.Powers.MetallicizePower>() &&
                    !CombatManager.Instance.History.Entries.Skip(historyStart).OfType<DamageReceivedEntry>().Any(),
                    "The exact added creature receives one fixed Minion grant after its normal sleeping powers, without damage or wake-up.");
                Check(encounter.GetAssetPaths(native).Contains("res://AllMatters/monsters/lagavulin/lagavulin.tscn"),
                    "The actual encounter asset enumeration includes the existing Mod Lagavulin scene before native combat preloading.");
                string opening = JigsawHuntingCombatTests.Opening(room) + JsonSerializer.Serialize(native.Rng.ToSerializable());
                if (replay == 0) openings[count] = opening;
                else Check(opening == openings[count],
                    "Repeating the same actual room entry seed reproduces HP, powers, moves, individual monster RNG positions and the run RNG together.");
                bool rejected = false;
                try { encounter.GenerateMonstersWithSlots(native); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && encounter.MonstersWithSlots.Count == 2 && room.CombatState.Enemies.Count == 2,
                    "The native repeat-generation guard still rejects a second generation without another companion.");
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), boss, boss.CurrentHp,
                    ValueProp.Unblockable | ValueProp.Unpowered, players[0].Creature);
                Check(boss.IsDead && minion.IsDead && !room.CombatState.ContainsCreature(boss) &&
                    !room.CombatState.ContainsCreature(minion),
                    "Actual lethal damage to the Matriarch runs native primary-enemy death and removes the still-sleeping Minion naturally.");
            }
            finally
            {
                CombatManager.Instance.Reset(true); manager.CleanUp(false); LocalContext.NetId = previousId;
            }
        }
    }

    private static async Task MatriarchMinionBoundaries()
    {
        foreach (var scope in new[] { "safe", "event", "non-Jigsaw", "independent" })
        {
            var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
            var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Underdocks>()], seed: "HUNTING84_MATRIARCH_MINION_BOUNDARIES");
            var manager = RunManager.Instance;
            ulong? previousId = LocalContext.NetId;
            manager.SetUpTest(native, new NetSingleplayerGameService()); LocalContext.NetId = 1;
            try
            {
                var encounter = ModelDb.AllEncounters.Single(model =>
                    model.AllPossibleMonsters.Any(monster => monster is LagavulinMatriarch)).ToMutable();
                var parent = scope == "event" ? ModelDb.Event<InfestedAutomaton>() : null;
                if (parent != null)
                {
                    manager.GenerateRooms(); await manager.GenerateMap();
                    native.AddVisitedMapCoord(native.Map.StartingMapPoint.coord);
                }
                if (scope != "non-Jigsaw")
                {
                    JigsawCheckpoint.Capture(native, Root(scope != "safe", scope == "event" ? JigsawRoom.Unknown : JigsawRoom.Boss));
                    JigsawNativeSession.Restore(native);
                }
                using (HeadlessCombatInProgress.EnterEvent())
                {
                    if (parent != null)
                    {
                        native.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, parent.Id);
                        await manager.EnterRoom(new EventRoom(parent));
                        await manager.EnterRoomWithoutExitingCurrentRoom(
                            new CombatRoom(encounter, native) { ParentEventId = parent.Id }, fadeToBlack: false);
                    }
                    else
                    {
                        native.AppendToMapPointHistory(MapPointType.Boss, RoomType.Boss, encounter.Id);
                        await manager.EnterRoom(new CombatRoom(encounter, native));
                    }
                }
                var room = (CombatRoom)native.CurrentRoom!;
                bool huntedBoss = scope == "independent";
                bool huntedEvent = scope == "event";
                Check(room.CombatState.Enemies.Count == (huntedBoss || huntedEvent ? 2 : 1) &&
                    room.CombatState.Enemies.Count(creature => creature.Monster is Seapunk) == (huntedEvent ? 1 : 0) &&
                    room.CombatState.Enemies.Where(creature => creature.Monster is Seapunk).All(creature =>
                        creature.IsPrimaryEnemy && !creature.HasPower<MinionPower>()) &&
                    room.CombatState.Enemies.Count(creature => creature.Monster is AllMatters.Monsters.Lagavulin) == (huntedBoss ? 1 : 0),
                    $"scope={scope}: only the hunted top-level Jigsaw Boss receives one Lagavulin; actual enemies={room.CombatState.Enemies.Count}.");
                Check(scope != "event" || native.CurrentRoomCount == 2 && room.ParentEventId != null && JigsawHuntingNative.RootIsHunted(player),
                    "The event counterexample is an actually hunted child Boss, rather than a safe run.");
                Check(scope != "non-Jigsaw" || JigsawNativeSession.TryGet(native) == null,
                    "The non-Jigsaw counterexample has no bound domain session.");
                var independent = await CreatureCmd.Add(ModelDb.Monster<AllMatters.Monsters.Lagavulin>().ToMutable(), room.CombatState);
                Check(!independent.HasPower<MinionPower>() && independent.IsPrimaryEnemy &&
                    independent.GetPowerAmount<AllMatters.Powers.AsleepLagavulinPower>() == 3 &&
                    independent.Monster!.NextMove.Id == AllMatters.Monsters.Lagavulin.SLEEP,
                    $"scope={scope}: an independent native Add of the same Mod Lagavulin retains its normal sleeping, primary-enemy identity.");
                Check(room.CombatState.Enemies.Count(creature => creature.HasPower<MinionPower>()) == (huntedBoss ? 1 : 0),
                    "Adding another source neither grants it Minion nor changes the previously added companion.");
            }
            finally
            {
                CombatManager.Instance.Reset(true); manager.CleanUp(false); LocalContext.NetId = previousId;
            }
        }
    }

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
