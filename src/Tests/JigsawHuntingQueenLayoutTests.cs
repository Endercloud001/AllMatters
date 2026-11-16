using AllMatters.Jigsaw;
using AllMatters.Monsters;
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
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
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

internal static class JigsawHuntingQueenLayoutTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw Queen native layout preserves slots with real mouse selection and controller navigation at both aspect ratios", Layout)];

    private static NCombatRoom? combatNode;
    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static bool SkipLifecycle() => false;
    private static bool SkipAudio() => false;
    private static bool CreatureNode(Creature __instance, ref NCreature? __result)
    {
        __result = combatNode?.GetCreatureNode(__instance);
        return false;
    }

    private static async Task Layout()
    {
        foreach (int count in new[] { 1, 4 })
        {
            using var battle = new QueenBattle(count);
            await battle.Queen.AfterAddedToRoom();
            using var host = new NativeHost();
            await host.Mount(battle);
            var room = combatNode!;
            var encounter = room.GetNode<Control>("CombatSceneContainer/EnemyContainer/Encounter");
            var originalAmalgam = room.GetCreatureNode(battle.Amalgam.Creature);
            Check(originalAmalgam is { } && originalAmalgam.IsInsideTree() && originalAmalgam.Visuals.IsInsideTree(),
                "The original living amalgam has a real native creature and visual before its death.");
            CheckOriginalMarkers(encounter);
            await CreatureCmd.Kill(battle.Amalgam.Creature);
            Check(battle.Amalgam.Creature.IsDead && battle.Queen.NextMove.Id == "REKINDLE",
                "The actual original amalgam death changes this hunted Queen to the real Rekindle move.");
            await battle.Queen.PerformMove();
            battle.Queen.RollMove(battle.Combat.PlayerCreatures);
            var enemies = battle.Combat.Enemies.Where(creature => creature.IsAlive).ToArray();
            Check(enemies.Length == 3 && enemies.Count(creature => creature.Monster is TorchHead) == 2 &&
                enemies.Single(creature => creature.Monster is Queen) == battle.Queen.Creature,
                "The original Queen move adds exactly two real TorchHeads beside the same Queen.");
            await Until(() => enemies.All(creature => room.GetCreatureNode(creature) is { Visible: true }), "all three native enemy nodes");
            CheckOriginalMarkers(encounter);
            foreach (var physical in new[] { new Vector2I(1280, 720), new Vector2I(1680, 720) })
            {
                host.Window.Size = physical;
                room.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                foreach (var enemy in enemies) await room.GetCreatureNode(enemy)!.RefreshIntents();
                await Until(() => enemies.All(creature => room.GetCreatureNode(creature)!.IntentContainer.Modulate.A > 0.95f), "actual intent fade");
                await Frames(4);
                var nodes = enemies.Select(creature => room.GetCreatureNode(creature)!).OrderBy(node => node.GlobalPosition.X).ToArray();
                CheckGeometry(room, nodes, count, physical);
                await CheckMouse(host.Target, nodes);
                await CheckController(host.Controller, room, nodes);
                CheckOriginalMarkers(encounter);
            }
        }
    }

    private static void CheckOriginalMarkers(Control encounter)
    {
        Check(encounter.GetNode<Marker2D>("amalgam").Position.IsEqualApprox(new Vector2(1207, 709)) &&
            encounter.GetNode<Marker2D>("queen").Position.IsEqualApprox(new Vector2(1606, 695)),
            "The original amalgam and Queen marker coordinates survive native death and summons unchanged.");
    }

    private static void CheckGeometry(NCombatRoom room, NCreature[] enemies, int count, Vector2I physical)
    {
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        var logical = viewport.GetVisibleRect();
        var stretch = viewport.GetStretchTransform();
        var physicalBounds = new Rect2(Vector2.Zero, physical);
        Check((stretch * logical).Position.IsEqualApprox(Vector2.Zero) && (stretch * logical).Size.IsEqualApprox(physical),
            "CanvasItems Expand maps the entire logical canvas to the requested physical window.");
        var hitboxes = enemies.Select(node => node.Hitbox.GetGlobalRect()).ToArray();
        var players = room.CreatureNodes.Where(node => node.Entity.IsPlayer).ToArray();
        Console.WriteLine($"QUEEN_LAYOUT roles={count} physical={physical} logical={logical} " + string.Join("; ", enemies.Select(node =>
            $"{node.Entity.SlotName}:hit={node.Hitbox.GetGlobalRect()},hp={HealthBounds(node)},intent=[{string.Join(",", IntentBounds(node))}]")) +
            "; players=" + string.Join(";", players.Select(node => node.Hitbox.GetGlobalRect())));
        Check(players.Length == count && enemies.All(node => node.IsVisibleInTree() && node.Visuals.IsInsideTree()) &&
            hitboxes.All(rect => rect.Size.X > 0 && rect.Size.Y > 0 && logical.Encloses(rect) && physicalBounds.Encloses(stretch * rect)),
            "The three real enemy hitboxes have positive sizes and fit both logical and physical bounds beside every actual player role.");
        for (int first = 0; first < hitboxes.Length; first++)
            for (int second = first + 1; second < hitboxes.Length; second++)
                Check(!hitboxes[first].Intersects(hitboxes[second]), "Every Queen/Torch native hitbox pair remains separate.");
        Check(players.All(player => hitboxes.All(rect => !rect.Intersects(player.Hitbox.GetGlobalRect()))),
            "Neither Torch nor Queen overlaps any actual player hitbox.");
        var displays = new List<(NCreature Owner, string Element, Rect2 Bounds)>();
        foreach (var enemy in enemies)
        {
            var values = IntentValues(enemy).Where(value => value.IsVisibleInTree() && !string.IsNullOrWhiteSpace(value.GetParsedText())).ToArray();
            if (enemy.Entity.Monster is TorchHead)
                Check(enemy.Entity.Monster.NextMove.Id == "ATTACK" && values.Length == 1 && values[0].GetParsedText().Trim() == "10",
                    "Each real Torch ATTACK intent displays its nonempty native damage text as exactly ten.");
            foreach (var value in values)
            {
                Console.WriteLine($"QUEEN_LAYOUT_INTENT_VALUE roles={count} physical={physical} slot={enemy.Entity.SlotName} text={value.GetParsedText()} rect={value.GetGlobalRect()} content=({value.GetContentWidth()},{value.GetContentHeight()}) outline={value.GetThemeConstant("outline_size")} envelope={IntentValueBounds(value)}");
                Check(value.GetContentWidth() > 0 && value.GetContentHeight() > 0,
                    "Every nonempty native intent value has actual laid-out text content.");
            }
            var intents = IntentBounds(enemy).ToArray();
            var hp = HealthBounds(enemy);
            Check(enemy.IntentContainer.IsVisibleInTree() && intents.Length > 0 && intents.All(rect =>
                logical.Encloses(rect) && physicalBounds.Encloses(stretch * rect)) &&
                enemy.GetNode<Control>("%HealthBar").IsVisibleInTree() && logical.Encloses(hp) && physicalBounds.Encloses(stretch * hp),
                "Each actual enemy intent sprite, nonempty value label and health bar fits both logical and physical bounds.");
            displays.Add((enemy, "hp", hp));
            for (int index = 0; index < intents.Length; index++) displays.Add((enemy, "intent" + index, intents[index]));
        }
        var overlaps = new List<string>();
        for (int first = 0; first < displays.Count; first++)
            for (int second = first + 1; second < displays.Count; second++)
                if (!ReferenceEquals(displays[first].Owner, displays[second].Owner))
                    RecordOverlap(displays[first].Bounds, displays[first].Owner.Entity.SlotName + "/" + displays[first].Element,
                        displays[second].Bounds, displays[second].Owner.Entity.SlotName + "/" + displays[second].Element, stretch, overlaps);
        foreach (var display in displays)
            foreach (var target in enemies.Concat(players).Where(target => !ReferenceEquals(target, display.Owner)))
                RecordOverlap(display.Bounds, display.Owner.Entity.SlotName + "/" + display.Element,
                    target.Hitbox.GetGlobalRect(), target.Entity.SlotName + "/hitbox", stretch, overlaps);
        foreach (string overlap in overlaps) Console.WriteLine($"QUEEN_LAYOUT_OVERLAP roles={count} physical={physical} {overlap}");
        Check(overlaps.Count == 0,
            "Every enemy health bar and intent remains readable: displays do not overlap another enemy display or another enemy/player target hitbox. " + string.Join("; ", overlaps));
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

    private static Rect2 HealthBounds(NCreature node) => node.GetNode<Control>("%HealthBar").GetNode<Control>("%HpBarHitbox").GetGlobalRect();
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
                "Native MouseMotion enters the exact Queen/Torch hitbox and its real IsFocused handler.");
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
            enemies.All(ring.Contains), "The native navigation ring contains every actual player and all three living enemies.");
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
                    $"Native direction={direction} step={step} reaches the exact X-ordered ring entity {ring[expected].Entity.SlotName}.");
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
        Check(ready(), "The Queen native layout did not reach " + stage + ".");
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
        private readonly Harmony patches = new("AllMatters.Tests.QueenNativeLayout");
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
        internal async Task Mount(QueenBattle battle)
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
                patches.Patch(AccessTools.Method(typeof(NGame), method), prefix: new HarmonyMethod(typeof(JigsawHuntingQueenLayoutTests), nameof(SkipLifecycle)));
            patches.Patch(AccessTools.Method(typeof(SfxCmd), nameof(SfxCmd.Play), [typeof(string), typeof(float)]), prefix: new HarmonyMethod(typeof(JigsawHuntingQueenLayoutTests), nameof(SkipAudio)));
            // The real native death animation calls this overload directly,
            // bypassing Play; this host owns no FMOD NAudioManager.
            patches.Patch(AccessTools.Method(typeof(SfxCmd), nameof(SfxCmd.PlayDeath), [typeof(MonsterModel)]), prefix: new HarmonyMethod(typeof(JigsawHuntingQueenLayoutTests), nameof(SkipAudio)));
            foreach (string method in new[] { "_Ready", "_Process" })
                patches.Patch(AccessTools.Method(typeof(NMapScreen), method), prefix: new HarmonyMethod(typeof(JigsawHuntingQueenLayoutTests), nameof(SkipLifecycle)));
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
            patches.Patch(AccessTools.Method(typeof(Creature), nameof(Creature.GetCreatureNode)), prefix: new HarmonyMethod(typeof(JigsawHuntingQueenLayoutTests), nameof(CreatureNode)));
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

    private sealed class QueenBattle : IDisposable
    {
        private readonly ulong? previousNetId = LocalContext.NetId;
        internal RunState Run { get; }
        internal CombatState Combat { get; }
        internal Queen Queen { get; }
        internal TorchHeadAmalgam Amalgam { get; }

        internal QueenBattle(int count)
        {
            var players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
            Run = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING87_QUEEN_LAYOUT");
            Run.CurrentActIndex = 2;
            var encounter = ModelDb.Encounter<QueenBoss>().ToMutable();
            Combat = new CombatState(encounter, Run) { RoundNumber = 1 };
            try
            {
                Combat.AddPlayer(players[0]); players[0].ResetCombatState();
                RunManager.Instance.SetUpTest(Run, new NetSingleplayerGameService());
                LocalContext.NetId = players[0].NetId;
                CombatManager.Instance.Reset(true); CombatManager.Instance.SetUpCombat(Combat);
                foreach (var player in players.Skip(1)) { player.RunState = Run; Combat.AddPlayer(player); player.ResetCombatState(); }
                Run.PushRoom(new CombatRoom(Combat));
                JigsawCheckpoint.Capture(Run, BossRoot(players[0].NetId)); JigsawNativeSession.Restore(Run);
                var projection = JigsawEntryProjection.Append(Run.Map, 1, JigsawRoom.Boss);
                Run.Map = projection.Map;
                Check(Run.AddVisitedMapCoord(projection.Point.coord), "The real Queen RNG has a visited native map coordinate.");
                encounter.GenerateMonstersWithSlots(Run);
                foreach (var (monster, slot) in encounter.MonstersWithSlots)
                {
                    Combat.AddCreature(Combat.CreateCreature(monster, CombatSide.Enemy, slot));
                    CombatManager.Instance.AddCreature(monster.Creature); monster.RollMove(Combat.PlayerCreatures);
                }
                Queen = Combat.Enemies.Select(enemy => enemy.Monster).OfType<Queen>().Single();
                Amalgam = Combat.Enemies.Select(enemy => enemy.Monster).OfType<TorchHeadAmalgam>().Single();
                Check(Run.Players.Count == 1 && Combat.Players.Count == count && JigsawHuntingNative.RootIsHunted(players[0]),
                    "One legal run owner binds every actual combat role to the accepted hunted Glory Boss root.");
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            JigsawNativeSession.TryGet(Run)?.Close(); CombatManager.Instance.Reset(true);
            RunManager.Instance.CleanUp(false); LocalContext.NetId = previousNetId;
        }
    }

    private static JigsawRun BossRoot(ulong owner)
    {
        var map = JigsawRun.Create(75, players: [owner]);
        Ok(map.ChooseEnd(owner, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        Ok(map.CompleteAncient(owner)); Ok(map.Continue(owner));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var pending = new Queue<int>(); pending.Enqueue(0);
        while (pending.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (map.Nodes.Single(node => node.Id == next).Room != JigsawRoom.SecondBoss && previous.TryAdd(next, current)) pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == JigsawRoom.Boss && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse(); Ok(map.SetRemainingSteps(0));
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!; Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        Check(map.CurrentRoomHunted, "Public map transitions establish the accepted hunted Boss root.");
        return map;
    }
}
