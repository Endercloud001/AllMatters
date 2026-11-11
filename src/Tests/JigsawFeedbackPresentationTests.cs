using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using Godot;

internal static class JigsawFeedbackPresentationTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Jigsaw feedback Ancient continue shows actual multiplayer consensus", ContinueConsensus),
        ("Jigsaw feedback visited unknown combat uses native resolved icon", ResolvedIcon),
        ("Jigsaw feedback multiplayer HUD restores actual visibility on close", HudVisibility)
    ];

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
        var map = Ancient(1, 2);
        var tree = (SceneTree)Engine.GetMainLoop();
        var originalSize = tree.Root.Size;
        tree.Root.Size = new(1280, 720);
        JigsawScreen? view = null;
        view = JigsawScreen.Create(map, 1, () => {}, () => Task.CompletedTask,
            () => { map.Continue(1); view!.Refresh(); return Task.CompletedTask; },
            _ => Task.CompletedTask, () => {}, new MemoryJigsawTutorialProfile { Sentence = 6 });
        try
        {
            await HeadlessUiHost.Mount(view);
            var button = view.DecisionControl.GetChildren().OfType<Button>().Single(b => b.Text == "继续前行");
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
            Require(map.Phase == JigsawPhase.Ancient, "The first player's engine click must wait for the second player.");
            var voters = view.DecisionControl.GetNodeOrNull<HBoxContainer>("ContinueVoters");
            Require(voters is { Visible: true } && voters.GetChildCount() == 1, "The waiting continue button must show the first player's portrait.");
            await Click();
            Require(voters!.GetChildCount() == 1 && map.Phase == JigsawPhase.Ancient, "Repeated input must neither duplicate nor revoke consent.");
            Require(map.Continue(2).Success, "The second original player can consent.");
            view.Refresh();
            Require(map.Phase == JigsawPhase.Exploring && !voters.Visible, "Exploration begins only after both players and removes the consent portraits.");
        }
        finally { view.Free(); tree.Root.Size = originalSize; }
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
