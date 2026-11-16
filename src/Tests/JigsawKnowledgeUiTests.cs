using AllMatters.Jigsaw;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Pooling;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.addons.mega_text;

internal static class JigsawKnowledgeUiTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw Knowledge native Tender choice releases its owner screen and restores combat input", Tender)];

    private static NCombatRoom? combatNode;
    private static NChooseACardSelectionScreen? selectedScreen;
    private static Task<IEnumerable<CardModel>>? selectionTask;
    private static int selectionCalls;
    private static int removals;

    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static bool SkipAudio() => false;
    private static bool SkipNativeLifecycle() => false;
    private static void ObserveSelection(NChooseACardSelectionScreen __instance, Task<IEnumerable<CardModel>> __result)
    {
        selectedScreen = __instance; selectionTask = __result; selectionCalls++;
    }
    private static void ObserveRemoval(IOverlayScreen screen)
    {
        if (ReferenceEquals(screen, selectedScreen)) removals++;
    }
    // Earlier backend actions can inline the TestMode lookup. Resolve the real
    // room's actual nodes once this case owns a mounted combat scene.
    private static bool CreatureNode(MegaCrit.Sts2.Core.Entities.Creatures.Creature __instance, ref NCreature? __result)
    {
        __result = combatNode?.GetCreatureNode(__instance);
        return false;
    }

    private sealed class FirstChoice : MegaCrit.Sts2.Core.TestSupport.ICardSelector
    {
        internal int Calls;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            var cards = options.ToArray();
            Check(cards is [MindRot, KnowledgeCalamityChoice], "The first actual punished curse offers its first-round pair.");
            Calls++;
            return Task.FromResult<IEnumerable<CardModel>>([cards[0]]);
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) => throw new InvalidOperationException();
    }

    private static JigsawRun PunishedBoss(ulong owner)
    {
        var map = JigsawRun.Create(75, players: [owner]);
        Ok(map.ChooseEnd(owner, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        Ok(map.CompleteAncient(owner)); Ok(map.Continue(owner)); Ok(map.SetRemainingSteps(0));
        int boss = map.Nodes.Single(node => node.Room == JigsawRoom.Boss).Id;
        var queue = new Queue<int>();
        var previous = new Dictionary<int, int> { [0] = -1 };
        queue.Enqueue(0);
        while (queue.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (previous.TryAdd(next, current)) queue.Enqueue(next);
            }
        var path = new List<int>();
        for (int node = boss; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!; Ok(map.ConfirmEnter(entry.Id));
            if (node != boss) Ok(map.CompleteRoom(entry.Id));
        }
        return map;
    }

    private static async Task Tender()
    {
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>()], seed: "KNOWLEDGE_85_TENDER_UI");
        run.CurrentActIndex = 1;
        using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<KnowledgeDemonBoss>().ToMutable(), setUpMonster: false);
        var room = new CombatRoom(battle.Combat);
        run.PushRoom(room);
        JigsawCheckpoint.Capture(run, PunishedBoss(player.NetId));
        JigsawNativeSession.Restore(run);
        var session = JigsawNativeSession.TryGet(run)!;
        Check(JigsawHuntingNative.RootIsHunted(player) && run.CurrentRoomCount == 1,
            "One native owner is bound to the accepted punished top-level Hive Boss root.");
        battle.Monster.SetUpForCombat();
        player.Creature.SetMaxHpInternal(2000); player.Creature.SetCurrentHpInternal(2000);
        var first = new FirstChoice();
        using (HeadlessNativeApi.UseSelector(run, first))
            foreach (string move in new[] { "CURSE_OF_KNOWLEDGE_MOVE", "SLAP_MOVE", "KNOWLEDGE_OVERWHELMING_MOVE", "PONDER_MOVE" })
            {
                battle.Monster.RollMove(battle.Combat.PlayerCreatures);
                Check(battle.Monster.NextMove.Id == move, "The unchanged native state machine reaches " + move + ".");
                await battle.Monster.PerformMove();
            }
        Check(first.Calls == 1, "The preparation selector handled only the first curse and is now disposed.");
        battle.Monster.RollMove(battle.Combat.PlayerCreatures);
        Check(battle.Monster.NextMove.Id == "CURSE_OF_KNOWLEDGE_MOVE", "Four native actions naturally reach the second curse.");
#if STS2_PUBLIC
        Check(CardSelectCmd.Selector == null, "The second curse has no public selector bypass.");
#else
        Check(RunManager.Instance.CardSelectorStack.GetSelector(player) == null && RunManager.Instance.CardSelectorStack.LocalSelector == null,
            "The second curse has neither a stacked/test selector nor a local selector bypass.");
#endif
        var tree = (SceneTree)Engine.GetMainLoop();
        var viewport = tree.Root;
        var size = viewport.Size; var content = viewport.ContentScaleSize;
        var mode = viewport.ContentScaleMode; var aspect = viewport.ContentScaleAspect;
        bool testing = TestMode.IsOn;
        var autoSlayer = NonInteractiveMode.AutoSlayerCheck;
        var consoleField = AccessTools.Field(typeof(MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole), "_instance");
        object? previousConsole = consoleField.GetValue(null);
        var pools = (System.Collections.IDictionary)AccessTools.Field(typeof(NodePool), "_pools").GetValue(null)!;
        object? previousCards = pools[typeof(NCard)], previousHolders = pools[typeof(NGridCardHolder)];
        bool replacedPools = false;
        NodePool<NCard>? cardsPool = null;
        NodePool<NGridCardHolder>? holdersPool = null;
        IDisposable? atlases = null;
        JigsawNativeMapUiHost? ui = null;
        Harmony? patches = null;
        NMapScreen? map = null;
        NOverlayStack? overlays = null;
        NCapstoneContainer? capstone = null;
        NSceneContainer? rooms = null;
        NHotkeyManager? hotkeys = null;
        NControllerManager? controller = null;
        NCursorManager? cursor = null;
        NTargetManager? target = null;
        NMultiplayerPlayerStateContainer? players = null;
        Node? hoverTips = null;
        var addedInputActions = new List<StringName>();
        MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole? console = null;
        MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.NScreenShake? shake = null;
        try
        {
            viewport.ContentScaleSize = new Vector2I(1920, 1080);
            viewport.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
            viewport.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;
            viewport.Size = new Vector2I(1920, 1080);
            HeadlessUiHost.EnsureInputActions();
            foreach (string name in new[] { "debug_hide_intents", "debug_hide_combat_ui", "debug_hide_play_container", "debug_hide_hand",
                "debug_hide_hp_bars", "debug_hide_text_vfx", "debug_hide_targeting_ui", "debug_slow_rewards", "debug_hide_mp_targeting", "debug_hide_mp_intents" }
                .Concat(Enumerable.Range(1, 10).Select(index => "mega_select_card_" + index))
                .Concat(new[] { DebugHotkey.speedUp.ToString(), DebugHotkey.speedDown.ToString(),
                    DebugHotkey.hideProceedButton.ToString(), DebugHotkey.hideHoverTips.ToString() }))
            {
                StringName action = name;
                if (InputMap.HasAction(action)) continue;
                InputMap.AddAction(action); addedInputActions.Add(action);
            }
            atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
            JigsawHuntingNativeTests.RegisterScripts();
            ui = new JigsawNativeMapUiHost();
            RunManager.Instance.GenerateRooms();
            await ui.Open(session); session.HideMap();
            var game = NGame.Instance!;
            var nativeRun = NRun.Instance!;
            // This host supplies an empty full-screen GlobalUi wrapper. Its
            // native modal children, rather than the empty wrapper, own input.
            nativeRun.GlobalUi.MouseFilter = Control.MouseFilterEnum.Ignore;
            hotkeys = new NHotkeyManager();
            AccessTools.Property(typeof(NGame), nameof(NGame.HotkeyManager)).SetValue(game, hotkeys);
            await HeadlessUiHost.Mount(hotkeys);
            controller = new NControllerManager();
            AccessTools.Property(typeof(NInputManager), "ControllerManager").SetValue(game.InputManager, controller);
            cursor = new NCursorManager();
            AccessTools.Property(typeof(NGame), nameof(NGame.CursorManager)).SetValue(game, cursor);
            // Native creature hover still reads these owners in singleplayer.
            // No remote rows or targeting session are opened by this case.
            target = new NTargetManager();
            players = new NMultiplayerPlayerStateContainer();
            players.Initialize(run);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.TargetManager)).SetValue(nativeRun.GlobalUi, target);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MultiplayerPlayerContainer)).SetValue(nativeRun.GlobalUi, players);
            console = new MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole { Visible = false };
            consoleField.SetValue(null, console);
            shake = new MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.NScreenShake();
            AccessTools.Field(typeof(NGame), "_screenShake").SetValue(game, shake);
            patches = new Harmony("AllMatters.Tests.KnowledgeNativeUi");
            // Supply a live viewport to native tooltip layout without starting
            // Steam/audio/full-game startup or shutting down the test runner.
            foreach (string method in new[] { "_EnterTree", "_Ready", "_ExitTree" })
                patches.Patch(AccessTools.Method(typeof(NGame), method), prefix: new HarmonyMethod(typeof(JigsawKnowledgeUiTests), nameof(SkipNativeLifecycle)));
            game.MouseFilter = Control.MouseFilterEnum.Ignore;
            hoverTips = new Node { Name = "HoverTipsContainer" };
            game.AddChild(hoverTips);
            AccessTools.Property(typeof(NGame), nameof(NGame.HoverTipsContainer)).SetValue(game, hoverTips);
            patches.Patch(AccessTools.Method(typeof(SfxCmd), nameof(SfxCmd.Play), [typeof(string), typeof(float)]),
                prefix: new HarmonyMethod(typeof(JigsawKnowledgeUiTests), nameof(SkipAudio)));
            patches.Patch(AccessTools.Method(typeof(NMapScreen), "_Ready"), prefix: new HarmonyMethod(typeof(JigsawKnowledgeUiTests), nameof(SkipNativeLifecycle)));
            patches.Patch(AccessTools.Method(typeof(NMapScreen), "_Process"), prefix: new HarmonyMethod(typeof(JigsawKnowledgeUiTests), nameof(SkipNativeLifecycle)));
            map = new NMapScreen();
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MapScreen)).SetValue(nativeRun.GlobalUi, map);
            capstone = new NCapstoneContainer();
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.CapstoneContainer)).SetValue(nativeRun.GlobalUi, capstone);
            overlays = new NOverlayStack { ProcessMode = Node.ProcessModeEnum.Always, MouseFilter = Control.MouseFilterEnum.Ignore };
            // NOverlayStack's real Ready/Push/Remove own this full-canvas backstop.
            var backstop = new Control { Name = "OverlayBackstop" };
            overlays.AddChild(backstop);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.Overlays)).SetValue(nativeRun.GlobalUi, overlays);
            AccessTools.Property(typeof(NRun), nameof(NRun.ScreenStateTracker)).SetValue(nativeRun, new ScreenStateTracker(map, capstone, overlays));
            nativeRun.GlobalUi.AddChild(map); map.Hide(); map.SetProcess(false);
            nativeRun.GlobalUi.AddChild(overlays);
            overlays.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            backstop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            rooms = new NSceneContainer { ProcessMode = Node.ProcessModeEnum.Always };
            AccessTools.Field(typeof(NRun), "_roomContainer").SetValue(nativeRun, rooms);
            nativeRun.AddChild(rooms);
            rooms.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            // GameReady subscribers may inspect NRun.CombatRoom immediately.
            // An owned container with no scene is the native null-room state.
            await HeadlessUiHost.Mount(game);
            game.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            // Put the native overlay above the actual room without changing its
            // own hit testing, selection callbacks or timing.
            nativeRun.MoveChild(nativeRun.GlobalUi, nativeRun.GetChildCount() - 1);
            replacedPools = true;
            pools.Remove(typeof(NCard)); pools.Remove(typeof(NGridCardHolder));
            cardsPool = NodePool.Init<NCard>(NCard.AssetPaths.First(), 0);
            holdersPool = NodePool.Init<NGridCardHolder>(NGridCardHolder.AssetPaths.Single(), 0);
            TestMode.IsOn = false;
            NonInteractiveMode.AutoSlayerCheck = () => false;
            // The backend host has no RunSet/Act preload pipeline. The native
            // factory's AssetCache.GetScene loads the matching PCK on a miss.
            combatNode = NCombatRoom.Create(room, CombatRoomMode.ActiveCombat)!;
            AccessTools.Property(typeof(NSceneContainer), nameof(NSceneContainer.CurrentScene)).SetValue(rooms, combatNode);
            rooms.AddChild(combatNode);
            combatNode.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            combatNode.Ui.Activate(battle.Combat);
            patches.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Entities.Creatures.Creature), "GetCreatureNode"),
                prefix: new HarmonyMethod(typeof(JigsawKnowledgeUiTests), nameof(CreatureNode)));
            patches.Patch(AccessTools.Method(typeof(NChooseACardSelectionScreen), nameof(NChooseACardSelectionScreen.CardsSelected)),
                postfix: new HarmonyMethod(typeof(JigsawKnowledgeUiTests), nameof(ObserveSelection)));
            patches.Patch(AccessTools.Method(typeof(NOverlayStack), nameof(NOverlayStack.Remove)),
                prefix: new HarmonyMethod(typeof(JigsawKnowledgeUiTests), nameof(ObserveRemoval)));
            selectedScreen = null; selectionTask = null; selectionCalls = removals = 0;
            ActiveScreenContext.Instance.Update();
            var hitbox = combatNode.GetCreatureNode(battle.Monster.Creature)!.Hitbox;
            int combatReleases = 0;
            hitbox.GuiInput += input => { if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) combatReleases++; };
            await Frames(3);
            Vector2 combatPoint = hitbox.GetGlobalRect().GetCenter();
            await Click(combatPoint);
            Check(combatReleases == 1, "The actual native monster hitbox receives a viewport click before the modal screen opens.");
            int deckCount = player.Deck.Cards.Count;
            Task performing = battle.Monster.PerformMove();
            await Until(() => selectedScreen != null || performing.IsCompleted, "second curse screen");
            if (performing.IsFaulted) await performing;
            Check(selectedScreen != null && selectionTask != null && !performing.IsCompleted && !selectionTask.IsCompleted,
                "Actual PerformMove awaits the original CardsSelected task on the owner's real screen.");
            var screen = selectedScreen!;
            bool exited = false;
            screen.TreeExited += () => exited = true;
            Check(ReferenceEquals(overlays.Peek(), screen) && overlays.ScreenCount == 1 && ActiveScreenContext.Instance.IsCurrent(screen),
                "The actual overlay stack and active-screen consumer both own the second curse screen.");
            var holders = screen.GetNode<Control>("CardRow").GetChildren().OfType<NGridCardHolder>().ToArray();
            Check(holders.Length == 2 && holders[0].CardModel is Sloth && holders[1].CardModel is KnowledgeTenderChoice &&
                holders.All(holder => ReferenceEquals(holder.CardModel.Owner, player)), "The two actual native holders show Sloth and Tender for this owner.");
            var offered = holders.Select(holder => holder.CardModel).ToArray();
            var tender = holders.Single(holder => holder.CardModel is KnowledgeTenderChoice);
            var card = tender.CardNode!;
            var portrait = card.GetNode<TextureRect>("%Portrait").Texture;
            string title = card.GetNode<MegaLabel>("%TitleLabel").Text;
            string description = card.GetNode<MegaRichTextLabel>("%DescriptionLabel").Text;
            string formatted = tender.CardModel.GetDescriptionForPile(PileType.None);
            Check(portrait != null && portrait.GetWidth() > 0 && portrait.GetHeight() > 0 &&
                portrait.ResourcePath == "res://AllMatters/images/card_portraits/tender.png", "The actual Tender card loads its supplied portrait texture.");
            Check(title == ModelDb.Power<TenderPower>().Title.GetFormattedText() && description.Contains(formatted) &&
                formatted.Contains('1') && !formatted.Contains('{'), "The actual Tender title and description format its one-stack power text.");
            await Click(combatPoint);
            Check(combatReleases == 1 && !performing.IsCompleted, "The native overlay blocks the same lower combat hitbox while its choice remains pending.");
            await tree.ToSignal(tree.CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
            Check(!performing.IsCompleted && !selectionTask!.IsCompleted && player.Creature.GetPowerAmount<TenderPower>() == 0,
                "Passing the native 350 ms guard alone cannot choose Tender or resume the original action.");
            Vector2 tenderPoint = tender.Hitbox.GetGlobalRect().GetCenter();
            await Click(tenderPoint);
            await performing.WaitAsync(TimeSpan.FromSeconds(10));
            await Until(() => exited && !GodotObject.IsInstanceValid(screen), "native selection screen QueueFree");
            Check(selectionTask.IsCompletedSuccessfully && selectionCalls == 1 && removals == 1 && overlays.ScreenCount == 0,
                "The one original CardsSelected continuation removes and frees its actual screen exactly once.");
            var received = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>().Where(entry => entry.Power is TenderPower or SlothPower).ToArray();
            Check(player.Creature.GetPowerAmount<TenderPower>() == 1 && player.Creature.GetPowerAmount<SlothPower>() == 0 &&
                received.Length == 1 && received[0].Power is TenderPower && received[0].Amount == 1 &&
                ReferenceEquals(received[0].Actor, player.Creature) && ReferenceEquals(received[0].Applier, player.Creature),
                "The native selected OnChosen applies and records only one Tender stack for its original owner.");
            Check(player.Deck.Cards.Count == deckCount && offered.All(option => option.Pile == null && !player.Deck.Cards.Contains(option)),
                "Neither actual display-card instance enters any combat pile or the permanent deck.");
            viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = tenderPoint, GlobalPosition = tenderPoint }, true);
            await Frames(3);
            Check(player.Creature.GetPowerAmount<TenderPower>() == 1 && selectionCalls == 1 && removals == 1 &&
                CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>().Count(entry => entry.Power is TenderPower) == 1,
                "A repeated viewport release after QueueFree cannot choose again or append another native power entry.");
            Check(ActiveScreenContext.Instance.IsCurrent(combatNode), "The actual native active-screen owner returns to the combat room.");
            await Click(combatPoint);
            Check(combatReleases == 2, "The same real combat hitbox receives viewport input again after native overlay removal.");
            battle.Monster.RollMove(battle.Combat.PlayerCreatures);
            Check(battle.Monster.NextMove.Id == "SLAP_MOVE" && first.Calls == 1 && selectionCalls == 1,
                "The completed second native curse advances once to Slap without another selector or choice.");
        }
        finally
        {
            try
            {
                if (overlays != null && GodotObject.IsInstanceValid(overlays)) overlays.Free();
                if (combatNode != null && GodotObject.IsInstanceValid(combatNode)) combatNode.Free();
                // Native owner TreeExiting callbacks remove registered tips;
                // their parent also owns any queued tooltip children.
                if (hoverTips != null && GodotObject.IsInstanceValid(hoverTips)) hoverTips.Free();
                FreePool(holdersPool); FreePool(cardsPool);
                foreach (Node? node in new Node?[] { map, capstone, rooms, hotkeys, controller, cursor, target, players, console, shake })
                    if (node != null && GodotObject.IsInstanceValid(node)) node.Free();
            }
            finally
            {
                combatNode = null; selectedScreen = null; selectionTask = null;
                if (replacedPools)
                {
                    pools.Remove(typeof(NCard)); pools.Remove(typeof(NGridCardHolder));
                    if (previousCards != null) pools[typeof(NCard)] = previousCards;
                    if (previousHolders != null) pools[typeof(NGridCardHolder)] = previousHolders;
                }
                consoleField.SetValue(null, previousConsole);
                try { ui?.Dispose(); }
                finally
                {
                    try { if (patches != null) patches.UnpatchAll(patches.Id); }
                    finally
                    {
                        atlases?.Dispose();
                        foreach (var action in addedInputActions) InputMap.EraseAction(action);
                        TestMode.IsOn = testing; NonInteractiveMode.AutoSlayerCheck = autoSlayer;
                        viewport.ContentScaleSize = content; viewport.ContentScaleMode = mode;
                        viewport.ContentScaleAspect = aspect; viewport.Size = size;
                    }
                }
            }
        }
    }

    private static void FreePool<T>(NodePool<T>? pool) where T : Node, IPoolable
    {
        if (pool == null) return;
        var used = (IEnumerable<T>)AccessTools.Field(typeof(NodePool<T>), "_usedObjects").GetValue(pool)!;
        foreach (var node in used.Concat(pool.DebugFreeObjects).Distinct().ToArray())
            if (GodotObject.IsInstanceValid(node)) node.Free();
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
        Check(ready(), "The native Knowledge choice did not reach " + stage + ".");
    }
    private static async Task Click(Vector2 point)
    {
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        await Frames(1);
        var hovered = viewport.GuiGetHoveredControl();
        var hierarchy = new List<string>();
        for (Node? node = hovered; node != null && hierarchy.Count < 5; node = node.GetParent())
            hierarchy.Add(node is Control control
                ? $"{node.GetType().Name}:{node.Name}[filter={control.MouseFilter},rect={control.GetGlobalRect()}]"
                : $"{node.GetType().Name}:{node.Name}");
        Console.WriteLine($"KNOWLEDGE_UI click={point} hovered={hovered?.GetPath()} hierarchy={string.Join(" <- ", hierarchy)}");
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point, GlobalPosition = point }, true);
        await Frames(1);
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point }, true);
        await Frames(2);
    }
}
