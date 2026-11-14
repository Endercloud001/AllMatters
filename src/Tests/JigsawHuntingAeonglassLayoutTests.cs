using AllMatters.Jigsaw;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Pooling;
using MegaCrit.Sts2.Core.Nodes.Cards;
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
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
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
using System.Text.Json;

using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
internal static class JigsawHuntingAeonglassLayoutTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases = (from count in new[] { 1, 4 }
        select ($"Jigsaw Aeonglass native layout roles={count}", (Func<Task>)(() => Layout(count)))).ToArray();
    private static string context = "not mounted", overlapPhase = "opening", width = "1280";
    private static readonly JsonSerializerOptions rngJson = new() { IncludeFields = true };
    private static Dictionary<string, (Rect2 First, Rect2 Second, Rect2 Intersection)> originalOverlaps = [];
    private static Dictionary<string, Rect2> originalRectangles = [];
    private static Dictionary<string, Transform2D> originalTransforms = [];
    private static Dictionary<string, HashSet<string>> originalVisibleHp = [];
    private static HashSet<string> observedOverlaps = [];
    private static Dictionary<string, float> hpHeights = [];
    private static bool captureSource;
    private static void Check(bool value, string reason) => NativeEventTests.Check(value, context + ": " + reason);
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static async Task Layout(int count)
    {
        originalOverlaps = []; originalRectangles = []; originalTransforms = []; originalVisibleHp = [];
        observedOverlaps = []; hpHeights = [];
        foreach (bool hunted in new[] { false, true })
        {
            captureSource = !hunted;
            context = $"roles={count} hunted={hunted}";
            using var battle = new Battle(count, hunted);
            using var host = new HuntingLayoutNativeHost();
            await host.Mount(battle.Run, battle.Combat, async () =>
            {
                battle.SelectNativeBosses();
                await PreloadManager.LoadRoomCombatAssets(battle.Encounter, battle.Run);
            }, room => room.SetUpBackground(battle.Run));
            foreach (var creature in battle.Combat.Creatures.ToArray()) await CombatManager.Instance.AfterCreatureAdded(creature);
            var room = HuntingLayoutNativeHost.CombatRoom!;
            await Until(() => room.CreatureNodes.Where(node => !node.Entity.IsPlayer || LocalContext.IsMe(node.Entity))
                .All(node => node.GetNode<Control>("%HealthBar").Modulate.A > 0.99999f), "opening HP fade");
            var enemies = room.CreatureNodes.Where(node => node.Entity.IsEnemy).ToArray();
            Check(enemies.Length == 1 && enemies[0].Entity.Monster is Aeonglass &&
                enemies[0].Entity.Monster!.NextMove.Id == "EBB_MOVE" && enemies[0].Entity.CurrentHp == enemies[0].Entity.MaxHp,
                "The original single full-HP Aeonglass opens with Ebb; no extra monster is introduced.");
            var identities = battle.Combat.Creatures.Select(creature => (creature, creature.Monster, move: creature.Monster?.NextMove)).ToArray();
            string before = State(battle);
            foreach (int pixels in new[] { 1280, 1680 })
            {
                width = pixels.ToString(); overlapPhase = width + "/opening";
                host.Window.Size = new Vector2I(pixels, 720);
                room.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                foreach (var enemy in enemies) await enemy.RefreshIntents();
                await Until(() => enemies.All(enemy => enemy.IntentContainer.Modulate.A > 0.99f), "intent fade");
                await Frames(5);
                // Reset real input focus before comparing this width's opening phase.
                await Motion(Vector2.Zero);
                CheckFocusedDisplays(room);
                await CheckMouse(host.Target, enemies);
                await CheckController(host.Controller, room, enemies);
                Check(State(battle) == before && identities.All(item => battle.Combat.Creatures.Contains(item.creature) &&
                    ReferenceEquals(item.creature.Monster, item.Monster) && ReferenceEquals(item.creature.Monster?.NextMove, item.move)),
                    "Resize, mouse selection and full directional rings preserve exact model identities, moves, all players, five piles and RNG.");
            }
        }
        Check(observedOverlaps.SetEquals(originalOverlaps.Keys), "Every original same-phase intersection is retained.");
    }
    private static void ValidateDisplays(NCombatRoom room)
    {
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        var stretch = viewport.GetStretchTransform();
        foreach (var node in room.CreatureNodes)
        {
            Check(node.Visuals.IsVisibleInTree() && node.Visuals.GetCurrentBody().IsVisibleInTree(), "Every original creature body is visible.");
            foreach (var bounds in new[] { node.Visuals.Bounds.GetGlobalRect(), node.Hitbox.GetGlobalRect() })
                Check(bounds.Size.X > 0 && bounds.Size.Y > 0 && viewport.GetVisibleRect().Encloses(bounds), "Complete visual and original hitbox fit viewport.");
            var health = node.GetNode<Control>("%HealthBar");
            if (health.IsVisibleInTree() && EffectiveAlpha(health) > 0.95f)
            {
                var label = HpLabel(node);
                string key = overlapPhase + "|" + Name(node);
                float height = PhysicalHpText(node, stretch).Y;
                if (captureSource) hpHeights[key] = height;
                Check(label.Text == $"{node.Entity.CurrentHp}/{node.Entity.MaxHp}" &&
                    label.Size.X >= label.GetCombinedMinimumSize().X && label.Size.Y >= label.GetCombinedMinimumSize().Y &&
                    height > 0 && hpHeights.TryGetValue(key, out float nativeHeight) && height >= nativeHeight * 0.85f,
                    "Current visible HP has complete actual numbers at original readable size.");
            }
            if (node.Entity.IsEnemy)
            {
                CheckIntentIdentity(node);
                foreach (var value in IntentValues(node).Where(value => value.IsVisibleInTree() && !string.IsNullOrWhiteSpace(value.GetParsedText())))
                {
                    Check(value.GetContentWidth() > 0 && value.GetContentHeight() > 0 &&
                        viewport.GetVisibleRect().Encloses(IntentValueBounds(value)) &&
                        new Rect2(Vector2.Zero, viewport.Size).Encloses(stretch * IntentValueBounds(value)),
                        "Complete current intent content and outline fit logical and physical viewport.");
                    if (value.GetParsedText().Any(char.IsDigit))
                    {
                        var transform = value.GetGlobalTransform();
                        var content = new Vector2(value.GetContentWidth() * transform.X.Length(), value.GetContentHeight() * transform.Y.Length());
                        Check((stretch * new Rect2(Vector2.Zero, content)).Size.Y >=
                            hpHeights[overlapPhase + "|" + Name(node)] * 0.75f, "Numeric intent retains native readable size.");
                    }
                }
                Check(node.IntentContainer.IsVisibleInTree() && IntentBounds(node).Any(), "Original current intent is visible.");
            }
        }
    }
    private static string Name(NCreature node) => node.Entity.IsPlayer ? "player-" + node.Entity.Player!.NetId : node.Entity.ModelId + "/" + node.Entity.CombatId + "/" + node.Entity.SlotName;
    private static IEnumerable<(NCreature Owner, string Element, Rect2 Bounds)> Displays(IEnumerable<NCreature> nodes)
    {
        foreach (var node in nodes)
        {
            // Native remote-role HP is intentionally hidden until actual focus.
            var health = node.GetNode<Control>("%HealthBar");
            if (health.IsVisibleInTree() && EffectiveAlpha(health) > 0.001f) yield return (node, "hp", HealthBounds(node));
            int index = 0;
            foreach (var bounds in IntentBounds(node)) yield return (node, "intent" + index++, bounds);
        }
    }

    private static float EffectiveAlpha(CanvasItem item)
    {
        float alpha = item.SelfModulate.A;
        for (Node? parent = item; parent != null; parent = parent.GetParent())
            if (parent is CanvasItem canvas) alpha *= canvas.Modulate.A;
        return alpha;
    }

    private static void RecordDisplayOverlaps((NCreature Owner, string Element, Rect2 Bounds)[] displays,
        NCreature[] nodes, Transform2D stretch, List<string> overlaps)
    {
        for (int first = 0; first < displays.Length; first++)
            for (int second = first + 1; second < displays.Length; second++)
                if (!ReferenceEquals(displays[first].Owner, displays[second].Owner))
                    RecordOverlap(displays[first].Bounds, Name(displays[first].Owner) + "/" + displays[first].Element,
                        displays[second].Bounds, Name(displays[second].Owner) + "/" + displays[second].Element, stretch, overlaps);
        foreach (var display in displays)
            foreach (var target in nodes.Where(target => !ReferenceEquals(target, display.Owner)))
                RecordOverlap(display.Bounds, Name(display.Owner) + "/" + display.Element,
                    target.Hitbox.GetGlobalRect(), Name(target) + "/hitbox", stretch, overlaps);
    }

    private static void CheckFocusedDisplays(NCombatRoom room)
    {
        CheckNativeGeometry(room);
        ValidateDisplays(room);
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        var nodes = room.CreatureNodes.ToArray();
        var displays = Displays(nodes).ToArray();
        var overlaps = new List<string>();
        foreach (var display in displays)
            Check(viewport.GetVisibleRect().Encloses(display.Bounds) && new Rect2(Vector2.Zero, viewport.Size).Encloses(viewport.GetStretchTransform() * display.Bounds),
                context + $": focus-revealed {Name(display.Owner)}/{display.Element}={display.Bounds} remains inside the viewport.");
        for (int first = 0; first < nodes.Length; first++)
            for (int second = first + 1; second < nodes.Length; second++)
                RecordOverlap(nodes[first].Hitbox.GetGlobalRect(), Name(nodes[first]) + "/hitbox",
                    nodes[second].Hitbox.GetGlobalRect(), Name(nodes[second]) + "/hitbox", viewport.GetStretchTransform(), overlaps);
        RecordDisplayOverlaps(displays, nodes, viewport.GetStretchTransform(), overlaps);
        foreach (string overlap in overlaps) Console.WriteLine($"AEONGLASS_NATIVE_LAYOUT_FOCUS_OVERLAP {context} {overlap}");
        Check(overlaps.Count == 0, context + ": actual controller focus retains the same native rectangles and intersections; only original overlaps are retained. " + string.Join("; ", overlaps));
    }

    private static void CheckNativeGeometry(NCombatRoom room)
    {
        var visibleHp = new HashSet<string>();
        RecordTransform("combat-scene", room.SceneContainer.GetGlobalTransform());
        foreach (var node in room.CreatureNodes)
        {
            string name = Name(node);
            RecordTransform(name + "/creature", node.GetGlobalTransform());
            RecordTransform(name + "/visuals", node.Visuals.GetGlobalTransform());
            RecordTransform(name + "/body", node.Visuals.GetCurrentBody().GetGlobalTransform());
            RecordTransform(name + "/intent-container", node.IntentContainer.GetGlobalTransform());
            RecordRectangle(name + "/visual-bounds", node.Visuals.Bounds.GetGlobalRect());
            RecordRectangle(name + "/hitbox", node.Hitbox.GetGlobalRect());
            var health = node.GetNode<Control>("%HealthBar");
            if (health.IsVisibleInTree() && EffectiveAlpha(health) > 0.001f)
            {
                visibleHp.Add(name + "/hp");
                RecordRectangle(name + "/hp", HealthBounds(node));
            }
            if (node.Entity.IsPlayer)
                Check(node.IsAncestorOf(node.Visuals.GetCurrentBody()) && node.Visuals.GetCurrentBody().IsVisibleInTree(),
                    context + ": each actual player body remains visibly owned by its original creature subtree.");
        }
        if (captureSource)
        {
            Check(!originalVisibleHp.TryGetValue(overlapPhase, out var previous) || previous.SetEquals(visibleHp),
                context + $": actual visible HP keys remain stable in source phase {overlapPhase}.");
            originalVisibleHp[overlapPhase] = visibleHp;
        }
        else
            Check(originalVisibleHp.TryGetValue(overlapPhase, out var source) && source.SetEquals(visibleHp),
                context + $": actual visible HP keys in phase {overlapPhase} preserve source=[{string.Join(",", source ?? [])}] actual=[{string.Join(",", visibleHp)}], including tree visibility and inherited alpha.");
    }

    private static void RecordTransform(string element, Transform2D actual)
    {
        string key = overlapPhase + "|" + element;
        if (captureSource)
        {
            Check(!originalTransforms.TryGetValue(key, out var previous) || previous.IsEqualApprox(actual),
                context + $": source transform {key} is stable in this exact native phase.");
            originalTransforms[key] = actual;
            return;
        }
        Check(originalTransforms.TryGetValue(key, out var source) && source.IsEqualApprox(actual),
            context + $": hunted transform {key}={actual} preserves source={source}, including original position and scale.");
    }

    private static void RecordRectangle(string element, Rect2 actual)
    {
        string key = overlapPhase + "|" + element;
        if (captureSource)
        {
            Check(!originalRectangles.TryGetValue(key, out var previous) || previous.IsEqualApprox(actual),
                context + $": source rectangle {key} is stable in this exact native phase.");
            originalRectangles[key] = actual;
            return;
        }
        Check(originalRectangles.TryGetValue(key, out var source) && source.IsEqualApprox(actual),
            context + $": hunted rectangle {key}={actual} preserves exact source={source}.");
    }

    private static void RecordOverlap(Rect2 first, string firstName, Rect2 second, string secondName,
        Transform2D stretch, List<string> overlaps)
    {
        if (!first.Intersects(second)) return;
        Vector2 start = new(Mathf.Max(first.Position.X, second.Position.X), Mathf.Max(first.Position.Y, second.Position.Y));
        Vector2 end = new(Mathf.Min(first.End.X, second.End.X), Mathf.Min(first.End.Y, second.End.Y));
        var intersection = new Rect2(start, end - start);
        if (string.CompareOrdinal(firstName, secondName) > 0)
        {
            (firstName, secondName) = (secondName, firstName);
            (first, second) = (second, first);
        }
        string key = overlapPhase + "|" + firstName + "|" + secondName;
        if (captureSource)
        {
            Check(!originalOverlaps.TryGetValue(key, out var previous) ||
                previous.First.IsEqualApprox(first) && previous.Second.IsEqualApprox(second) && previous.Intersection.IsEqualApprox(intersection),
                context + $": original same-phase intersection {key} retains both exact native rectangles.");
            originalOverlaps[key] = (first, second, intersection);
            Console.WriteLine($"AEONGLASS_NATIVE_LAYOUT_SOURCE_INTERSECTION {context} phase={overlapPhase} pair={key} first={first} second={second} intersection={intersection} physical={stretch * intersection}");
            return;
        }
        if (originalOverlaps.TryGetValue(key, out var source) && first.IsEqualApprox(source.First) &&
            second.IsEqualApprox(source.Second) && intersection.IsEqualApprox(source.Intersection))
        {
            observedOverlaps.Add(key);
            Console.WriteLine($"AEONGLASS_NATIVE_LAYOUT_UNCHANGED_INTERSECTION {context} phase={overlapPhase} pair={key} first={first} second={second} intersection={intersection} physical={stretch * intersection}");
            return;
        }
        overlaps.Add($"phase={overlapPhase} {firstName}={first} intersects {secondName}={second}, intersection={intersection}, physical={stretch * intersection}; no identical native source rectangles/intersection.");
    }

    private static Rect2 HealthBounds(NCreature node)
    {
        var display = node.GetNode<Control>("%HealthBar");
        return display.GetNode<Control>("%HpBarHitbox").GetGlobalRect()
            .Merge(display.GetNode<NHealthBar>("%HealthBar").HpBarContainer.GetGlobalRect())
            .Merge(HpTextBounds(node));
    }
    private static Rect2 HpTextBounds(NCreature node)
    {
        var label = HpLabel(node);
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

    private static MegaLabel HpLabel(NCreature enemy) =>
        enemy.GetNode<Control>("%HealthBar").GetNode<NHealthBar>("%HealthBar").GetNode<MegaLabel>("%HpLabel");

    private static Vector2 PhysicalHpText(NCreature enemy, Transform2D stretch)
    {
        var label = HpLabel(enemy);
        var transform = label.GetGlobalTransform();
        var minimum = label.GetCombinedMinimumSize();
        var content = new Vector2(minimum.X * transform.X.Length(), minimum.Y * transform.Y.Length());
        return (stretch * new Rect2(Vector2.Zero, content)).Size;
    }

    private static void CheckIntentIdentity(NCreature enemy)
    {
        var intentNodes = enemy.IntentContainer.GetChildren().OfType<NIntent>().ToArray();
        var modelIntents = enemy.Entity.Monster!.NextMove.Intents;
        Check(intentNodes.Length == modelIntents.Count, context + ": every actual current-move intent owns one native intent node.");
        for (int index = 0; index < intentNodes.Length; index++)
        {
            var node = intentNodes[index];
            var expected = modelIntents[index];
            var targets = (IEnumerable<Creature>)AccessTools.Field(typeof(NIntent), "_targets").GetValue(node)!;
            Check(ReferenceEquals(AccessTools.Field(typeof(NIntent), "_intent").GetValue(node), expected) &&
                ReferenceEquals(AccessTools.Field(typeof(NIntent), "_owner").GetValue(node), enemy.Entity) &&
                targets.SequenceEqual(enemy.Entity.CombatState!.Players.Select(player => player.Creature)),
                context + ": displayed intent retains the exact current-move model, owner and native target sequence.");
            string text = expected is MegaCrit.Sts2.Core.MonsterMoves.Intents.AttackIntent or MegaCrit.Sts2.Core.MonsterMoves.Intents.StatusIntent
                ? expected.GetIntentLabel(targets, enemy.Entity).GetFormattedText() ?? string.Empty : string.Empty;
            var value = node.GetNode<MegaRichTextLabel>("%Value");
            Check(value.Text == text && (string.IsNullOrEmpty(text) || value.GetParsedText().Any(char.IsDigit)),
                context + $": the actual native intent text '{value.Text}' is the current model's formatted value '{text}', including every numeric opening value.");
        }
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
            // Reuse the actual safe native point; do not follow a moved target.
            Vector2 point = originalRectangles[width + "/opening|" + Name(enemy) + "/hitbox"].GetCenter();
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
                await Until(() => room.CreatureNodes.All(node =>
                {
                    float alpha = node.GetNode<Control>("%HealthBar").Modulate.A;
                    return alpha < 0.00001f || alpha > 0.99999f;
                }), "all native HP show/hide fades settle at this actual focus phase");
                overlapPhase = width + $"/direction={direction}/step={step}/focus={Name(ring[expected])}";
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
    private static Task Frames(int count) => HuntingLayoutNativeHost.Frames(count);
    private static Task Until(Func<bool> ready, string stage) =>
        HuntingLayoutNativeHost.Until(ready, context + ": the Aeonglass native layout did not reach " + stage + ".");

    private static string State(Battle battle) => JsonSerializer.Serialize(new
    {
        RunRng = battle.Run.Rng.ToSerializable(),
        Players = battle.Combat.Players.Select(player => new
        {
            Data = player.ToSerializable(), player.PlayerCombatState!.Energy, player.Creature.Block,
            Powers = player.Creature.Powers.Select(power => new { power.Id, power.Amount }).ToArray(),
            Piles = new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Play }
                .Select(pile => pile.GetPile(player).Cards.Select(card => card.ToSerializable()).ToArray()).ToArray()
        }).ToArray(),
        Enemies = battle.Combat.Enemies.Select(enemy => new
        {
            enemy.CurrentHp, enemy.MaxHp, enemy.Block, enemy.Monster!.NextMove.Id,
            Rng = MonsterRng(enemy.Monster),
            WitherUpgradeCount = ((Aeonglass)enemy.Monster).WitherUpgradeCount,
            Powers = enemy.Powers.Select(power => new { power.Id, power.Amount, power.DisplayAmount }).ToArray()
        }).ToArray()
    }, rngJson);

    private static string MonsterRng(MonsterModel monster) =>
#if STS2_PUBLIC
        $"{monster.Rng.Seed}/{monster.Rng.Counter}";
#else
        JsonSerializer.Serialize(monster.Rng.ToSerializable(), rngJson);
#endif

    private sealed class Battle : IDisposable
    {
        private readonly ulong? previousNetId = LocalContext.NetId;
        internal RunState Run { get; }
        internal CombatState Combat { get; }
        internal EncounterModel Encounter { get; }
        internal bool Hunted { get; }

        internal Battle(int count, bool hunted)
        {
            Hunted = hunted;
            var players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
            Run = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING87_AEONGLASS_UI");
            Run.CurrentActIndex = 2;
            Encounter = ModelDb.Encounter<AeonglassBoss>().ToMutable();
            Combat = new CombatState(Encounter, Run) { RoundNumber = 1 };
            try
            {
                RunManager.Instance.SetUpTest(Run, new NetSingleplayerGameService());
                LocalContext.NetId = players[0].NetId;
                CombatManager.Instance.Reset(true);
                Combat.AddPlayer(players[0]); players[0].ResetCombatState();
                CombatManager.Instance.SetUpCombat(Combat);
                foreach (var player in players.Skip(1)) { player.RunState = Run; Combat.AddPlayer(player); player.ResetCombatState(); }
                Run.PushRoom(new CombatRoom(Combat));
                JigsawCheckpoint.Capture(Run, AcceptedRoot(players[0].NetId, hunted, JigsawRoom.Boss)); JigsawNativeSession.Restore(Run);
                var projection = JigsawEntryProjection.Append(Run.Map, 2, JigsawRoom.Boss);
                Run.Map = projection.Map;
                Check(Run.AddVisitedMapCoord(projection.Point.coord), context + ": native monster RNG has an actual visited map coordinate.");
                Encounter.GenerateMonstersWithSlots(Run);
                foreach (var (monster, slot) in Encounter.MonstersWithSlots)
                {
                    Combat.AddCreature(Combat.CreateCreature(monster, CombatSide.Enemy, slot));
                    CombatManager.Instance.AddCreature(monster.Creature);
                    monster.RollMove(Combat.PlayerCreatures);
                }
                Check(Run.Players.Count == 1 && Combat.Players.Count == count && Run.Act is Glory &&
                    JigsawHuntingNative.RootIsHunted(players[0]) == hunted,
                    context + ": one legal run owner binds all actual combat roles to the accepted top-level Glory Boss root; these are not remote clients.");
            }
            catch { Dispose(); throw; }
        }

        internal void SelectNativeBosses()
        {
            var act = Run.Act;
            act.SetBossEncounter(ModelDb.Encounter<AeonglassBoss>());
            act.SetSecondBossEncounter(null);
            Check(act.PullNextEncounter(RoomType.Boss) is AeonglassBoss && !act.HasSecondBoss &&
                act.ToSave().SerializableRooms.BossEncountersVisited == 0,
                context + ": the native Glory act selects the displayed Aeonglass at its actual first Boss room.");
        }

        public void Dispose()
        {
            JigsawNativeSession.TryGet(Run)?.Close(); CombatManager.Instance.Reset(true);
            RunManager.Instance.CleanUp(false); LocalContext.NetId = previousNetId;
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
                if (map.Nodes.Single(node => node.Id == next).Room != JigsawRoom.SecondBoss &&
                    previous.TryAdd(next, current)) pending.Enqueue(next);
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
}
