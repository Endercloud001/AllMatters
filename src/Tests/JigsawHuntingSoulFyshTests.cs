using AllMatters.Jigsaw;
using AllMatters;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.addons.mega_text;

using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using System.Text.Json;

internal static class JigsawHuntingSoulFyshTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw SoulFysh hunted Boss opening one role and natural minion cleanup", () => Opening(1, JigsawRoom.Boss)),
     ("Jigsaw SoulFysh hunted Boss opening four roles and natural minion cleanup", () => Opening(4, JigsawRoom.Boss)),
     ("Jigsaw SoulFysh hunted second Boss opening one role and natural minion cleanup", () => Opening(1, JigsawRoom.SecondBoss)),
     ("Jigsaw SoulFysh hunted second Boss opening four roles and natural minion cleanup", () => Opening(4, JigsawRoom.SecondBoss)),
     ("Jigsaw SoulFysh excludes safe event unrelated roots and independently added native monsters", Boundaries),
     ("Jigsaw SoulFysh original initializer await rejects stale identities and preserves dependency failures", AwaitedOpening),
     ("Jigsaw SoulFysh native layout one role compact actual input", () => Layout(1, new Vector2I(1280, 720))),
     ("Jigsaw SoulFysh native layout one role wide actual input", () => Layout(1, new Vector2I(1680, 720))),
     ("Jigsaw SoulFysh native layout four roles compact actual input", () => Layout(4, new Vector2I(1280, 720))),
     ("Jigsaw SoulFysh native layout four roles wide actual input", () => Layout(4, new Vector2I(1680, 720)))];

    private static async Task Opening(int count, JigsawRoom kind)
    {
        var openings = new Dictionary<(int, JigsawRoom), string>();
        for (int replay = 0; replay < 2; replay++)
        {
        var players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
        var player = players[0];
        var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Underdocks>()], seed: "HUNTING84_SOULFYSH_MINIONS");
        var manager = RunManager.Instance;
        ulong? previousId = LocalContext.NetId;
        manager.SetUpTest(native, new NetSingleplayerGameService());
        LocalContext.NetId = 1;
        try
        {
            var encounter = ModelDb.Encounter<SoulFyshBoss>().ToMutable();
            JigsawCheckpoint.Capture(native, Root(kind: kind));
            JigsawNativeSession.Restore(native);
            native.AppendToMapPointHistory(MapPointType.Boss, RoomType.Boss, encounter.Id);
            for (int index = 1; index < players.Length; index++) native.AddPlayerDebug(players[index], index);
            int historyStart = CombatManager.Instance.History.Entries.Count();
            using (HeadlessCombatInProgress.EnterEvent())
                await manager.EnterRoom(new CombatRoom(encounter, native));
            var room = (CombatRoom)native.CurrentRoom!;
            var combat = room.CombatState;
            Check(combat.Enemies.Count == 3,
                $"Actual hunted SoulFyshBoss opening must contain exactly left Toadpole, original SoulFysh and right SludgeSpinner; expected=3, actual={combat.Enemies.Count}.");
            var left = combat.Enemies[0];
            var boss = combat.Enemies[1];
            var right = combat.Enemies[2];
            Check(left.Monster is Toadpole { IsFront: false } && boss.Monster is SoulFysh && right.Monster is SludgeSpinner &&
                left.GetPowerAmount<MinionPower>() == 1 && right.GetPowerAmount<MinionPower>() == 1 &&
                left.IsSecondaryEnemy && right.IsSecondaryEnemy && boss.IsPrimaryEnemy && !boss.HasPower<MinionPower>(),
                "Both added original native monsters open as Minion one in native left-to-right list order; left positioning does not change Toadpole's original IsFront false.");
            Check(!encounter.HasScene && encounter.MonstersWithSlots.All(pair => pair.Item2 == null) &&
                encounter.MonstersWithSlots.Select(pair => pair.Item1).SequenceEqual(new[] { left.Monster, boss.Monster, right.Monster }) &&
                left.Monster!.NextMove.Id == "WHIRL_MOVE" && boss.Monster!.NextMove.Id == "BECKON_MOVE" && right.Monster!.NextMove.Id == "OIL_SPRAY_MOVE",
                "The original unslotted Boss uses its native monster list and every original initial move, without synthetic scene slots or a changed Toadpole branch.");
            var grants = CombatManager.Instance.History.Entries.Skip(historyStart).OfType<PowerReceivedEntry>()
                .Where(entry => entry.Power is MinionPower).ToArray();
            Check(grants.Length == 2 && grants.All(entry => entry.Amount == 1) &&
                grants.Count(entry => ReferenceEquals(entry.Actor, left)) == 1 && grants.Count(entry => ReferenceEquals(entry.Actor, right)) == 1,
                "Each actual added monster receives exactly one original native Minion grant of one.");
            Check(combat.Players.Count == count &&
                new[] { left, boss, right }.All(creature => ReferenceEquals(creature.Monster!.RunRng, native.Rng)) &&
                new[] { left.Monster!.Rng, boss.Monster!.Rng, right.Monster!.Rng }.Distinct().Count() == 3,
                "The real one to four combat roles retain native shared run RNG and three independent monster RNG streams.");
            CheckHealth(left, encounter, count, 21, 25);
            CheckHealth(boss, encounter, count, 211, 211);
            CheckHealth(right, encounter, count, 37, 39);
            string snapshot = JigsawHuntingCombatTests.Opening(room) + JsonSerializer.Serialize(native.Rng.ToSerializable());
            if (replay == 0) openings[(count, kind)] = snapshot;
            else Check(snapshot == openings[(count, kind)],
                "Repeated real entries with the same seed reproduce full ordered opening HP, powers, native moves and monster/run RNG together.");
            bool rejected = false;
            try { encounter.GenerateMonstersWithSlots(native); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && encounter.MonstersWithSlots.Count == 3 && combat.Enemies.Count == 3,
                "Native repeated-generation protection still rejects a second generation without another pair.");
            using var ending = new JigsawHuntingCombatTests.NativeEndingScope();
            Check(CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsEnding && left.IsAlive && right.IsAlive,
                "Native ending detection begins with one living primary Boss and two living secondary companions.");
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), boss, boss.CurrentHp,
                ValueProp.Unblockable | ValueProp.Unpowered, player.Creature);
            Check(boss.IsDead && left.IsDead && right.IsDead &&
                new[] { left, boss, right }.All(creature => !combat.ContainsCreature(creature)) && CombatManager.Instance.IsEnding,
                "Only actual lethal damage to the Boss naturally kills both still-living secondary enemies and exposes native Ending; the test never kills a companion.");
            Console.WriteLine($"SOULFYSH_NATIVE roles={count} root={kind} replay={replay} enemies=3 minion=1/1 hp={left.MaxHp}/{boss.MaxHp}/{right.MaxHp} ending=native");
        }
        finally
        {
            CombatManager.Instance.Reset(true);
            manager.CleanUp(false);
            LocalContext.NetId = previousId;
        }
        }
    }

    private static void CheckHealth(Creature creature, EncounterModel encounter, int count, int minimum, int maximum)
    {
        Check(creature.Monster!.MinInitialHp == minimum && creature.Monster.MaxInitialHp == maximum &&
            creature.CurrentHp == creature.MaxHp && Enumerable.Range(minimum, maximum - minimum + 1).Any(hp =>
                creature.MaxHp == (int)Creature.ScaleHpForMultiplayer(hp, encounter, count, 0)),
            $"roles={count}: {creature.Monster.Id} retains its original A0 HP range {minimum}..{maximum} and exactly one native multiplayer scaling; actual={creature.CurrentHp}/{creature.MaxHp}.");
    }

    private static async Task Boundaries()
    {
        foreach (string scope in new[] { "safe", "event", "non-Jigsaw", "other-act", "ordinary-root", "no-acts", "hunted" })
        {
            using var battle = new SoulBattle(4, scope);
            await battle.Initialize();
            bool hunted = scope == "hunted";
            Check(battle.Combat.Enemies.Count == (hunted ? 3 : 1) &&
                battle.Combat.Enemies.Count(enemy => enemy.Monster is SoulFysh) == 1 &&
                battle.Combat.Enemies.Count(enemy => enemy.HasPower<MinionPower>()) == (hunted ? 2 : 0),
                $"scope={scope}: only the actually hunted top-level Underdocks Boss gets its pair; actual={battle.Combat.Enemies.Count}.");
            Check(scope != "event" || battle.Run.CurrentRoomCount == 2 && battle.Run.BaseRoom is EventRoom &&
                battle.Run.CurrentRoom is CombatRoom { ParentEventId: not null } && JigsawHuntingNative.RootIsHunted(battle.Run.Players[0]),
                "The event counterexample has a real matching parent/child stack in a hunted root.");
            Check(scope != "non-Jigsaw" || JigsawNativeSession.TryGet(battle.Run) == null,
                "The non-Jigsaw counterexample has no bound domain session.");
            Check(scope != "other-act" || battle.Run.Act is Overgrowth,
                "The other-act counterexample genuinely belongs to Overgrowth.");
            Check(scope != "no-acts" || battle.Run.Acts.Count == 0 && JigsawNativeSession.TryGet(battle.Run) == null,
                "Native no-act generation never accesses a nonexistent act through the enhancement guard.");
            foreach (var prototype in new MonsterModel[] { ModelDb.Monster<Toadpole>(), ModelDb.Monster<SludgeSpinner>() })
            {
                var independent = await CreatureCmd.Add(prototype.ToMutable(), battle.Combat);
                Check(independent.IsPrimaryEnemy && !independent.HasPower<MinionPower>() &&
                    independent.Monster!.NextMove.Id == (prototype is Toadpole ? "WHIRL_MOVE" : "OIL_SPRAY_MOVE"),
                    $"scope={scope}: independent real CreatureCmd.Add of {prototype.Id} remains a native primary enemy with its original first move.");
            }
            Check(battle.Combat.Enemies.Count(enemy => enemy.HasPower<MinionPower>()) == (hunted ? 2 : 0),
                "Independent additions neither inherit nor alter the exact originally generated pair's Minion.");
        }
    }

    private sealed class OpeningPause(MonsterModel target)
    {
        internal readonly MonsterModel Target = target;
        internal readonly TaskCompletionSource Reached = new();
        internal readonly TaskCompletionSource Release = new();
        internal Exception? Failure;
    }
    private static OpeningPause? openingPause;
    private static void PauseInitializer(MonsterModel __instance, ref Task __result)
    {
        if (openingPause is { } probe && ReferenceEquals(probe.Target, __instance))
            __result = PauseOriginal(__result, probe);
    }
    private static async Task PauseOriginal(Task original, OpeningPause probe)
    {
        await original;
        probe.Reached.TrySetResult();
        await probe.Release.Task;
        if (probe.Failure != null) throw probe.Failure;
    }
    private static async Task AwaitedOpening()
    {
        var patch = new Harmony("AllMatters.Tests.SoulFyshAwait");
        // Wrap the actual inherited no-op native initializer before the Mod's
        // postfix sees its returned Task, rather than calling GrantOpening.
        patch.Patch(AccessTools.Method(typeof(MonsterModel), nameof(MonsterModel.AfterAddedToRoom)),
            postfix: new HarmonyMethod(typeof(JigsawHuntingSoulFyshTests), nameof(PauseInitializer)) { before = [ModEntry.ModId] });
        try
        {
            foreach (string boundary in new[] { "valid", "closed-session", "replacement-session", "room", "removed", "dead", "replacement-run", "detached-combat", "failure" })
            foreach (int index in new[] { 0, 2 })
            {
                using var battle = new SoulBattle();
                var monster = battle.Combat.Enemies[index].Monster!;
                var probe = new OpeningPause(monster);
                openingPause = probe;
                var operation = monster.AfterAddedToRoom();
                try
                {
                    await probe.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Check(!operation.IsCompleted && !monster.Creature.HasPower<MinionPower>(),
                        "The actual marked initializer is still awaiting its native dependency before the first Minion grant.");
                    if (boundary == "closed-session") JigsawNativeSession.TryGet(battle.Run)!.Close();
                    if (boundary == "replacement-session")
                    { JigsawCheckpoint.Capture(battle.Run, Root()); JigsawNativeSession.Restore(battle.Run); }
                    if (boundary == "room") battle.Run.PushRoom(new CombatRoom(battle.Combat));
                    if (boundary == "removed") battle.Combat.RemoveCreature(monster.Creature, unattach: false);
                    if (boundary == "dead") await CreatureCmd.Kill(monster.Creature);
                    if (boundary == "replacement-run")
                    {
                        RunManager.Instance.CleanUp(false);
                        RunManager.Instance.SetUpTest(RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                            acts: [ModelDb.Act<Underdocks>()], seed: "HUNTING84_SOULFYSH_REPLACEMENT"), new NetSingleplayerGameService());
                    }
                    if (boundary == "detached-combat")
                    { CombatManager.Instance.Reset(true); CombatManager.Instance.SetUpCombat(new CombatState(runState: battle.Run)); }
                    if (boundary == "failure") probe.Failure = new InvalidOperationException("Injected SoulFysh native initializer failure");
                    probe.Release.TrySetResult();
                    Exception? actual = null;
                    try { await operation; } catch (Exception error) { actual = error; }
                    Check(boundary == "failure" ? ReferenceEquals(actual, probe.Failure) : actual == null,
                        $"boundary={boundary}: the exact native dependency failure or successful result propagates unchanged.");
                    Check(monster.Creature.GetPowerAmount<MinionPower>() == (boundary == "valid" ? 1 : 0) && !JigsawHuntingSoulFysh.IsMarked(monster),
                        $"boundary={boundary}: only the still-current original source gains Minion; every original source token is consumed.");
                    openingPause = null;
                    await monster.AfterAddedToRoom();
                    Check(monster.Creature.GetPowerAmount<MinionPower>() == (boundary == "valid" ? 1 : 0),
                        "A repeated actual initializer cannot retry a consumed stale or failed source or double a successful grant.");
                }
                finally { probe.Release.TrySetResult(); try { await operation; } catch (Exception error) when (ReferenceEquals(error, probe.Failure)) { } openingPause = null; }
            }
        }
        finally { openingPause = null; patch.UnpatchAll(patch.Id); }
    }

    private sealed class SoulBattle : IDisposable
    {
        private readonly ulong? previousNetId = LocalContext.NetId;
        internal RunState Run { get; }
        internal CombatState Combat { get; }
        internal EncounterModel Encounter { get; }
        internal SoulBattle(int count = 1, string scope = "hunted")
        {
            var players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
            Run = RunState.CreateForTest([players[0]], acts: scope == "no-acts" ? [] :
                [scope == "other-act" ? ModelDb.Act<Overgrowth>() : ModelDb.Act<Underdocks>()], seed: "HUNTING84_SOULFYSH_NATIVE");
            Encounter = ModelDb.Encounter<SoulFyshBoss>().ToMutable();
            Combat = new CombatState(Encounter, Run) { RoundNumber = 1 };
            try
            {
                RunManager.Instance.SetUpTest(Run, new NetSingleplayerGameService());
                LocalContext.NetId = 1;
                CombatManager.Instance.Reset(true);
                Combat.AddPlayer(players[0]); players[0].ResetCombatState();
                CombatManager.Instance.SetUpCombat(Combat);
                foreach (var player in players.Skip(1)) { player.RunState = Run; Combat.AddPlayer(player); player.ResetCombatState(); }
                var parent = ModelDb.Event<InfestedAutomaton>();
                if (scope == "event") Run.PushRoom(new EventRoom(parent));
                Run.PushRoom(new CombatRoom(Combat) { ParentEventId = scope == "event" ? parent.Id : null });
                if (scope is not ("non-Jigsaw" or "no-acts"))
                {
                    JigsawCheckpoint.Capture(Run, Root(scope != "safe", scope is "ordinary-root" or "event" ? JigsawRoom.Unknown : JigsawRoom.Boss));
                    JigsawNativeSession.Restore(Run);
                }
                var projection = JigsawEntryProjection.Append(Run.Map, 1, JigsawRoom.Boss);
                Run.Map = projection.Map;
                Check(Run.AddVisitedMapCoord(projection.Point.coord), "The real SoulFysh encounter has a visited native coordinate for its original RNG initialization.");
                Encounter.GenerateMonstersWithSlots(Run);
                foreach (var (monster, slot) in Encounter.MonstersWithSlots)
                {
                    Combat.AddCreature(Combat.CreateCreature(monster, CombatSide.Enemy, slot));
                    CombatManager.Instance.AddCreature(monster.Creature);
                    monster.RollMove(Combat.PlayerCreatures);
                }
            }
            catch { Dispose(); throw; }
        }
        internal async Task Initialize()
        {
            foreach (var creature in Combat.Creatures.ToArray()) await CombatManager.Instance.AfterCreatureAdded(creature);
        }
        public void Dispose()
        {
            JigsawNativeSession.TryGet(Run)?.Close();
            CombatManager.Instance.Reset(true); RunManager.Instance.CleanUp(false); LocalContext.NetId = previousNetId;
        }
    }

    private static string context = "not mounted";
    private static NCombatRoom? combatNode;
    private static bool SkipLifecycle() => false;
    private static bool SkipAudio() => false;
    private static bool CreatureNode(Creature __instance, ref NCreature? __result)
    {
        __result = combatNode?.GetCreatureNode(__instance);
        return false;
    }
    private static async Task Layout(int count, Vector2I physical)
    {
            var safePlayers = await SafePlayerRects(count, physical);
            using var battle = new SoulBattle(count);
            using var host = new NativeHost();
            await host.Mount(battle);
            await battle.Initialize();
            var room = combatNode!;
            var enemies = battle.Combat.Enemies.ToArray();
            Check(enemies.Length == 3 && enemies[0].Monster is Toadpole && enemies[1].Monster is SoulFysh && enemies[2].Monster is SludgeSpinner &&
                !battle.Encounter.HasScene && enemies.All(enemy => enemy.SlotName == null),
                "Formal NCombatRoom mounts the same real native ordered trio with automatic placement, without authored or synthetic markers.");
            await Until(() => room.CreatureNodes.Where(node => !node.Entity.IsPlayer || LocalContext.IsMe(node.Entity))
                .All(node => node.GetNode<Control>("%HealthBar").Modulate.A > 0.99999f), "actual opening HP fade");
                context = $"roles={count} physical={physical}";
                host.Window.Size = physical;
                room.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                await Until(() => enemies.All(creature => room.GetCreatureNode(creature) is { Visible: true }), "all three actual native enemy nodes");
                foreach (var enemy in enemies) await room.GetCreatureNode(enemy)!.RefreshIntents();
                await Until(() => enemies.All(creature => room.GetCreatureNode(creature)!.IntentContainer.Modulate.A > 0.95f), "native intent fade");
                await Frames(4);
                var nodes = enemies.Select(enemy => room.GetCreatureNode(enemy)!).OrderBy(node => node.GlobalPosition.X).ToArray();
                Check(nodes[0].Entity.Monster is Toadpole && nodes[1].Entity.Monster is SoulFysh && nodes[2].Entity.Monster is SludgeSpinner,
                    context + ": real scene X positions preserve Toadpole left, same original SoulFysh center and Spinner right.");
                CheckGeometry(room, nodes, count, physical, safePlayers);
                await CheckMouse(host.Target, nodes);
                await CheckController(host.Controller, room, nodes);
    }

    private static async Task<Dictionary<ulong, Rect2>> SafePlayerRects(int count, Vector2I physical)
    {
        using var safe = new SoulBattle(count, "safe");
        using var host = new NativeHost();
        await host.Mount(safe);
        await safe.Initialize();
        host.Window.Size = physical;
        await Frames(4);
        var room = combatNode!;
        Check(safe.Combat.Enemies.Count == 1 && safe.Combat.Enemies[0].Monster is SoulFysh,
            "The unenhanced Soul Fysh scene supplies the native player layout baseline.");
        return room.CreatureNodes.Where(node => node.Entity.IsPlayer)
            .ToDictionary(node => node.Entity.Player!.NetId, node => node.Hitbox.GetGlobalRect());
    }

    private static void CheckGeometry(NCombatRoom room, NCreature[] enemies, int count, Vector2I physical,
        Dictionary<ulong, Rect2> safePlayers)
    {
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        var logical = viewport.GetVisibleRect();
        var stretch = viewport.GetStretchTransform();
        var physicalBounds = new Rect2(Vector2.Zero, physical);
        Check((stretch * logical).Position.IsEqualApprox(Vector2.Zero) && (stretch * logical).Size.IsEqualApprox(physical),
            context + ": CanvasItems Expand maps the whole logical canvas to the requested physical window.");
        var players = room.CreatureNodes.Where(node => node.Entity.IsPlayer).ToArray();
        var nodes = enemies.Concat(players).ToArray();
        Check(players.Length == safePlayers.Count && players.All(node => safePlayers.ContainsKey(node.Entity.Player!.NetId)),
            context + ": safe and hunted scenes have the same native player identities for pairwise overlap comparison.");
        Console.WriteLine($"SOULFYSH_LAYOUT {context} logical={logical} " + string.Join("; ", nodes.Select(node =>
            $"{Name(node)}:scale={node.Scale},visual={node.Visuals.Bounds.GetGlobalRect()},hit={node.Hitbox.GetGlobalRect()},hp={HealthBounds(node)},hpVisible={node.GetNode<Control>("%HealthBar").IsVisibleInTree()},intent=[{string.Join(",", IntentBounds(node))}]")));
        Check(players.Length == count && nodes.All(node => node.IsVisibleInTree() && node.Visuals.IsInsideTree() && node.Visuals.IsVisibleInTree()),
            context + ": every actual enemy and player role owns a visible native creature and visual.");
        foreach (var node in nodes)
            foreach (var (element, rect) in new[] { ("visual", node.Visuals.Bounds.GetGlobalRect()), ("hitbox", node.Hitbox.GetGlobalRect()), ("hp", HealthBounds(node)) })
                Check(rect.Size.X > 0 && rect.Size.Y > 0 && logical.Encloses(rect) && physicalBounds.Encloses(stretch * rect),
                    context + $": {Name(node)}/{element}={rect} fits logical={logical} and physical={physicalBounds}; transformed={stretch * rect}.");
        var overlaps = new List<string>();
        for (int first = 0; first < nodes.Length; first++)
            for (int second = first + 1; second < nodes.Length; second++)
            {
                if (nodes[first].Entity.IsPlayer && nodes[second].Entity.IsPlayer &&
                    safePlayers[nodes[first].Entity.Player!.NetId].Intersects(safePlayers[nodes[second].Entity.Player!.NetId]))
                    continue;
                RecordOverlap(nodes[first].Hitbox.GetGlobalRect(), Name(nodes[first]) + "/hitbox",
                    nodes[second].Hitbox.GetGlobalRect(), Name(nodes[second]) + "/hitbox", stretch, overlaps);
            }
        var displays = Displays(nodes).ToArray();
        foreach (var enemy in enemies)
        {
            var values = IntentValues(enemy).Where(value => value.IsVisibleInTree() && !string.IsNullOrWhiteSpace(value.GetParsedText())).ToArray();
            if (enemy.Entity.Monster is Toadpole or SludgeSpinner)
                Check(values.Length == 1 && values[0].GetParsedText().Trim() == (enemy.Entity.Monster is Toadpole ? "7" : "8"),
                    context + ": actual native Toadpole/Spinner opening attack label displays exactly seven/eight.");
            foreach (var value in values)
            {
                var valueTransform = value.GetGlobalTransform();
                var textSize = new Vector2(value.GetContentWidth() * valueTransform.X.Length(), value.GetContentHeight() * valueTransform.Y.Length());
                var physicalText = stretch * new Rect2(Vector2.Zero, textSize);
                Console.WriteLine($"SOULFYSH_LAYOUT_INTENT_VALUE {context} entity={Name(enemy)} scale={enemy.Scale} text={value.GetParsedText()} rect={value.GetGlobalRect()} content=({value.GetContentWidth()},{value.GetContentHeight()}) physicalContent={physicalText.Size} outline={value.GetThemeConstant("outline_size")} envelope={IntentValueBounds(value)}");
                Check(value.GetContentWidth() > 0 && value.GetContentHeight() > 0,
                    context + ": every visible nonempty native intent value has laid-out text content.");
            }
            var intents = IntentBounds(enemy).ToArray();
            Check(enemy.IntentContainer.IsVisibleInTree() && intents.Length > 0 && enemy.GetNode<Control>("%HealthBar").IsVisibleInTree(),
                context + $": {Name(enemy)} retains its visible native intent and HP display.");
        }
        foreach (var display in displays)
            Check(logical.Encloses(display.Bounds) && physicalBounds.Encloses(stretch * display.Bounds),
                context + $": {Name(display.Owner)}/{display.Element}={display.Bounds} fits logical/physical bounds; transformed={stretch * display.Bounds}.");
        RecordDisplayOverlaps(displays, nodes, stretch, overlaps);
        foreach (string overlap in overlaps) Console.WriteLine($"SOULFYSH_LAYOUT_OVERLAP {context} {overlap}");
        Check(overlaps.Count == 0, context + ": all native enemy target/display rectangles remain separate from other entities. " + string.Join("; ", overlaps));
    }

    private static string Name(NCreature node) => node.Entity.IsPlayer ? "player-" + node.Entity.Player!.NetId : node.Entity.ModelId + "/" + node.Entity.CombatId + "/" + node.Entity.SlotName;
    private static IEnumerable<(NCreature Owner, string Element, Rect2 Bounds)> Displays(IEnumerable<NCreature> nodes)
    {
        foreach (var node in nodes)
        {
            // Native remote-role HP is intentionally hidden until actual focus.
            if (node.GetNode<Control>("%HealthBar").IsVisibleInTree()) yield return (node, "hp", HealthBounds(node));
            int index = 0;
            foreach (var bounds in IntentBounds(node)) yield return (node, "intent" + index++, bounds);
        }
    }

    private static void RecordDisplayOverlaps((NCreature Owner, string Element, Rect2 Bounds)[] displays,
        NCreature[] nodes, Transform2D stretch, List<string> overlaps)
    {
        for (int first = 0; first < displays.Length; first++)
            for (int second = first + 1; second < displays.Length; second++)
                if (!ReferenceEquals(displays[first].Owner, displays[second].Owner) &&
                    !(displays[first].Owner.Entity.IsPlayer && displays[second].Owner.Entity.IsPlayer))
                    RecordOverlap(displays[first].Bounds, Name(displays[first].Owner) + "/" + displays[first].Element,
                        displays[second].Bounds, Name(displays[second].Owner) + "/" + displays[second].Element, stretch, overlaps);
        foreach (var display in displays)
            foreach (var target in nodes.Where(target => !ReferenceEquals(target, display.Owner) &&
                !(target.Entity.IsPlayer && display.Owner.Entity.IsPlayer)))
                RecordOverlap(display.Bounds, Name(display.Owner) + "/" + display.Element,
                    target.Hitbox.GetGlobalRect(), Name(target) + "/hitbox", stretch, overlaps);
    }

    private static void CheckFocusedDisplays(NCombatRoom room)
    {
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        var nodes = room.CreatureNodes.ToArray();
        var displays = Displays(nodes).ToArray();
        var overlaps = new List<string>();
        foreach (var display in displays)
            Check(viewport.GetVisibleRect().Encloses(display.Bounds) && new Rect2(Vector2.Zero, viewport.Size).Encloses(viewport.GetStretchTransform() * display.Bounds),
                context + $": focus-revealed {Name(display.Owner)}/{display.Element}={display.Bounds} remains inside the viewport.");
        RecordDisplayOverlaps(displays, nodes, viewport.GetStretchTransform(), overlaps);
        foreach (string overlap in overlaps) Console.WriteLine($"SOULFYSH_LAYOUT_FOCUS_OVERLAP {context} {overlap}");
        Check(overlaps.Count == 0, context + ": actual controller focus reveals no obscured native HP or intent. " + string.Join("; ", overlaps));
    }

    private static void RecordOverlap(Rect2 first, string firstName, Rect2 second, string secondName,
        Transform2D stretch, List<string> overlaps)
    {
        if (!first.Intersects(second)) return;
        Vector2 start = new(Mathf.Max(first.Position.X, second.Position.X), Mathf.Max(first.Position.Y, second.Position.Y));
        Vector2 end = new(Mathf.Min(first.End.X, second.End.X), Mathf.Min(first.End.Y, second.End.Y));
        var intersection = new Rect2(start, end - start);
        overlaps.Add($"{firstName}={first} intersects {secondName}={second}, overlapLogical={intersection}, overlapPhysical={stretch * intersection}");
    }

    private static Rect2 HealthBounds(NCreature node)
    {
        var display = node.GetNode<Control>("%HealthBar");
        return display.GetNode<Control>("%HpBarHitbox").GetGlobalRect()
            .Merge(display.GetNode<NHealthBar>("%HealthBar").HpBarContainer.GetGlobalRect());
    }
    private static IEnumerable<MegaRichTextLabel> IntentValues(NCreature node) => node.IntentContainer.GetChildren().OfType<NIntent>()
        .Select(intent => intent.GetNode<MegaRichTextLabel>("%Value"));
    private static IEnumerable<Rect2> IntentBounds(NCreature node)
    {
        foreach (var intent in node.IntentContainer.GetChildren().OfType<NIntent>())
        {
            var sprite = intent.GetNode<Sprite2D>("%Intent");
            yield return new Rect2(sprite.GlobalPosition + sprite.GetRect().Position * sprite.GlobalScale, sprite.GetRect().Size * sprite.GlobalScale);
            var value = intent.GetNode<MegaRichTextLabel>("%Value");
            if (value.IsVisibleInTree() && !string.IsNullOrWhiteSpace(value.GetParsedText())) yield return IntentValueBounds(value);
        }
    }
    private static Rect2 IntentValueBounds(MegaRichTextLabel value)
    {
        var frame = value.GetGlobalRect();
        var transform = value.GetGlobalTransform();
        var scale = new Vector2(transform.X.Length(), transform.Y.Length());
        var content = new Vector2(value.GetContentWidth(), value.GetContentHeight()) * scale;
        // The native label does not clip. Keep its entire frame and cover any
        // content overflow on both sides, independent of BBCode alignment.
        float horizontal = Mathf.Max(0, content.X - frame.Size.X);
        float vertical = Mathf.Max(0, content.Y - frame.Size.Y);
        float outline = value.GetThemeConstant("outline_size");
        return frame.GrowIndividual(horizontal + outline * scale.X, vertical + outline * scale.Y,
            horizontal + outline * scale.X, vertical + outline * scale.Y);
    }

    private static async Task CheckMouse(NTargetManager target, NCreature[] enemies)
    {
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        // A previous display may finish in controller mode. A real neutral
        // mouse press changes native mode before any selection is opened.
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = Vector2.Zero, GlobalPosition = Vector2.Zero }, true);
        await Frames(1);
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = Vector2.Zero, GlobalPosition = Vector2.Zero }, true);
        await Frames(2);
        foreach (var enemy in enemies)
        {
            await Motion(Vector2.Zero);
            Vector2 point = enemy.Hitbox.GetGlobalRect().GetCenter();
            await Motion(point);
            Check(enemy.IsFocused && ReferenceEquals(viewport.GuiGetHoveredControl(), enemy.Hitbox),
                context + ": Native MouseMotion enters exact " + Name(enemy) + " hitbox and its real IsFocused handler.");
            await Motion(Vector2.Zero);
            target.StartTargeting(TargetType.AnyEnemy, Vector2.Zero, TargetMode.ClickMouseToTarget, null, null);
            var selected = target.SelectionFinished();
            await Motion(point);
            viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point, GlobalPosition = point }, true);
            await Frames(1);
            viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point }, true);
            await Until(() => selected.IsCompleted, "real targeting selection completion");
            Check(ReferenceEquals(await selected, enemy) && !target.IsInSelection,
                "Native targeting press/release returns the exact hovered creature and closes the selection.");
        }
        await Motion(Vector2.Zero);
    }

    private static async Task CheckController(NControllerManager controller, NCombatRoom room, NCreature[] enemies)
    {
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        await Action(Controller.faceButtonNorth);
#if STS2_PUBLIC
        Check(controller.IsUsingController, "Real native controller detection enters controller mode through viewport input.");
#else
        Check(controller.IsUsingDirectionalNavigation, "Real native controller detection enters directional-navigation mode through viewport input.");
#endif
        var ring = room.CreatureNodes.Where(node => node.IsInteractable).OrderBy(node => node.GlobalPosition.X).ToArray();
        Check(ring.Length == enemies.Length + room.CreatureNodes.Count(node => node.Entity.IsPlayer) &&
            enemies.All(ring.Contains), "The native navigation ring contains every actual player and every living enemy.");
        for (int index = 0; index < ring.Length; index++)
        {
            Check(ring[index].Hitbox.GetNode<Control>(ring[index].Hitbox.FocusNeighborRight) == ring[(index + 1) % ring.Length].Hitbox &&
                ring[index].Hitbox.GetNode<Control>(ring[index].Hitbox.FocusNeighborLeft) == ring[(index + ring.Length - 1) % ring.Length].Hitbox,
                "Native summon navigation builds the complete left/right ring in whole-room X order, including player nodes.");
        }
        // One entry point per display. Every following target is reached only
        // through real Godot direction input and the native neighbour links.
        ring[0].Hitbox.GrabFocus();
        await Frames(2);
        Check(ReferenceEquals(viewport.GuiGetFocusOwner(), ring[0].Hitbox), "The controller ring has one explicit initial entry point.");
        foreach (var direction in new[] { MegaInput.right, MegaInput.left })
            for (int step = 1; step <= ring.Length; step++)
            {
                await Action(direction);
                int expected = direction == MegaInput.right ? step % ring.Length : (ring.Length - step % ring.Length) % ring.Length;
                Check(ReferenceEquals(viewport.GuiGetFocusOwner(), ring[expected].Hitbox) && ring[expected].IsFocused,
                    context + $": native direction={direction} step={step} reaches exact X-ordered entity {Name(ring[expected])}.");
                await Until(() => ring[expected].GetNode<Control>("%HealthBar").Modulate.A > 0.99999f,
                    "the actual focused HP fade for " + Name(ring[expected]));
                CheckFocusedDisplays(room);
            }
    }

    private static async Task Action(StringName action)
    {
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        viewport.PushInput(new InputEventAction { Action = action, Pressed = true, Strength = 1 }, true);
        await Frames(1);
        viewport.PushInput(new InputEventAction { Action = action, Pressed = false, Strength = 0 }, true);
        await Frames(2);
    }

    private static async Task Motion(Vector2 point)
    {
        ((SceneTree)Engine.GetMainLoop()).Root.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        await Frames(2);
    }
    private static async Task Frames(int count)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int frame = 0; frame < count; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }
    private static async Task Until(Func<bool> ready, string stage)
    {
        ulong deadline = Time.GetTicksMsec() + 10000;
        while (!ready() && Time.GetTicksMsec() < deadline) await Frames(1);
        Check(ready(), context + ": the SoulFysh native layout did not reach " + stage + ".");
    }

    private sealed class NativeHost : IDisposable
    {
        internal readonly Window Window = ((SceneTree)Engine.GetMainLoop()).Root;
        private readonly Vector2I size;
        private readonly Vector2I content;
        private readonly Window.ContentScaleModeEnum mode;
        private readonly Window.ContentScaleAspectEnum aspect;
        private readonly bool testing = TestMode.IsOn;
        private readonly Input.MouseModeEnum mouseMode = Input.MouseMode;
        private readonly Func<bool> autoSlayer = NonInteractiveMode.AutoSlayerCheck;
        private readonly System.Reflection.FieldInfo consoleField = AccessTools.Field(typeof(MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole), "_instance");
        private readonly object? previousConsole;
        private readonly Harmony patches = new("AllMatters.Tests.SoulFyshNativeLayout");
        private readonly List<StringName> addedActions = [];
        private readonly List<Node> owned = [];
        private JigsawNativeMapUiHost? ui;
        private IDisposable? atlases;
        internal NTargetManager Target = null!;
        internal NControllerManager Controller = null!;

        internal NativeHost()
        {
            size = Window.Size; content = Window.ContentScaleSize; mode = Window.ContentScaleMode; aspect = Window.ContentScaleAspect;
            previousConsole = consoleField.GetValue(null);
        }

        private T Own<T>(T node) where T : Node { owned.Add(node); return node; }
        internal async Task Mount(SoulBattle battle)
        {
            Window.ContentScaleSize = new Vector2I(1920, 1080);
            Window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
            Window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
            Window.Size = new Vector2I(1280, 720);
            foreach (string name in MegaInput.AllInputs.Concat(MegaCrit.Sts2.Core.ControllerInput.Controller.AllControllerInputs.Select(action => action.ToString())).Concat(new[] { "debug_hide_intents", "debug_hide_combat_ui", "debug_hide_play_container", "debug_hide_hand",
                "debug_hide_hp_bars", "debug_hide_text_vfx", "debug_hide_targeting_ui", "debug_slow_rewards", "debug_hide_mp_targeting", "debug_hide_mp_intents" }
                .Concat(Enumerable.Range(1, 10).Select(index => "mega_select_card_" + index))
                .Concat(new[] { DebugHotkey.speedUp.ToString(), DebugHotkey.speedDown.ToString(), DebugHotkey.hideProceedButton.ToString(), DebugHotkey.hideHoverTips.ToString() })))
            {
                StringName action = name;
                if (!InputMap.HasAction(action)) { InputMap.AddAction(action); addedActions.Add(action); }
            }
            atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
            JigsawHuntingNativeTests.RegisterScripts();
            ui = new JigsawNativeMapUiHost();
            RunManager.Instance.GenerateRooms();
            var session = JigsawNativeSession.TryGet(battle.Run)!;
            await ui.Open(session); session.HideMap();
            var game = NGame.Instance!;
            var run = NRun.Instance!;
            run.GlobalUi.MouseFilter = Control.MouseFilterEnum.Ignore;
            var hotkeys = Own(new NHotkeyManager());
            AccessTools.Property(typeof(NGame), nameof(NGame.HotkeyManager)).SetValue(game, hotkeys);
            await HeadlessUiHost.Mount(hotkeys);
            Controller = Own(new NControllerManager { ProcessMode = Node.ProcessModeEnum.Always });
            // Reproduce the original game.tscn label atom. Init's remaining
            // work starts Steam; native detection and notification stay real.
            var label = new MegaLabel { Name = "Label", AutoSizeEnabled = false, Modulate = Colors.Transparent,
                AnchorLeft = 1, AnchorTop = 1, AnchorRight = 1, AnchorBottom = 1,
                OffsetLeft = -133, OffsetTop = -74, OffsetRight = -41, OffsetBottom = -28,
                GrowHorizontal = Control.GrowDirection.Begin, GrowVertical = Control.GrowDirection.Begin };
            label.AddThemeColorOverride("font_color", new Color(0.34902f, 1, 1, 1));
            label.AddThemeColorOverride("font_outline_color", Colors.Black);
            label.AddThemeConstantOverride("outline_size", 10);
            label.AddThemeFontOverride("font", PreloadManager.Cache.GetAsset<Font>("res://themes/kreon_bold_glyph_space_one.tres"));
            label.AddThemeFontSizeOverride("font_size", 36);
            Controller.AddChild(label);
            AccessTools.Field(typeof(NControllerManager), "_label").SetValue(Controller, label);
            AccessTools.Property(typeof(NInputManager), "ControllerManager").SetValue(game.InputManager, Controller);
            AccessTools.Property(typeof(NGame), nameof(NGame.CursorManager)).SetValue(game, Own(new NCursorManager()));
            Target = Own(new NTargetManager { ProcessMode = Node.ProcessModeEnum.Always });
            foreach (string path in NTargetingArrow.AssetPaths) PreloadManager.Cache.GetTexture2D(path);
            var arrow = PreloadManager.Cache.GetScene("res://scenes/combat/targeting_arrow.tscn").Instantiate<NTargetingArrow>();
            arrow.Name = "TargetingArrow"; Target.AddChild(arrow);
            var players = Own(new NMultiplayerPlayerStateContainer());
            players.Initialize(battle.Run);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.TargetManager)).SetValue(run.GlobalUi, Target);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MultiplayerPlayerContainer)).SetValue(run.GlobalUi, players);
            consoleField.SetValue(null, Own(new MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole { Visible = false }));
            AccessTools.Field(typeof(NGame), "_screenShake").SetValue(game, Own(new MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.NScreenShake()));
            foreach (string method in new[] { "_EnterTree", "_Ready", "_ExitTree" })
                patches.Patch(AccessTools.Method(typeof(NGame), method), prefix: new HarmonyMethod(typeof(JigsawHuntingSoulFyshTests), nameof(SkipLifecycle)));
            patches.Patch(AccessTools.Method(typeof(SfxCmd), nameof(SfxCmd.Play), [typeof(string), typeof(float)]), prefix: new HarmonyMethod(typeof(JigsawHuntingSoulFyshTests), nameof(SkipAudio)));
            foreach (string method in new[] { "_Ready", "_Process" })
                patches.Patch(AccessTools.Method(typeof(NMapScreen), method), prefix: new HarmonyMethod(typeof(JigsawHuntingSoulFyshTests), nameof(SkipLifecycle)));
            game.MouseFilter = Control.MouseFilterEnum.Ignore;
            var tips = Own(new Node { Name = "HoverTipsContainer" }); game.AddChild(tips);
            AccessTools.Property(typeof(NGame), nameof(NGame.HoverTipsContainer)).SetValue(game, tips);
            var map = Own(new NMapScreen());
            var capstone = Own(new NCapstoneContainer());
            var overlays = Own(new NOverlayStack { ProcessMode = Node.ProcessModeEnum.Always, MouseFilter = Control.MouseFilterEnum.Ignore });
            var backstop = new Control { Name = "OverlayBackstop" }; overlays.AddChild(backstop);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MapScreen)).SetValue(run.GlobalUi, map);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.CapstoneContainer)).SetValue(run.GlobalUi, capstone);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.Overlays)).SetValue(run.GlobalUi, overlays);
            AccessTools.Property(typeof(NRun), nameof(NRun.ScreenStateTracker)).SetValue(run, new ScreenStateTracker(map, capstone, overlays));
            run.GlobalUi.AddChild(map); map.Hide(); map.SetProcess(false);
            run.GlobalUi.AddChild(overlays); overlays.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); backstop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            run.GlobalUi.AddChild(Target);
            var rooms = Own(new NSceneContainer { ProcessMode = Node.ProcessModeEnum.Always });
            AccessTools.Field(typeof(NRun), "_roomContainer").SetValue(run, rooms);
            run.AddChild(rooms); rooms.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await HeadlessUiHost.Mount(game);
            await HeadlessUiHost.Mount(Controller);
            game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            run.MoveChild(run.GlobalUi, run.GetChildCount() - 1);
            TestMode.IsOn = false; NonInteractiveMode.AutoSlayerCheck = () => false;
            combatNode = NCombatRoom.Create((CombatRoom)battle.Run.CurrentRoom!, CombatRoomMode.ActiveCombat)!;
            AccessTools.Property(typeof(NSceneContainer), nameof(NSceneContainer.CurrentScene)).SetValue(rooms, combatNode);
            rooms.AddChild(combatNode); combatNode.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            combatNode.Ui.Activate(battle.Combat);
            patches.Patch(AccessTools.Method(typeof(Creature), nameof(Creature.GetCreatureNode)), prefix: new HarmonyMethod(typeof(JigsawHuntingSoulFyshTests), nameof(CreatureNode)));
            ActiveScreenContext.Instance.Update();
            await Frames(4);
        }

        public void Dispose()
        {
            try
            {
                if (Target != null && GodotObject.IsInstanceValid(Target)) Target.CancelTargeting();
                if (combatNode != null && GodotObject.IsInstanceValid(combatNode)) combatNode.Free();
                for (int index = owned.Count - 1; index >= 0; index--)
                    if (GodotObject.IsInstanceValid(owned[index])) owned[index].Free();
            }
            finally
            {
                combatNode = null; consoleField.SetValue(null, previousConsole);
                try { ui?.Dispose(); }
                finally
                {
                    try { patches.UnpatchAll(patches.Id); }
                    finally
                    {
                        atlases?.Dispose();
                        foreach (var action in addedActions) InputMap.EraseAction(action);
                        TestMode.IsOn = testing; NonInteractiveMode.AutoSlayerCheck = autoSlayer;
                        Input.MouseMode = mouseMode;
                        Window.ContentScaleSize = content; Window.ContentScaleMode = mode; Window.ContentScaleAspect = aspect; Window.Size = size;
                    }
                }
            }
        }
    }

    private static JigsawRun Root(bool hunted = true, JigsawRoom kind = JigsawRoom.Boss)
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
                if ((kind == JigsawRoom.SecondBoss || map.Nodes.Single(node => node.Id == next).Room != JigsawRoom.SecondBoss) && previous.TryAdd(next, current))
                    pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == kind && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        Ok(map.SetRemainingSteps(hunted ? 0 : 100));
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!;
            Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        Check(map.CurrentRoomHunted == hunted, "A legal domain path establishes the actual safe or hunted root.");
        return map;
    }

    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Check(bool condition, string reason) => NativeEventTests.Check(condition, reason);
}
