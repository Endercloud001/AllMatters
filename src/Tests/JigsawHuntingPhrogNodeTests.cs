using AllMatters.Jigsaw;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Debug;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.addons.mega_text;

// The native test runner omits NRun's room scene. Supply that scene ownership,
// while retaining the actual combat room, creature factories and death animation.
internal static class JigsawHuntingPhrogNodeTests
{
    private static NodeHost? current;
    private static bool SkipLifecycle() => false;

    internal static async Task<FreshDeathNodeScope> CreateFreshDeathNodes(Creature phrog)
    {
        var scope = new FreshDeathNodeScope();
        try { await scope.Mount(phrog); return scope; }
        catch { await scope.DisposeAsync(); throw; }
    }

    // Supply only the real room/death observation boundary. The caller keeps
    // its original run, encounter queue and naturally started coordinator.
    internal sealed class FreshDeathNodeScope : IAsyncDisposable
    {
        private readonly NativeDisplayScope display = new();
        private readonly JigsawNativeMapUiHost mapHost = new();
        private NodeHost? nodes;
        private Task? killing;
        private Creature phrog = null!;
        internal NCombatRoom Room => nodes!.Room;
        internal float AnimationLength => nodes!.AnimationLength;
        internal int WaitCount => nodes!.WaitCount;

        internal async Task Mount(Creature creature)
        {
            phrog = creature;
            JigsawHuntingNativeTests.RegisterScripts();
            HeadlessUiHost.EnsureInputActions();
            var native = (RunState)phrog.CombatState!.RunState;
            var session = JigsawNativeSession.TryGet(native)!;
            await mapHost.Open(session);
            session.HideMap();
            nodes = new NodeHost(phrog);
            await nodes.Mount((CombatRoom)native.CurrentRoom!);
            Check(Room.GetCreatureNode(phrog) is { } node && node.IsInsideTree() && node.Visuals.HasSpineAnimation,
                "Fresh Phrog uses the actual mounted native creature and its original animated visual.");
        }

        internal async Task<Creature[]> KillAndReveal()
        {
            var combat = phrog.CombatState!;
            killing = CreatureCmd.Kill(phrog);
#if STS2_PUBLIC
            await FramesUntil(() => WaitCount >= 1 || killing.IsCompleted, "fresh public original death wait");
            if (killing.IsFaulted) await killing;
            Check(AnimationLength > 0 && WaitCount == 1 && !killing.IsCompleted &&
                !combat.Enemies.Any(enemy => enemy.Monster is Wriggler),
                "Fresh public retains the nonzero original death wait before any worm is created.");
#else
            await FramesUntil(() => WaitCount >= 2 || killing.IsFaulted, "fresh beta original reveal waits");
            if (killing.IsFaulted) await killing;
            var hidden = combat.Enemies.Where(enemy => enemy.Monster is Wriggler).ToArray();
            Check(AnimationLength > 0 && WaitCount == 2 && hidden.Length == 5 &&
                hidden.All(enemy => Room.GetCreatureNode(enemy) is { Visible: false } node && node.IsInsideTree()),
                "Fresh beta creates five hidden actual worms before both nonzero original reveal waits settle.");
#endif
            // HoldActualWait awaits the native timer before this observation
            // barrier. Releasing it never replaces or shortens that timer.
            nodes!.Release.TrySetResult();
            await killing;
            await Task.WhenAll(nodes.Waits);
            var worms = combat.Enemies.Where(enemy => enemy.Monster is Wriggler)
                .OrderBy(enemy => enemy.SlotName, StringComparer.Ordinal).ToArray();
            await FramesUntil(() => worms.All(enemy => Room.GetCreatureNode(enemy) is { Visible: true }), "fresh five visible native worms");
            Check(worms.Length == 5 && worms.All(enemy => enemy.IsAlive &&
                Room.GetCreatureNode(enemy) is { Visible: true } node && node.IsInsideTree() && node.Entity == enemy) &&
                worms.Select(enemy => enemy.SlotName).SequenceEqual(new[] { "wriggler1", "wriggler2", "wriggler3", "wriggler4", "wriggler5" }),
                "Settled actual Phrog death owns five living visible native worms in all five original numbered slots.");
            return worms;
        }

        public async ValueTask DisposeAsync()
        {
            var failures = new List<Exception>();
            nodes?.Release.TrySetResult();
            if (killing != null) try { await killing; } catch (Exception error) { failures.Add(error); }
            if (nodes != null)
                try { await Task.WhenAll(nodes.Waits); } catch (Exception error) { failures.Add(error); }
            try { await Frames(4); } catch (Exception error) { failures.Add(error); }
            try { nodes?.Dispose(); } catch (Exception error) { failures.Add(error); }
            try { mapHost.Dispose(); } catch (Exception error) { failures.Add(error); }
            try { display.Dispose(); } catch (Exception error) { failures.Add(error); }
            if (failures.Count > 0) throw new AggregateException("Fresh Phrog native death scope cleanup failed.", failures);
        }
    }

    internal static Task NonzeroDeath(Creature phrog) => Exercise(phrog, false);
    internal static Task OldReveal(Creature phrog) => Exercise(phrog, true);
    internal static Task Geometry(Creature phrog) => phrog.CombatState!.Players.Count is 1 or 4
        ? Exercise(phrog, false, true) : Task.CompletedTask;

    internal static async Task Cleanup(Creature phrog)
    {
        var native = (RunState)phrog.CombatState!.RunState;
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        JigsawHuntingNativeTests.RegisterScripts();
        HeadlessUiHost.EnsureInputActions();
        using var mapHost = new JigsawNativeMapUiHost();
        RunManager.Instance.GenerateRooms();
        await mapHost.Open(JigsawNativeSession.TryGet(native)!);
        var game = NGame.Instance!;
        var run = NRun.Instance!;
        var screenField = AccessTools.Field(typeof(NGame), "_screenShake");
        var hotkeyProperty = AccessTools.Property(typeof(NGame), nameof(NGame.HotkeyManager));
        var containerField = AccessTools.Field(typeof(NRun), "_roomContainer");
        var previousShake = screenField.GetValue(game);
        var previousHotkeys = hotkeyProperty.GetValue(game);
        var previousContainer = containerField.GetValue(run);
        var callbacksField = AccessTools.Field(typeof(CombatManager), nameof(CombatManager.CreaturesChanged));
        var callbacksBefore = ((Delegate)callbacksField.GetValue(CombatManager.Instance)!).GetInvocationList();
        var shake = new MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.NScreenShake();
        var hotkeys = new NHotkeyManager();
        var target = new Control();
        var container = new NSceneContainer();
        screenField.SetValue(game, shake);
        hotkeyProperty.SetValue(game, hotkeys);
        containerField.SetValue(run, container);
        try
        {
            foreach (bool fail in new[] { false, true })
            {
                try
                {
                    using var nodes = new NodeHost(phrog);
                    await nodes.Mount((CombatRoom)native.CurrentRoom!);
                    var tree = (SceneTree)Engine.GetMainLoop();
                    for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                    if (fail) throw new CleanupProbeException();
                }
                catch (CleanupProbeException) when (fail) { }
                Check(ReferenceEquals(screenField.GetValue(game), shake) &&
                    ReferenceEquals(hotkeyProperty.GetValue(game), hotkeys) &&
                    ReferenceEquals(containerField.GetValue(run), container),
                    $"Disposing the actual Phrog node host restores all original owner references after failure={fail}.");
                Check(((Delegate)callbacksField.GetValue(CombatManager.Instance)!).GetInvocationList().SequenceEqual(callbacksBefore),
                    "Disposal restores every native creature-change callback in its original order.");
                Check(GodotObject.IsInstanceValid(hotkeys) && GodotObject.IsInstanceValid(shake),
                    "The preceding native UI objects remain alive after node-host disposal.");
                game.SetScreenShakeTarget(target);
                Check(ReferenceEquals(game.ScreenshakeTarget, target),
                    "The next native UI consumer uses the restored screen-shake object in the same process.");
                game.HotkeyManager.AddBlockingScreen(target);
                game.HotkeyManager.RemoveBlockingScreen(target);
                game.ClearScreenShakeTarget();
            }
            var replacement = new NGame();
            var replacementScenes = new NSceneContainer();
            var replacementRun = new NRun();
            var replacementContainer = new NSceneContainer();
            try
            {
                AccessTools.Property(typeof(NGame), nameof(NGame.RootSceneContainer)).SetValue(replacement, replacementScenes);
                AccessTools.Property(typeof(NSceneContainer), nameof(NSceneContainer.CurrentScene)).SetValue(replacementScenes, replacementRun);
                containerField.SetValue(replacementRun, replacementContainer);
                // A scope can be disposed after global scene ownership changes,
                // including construction/mount failure before a room exists.
                using (var nodes = new NodeHost(phrog))
                    AccessTools.Property(typeof(NGame), nameof(NGame.Instance)).SetValue(null, replacement);
                Check(ReferenceEquals(NGame.Instance, replacement) &&
                    ReferenceEquals(containerField.GetValue(replacementRun), replacementContainer) &&
                    ReferenceEquals(screenField.GetValue(game), shake) &&
                    ReferenceEquals(hotkeyProperty.GetValue(game), hotkeys),
                    "Cleanup restores its captured owners without writing old references into a replacement global scene.");
            }
            finally
            {
                AccessTools.Property(typeof(NGame), nameof(NGame.Instance)).SetValue(null, game);
                replacement.Free(); replacementScenes.Free(); replacementRun.Free(); replacementContainer.Free();
            }
        }
        finally
        {
            screenField.SetValue(game, previousShake);
            hotkeyProperty.SetValue(game, previousHotkeys);
            containerField.SetValue(run, previousContainer);
            shake.Free(); hotkeys.Free(); target.Free(); container.Free();
        }
    }

    private sealed class CleanupProbeException : Exception;

    private static async Task Exercise(Creature phrog, bool replace, bool geometry = false)
    {
        using var display = new NativeDisplayScope();
        var native = (RunState)phrog.CombatState!.RunState;
        var combat = (CombatRoom)native.CurrentRoom!;
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        JigsawHuntingNativeTests.RegisterScripts();
        HeadlessUiHost.EnsureInputActions();
        using var mapHost = new JigsawNativeMapUiHost();
        var session = JigsawNativeSession.TryGet(native)!;
        RunManager.Instance.GenerateRooms();
        await mapHost.Open(session);
        session.HideMap();
        using var nodes = new NodeHost(phrog);
        await nodes.Mount(combat);
        var actualPhrog = nodes.Room.GetCreatureNode(phrog);
        Check(actualPhrog != null && actualPhrog.IsInsideTree() && actualPhrog.Visuals.HasSpineAnimation,
            "The Phrog has its production NCreature and animated visual inside the actual Godot tree.");
        Console.WriteLine("PHROG native scene slots: " + string.Join("; ",
            nodes.Room.GetNode<Control>("CombatSceneContainer/EnemyContainer/Encounter").GetChildren()
                .OfType<Marker2D>().Select(marker => marker.Name + "=" + marker.Position)));

        using var lifecycle = new JigsawHuntingCombatTests.NativeEndingScope();
        Task? killing = null;
        try
        {
            killing = CreatureCmd.Kill(phrog);
#if STS2_PUBLIC
            await FramesUntil(() => nodes.WaitCount >= 1 || killing.IsCompleted, "native public death wait");
            if (killing.IsFaulted) await killing;
            Check(nodes.AnimationLength > 0 && nodes.WaitCount == 1 && !killing.IsCompleted &&
                !combat.CombatState.Enemies.Any(creature => creature.Monster is Wriggler),
                "With a real nonzero death animation, public retains the native wait before spawning any worm.");
#else
            await FramesUntil(() => nodes.WaitCount >= 2 || killing.IsFaulted, "both native beta reveal waits");
            if (killing.IsFaulted) await killing;
            var hidden = combat.CombatState.Enemies.Where(creature => creature.Monster is Wriggler).ToArray();
            Check(nodes.AnimationLength > 0 && nodes.WaitCount == 2 && hidden.Length == 5 &&
                hidden.All(creature => nodes.Room.GetCreatureNode(creature) is { Visible: false } node && node.IsInsideTree()),
                "Beta creates all five actual nodes before the nonzero reveal waits and keeps every node hidden.");
#endif
            if (replace)
            {
                var replacement = await nodes.ReplaceRun();
                string before = NativeState(replacement);
                var originalNodes = nodes.Room.CreatureNodes.ToArray();
                var visibility = originalNodes.Select(node => node.Visible).ToArray();
                nodes.Release.TrySetResult();
                await killing;
                await Task.WhenAll(nodes.Waits);
                var tree = (SceneTree)Engine.GetMainLoop();
                for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                Check(ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), replacement) &&
                    nodes.Room.CreatureNodes.SequenceEqual(originalNodes) &&
                    originalNodes.Select(node => node.Visible).SequenceEqual(visibility) &&
                    NativeState(replacement) == before && JigsawNativeSession.TryGet(replacement) == null,
                    "Releasing an old real Phrog death/reveal cannot change replacement native players, RNG, history or exact room nodes.");
                return;
            }
            nodes.Release.TrySetResult();
            await killing;
            await Task.WhenAll(nodes.Waits);
            await FramesUntil(() => combat.CombatState.Enemies.Where(creature => creature.Monster is Wriggler)
                .All(creature => nodes.Room.GetCreatureNode(creature) is { Visible: true }), "five actual visible worms");
            var worms = combat.CombatState.Enemies.Where(creature => creature.Monster is Wriggler)
                .OrderBy(creature => creature.SlotName, StringComparer.Ordinal).ToArray();
            Check(worms.Length == 5 && worms.All(creature =>
                nodes.Room.GetCreatureNode(creature) is { Visible: true } node && node.IsInsideTree() &&
                node.Visuals.IsInsideTree() && node.Entity == creature),
                "After the real wait, the same combat owns five distinct visible production creature/visual nodes.");
            Check(worms.Select(creature => creature.SlotName).SequenceEqual(
                    new[] { "wriggler1", "wriggler2", "wriggler3", "wriggler4", "wriggler5" }),
                "The actual AddCreature path resolves all five named encounter slots.");
            var originalGround = worms.Take(4).Select(worm => nodes.Room.GetCreatureNode(worm)!.GlobalPosition.Y).ToArray();
            float fifthY = nodes.Room.GetCreatureNode(worms[4])!.GlobalPosition.Y;
            Check(fifthY >= originalGround.Min() - 1f && fifthY <= originalGround.Max() + 1f &&
                Mathf.IsEqualApprox(fifthY, originalGround[0]),
                $"The fifth worm stands on the first native marker's ground within the four original ground heights; fifth={fifthY}, original=[{string.Join(",", originalGround)}].");
            if (geometry)
            {
                using var input = new NativeInputScope(native);
                await input.Mount(nodes.Room, combat.CombatState);
                await CheckGeometry(nodes.Room, worms, input);
            }
        }
        finally
        {
            nodes.Release.TrySetResult();
            if (killing != null)
                try { await killing; } catch { /* Preserve the original test failure after settling its native continuation. */ }
            // Health bars schedule native deferred sizing when the worms enter
            // the tree; drain those callbacks before disposing their controls.
            var tree = (SceneTree)Engine.GetMainLoop();
            for (int frame = 0; frame < 4; frame++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static async Task FramesUntil(Func<bool> ready, string stage)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        ulong deadline = Time.GetTicksMsec() + 5000;
        while (Time.GetTicksMsec() < deadline && !ready())
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        Check(ready(), "Native node test did not reach " + stage + ".");
    }

    private static void Check(bool condition, string message) => NativeEventTests.Check(condition, message);

    private static async Task CheckGeometry(NCombatRoom room, Creature[] worms, NativeInputScope input)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var viewport = tree.Root;
        Vector2I previousSize = viewport.Size;
        try
        {
            // The real first post-stun move is rolled once; resizing and input
            // must not roll another move or consume monster RNG.
            foreach (var worm in worms)
            {
                var monster = worm.Monster!;
                monster.MoveStateMachine.OnMovePerformed(monster.NextMove);
                monster.RollMove(monster.CombatState.PlayerCreatures);
            }
            string opening = NativeState((RunState)worms[0].CombatState!.RunState);
            string moves = WormMoves(worms);
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1680, 720) })
            {
                viewport.Size = size;
                room.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                foreach (var worm in worms)
                    await room.GetCreatureNode(worm)!.RefreshIntents();
                await FramesUntil(() => worms.All(creature => room.GetCreatureNode(creature)!.IntentContainer.Modulate.A > 0.95f),
                    "actual native intent fade");
                await FramesUntil(() => worms.All(creature => room.GetCreatureNode(creature)!.GetNode<Control>("%HealthBar").Modulate.A > 0.95f),
                    "actual native HP fade");
                await Frames(4);
                var bounds = viewport.GetVisibleRect();
                var stretch = viewport.GetStretchTransform();
                var physical = stretch * bounds;
                var physicalBounds = new Rect2(Vector2.Zero, size);
                Check(physical.Position.IsEqualApprox(Vector2.Zero) && physical.Size.IsEqualApprox(size),
                    "The native canvas visible bounds map to the entire requested physical window.");
                var creatures = worms.Select(creature => room.GetCreatureNode(creature)!).ToArray();
                var hitboxes = creatures.Select(node => node.Hitbox.GetGlobalRect()).ToArray();
                Console.WriteLine($"PHROG_GEOMETRY players={worms[0].CombatState!.Players.Count} size={size} " +
                    string.Join("; ", creatures.Select(node => $"{node.Entity.SlotName}:hit={node.Hitbox.GetGlobalRect()},hp={HealthBounds(node)},intent=[{string.Join(",", IntentBounds(node))}]")));
                Check(creatures.All(node => node.IsVisibleInTree() && node.Hitbox.Size.X > 0 && node.Hitbox.Size.Y > 0) &&
                    hitboxes.All(bounds.Encloses) && hitboxes.All(rect => physicalBounds.Encloses(stretch * rect)),
                    "All five real native hitboxes are positive and inside the actual viewport.");
                for (int first = 0; first < hitboxes.Length; first++)
                    for (int second = first + 1; second < hitboxes.Length; second++)
                        Check(!hitboxes[first].Intersects(hitboxes[second]), "Every pair of native worm hitboxes remains separate.");
                var players = room.CreatureNodes.Where(node => node.Entity.IsPlayer).ToArray();
                Console.WriteLine("PHROG_GEOMETRY players hitboxes=" + string.Join("; ", players.Select(player => player.Hitbox.GetGlobalRect())));
                Check(players.Length == worms[0].CombatState!.Players.Count,
                    "The actual room retains every native player role beside the five worms.");
                Check(players.All(player => hitboxes.All(hitbox => !hitbox.Intersects(player.Hitbox.GetGlobalRect()))),
                    "The five enemy hitboxes remain separate from all actual player hitboxes.");
                var displays = new List<(NCreature Owner, string Element, Rect2 Bounds)>();
                foreach (var creature in creatures)
                {
                    var hp = HealthBounds(creature);
                    var label = HpLabel(creature);
                    Check(label.IsVisibleInTree() && label.Modulate.A > 0.95f &&
                        label.Text == $"{creature.Entity.CurrentHp}/{creature.Entity.MaxHp}" &&
                        label.Size.X >= label.GetCombinedMinimumSize().X && label.Size.Y >= label.GetCombinedMinimumSize().Y &&
                        PhysicalTextSize(label, stretch).Y > 0,
                        $"{creature.Entity.SlotName} shows its complete actual HP numbers after native death and reveal.");
                    Check(creature.GetNode<Control>("%HealthBar").IsVisibleInTree() && bounds.Encloses(hp) && physicalBounds.Encloses(stretch * hp),
                        $"{creature.Entity.SlotName} complete native HP display fits the requested viewport.");
                    displays.Add((creature, "hp", hp));
                    var values = IntentValues(creature).Where(value => value.IsVisibleInTree() && !string.IsNullOrWhiteSpace(value.GetParsedText())).ToArray();
                    Check(values.Any(value => value.GetParsedText().Any(char.IsDigit)),
                        $"{creature.Entity.SlotName} naturally rolls a visible numeric native post-stun intent.");
                    foreach (var value in values)
                    {
                        var content = PhysicalIntentTextSize(value, stretch);
                        Console.WriteLine($"PHROG_INTENT_VALUE players={players.Length} size={size} slot={creature.Entity.SlotName} text={value.GetParsedText()} content={content} envelope={IntentValueBounds(value)}");
                        Check(value.GetContentWidth() > 0 && value.GetContentHeight() > 0 &&
                            (!value.GetParsedText().Any(char.IsDigit) || content.Y >= PhysicalTextSize(label, stretch).Y * 0.75f),
                            $"{creature.Entity.SlotName} numeric intent retains native laid-out text size.");
                    }
                    var intents = IntentBounds(creature).ToArray();
                    Check(creature.IntentContainer.IsVisibleInTree() && intents.Length > 0 &&
                        intents.All(rect => bounds.Encloses(rect) && physicalBounds.Encloses(stretch * rect)),
                        $"{creature.Entity.SlotName} complete native intent sprite, value and outline fit the requested viewport.");
                    for (int index = 0; index < intents.Length; index++) displays.Add((creature, "intent" + index, intents[index]));
                }
                CheckDisplayOverlaps(displays, creatures.Concat(players).ToArray(), stretch);
                await CheckMouse(input.Target, creatures);
                await CheckController(input.Controller, room, creatures);
                var focusedDisplays = new List<(NCreature Owner, string Element, Rect2 Bounds)>();
                foreach (var creature in creatures)
                {
                    focusedDisplays.Add((creature, "hp", HealthBounds(creature)));
                    int index = 0;
                    foreach (var intent in IntentBounds(creature)) focusedDisplays.Add((creature, "intent" + index++, intent));
                }
                CheckDisplayOverlaps(focusedDisplays, creatures.Concat(players).ToArray(), stretch);
                Check(NativeState((RunState)worms[0].CombatState!.RunState) == opening && WormMoves(worms) == moves,
                    "Resizing, native hover/target selection and direction input preserve five worm models, HP, moves and RNG.");
            }
        }
        finally { viewport.Size = previousSize; }
    }

    private static void CheckDisplayOverlaps(List<(NCreature Owner, string Element, Rect2 Bounds)> displays,
        NCreature[] targets, Transform2D stretch)
    {
        var overlaps = new List<string>();
        for (int first = 0; first < displays.Count; first++)
            for (int second = first + 1; second < displays.Count; second++)
                if (!ReferenceEquals(displays[first].Owner, displays[second].Owner))
                    RecordOverlap(displays[first].Bounds, displays[first].Owner.Entity.SlotName + "/" + displays[first].Element,
                        displays[second].Bounds, displays[second].Owner.Entity.SlotName + "/" + displays[second].Element, stretch, overlaps);
        foreach (var display in displays)
            foreach (var target in targets.Where(node => !ReferenceEquals(node, display.Owner)))
                RecordOverlap(display.Bounds, display.Owner.Entity.SlotName + "/" + display.Element,
                    target.Hitbox.GetGlobalRect(), target.Entity.SlotName + "/hitbox", stretch, overlaps);
        foreach (string overlap in overlaps) Console.WriteLine("PHROG_DISPLAY_OVERLAP " + overlap);
        Check(overlaps.Count == 0, "Every worm HP and intent display remains clear of another display or target hitbox. " + string.Join("; ", overlaps));
    }

    private static void RecordOverlap(Rect2 first, string firstName, Rect2 second, string secondName,
        Transform2D stretch, List<string> overlaps)
    {
        if (!first.Intersects(second)) return;
        Vector2 start = new(Mathf.Max(first.Position.X, second.Position.X), Mathf.Max(first.Position.Y, second.Position.Y));
        Vector2 end = new(Mathf.Min(first.End.X, second.End.X), Mathf.Min(first.End.Y, second.End.Y));
        var intersection = new Rect2(start, end - start);
        overlaps.Add($"{firstName}={first} intersects {secondName}={second}, logical={intersection}, physical={stretch * intersection}");
    }

    private static Rect2 HealthBounds(NCreature node)
    {
        var display = node.GetNode<Control>("%HealthBar");
        return display.GetNode<Control>("%HpBarHitbox").GetGlobalRect()
            .Merge(display.GetNode<NHealthBar>("%HealthBar").HpBarContainer.GetGlobalRect())
            .Merge(HpLabelBounds(HpLabel(node)));
    }

    private static MegaLabel HpLabel(NCreature node) =>
        node.GetNode<Control>("%HealthBar").GetNode<NHealthBar>("%HealthBar").GetNode<MegaLabel>("%HpLabel");

    private static Rect2 HpLabelBounds(MegaLabel label)
    {
        var frame = label.GetGlobalRect();
        var transform = label.GetGlobalTransform();
        var scale = new Vector2(transform.X.Length(), transform.Y.Length());
        var content = label.GetCombinedMinimumSize() * scale;
        float horizontal = Mathf.Max(0, content.X - frame.Size.X);
        float vertical = Mathf.Max(0, content.Y - frame.Size.Y);
        float outline = label.GetThemeConstant("outline_size");
        return frame.GrowIndividual(horizontal + outline * scale.X, vertical + outline * scale.Y,
            horizontal + outline * scale.X, vertical + outline * scale.Y);
    }

    private static Vector2 PhysicalTextSize(MegaLabel label, Transform2D stretch)
    {
        var transform = label.GetGlobalTransform();
        var minimum = label.GetCombinedMinimumSize();
        return (stretch * new Rect2(Vector2.Zero,
            new Vector2(minimum.X * transform.X.Length(), minimum.Y * transform.Y.Length()))).Size;
    }

    private static IEnumerable<MegaRichTextLabel> IntentValues(NCreature node) => node.IntentContainer.GetChildren().OfType<NIntent>()
        .Select(intent => intent.GetNode<MegaRichTextLabel>("%Value"));

    private static IEnumerable<Rect2> IntentBounds(NCreature node)
    {
        foreach (var intent in node.IntentContainer.GetChildren().OfType<NIntent>())
        {
            var sprite = intent.GetNode<Sprite2D>("%Intent");
            yield return new Rect2(sprite.GlobalPosition + sprite.GetRect().Position * sprite.GlobalScale,
                sprite.GetRect().Size * sprite.GlobalScale);
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
        float horizontal = Mathf.Max(0, content.X - frame.Size.X);
        float vertical = Mathf.Max(0, content.Y - frame.Size.Y);
        float outline = value.GetThemeConstant("outline_size");
        return frame.GrowIndividual(horizontal + outline * scale.X, vertical + outline * scale.Y,
            horizontal + outline * scale.X, vertical + outline * scale.Y);
    }

    private static Vector2 PhysicalIntentTextSize(MegaRichTextLabel value, Transform2D stretch)
    {
        var transform = value.GetGlobalTransform();
        return (stretch * new Rect2(Vector2.Zero,
            new Vector2(value.GetContentWidth() * transform.X.Length(), value.GetContentHeight() * transform.Y.Length()))).Size;
    }

    private static async Task CheckMouse(NTargetManager target, NCreature[] worms)
    {
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true,
            Position = Vector2.Zero, GlobalPosition = Vector2.Zero }, true);
        await Frames(1);
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false,
            Position = Vector2.Zero, GlobalPosition = Vector2.Zero }, true);
        await Frames(2);
        foreach (var worm in worms)
        {
            await Motion(Vector2.Zero);
            Vector2 point = worm.Hitbox.GetGlobalRect().GetCenter();
            await Motion(point);
            Check(worm.IsFocused && ReferenceEquals(viewport.GuiGetHoveredControl(), worm.Hitbox),
                $"MouseMotion enters the exact {worm.Entity.SlotName} native hitbox and focus handler.");
            await Motion(Vector2.Zero);
            target.StartTargeting(TargetType.AnyEnemy, Vector2.Zero, TargetMode.ClickMouseToTarget, null, null);
            var selected = target.SelectionFinished();
            await Motion(point);
            viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true,
                Position = point, GlobalPosition = point }, true);
            await Frames(1);
            viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false,
                Position = point, GlobalPosition = point }, true);
            await FramesUntil(() => selected.IsCompleted, "real Phrog worm targeting completion");
            Check(ReferenceEquals(await selected, worm) && !target.IsInSelection,
                $"Native press/release selects exactly {worm.Entity.SlotName} and closes targeting.");
        }
        await Motion(Vector2.Zero);
    }

    private static async Task CheckController(NControllerManager controller, NCombatRoom room, NCreature[] worms)
    {
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        await Action(Controller.faceButtonNorth);
#if STS2_PUBLIC
        Check(controller.IsUsingController, "Native controller detection enters controller mode through viewport input.");
#else
        Check(controller.IsUsingDirectionalNavigation, "Native controller detection enters directional mode through viewport input.");
#endif
        var ring = room.CreatureNodes.Where(node => node.IsInteractable).OrderBy(node => node.GlobalPosition.X).ToArray();
        Check(ring.Length == worms.Length + room.CreatureNodes.Count(node => node.Entity.IsPlayer) &&
            worms.All(ring.Contains), "The native navigation ring contains every player and all five living worms.");
        for (int index = 0; index < ring.Length; index++)
            Check(ring[index].Hitbox.GetNode<Control>(ring[index].Hitbox.FocusNeighborRight) == ring[(index + 1) % ring.Length].Hitbox &&
                ring[index].Hitbox.GetNode<Control>(ring[index].Hitbox.FocusNeighborLeft) == ring[(index + ring.Length - 1) % ring.Length].Hitbox,
                "Native summon navigation links the complete left/right ring in whole-room X order.");
        ring[0].Hitbox.GrabFocus();
        await Frames(2);
        Check(ReferenceEquals(viewport.GuiGetFocusOwner(), ring[0].Hitbox), "The native ring accepts one initial focus entry.");
        foreach (var direction in new[] { MegaInput.right, MegaInput.left })
            for (int step = 1; step <= ring.Length; step++)
            {
                await Action(direction);
                int expected = direction == MegaInput.right ? step % ring.Length : (ring.Length - step % ring.Length) % ring.Length;
                Check(ReferenceEquals(viewport.GuiGetFocusOwner(), ring[expected].Hitbox) && ring[expected].IsFocused,
                    $"Native {direction} step {step} reaches the exact player/worm ring entity {ring[expected].Entity.SlotName}.");
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

    private static string NativeState(RunState run) => System.Text.Json.JsonSerializer.Serialize(new
    {
        Players = run.Players.Select(player => player.ToSerializable()).ToArray(),
        Random = run.Rng.ToSerializable(),
        History = run.MapPointHistory,
        Combat = ((CombatRoom)run.CurrentRoom!).CombatState.Enemies.Select(creature => new
        {
            Id = creature.Monster!.Id, creature.CurrentHp, creature.MaxHp, creature.SlotName,
            Random = HeadlessTestSetup.RngPosition(creature.Monster.Rng)
        }).ToArray()
    });

    private static string WormMoves(Creature[] worms) => System.Text.Json.JsonSerializer.Serialize(worms.Select(worm => new
    {
        worm.SlotName,
        Move = worm.Monster!.NextMove.Id,
        Powers = worm.Powers.Select(power => new { power.Id, power.Amount }).ToArray()
    }));

    private sealed class NativeDisplayScope : IDisposable
    {
        private readonly Window window = ((SceneTree)Engine.GetMainLoop()).Root;
        private readonly Vector2I size;
        private readonly Vector2I content;
        private readonly Window.ContentScaleModeEnum mode;
        private readonly Window.ContentScaleAspectEnum aspect;
        internal NativeDisplayScope()
        {
            size = window.Size; content = window.ContentScaleSize;
            mode = window.ContentScaleMode; aspect = window.ContentScaleAspect;
            // Match the game's project settings and 16:9 display configuration:
            // physical window pixels are distinct from its 1920x1080 UI canvas.
            window.ContentScaleSize = new Vector2I(1920, 1080);
            window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
            window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
            window.Size = new Vector2I(1280, 720);
        }
        public void Dispose()
        {
            window.ContentScaleSize = content; window.ContentScaleMode = mode;
            window.ContentScaleAspect = aspect; window.Size = size;
        }
    }

    private sealed class NativeInputScope : IDisposable
    {
        private readonly Harmony patches = new("AllMatters.Tests.PhrogNativeInput");
        private readonly RunState native;
        private readonly NGame game = NGame.Instance!;
        private readonly NRun run = NRun.Instance!;
        private readonly List<Node> owned = [];
        private readonly List<StringName> addedActions = [];
        private readonly object? previousController;
        private readonly object? previousCursor;
        private readonly object? previousTarget;
        private readonly object? previousPlayers;
        private readonly object? previousRelics;
        private readonly object? previousTips;
        private readonly object? previousTopBar;
        private readonly object? previousMap;
        private readonly object? previousCapstone;
        private readonly object? previousOverlays;
        private readonly object? previousScreenTracker;
        private readonly object? previousConsole;
        private readonly Control.MouseFilterEnum previousGlobalFilter;
        private readonly Control.MouseFilterEnum previousGameFilter;
        private readonly Input.MouseModeEnum previousMouseMode = Input.MouseMode;
        private readonly bool previousTestMode = TestMode.IsOn;
        private readonly bool gameWasMounted = NGame.Instance!.IsInsideTree();
        private Node? tips;
        internal NTargetManager Target = null!;
        internal NControllerManager Controller = null!;

        internal NativeInputScope(RunState native)
        {
            this.native = native;
            previousController = AccessTools.Property(typeof(NInputManager), "ControllerManager").GetValue(game.InputManager);
            previousCursor = AccessTools.Property(typeof(NGame), nameof(NGame.CursorManager)).GetValue(game);
            previousTarget = AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.TargetManager)).GetValue(run.GlobalUi);
            previousPlayers = AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MultiplayerPlayerContainer)).GetValue(run.GlobalUi);
            previousRelics = AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.RelicInventory)).GetValue(run.GlobalUi);
            previousTips = AccessTools.Property(typeof(NGame), nameof(NGame.HoverTipsContainer)).GetValue(game);
            previousTopBar = AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.TopBar)).GetValue(run.GlobalUi);
            previousMap = AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MapScreen)).GetValue(run.GlobalUi);
            previousCapstone = AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.CapstoneContainer)).GetValue(run.GlobalUi);
            previousOverlays = AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.Overlays)).GetValue(run.GlobalUi);
            previousScreenTracker = AccessTools.Property(typeof(NRun), nameof(NRun.ScreenStateTracker)).GetValue(run);
            previousConsole = AccessTools.Field(typeof(NDevConsole), "_instance").GetValue(null);
            previousGlobalFilter = run.GlobalUi.MouseFilter;
            previousGameFilter = game.MouseFilter;
        }

        private T Own<T>(T node) where T : Node { owned.Add(node); return node; }

        internal async Task Mount(NCombatRoom room, CombatState combat)
        {
            foreach (string name in MegaInput.AllInputs
                .Concat(MegaCrit.Sts2.Core.ControllerInput.Controller.AllControllerInputs.Select(action => action.ToString()))
                .Concat(new[] { "debug_hide_intents", "debug_hide_combat_ui", "debug_hide_play_container", "debug_hide_hand",
                    "debug_hide_hp_bars", "debug_hide_text_vfx", "debug_hide_targeting_ui", "debug_slow_rewards",
                    "debug_hide_mp_targeting", "debug_hide_mp_intents", "debug_hide_mp_health_bars",
                    "debug_hide_top_bar" })
                .Concat(Enumerable.Range(1, 10).Select(index => "mega_select_card_" + index))
                .Concat(new[] { DebugHotkey.speedUp.ToString(), DebugHotkey.speedDown.ToString(),
                    DebugHotkey.hideProceedButton.ToString(), DebugHotkey.hideHoverTips.ToString() }))
            {
                StringName action = name;
                if (!InputMap.HasAction(action)) { InputMap.AddAction(action); addedActions.Add(action); }
            }
            run.GlobalUi.MouseFilter = Control.MouseFilterEnum.Ignore;
            foreach (string method in new[] { "_EnterTree", "_Ready", "_ExitTree" })
                patches.Patch(AccessTools.Method(typeof(NGame), method),
                    prefix: new HarmonyMethod(typeof(JigsawHuntingPhrogNodeTests), nameof(SkipLifecycle)));
            foreach (string method in new[] { "_Ready", "_Process" })
                patches.Patch(AccessTools.Method(typeof(NMapScreen), method),
                    prefix: new HarmonyMethod(typeof(JigsawHuntingPhrogNodeTests), nameof(SkipLifecycle)));
            game.MouseFilter = Control.MouseFilterEnum.Ignore;
            tips = Own(new Node { Name = "HoverTipsContainer" });
            game.AddChild(tips);
            AccessTools.Property(typeof(NGame), nameof(NGame.HoverTipsContainer)).SetValue(game, tips);
            var map = Own(new NMapScreen());
            var capstone = Own(new NCapstoneContainer());
            var overlays = Own(new NOverlayStack { ProcessMode = Node.ProcessModeEnum.Always,
                MouseFilter = Control.MouseFilterEnum.Ignore });
            var backstop = new Control { Name = "OverlayBackstop" };
            overlays.AddChild(backstop);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MapScreen)).SetValue(run.GlobalUi, map);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.CapstoneContainer)).SetValue(run.GlobalUi, capstone);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.Overlays)).SetValue(run.GlobalUi, overlays);
            AccessTools.Property(typeof(NRun), nameof(NRun.ScreenStateTracker)).SetValue(run,
                new ScreenStateTracker(map, capstone, overlays));
            run.GlobalUi.AddChild(map); map.Hide(); map.SetProcess(false);
            run.GlobalUi.AddChild(overlays);
            overlays.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            backstop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            Controller = Own(new NControllerManager { ProcessMode = Node.ProcessModeEnum.Always });
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
            var relics = Own(new NRelicInventory { Name = "RelicInventory", MouseFilter = Control.MouseFilterEnum.Ignore });
            run.GlobalUi.AddChild(relics);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.RelicInventory)).SetValue(run.GlobalUi, relics);
            var topBar = Own(new NTopBar());
            var activeScreenProxy = Own(new Control { Name = "ActiveScreenProxy", FocusMode = Control.FocusModeEnum.All });
            run.GlobalUi.AddChild(activeScreenProxy);
            AccessTools.Property(typeof(NTopBar), nameof(NTopBar.ActiveScreenProxy)).SetValue(topBar, activeScreenProxy);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.TopBar)).SetValue(run.GlobalUi, topBar);
            var players = Own(new NMultiplayerPlayerStateContainer());
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.TargetManager)).SetValue(run.GlobalUi, Target);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MultiplayerPlayerContainer)).SetValue(run.GlobalUi, players);
            run.GlobalUi.AddChild(players);
            Check(players.IsInsideTree() && relics.IsInsideTree() && activeScreenProxy.IsInsideTree(),
                "Native multiplayer, relic and top-bar focus owners are mounted before player-state initialization.");
            players.Initialize(native);
            run.GlobalUi.AddChild(Target);
            AccessTools.Field(typeof(NDevConsole), "_instance").SetValue(null,
                Own(new NDevConsole { Visible = false }));
            if (!gameWasMounted) await HeadlessUiHost.Mount(game);
            await HeadlessUiHost.Mount(Controller);
            Check(game.IsInsideTree() && tips.IsInsideTree() && run.GlobalUi.IsInsideTree(),
                "The native game, hover tips and global UI are mounted for actual input dispatch.");
            game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            // This room entered after CombatSetUp. Restore its native setup
            // callback before screen changes can enable the combat UI.
            TestMode.IsOn = false;
            room.Ui.Activate(combat);
            var energy = (Node?)AccessTools.Field(typeof(NCombatUi), "_energyCounter").GetValue(room.Ui);
            Check(energy != null, "Native combat UI created its real energy counter outside test mode.");
            await FramesUntil(() => energy!.IsNodeReady(), "native energy counter readiness");
            ActiveScreenContext.Instance.Update();
            await Frames(2);
        }

        public void Dispose()
        {
            try
            {
                if (GodotObject.IsInstanceValid(Target)) Target.CancelTargeting();
                ((SceneTree)Engine.GetMainLoop()).Root.GuiReleaseFocus();
                AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.TargetManager)).SetValue(run.GlobalUi, previousTarget);
                AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MultiplayerPlayerContainer)).SetValue(run.GlobalUi, previousPlayers);
                AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.RelicInventory)).SetValue(run.GlobalUi, previousRelics);
                AccessTools.Property(typeof(NInputManager), "ControllerManager").SetValue(game.InputManager, previousController);
                AccessTools.Property(typeof(NGame), nameof(NGame.CursorManager)).SetValue(game, previousCursor);
                AccessTools.Property(typeof(NGame), nameof(NGame.HoverTipsContainer)).SetValue(game, previousTips);
                AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.TopBar)).SetValue(run.GlobalUi, previousTopBar);
                AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MapScreen)).SetValue(run.GlobalUi, previousMap);
                AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.CapstoneContainer)).SetValue(run.GlobalUi, previousCapstone);
                AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.Overlays)).SetValue(run.GlobalUi, previousOverlays);
                AccessTools.Property(typeof(NRun), nameof(NRun.ScreenStateTracker)).SetValue(run, previousScreenTracker);
                AccessTools.Field(typeof(NDevConsole), "_instance").SetValue(null, previousConsole);
                run.GlobalUi.MouseFilter = previousGlobalFilter;
                game.MouseFilter = previousGameFilter;
                for (int index = owned.Count - 1; index >= 0; index--)
                    if (GodotObject.IsInstanceValid(owned[index])) owned[index].Free();
                if (!gameWasMounted) game.GetParent()?.RemoveChild(game);
            }
            finally
            {
                patches.UnpatchAll(patches.Id);
                foreach (var action in addedActions) InputMap.EraseAction(action);
                Input.MouseMode = previousMouseMode;
                TestMode.IsOn = previousTestMode;
            }
        }
    }

    private sealed class NodeHost : IDisposable
    {
        private readonly Harmony harmony = new("AllMatters.Tests.PhrogNativeNodes");
        private readonly Creature phrog;
        private readonly NSceneContainer roomContainer = new();
        private readonly NHotkeyManager hotkeys = new();
        private readonly MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.NScreenShake shake = new();
        private readonly object? previousContainer;
        private readonly NGame game;
        private readonly NRun run;
        private readonly object? previousShake;
        private readonly object? previousHotkeys;
        private readonly CombatManager manager = CombatManager.Instance;
        private readonly CombatState combat;
        private readonly Action<CombatState> nativeCreaturesChanged;
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal NCombatRoom Room = null!;
        internal float AnimationLength;
        internal int WaitCount;
        internal readonly List<Task> Waits = [];

        internal NodeHost(Creature phrog)
        {
            this.phrog = phrog;
            combat = (CombatState)phrog.CombatState!;
            var callbacks = (Action<CombatState>)AccessTools.Field(typeof(CombatManager), nameof(CombatManager.CreaturesChanged)).GetValue(manager)!;
            nativeCreaturesChanged = callbacks.GetInvocationList().Cast<Action<CombatState>>().Single(callback =>
                ReferenceEquals(callback.Target, manager.StateTracker) && callback.Method.Name == "OnCreaturesChanged");
            game = NGame.Instance!;
            run = NRun.Instance!;
            previousContainer = AccessTools.Field(typeof(NRun), "_roomContainer").GetValue(run);
            previousShake = AccessTools.Field(typeof(NGame), "_screenShake").GetValue(game);
            previousHotkeys = AccessTools.Property(typeof(NGame), nameof(NGame.HotkeyManager)).GetValue(game);
            current = this;
            harmony.Patch(AccessTools.PropertyGetter(typeof(NCombatRoom), nameof(NCombatRoom.Instance)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingPhrogNodeTests), nameof(RoomInstance)));
            harmony.Patch(AccessTools.Method(typeof(Creature), nameof(Creature.GetCreatureNode)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingPhrogNodeTests), nameof(CreatureNode)));
#if !STS2_PUBLIC
            // SetNodeVisible may inline the TestMode-only null lookup. Execute
            // the unchanged native setter with its real room lookup enabled.
            harmony.Patch(AccessTools.Method(typeof(Creature), nameof(Creature.SetNodeVisible)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingPhrogNodeTests), nameof(CreatePrefix)),
                finalizer: new HarmonyMethod(typeof(JigsawHuntingPhrogNodeTests), nameof(CreateFinalizer)));
#endif
            harmony.Patch(AccessTools.Method(typeof(NCreature), nameof(NCreature.Create)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingPhrogNodeTests), nameof(CreatePrefix)),
                finalizer: new HarmonyMethod(typeof(JigsawHuntingPhrogNodeTests), nameof(CreateFinalizer)));
            // Native backend-only tests reject UI subscriptions. This case mounts
            // the real UI, so execute its actual tracker notification as a UI run.
            harmony.Patch(AccessTools.Method(typeof(CombatStateTracker), "NotifyCombatStateChanged"),
                prefix: new HarmonyMethod(typeof(JigsawHuntingPhrogNodeTests), nameof(CreatePrefix)),
                finalizer: new HarmonyMethod(typeof(JigsawHuntingPhrogNodeTests), nameof(CreateFinalizer)));
            harmony.Patch(AccessTools.Method(typeof(NCreature), nameof(NCreature.StartDeathAnim)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingPhrogNodeTests), nameof(DeathAnimation)));
            harmony.Patch(AccessTools.Method(typeof(Cmd), nameof(Cmd.CustomScaledWait)),
                postfix: new HarmonyMethod(typeof(JigsawHuntingPhrogNodeTests), nameof(ObserveWait)));
            // Earlier backend calls may inline the native tracker callback into
            // its event invocation. Scope UI mode at the delegate boundary,
            // retaining the actual callback and every other listener's order.
            ReplaceCreaturesChanged(nativeCreaturesChanged, NotifyCreaturesChanged);
        }

        internal async Task Mount(CombatRoom combat)
        {
            AccessTools.Field(typeof(NGame), "_screenShake").SetValue(game, shake);
            AccessTools.Property(typeof(NGame), nameof(NGame.HotkeyManager)).SetValue(game, hotkeys);
            AccessTools.Field(typeof(NRun), "_roomContainer").SetValue(run, roomContainer);
            if (!hotkeys.IsInsideTree()) await HeadlessUiHost.Mount(hotkeys);
            await PreloadManager.LoadRoomCombatAssets(combat.Encounter, combat.CombatState.RunState);
            bool testing = TestMode.IsOn;
            try { TestMode.IsOn = false; Room = NCombatRoom.Create(combat, CombatRoomMode.ActiveCombat)!; }
            finally { TestMode.IsOn = testing; }
            Check(Room != null, "The native combat room factory returned an actual room scene.");
            AccessTools.Property(typeof(NSceneContainer), nameof(NSceneContainer.CurrentScene)).SetValue(roomContainer, Room);
            await HeadlessUiHost.Mount(Room);
            Check(combat.CombatState.Enemies.All(creature => Room.GetCreatureNode(creature) != null) && Room.Ui != null,
                "Native combat room initialization created the real Phrog node and combat UI.");
        }

        internal async Task<RunState> ReplaceRun()
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            for (int frame = 0; frame < 4; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Room.Free(); Room = null!;
            AccessTools.Property(typeof(NSceneContainer), nameof(NSceneContainer.CurrentScene)).SetValue(roomContainer, null);
            CombatManager.Instance.Reset(true);
            RunManager.Instance.CleanUp(false);
            var replacement = RunState.CreateForTest(
                [Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)],
                acts: [ModelDb.Act<Overgrowth>()], seed: "PHROG_84_REPLACEMENT");
            RunManager.Instance.SetUpTest(replacement, new NetSingleplayerGameService());
            RunManager.Instance.GenerateRooms(); await RunManager.Instance.GenerateMap();
            var encounter = ModelDb.Encounter<PhrogParasiteElite>().ToMutable();
            replacement.AppendToMapPointHistory(MapPointType.Elite, RoomType.Elite, encounter.Id);
            var game = NGame.Instance;
            try
            {
                // The local host has no top bar; native TestMode entry normally
                // has no NGame either. Restore that ownership while entering.
                AccessTools.Property(typeof(NGame), nameof(NGame.Instance)).SetValue(null, null);
                using (HeadlessCombatInProgress.EnterEvent())
                    await RunManager.Instance.EnterRoom(new CombatRoom(encounter, replacement));
            }
            finally { AccessTools.Property(typeof(NGame), nameof(NGame.Instance)).SetValue(null, game); }
            await Mount((CombatRoom)replacement.CurrentRoom!);
            return replacement;
        }

        public void Dispose()
        {
            Release.TrySetResult();
            try
            {
                // Native room _ExitTree still consumes this host's hotkeys.
                if (GodotObject.IsInstanceValid(Room)) Room.Free();
            }
            finally
            {
                AccessTools.Field(typeof(NGame), "_screenShake").SetValue(game, previousShake);
                AccessTools.Property(typeof(NGame), nameof(NGame.HotkeyManager)).SetValue(game, previousHotkeys);
                AccessTools.Field(typeof(NRun), "_roomContainer").SetValue(run, previousContainer);
                ReplaceCreaturesChanged(NotifyCreaturesChanged, nativeCreaturesChanged);
                hotkeys.Free(); shake.Free(); roomContainer.Free();
                current = null;
                harmony.UnpatchAll(harmony.Id);
            }
        }

        internal bool Owns(Creature creature) => creature.CombatState == phrog.CombatState;
        internal bool IsPhrog(NCreature node) => node.Entity == phrog;

        private void NotifyCreaturesChanged(CombatState state)
        {
            bool testing = TestMode.IsOn;
            try
            {
                if (ReferenceEquals(state, combat) && ReferenceEquals(NGame.Instance, game) &&
                    ReferenceEquals(NRun.Instance, run) &&
                    ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), combat.RunState)) TestMode.IsOn = false;
                nativeCreaturesChanged(state);
            }
            finally { TestMode.IsOn = testing; }
        }

        private void ReplaceCreaturesChanged(Action<CombatState> before, Action<CombatState> after)
        {
            var field = AccessTools.Field(typeof(CombatManager), nameof(CombatManager.CreaturesChanged));
            var callbacks = ((Action<CombatState>)field.GetValue(manager)!).GetInvocationList()
                .Cast<Action<CombatState>>().Select(callback => callback == before ? after : callback).ToArray();
            field.SetValue(manager, callbacks.Aggregate((Action<CombatState>?)null, (all, callback) => all + callback));
        }
    }

    private static bool RoomInstance(ref NCombatRoom? __result)
    {
        if (current?.Room == null) return true;
        __result = current.Room;
        return false;
    }

    private static bool CreatureNode(Creature __instance, ref NCreature? __result)
    {
        if (current?.Room == null || !current.Owns(__instance)) return true;
        __result = current.Room.GetCreatureNode(__instance);
        return false;
    }

    private static void CreatePrefix(out bool __state)
    {
        __state = TestMode.IsOn;
        TestMode.IsOn = false;
    }

    private static Exception? CreateFinalizer(Exception? __exception, bool __state)
    {
        TestMode.IsOn = __state;
        return __exception;
    }

    private static void DeathAnimation(NCreature __instance, float __result)
    {
        if (current?.IsPhrog(__instance) == true)
        {
            current.AnimationLength = __result;
            Console.WriteLine("PHROG native death animation seconds=" + __result);
        }
    }

    private static void ObserveWait(float fastSeconds, float standardSeconds, ref Task __result)
    {
        if (current == null || current.AnimationLength <= 0 ||
            fastSeconds != current.AnimationLength || standardSeconds != current.AnimationLength) return;
        current.WaitCount++;
        __result = HoldActualWait(__result, current.Release.Task);
        current.Waits.Add(__result);
    }

    private static async Task HoldActualWait(Task nativeWait, Task release)
    {
        await nativeWait;
        await release;
    }
}
