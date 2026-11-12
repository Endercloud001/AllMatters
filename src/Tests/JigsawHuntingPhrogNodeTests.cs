using AllMatters.Jigsaw;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

// The native test runner omits NRun's room scene. Supply that scene ownership,
// while retaining the actual combat room, creature factories and death animation.
internal static class JigsawHuntingPhrogNodeTests
{
    private static NodeHost? current;

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
            if (geometry) await CheckGeometry(nodes.Room, worms);
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

    private static async Task CheckGeometry(NCombatRoom room, Creature[] worms)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var viewport = tree.Root;
        Vector2I previousSize = viewport.Size, previousScale = viewport.ContentScaleSize;
        try
        {
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1680, 720) })
            {
                viewport.Size = viewport.ContentScaleSize = size;
                room.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                foreach (var worm in worms)
                {
                    var monster = worm.Monster!;
                    monster.MoveStateMachine.OnMovePerformed(monster.NextMove);
                    monster.RollMove(monster.CombatState.PlayerCreatures);
                    await room.GetCreatureNode(worm)!.RefreshIntents();
                }
                await FramesUntil(() => worms.All(creature => room.GetCreatureNode(creature)!.IntentContainer.Modulate.A > 0.95f),
                    "actual native intent fade");
                var bounds = new Rect2(Vector2.Zero, size);
                var creatures = worms.Select(creature => room.GetCreatureNode(creature)!).ToArray();
                var hitboxes = creatures.Select(node => node.Hitbox.GetGlobalRect()).ToArray();
                Console.WriteLine($"PHROG_GEOMETRY players={worms[0].CombatState!.Players.Count} size={size} " +
                    string.Join("; ", creatures.Select(node => $"{node.Entity.SlotName}:hit={node.Hitbox.GetGlobalRect()},hp={node.GetNode<Control>("%HealthBar").GetNode<Control>("%HpBarHitbox").GetGlobalRect()},intent={IntentBounds(node).FirstOrDefault()}")));
                Check(creatures.All(node => node.IsVisibleInTree() && node.Hitbox.Size.X > 0 && node.Hitbox.Size.Y > 0) &&
                    hitboxes.All(bounds.Encloses), "All five real native hitboxes are positive and inside the actual viewport.");
                for (int first = 0; first < hitboxes.Length; first++)
                    for (int second = first + 1; second < hitboxes.Length; second++)
                        Check(!hitboxes[first].Intersects(hitboxes[second]), "Every pair of native worm hitboxes remains separate.");
                var players = room.CreatureNodes.Where(node => node.Entity.IsPlayer).ToArray();
                Console.WriteLine("PHROG_GEOMETRY players hitboxes=" + string.Join("; ", players.Select(player => player.Hitbox.GetGlobalRect())));
                Check(players.All(player => hitboxes.All(hitbox => !hitbox.Intersects(player.Hitbox.GetGlobalRect()))),
                    "The five enemy hitboxes remain separate from all actual player hitboxes.");
                Check(creatures.All(node => node.IntentContainer.IsVisibleInTree() && node.IntentContainer.GetChildCount() > 0 &&
                    IntentBounds(node).All(bounds.Encloses) &&
                    node.GetNode<Control>("%HealthBar").IsVisibleInTree() &&
                    bounds.Encloses(node.GetNode<Control>("%HealthBar").GetNode<Control>("%HpBarHitbox").GetGlobalRect())),
                    "Every real worm intent and health display remains visible inside the viewport.");
            }
        }
        finally { viewport.Size = previousSize; viewport.ContentScaleSize = previousScale; }
    }

    private static IEnumerable<Rect2> IntentBounds(NCreature creature) => creature.IntentContainer.GetChildren()
        .OfType<NIntent>().Select(intent => intent.GetNode<Sprite2D>("%Intent")).Select(sprite =>
            new Rect2(sprite.GlobalPosition + sprite.GetRect().Position * sprite.GlobalScale,
                sprite.GetRect().Size * sprite.GlobalScale));

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
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal NCombatRoom Room = null!;
        internal float AnimationLength;
        internal int WaitCount;
        internal readonly List<Task> Waits = [];

        internal NodeHost(Creature phrog)
        {
            this.phrog = phrog;
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
                hotkeys.Free(); shake.Free(); roomContainer.Free();
                current = null;
                harmony.UnpatchAll(harmony.Id);
            }
        }

        internal bool Owns(Creature creature) => creature.CombatState == phrog.CombatState;
        internal bool IsPhrog(NCreature node) => node.Entity == phrog;
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
