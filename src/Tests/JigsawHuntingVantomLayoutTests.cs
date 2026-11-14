using AllMatters.Jigsaw;
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

internal static class JigsawHuntingVantomLayoutTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
        (from root in new[] { JigsawRoom.Boss, JigsawRoom.SecondBoss }
         from count in new[] { 1, 4 }
         from physical in new[] { new Vector2I(1280, 720), new Vector2I(1680, 720) }
         select ($"Jigsaw Vantom native layout hunted {root} roles={count} physical={physical.X}x{physical.Y}",
             (Func<Task>)(() => Layout(root, count, physical)))).ToArray();

    private static string context = "not mounted";
    private static readonly JsonSerializerOptions rngJson = new() { IncludeFields = true };
    private static string overlapPhase = "opening";
    private static Dictionary<string, float> originalOverlaps = [];
    private static bool captureSource;
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

    private static async Task Layout(JigsawRoom root, int count, Vector2I physical)
    {
        context = $"root={root} roles={count} physical={physical}";
        Source source;
        using (var safe = new VantomBattle(count, false, root))
        {
            source = ReadSource(safe);
            using var safeHost = new NativeHost();
            await Initialize(safeHost, safe);
            context = $"root={root} roles={count} physical={physical} source=safe";
            var native = await PrepareViewport(safeHost, safe, physical);
            originalOverlaps = [];
            overlapPhase = "opening";
            captureSource = true;
            try
            {
                CaptureSourceGeometry(combatNode!, native);
                await CheckController(safeHost.Controller, combatNode!, native);
            }
            finally { captureSource = false; }
            source.Overlaps.Add(physical, originalOverlaps);
            source.NativeHpHeights.Add(physical, PhysicalHpText(native.Single(), ((SceneTree)Engine.GetMainLoop()).Root.GetStretchTransform()).Y);
        }
        context = $"root={root} roles={count} physical={physical} source=hunted";
        using var battle = new VantomBattle(count, true, root);
        CheckGeneration(battle, source);
        using var host = new NativeHost();
        await Initialize(host, battle);
        CheckOpening(battle);
        originalOverlaps = source.Overlaps[physical];
        var identities = battle.Combat.Enemies.Select(creature => (Creature: creature, Monster: creature.Monster!, Move: creature.Monster!.NextMove)).ToArray();
        var opening = Opening(battle.Combat.Enemies);
        var playerHealth = battle.Combat.Players.Select(player => (player.Creature.CurrentHp, player.Creature.MaxHp)).ToArray();
        string runRng = JsonSerializer.Serialize(battle.Run.Rng.ToSerializable(), rngJson);
        void CheckState(string phase) => Check(battle.Combat.Enemies.Count == identities.Length &&
            battle.Combat.Enemies.Select((creature, index) => ReferenceEquals(creature, identities[index].Creature) &&
                ReferenceEquals(creature.Monster, identities[index].Monster) &&
                ReferenceEquals(creature.Monster!.NextMove, identities[index].Move)).All(same => same) &&
            Opening(battle.Combat.Enemies) == opening &&
            battle.Combat.Players.Select(player => (player.Creature.CurrentHp, player.Creature.MaxHp)).SequenceEqual(playerHealth) &&
            JsonSerializer.Serialize(battle.Run.Rng.ToSerializable(), rngJson) == runRng,
            context + $": {phase} preserves the same enemy/monster/move objects, their HP/powers/monster RNG, player HP and complete run RNG.");
        var nodes = await PrepareViewport(host, battle, physical);
        CheckState("native resize");
        Check(!battle.Encounter.HasScene && nodes.Select(node => node.Entity.Monster).All(monster => monster != null) &&
            nodes[0].Entity.Monster is Inklet && nodes[1].Entity.Monster is Vantom && nodes[2].Entity.Monster is Inklet &&
            nodes.All(node => node.Entity.SlotName == null),
            context + ": the three original automatic nodes preserve Inklet/Vantom/Inklet left-to-right order without invented scene markers.");
        overlapPhase = "opening";
        CheckGeometry(combatNode!, nodes, count, physical, source.NativeHpHeights[physical]);
        await CheckMouse(host.Target, nodes);
        await CheckController(host.Controller, combatNode!, nodes);
        CheckOpening(battle);
        CheckState("native mouse and directional input");
    }

    private sealed record Source((ModelId Id, string? Slot)[] Monsters,
        Dictionary<Vector2I, Dictionary<string, float>> Overlaps, Dictionary<Vector2I, float> NativeHpHeights);

    private static async Task Initialize(NativeHost host, VantomBattle battle)
    {
        try
        {
            await host.Mount(battle);
            // Await each actual original initialization once after nodes exist.
            // Native initializers may apply powers or change their own visuals.
            foreach (var creature in battle.Combat.Creatures.ToArray())
                await CombatManager.Instance.AfterCreatureAdded(creature);
            // This is an opening-only transition. Later remote-role focus
            // deliberately hides the local HP through SetRemotePlayerFocused.
            await Until(() => combatNode!.CreatureNodes.Where(node => !node.Entity.IsPlayer || LocalContext.IsMe(node.Entity))
                .All(node => node.GetNode<Control>("%HealthBar").Modulate.A > 0.99999f), "actual opening HP fade");
            await Frames(4);
        }
        catch (Exception error)
        {
            throw new InvalidOperationException("VANTOM_LAYOUT_HOST " + context + ": native preparation failed before geometry measurement.", error);
        }
    }

    private static async Task<NCreature[]> PrepareViewport(NativeHost host, VantomBattle battle, Vector2I physical)
    {
        host.Window.Size = physical;
        var room = combatNode!;
        room.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var enemies = battle.Combat.Enemies.Where(creature => creature.IsAlive).ToArray();
        await Until(() => enemies.All(creature => room.GetCreatureNode(creature) is { Visible: true }), "all native enemy nodes");
        foreach (var enemy in enemies) await room.GetCreatureNode(enemy)!.RefreshIntents();
        await Until(() => enemies.All(creature => room.GetCreatureNode(creature)!.IntentContainer.Modulate.A > 0.95f), "actual intent fade");
        await Frames(4);
        return enemies.Select(creature => room.GetCreatureNode(creature)!).OrderBy(node => node.GlobalPosition.X).ToArray();
    }

    private static void CaptureSourceGeometry(NCombatRoom room, NCreature[] enemies)
    {
        var nodes = enemies.Concat(room.CreatureNodes.Where(node => node.Entity.IsPlayer)).ToArray();
        var stretch = ((SceneTree)Engine.GetMainLoop()).Root.GetStretchTransform();
        foreach (var enemy in enemies) LogNativeNumbers(enemy, stretch, "source");
        Console.WriteLine($"VANTOM_LAYOUT_SOURCE {context} logical={((SceneTree)Engine.GetMainLoop()).Root.GetVisibleRect()} " +
            string.Join("; ", nodes.Select(node => $"{Name(node)}:visual={node.Visuals.Bounds.GetGlobalRect()},hit={node.Hitbox.GetGlobalRect()},hp={HealthBounds(node)},intent=[{string.Join(",", IntentBounds(node))}]")));
        var overlaps = new List<string>();
        for (int first = 0; first < nodes.Length; first++)
            for (int second = first + 1; second < nodes.Length; second++)
                RecordOverlap(nodes[first].Hitbox.GetGlobalRect(), Name(nodes[first]) + "/hitbox",
                    nodes[second].Hitbox.GetGlobalRect(), Name(nodes[second]) + "/hitbox", stretch, overlaps);
        RecordDisplayOverlaps(Displays(nodes).ToArray(), nodes, stretch, overlaps);
    }

    private static Source ReadSource(VantomBattle battle)
    {
        var monsters = battle.Encounter.MonstersWithSlots.Select(pair => (pair.Item1.Id, pair.Item2)).ToArray();
        return new Source(monsters, [], []);
    }

    private static void CheckGeneration(VantomBattle battle, Source source)
    {
        var generated = battle.Encounter.MonstersWithSlots;
        var boss = generated.Count > 1 ? generated[1].Item1.Creature : null;
        Console.WriteLine($"VANTOM_GENERATION {context} source=[{string.Join(",", source.Monsters.Select(pair => $"{pair.Id}/{pair.Slot}"))}] " +
            $"actual=[{string.Join(",", generated.Select(pair => $"{pair.Item1.Id}/{pair.Item2}"))}] " +
            $"actualBoss={(boss == null ? "missing" : Opening([boss]))}");
        Check(source.Monsters.Length == 1 && source.Monsters[0].Id == ModelDb.Monster<Vantom>().Id && source.Monsters[0].Slot == null,
            context + ": safe source is exactly one unslotted native Vantom.");
        Check(generated.Count == 3 && generated[0].Item1 is Inklet && generated[1].Item1 is Vantom && generated[2].Item1 is Inklet &&
            generated.All(pair => pair.Item2 == null),
            context + ": hunted generation adds exactly two unslotted Inklets around the native Vantom.");
        Check(boss != null && boss.CurrentHp == boss.MaxHp && boss.MaxHp >= boss.Monster!.MinInitialHp,
            context + ": the original Vantom enters at its actual full native multiplayer HP; flanking Inklets have their own native combat IDs and RNG.");
    }

    private static void CheckOpening(VantomBattle battle)
    {
        if (!JigsawHuntingNative.RootIsHunted(battle.Combat.Players[0])) return;
        var generated = battle.Encounter.MonstersWithSlots;
        int SlipperyAmount(Creature creature, int amount) => battle.Combat.Players.Count == 1 ? amount :
            (int)ModelDb.Power<SlipperyPower>().GetScaledAmountForMultiplayer(battle.Combat, creature, amount, creature, null);
        Check(generated.Count == 3 && generated[0].Item1 is Inklet && generated[1].Item1 is Vantom && generated[2].Item1 is Inklet &&
            !ReferenceEquals(generated[0].Item1, generated[2].Item1) &&
            generated[1].Item1.Creature.GetPowerAmount<SlipperyPower>() ==
                SlipperyAmount(generated[1].Item1.Creature, ((Vantom)generated[1].Item1).SlipperyAmt) &&
            generated.Where((_, index) => index != 1).All(pair => pair.Item1.Creature.CurrentHp == pair.Item1.Creature.MaxHp &&
                pair.Item1.NextMove.Id == "JAB_MOVE" &&
                pair.Item1.Creature.GetPowerAmount<SlipperyPower>() == SlipperyAmount(pair.Item1.Creature, 1) &&
                pair.Item1.Creature.Powers.All(power => power.GetType().Name != "MinionPower")),
            context + ": both independent normal Inklets retain full native HP, Jab, native scaled Slippery and no Minion.");
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
        JsonSerializer.Serialize(monster.Rng.ToSerializable(), rngJson);
#endif

    private static void CheckGeometry(NCombatRoom room, NCreature[] enemies, int count, Vector2I physical, float nativeHpHeight)
    {
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        var logical = viewport.GetVisibleRect();
        var stretch = viewport.GetStretchTransform();
        var physicalBounds = new Rect2(Vector2.Zero, physical);
        Check((stretch * logical).Position.IsEqualApprox(Vector2.Zero) && (stretch * logical).Size.IsEqualApprox(physical),
            context + ": CanvasItems Expand maps the whole logical canvas to the requested physical window.");
        var players = room.CreatureNodes.Where(node => node.Entity.IsPlayer).ToArray();
        var nodes = enemies.Concat(players).ToArray();
        foreach (var enemy in enemies) LogNativeNumbers(enemy, stretch, "hunted");
        Console.WriteLine($"VANTOM_LAYOUT {context} logical={logical} " + string.Join("; ", nodes.Select(node =>
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
                RecordOverlap(nodes[first].Hitbox.GetGlobalRect(), Name(nodes[first]) + "/hitbox",
                    nodes[second].Hitbox.GetGlobalRect(), Name(nodes[second]) + "/hitbox", stretch, overlaps);
        var displays = Displays(nodes).ToArray();
        foreach (var enemy in enemies)
        {
            var hpLabel = HpLabel(enemy);
            var hpTextSize = PhysicalHpText(enemy, stretch);
            Check(hpLabel.IsVisibleInTree() && hpLabel.Modulate.A > 0.95f &&
                hpLabel.Text == $"{enemy.Entity.CurrentHp}/{enemy.Entity.MaxHp}" &&
                hpLabel.Size.X >= hpLabel.GetCombinedMinimumSize().X && hpLabel.Size.Y >= hpLabel.GetCombinedMinimumSize().Y &&
                hpTextSize.X > 0 && hpTextSize.Y >= nativeHpHeight * enemy.Scale.Y * 0.85f,
                context + $": {Name(enemy)} shows its true native HP numbers with complete text at {hpTextSize} physical pixels, at least 85% of the same encounter's safe native height after the actual native scale {enemy.Scale.Y}.");
            var values = IntentValues(enemy).Where(value => value.IsVisibleInTree() && !string.IsNullOrWhiteSpace(value.GetParsedText())).ToArray();
            foreach (var value in values)
            {
                var valueTransform = value.GetGlobalTransform();
                var textSize = new Vector2(value.GetContentWidth() * valueTransform.X.Length(), value.GetContentHeight() * valueTransform.Y.Length());
                var physicalText = stretch * new Rect2(Vector2.Zero, textSize);
                Console.WriteLine($"VANTOM_LAYOUT_INTENT_VALUE {context} entity={Name(enemy)} scale={enemy.Scale} text={value.GetParsedText()} rect={value.GetGlobalRect()} content=({value.GetContentWidth()},{value.GetContentHeight()}) physicalContent={physicalText.Size} outline={value.GetThemeConstant("outline_size")} envelope={IntentValueBounds(value)}");
                Check(value.GetContentWidth() > 0 && value.GetContentHeight() > 0,
                    context + ": every visible nonempty native intent value has laid-out text content.");
                if (value.GetParsedText().Any(char.IsDigit))
                    Check(physicalText.Size.X > 0 && physicalText.Size.Y >= nativeHpHeight * enemy.Scale.Y * 0.75f &&
                        logical.Encloses(IntentValueBounds(value)) && physicalBounds.Encloses(stretch * IntentValueBounds(value)),
                        context + $": {Name(enemy)} intent numbers retain at least 75% of the safe native HP text height and their actual content/outline envelope fits the viewport; content={physicalText.Size}, native HP height={nativeHpHeight}.");
            }
            var intents = IntentBounds(enemy).ToArray();
            Check(enemy.IntentContainer.IsVisibleInTree() && intents.Length > 0 && enemy.GetNode<Control>("%HealthBar").IsVisibleInTree(),
                context + $": {Name(enemy)} retains its visible native intent and HP display.");
        }
        foreach (var display in displays)
            Check(logical.Encloses(display.Bounds) && physicalBounds.Encloses(stretch * display.Bounds),
                context + $": {Name(display.Owner)}/{display.Element}={display.Bounds} fits logical/physical bounds; transformed={stretch * display.Bounds}.");
        RecordDisplayOverlaps(displays, nodes, stretch, overlaps);
        foreach (string overlap in overlaps) Console.WriteLine($"VANTOM_LAYOUT_OVERLAP {context} {overlap}");
        Check(overlaps.Count == 0, context + ": the hunted layout adds no cross-entity target/display overlap and enlarges no existing native overlap. " + string.Join("; ", overlaps));
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
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        var nodes = room.CreatureNodes.ToArray();
        var displays = Displays(nodes).ToArray();
        var overlaps = new List<string>();
        foreach (var display in displays)
            Check(viewport.GetVisibleRect().Encloses(display.Bounds) && new Rect2(Vector2.Zero, viewport.Size).Encloses(viewport.GetStretchTransform() * display.Bounds),
                context + $": focus-revealed {Name(display.Owner)}/{display.Element}={display.Bounds} remains inside the viewport.");
        RecordDisplayOverlaps(displays, nodes, viewport.GetStretchTransform(), overlaps);
        foreach (string overlap in overlaps) Console.WriteLine($"VANTOM_LAYOUT_FOCUS_OVERLAP {context} {overlap}");
        if (!captureSource)
            Check(overlaps.Count == 0, context + ": actual controller focus adds no player or enemy display/target overlap beyond the same native focus state. " + string.Join("; ", overlaps));
    }

    private static void RecordOverlap(Rect2 first, string firstName, Rect2 second, string secondName,
        Transform2D stretch, List<string> overlaps)
    {
        if (!first.Intersects(second)) return;
        Vector2 start = new(Mathf.Max(first.Position.X, second.Position.X), Mathf.Max(first.Position.Y, second.Position.Y));
        Vector2 end = new(Mathf.Min(first.End.X, second.End.X), Mathf.Min(first.End.Y, second.End.Y));
        var intersection = new Rect2(start, end - start);
        string pair = string.CompareOrdinal(firstName, secondName) < 0 ? firstName + "|" + secondName : secondName + "|" + firstName;
        string key = overlapPhase + "|" + pair;
        float area = intersection.Size.X * intersection.Size.Y;
        if (captureSource)
        {
            originalOverlaps[key] = originalOverlaps.TryGetValue(key, out float previous) ? Mathf.Max(previous, area) : area;
            Console.WriteLine($"VANTOM_LAYOUT_NATIVE_SOURCE_OVERLAP {context} {firstName}={first} intersects {secondName}={second}, overlapLogical={intersection}, overlapPhysical={stretch * intersection}");
            return;
        }
        if (originalOverlaps.TryGetValue(key, out float originalArea) && area <= originalArea + 0.5f)
        {
            Console.WriteLine($"VANTOM_LAYOUT_RETAINED_NATIVE_OVERLAP {context} pair={key} sourceArea={originalArea} actualArea={area} logical={intersection} physical={stretch * intersection}");
            return;
        }
        overlaps.Add($"{firstName}={first} intersects {secondName}={second}, overlapLogical={intersection}, overlapPhysical={stretch * intersection}");
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

    private static void LogNativeNumbers(NCreature enemy, Transform2D stretch, string phase)
    {
        var hpLabel = HpLabel(enemy);
        var hpMinimum = hpLabel.GetCombinedMinimumSize();
        Console.WriteLine($"VANTOM_NATIVE_NUMBER {context} phase={phase} entity={Name(enemy)} kind=hp text={hpLabel.Text} visible={hpLabel.IsVisibleInTree()} alpha={hpLabel.Modulate.A} size={hpLabel.Size} minimum={hpMinimum} fontSize={hpLabel.GetThemeFontSize("font_size")} physicalMinimum={PhysicalHpText(enemy, stretch)}");
        foreach (var value in IntentValues(enemy).Where(value => value.IsVisibleInTree() && !string.IsNullOrWhiteSpace(value.GetParsedText())))
        {
            var transform = value.GetGlobalTransform();
            var content = new Vector2(value.GetContentWidth() * transform.X.Length(), value.GetContentHeight() * transform.Y.Length());
            Console.WriteLine($"VANTOM_NATIVE_NUMBER {context} phase={phase} entity={Name(enemy)} kind=intent text={value.GetParsedText()} content={content} physicalContent={(stretch * new Rect2(Vector2.Zero, content)).Size}");
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
                overlapPhase = ring[expected].Entity.IsPlayer
                    ? "focus-player-" + ring[expected].Entity.Player!.NetId : "focus-enemy";
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
        Check(ready(), context + ": the Vantom native layout did not reach " + stage + ".");
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
        private readonly Harmony patches = new("AllMatters.Tests.VantomNativeLayout");
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
        internal async Task Mount(VantomBattle battle)
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
            battle.SelectNativeBosses();
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
                patches.Patch(AccessTools.Method(typeof(NGame), method), prefix: new HarmonyMethod(typeof(JigsawHuntingVantomLayoutTests), nameof(SkipLifecycle)));
            patches.Patch(AccessTools.Method(typeof(SfxCmd), nameof(SfxCmd.Play), [typeof(string), typeof(float)]), prefix: new HarmonyMethod(typeof(JigsawHuntingVantomLayoutTests), nameof(SkipAudio)));
            foreach (string method in new[] { "_Ready", "_Process" })
                patches.Patch(AccessTools.Method(typeof(NMapScreen), method), prefix: new HarmonyMethod(typeof(JigsawHuntingVantomLayoutTests), nameof(SkipLifecycle)));
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
            patches.Patch(AccessTools.Method(typeof(Creature), nameof(Creature.GetCreatureNode)), prefix: new HarmonyMethod(typeof(JigsawHuntingVantomLayoutTests), nameof(CreatureNode)));
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

    private sealed class VantomBattle : IDisposable
    {
        private readonly ulong? previousNetId = LocalContext.NetId;
        internal RunState Run { get; }
        internal CombatState Combat { get; }
        internal EncounterModel Encounter { get; }
        private readonly JigsawRoom root;

        internal VantomBattle(int count, bool hunted, JigsawRoom root)
        {
            this.root = root;
            var players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
            Run = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Overgrowth>()], seed: "HUNTING84_VANTOM_LAYOUT");
            Encounter = ModelDb.Encounter<VantomBoss>().ToMutable();
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
                JigsawCheckpoint.Capture(Run, AcceptedRoot(players[0].NetId, hunted, root)); JigsawNativeSession.Restore(Run);
                var projection = JigsawEntryProjection.Append(Run.Map, 1, root);
                Run.Map = projection.Map;
                Check(Run.AddVisitedMapCoord(projection.Point.coord), context + ": native monster RNG has an actual visited map coordinate.");
                Encounter.GenerateMonstersWithSlots(Run);
                foreach (var (monster, slot) in Encounter.MonstersWithSlots)
                {
                    Combat.AddCreature(Combat.CreateCreature(monster, CombatSide.Enemy, slot));
                    CombatManager.Instance.AddCreature(monster.Creature);
                    monster.RollMove(Combat.PlayerCreatures);
                }
                Check(Run.Players.Count == 1 && Combat.Players.Count == count && JigsawHuntingNative.RootIsHunted(players[0]) == hunted,
                    context + ": one legal run owner binds all actual combat roles to the accepted top-level Overgrowth root.");
            }
            catch { Dispose(); throw; }
        }

        internal void SelectNativeBosses()
        {
            var act = Run.Act;
            var selected = ModelDb.Encounter<VantomBoss>();
            act.SetBossEncounter(root == JigsawRoom.SecondBoss
                ? act.AllBossEncounters.First(encounter => encounter.Id != selected.Id) : selected);
            act.SetSecondBossEncounter(root == JigsawRoom.SecondBoss ? selected : null);
            if (root == JigsawRoom.SecondBoss) act.MarkRoomVisited(RoomType.Boss);
            Check(act.PullNextEncounter(RoomType.Boss) is VantomBoss &&
                act.HasSecondBoss == (root == JigsawRoom.SecondBoss) &&
                act.ToSave().SerializableRooms.BossEncountersVisited == (root == JigsawRoom.SecondBoss ? 1 : 0),
                context + ": the native act selects the displayed Vantom at this exact first/second Boss room.");
        }

        public void Dispose()
        {
            JigsawNativeSession.TryGet(Run)?.Close(); CombatManager.Instance.Reset(true);
            RunManager.Instance.CleanUp(false); LocalContext.NetId = previousNetId;
        }
    }

    private static JigsawRun AcceptedRoot(ulong owner, bool hunted, JigsawRoom roomType)
    {
        var map = JigsawRun.Create(75, doubleBoss: roomType == JigsawRoom.SecondBoss, players: [owner]);
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
                if ((roomType == JigsawRoom.SecondBoss || map.Nodes.Single(node => node.Id == next).Room != JigsawRoom.SecondBoss) &&
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
