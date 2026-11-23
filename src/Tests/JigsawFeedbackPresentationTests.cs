using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;

internal static class JigsawFeedbackPresentationTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Jigsaw feedback Ancient continue shows actual multiplayer consensus", ContinueConsensus),
        ("Jigsaw feedback visited unknown combat uses native resolved icon", ResolvedIcon),
        ("Jigsaw feedback multiplayer HUD restores actual visibility on close", HudVisibility),
        ("Jigsaw feedback picking the second pool piece keeps its outline", PoolPieceOutline)
    ];

    private static async Task PoolPieceOutline()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("ui_atlas", "compressed");
        var map = JigsawRun.Create(74, players: [1, 2]);
        HeadlessUiHost.EnsureInputActions();
        var tree = (SceneTree)Engine.GetMainLoop();
        var originalSize = tree.Root.Size;
        var view = JigsawScreen.Create(map, 1, () => {}, () => Task.CompletedTask, () => Task.CompletedTask,
            _ => Task.CompletedTask, () => {}, new MemoryJigsawTutorialProfile { Sentence = 6 });
        try
        {
            tree.Root.Size = new Vector2I(1280, 720);
            await HeadlessUiHost.Mount(view);
            view.Refresh();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var cards = view.PoolControl.GetChild<VBoxContainer>(0);
            Button[] Rows() => cards.GetChildren().OfType<Button>()
                .Where(button => button.GetChildren().OfType<JigsawPiecePreview>().Any()).ToArray();
            var target = Rows()[1];
            var position = target.GetGlobalRect().GetCenter();
            var viewportRect = tree.Root.GetVisibleRect();
            var poolRect = view.PoolControl.GetGlobalRect();
            Require(viewportRect.HasPoint(position) && poolRect.HasPoint(position),
                $"Second row center {position} must be visible in the pool {poolRect} and viewport {viewportRect}.");
            tree.Root.PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var hovered = tree.Root.GuiGetHoveredControl();
            Require(hovered == target,
                $"Engine hover at {position} must hit {target.GetPath()}. Actual: {hovered?.GetPath().ToString() ?? "no control"}.");
            tree.Root.PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            tree.Root.PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            Require(map.Holdings.TryGetValue(1, out var held) && held.PieceId == map.Pool[1].Id,
                "The first click takes the second available piece.");
            var rows = Rows();
            Require(rows[1].Modulate.A < 1 && rows[1].GetChildren().OfType<TextureRect>().Count() == 1,
                "The held piece's row is dimmed and shows a portrait control.");
            Require(tree.Root.GuiGetFocusOwner() == rows[1],
                $"The held second row keeps the outline after refresh. Focused pool row: {Array.IndexOf(rows, tree.Root.GuiGetFocusOwner()) + 1}.");

            Require(map.Take(2, map.Pool[0].Id).Success && map.Drop(2, new GridPoint(0, 7)).Success,
                "Another player places the piece above the focused piece.");
            view.Refresh();
            rows = Rows();
            Require(rows[0].GetChildren().OfType<JigsawPiecePreview>().Single().Piece.Id == map.Pool[1].Id && rows[0].HasFocus(),
                "The outline follows the same piece when it moves to the first row.");

            tree.Root.PushInput(new InputEventAction { Action = MegaInput.down, Pressed = true }, true);
            Require(rows[1].HasFocus(), "Controller down moves one row from the focused piece after the pool refresh.");
        }
        finally { view.Free(); tree.Root.Size = originalSize; }
    }

    private static async Task HudVisibility()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("ui_atlas", "compressed");
        var map = Ancient(1);
        HeadlessUiHost.EnsureInputActions();
        var screen = JigsawScreen.Create(map, 1, () => {}, () => Task.CompletedTask, () => Task.CompletedTask, _ => Task.CompletedTask, () => {}, new MemoryJigsawTutorialProfile { Sentence = 6 });
        var hud = new Control();
        try
        {
            await HeadlessUiHost.Mount(screen, hud);
            screen.BindMultiplayerHud(hud);
            Require(!hud.Visible, "A visible puzzle hides the overlapping multiplayer HUD.");
            screen.Hide();
            Require(hud.Visible, "Returning to a native room restores the previously visible HUD.");
            screen.Show();
            Require(!hud.Visible, "Reopening the puzzle hides the HUD again.");
            Require(map.Continue(1).Success, "The player finishes Ancient.");
            screen.Refresh();
            Require(hud.Visible, "Changing the same visible screen to exploration restores the multiplayer HUD.");
            screen.GetParent().RemoveChild(screen);
            Require(hud.Visible, "External screen removal restores the HUD even without HideMap.");
            hud.Hide();
            await HeadlessUiHost.Mount(screen);
            using var hiddenScope = new JigsawMultiplayerHud(screen, hud);
            screen.Hide();
            Require(!hud.Visible, "A HUD that was already hidden must stay hidden after leaving the puzzle.");
        }
        finally { screen.Free(); hud.Free(); }
    }

    private static JigsawRun Ancient(params ulong[] players)
    {
        var map = JigsawRun.Create(74, players: players);
        foreach (var player in players) map.ChooseEnd(player, EndChoice.Automatic);
        Require(map.FinishAutomatic(map.AutomaticOperation!, map.AutomaticWitness).Success, "The real automatic layout enters Ancient.");
        foreach (var player in players) map.CompleteAncient(player);
        return map;
    }

    private static async Task ContinueConsensus()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("ui_atlas", "compressed");
        HeadlessUiHost.EnsureInputActions();
        var tree = (SceneTree)Engine.GetMainLoop();
        var originalSize = tree.Root.Size;
        try
        {
        foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
        foreach (int playerCount in new[] { 1, 2, 3 })
        {
        var players = Enumerable.Range(1, playerCount).Select(id => (ulong)id).ToArray();
        var map = Ancient(players);
        tree.Root.Size = size;
        JigsawScreen? view = null;
        view = JigsawScreen.Create(map, 1, () => {}, () => Task.CompletedTask,
            () => { map.Continue(1); view!.Refresh(); return Task.CompletedTask; },
            _ => Task.CompletedTask, () => {}, new MemoryJigsawTutorialProfile { Sentence = 6 });
        try
        {
            await HeadlessUiHost.Mount(view);
            var button = view.DecisionControl.GetChildren().OfType<Button>().Single(b => b.Text == "继续前行");
            Require(button.Size.X <= size.X * .25f, "Continue stays compact rather than filling the two-choice region.");
            async Task Click()
            {
                var at = button.GetGlobalRect().GetCenter();
                tree.Root.PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
                tree.Root.PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                tree.Root.PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = false }, true);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
            await Click();
            Require(map.ContinuedPlayers.Contains(1UL), "The engine click reaches the actual Continue command.");
            if (playerCount == 1)
            {
                Require(map.Phase == JigsawPhase.Exploring, "Solo Continue advances immediately.");
                continue;
            }
            Require(map.Phase == JigsawPhase.Ancient, "The first player's engine click must wait for the second player.");
            var voters = button.GetNodeOrNull<HBoxContainer>("ContinueVoters");
            Require(voters is { Visible: true } && voters.GetChildCount() == 1, "The waiting continue button must show the first player's portrait.");
            void CheckPortraitPosition()
            {
                var row = voters!.GetGlobalRect();
                var target = button.GetGlobalRect();
                Require(row.End.Y <= target.Position.Y && Math.Abs(row.GetCenter().X - target.GetCenter().X) < 2,
                    "Consent portraits sit centered above Continue without covering its label.");
            }
            CheckPortraitPosition();
            await Click();
            Require(voters!.GetChildCount() == 1 && map.Phase == JigsawPhase.Ancient, "Repeated input must neither duplicate nor revoke consent.");
            if (playerCount == 3)
            {
                Require(map.Continue(2).Success, "The second original player can consent.");
                view.Refresh();
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                Require(voters.GetChildCount() == 2 && map.Phase == JigsawPhase.Ancient, "Two portraits still wait for the third player.");
                CheckPortraitPosition();
            }
            Require(map.Continue(players[^1]).Success, "The final original player can consent.");
            view.Refresh();
            Require(map.Phase == JigsawPhase.Exploring && !voters.Visible, "Exploration begins only after both players and removes the consent portraits.");
        }
        finally { view.Free(); }
        }
        }
        finally { tree.Root.Size = originalSize; }
    }

    private static async Task ResolvedIcon()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("ui_atlas", "compressed");
        var map = Ancient(1);
        map.Continue(1);
        HeadlessUiHost.EnsureInputActions();
        var view = JigsawScreen.Create(map, 1, () => {}, () => Task.CompletedTask, () => Task.CompletedTask, _ => Task.CompletedTask, () => {}, new MemoryJigsawTutorialProfile { Sentence = 6 });
        try
        {
            await HeadlessUiHost.Mount(view);
            // Travel the real layout until its reachable Unknown room is entered.
            var unknown = map.Nodes.First(n => n.Room == JigsawRoom.Unknown);
            var path = new Queue<int[]>(); path.Enqueue([0]);
            int[] route = [];
            while (path.Count > 0)
            {
                var current = path.Dequeue();
                if (current[^1] == unknown.Id) { route = current; break; }
                foreach (var edge in map.Edges)
                {
                    int next = edge.First == current[^1] ? edge.Second : edge.Second == current[^1] ? edge.First : -1;
                    if (next >= 0 && !current.Contains(next)) path.Enqueue([..current, next]);
                }
            }
            Require(route.Length > 1, "Automatic layout has a reachable unknown room.");
            Require(view.MapControl.GetNode<Sprite2D>($"Room{unknown.Id}").Texture.ResourcePath.EndsWith("map_unknown.tres"), "Unvisited Unknown keeps its mystery icon.");
            foreach (int node in route.Skip(1))
            {
                var entry = map.BeginEnter(node);
                Require(entry != null && map.ConfirmEnter(entry.Id).Success, "Enter each destination through actual traversal rules.");
                if (node == unknown.Id) Require(map.RecordResolvedRoom(node, JigsawRoom.Combat).Success, "Native resolved combat is recorded.");
                Require(map.CompleteRoom(entry!.Id).Success, "The native room finishes.");
            }
            view.Refresh();
            var tree = (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Require(view.MapControl.GetNode<Sprite2D>($"Room{unknown.Id}").Texture.ResourcePath.EndsWith("map_unknown_monster.tres"), "A completed Unknown combat uses the native special Unknown-combat icon.");
            var restored = JigsawRun.Restore(map.CreateSave());
            var reopened = JigsawScreen.Create(restored, 1, () => {}, () => Task.CompletedTask, () => Task.CompletedTask, _ => Task.CompletedTask, () => {}, new MemoryJigsawTutorialProfile { Sentence = 6 });
            try
            {
                await HeadlessUiHost.Mount(reopened);
                Require(reopened.MapControl.GetNode<Sprite2D>($"Room{unknown.Id}").Texture.ResourcePath.EndsWith("map_unknown_monster.tres"), "Save/restore retains the native special Unknown-combat icon.");
            }
            finally { reopened.Free(); }
        }
        finally { view.Free(); }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
