using AllMatters.Feedback;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Debug;
using MegaCrit.Sts2.Core.Nodes.Screens.FeedbackScreen;
using MegaCrit.Sts2.addons.mega_text;

internal static class FeedbackUiTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Feedback export native dropdown bilingual repopulation and button routing", Routing)
    ];
    private static bool SkipDecoration() => false;
    private static void Check(bool condition, string message) => NativeEventTests.Check(condition, message);

    private static async Task Routing()
    {
        JigsawHuntingNativeTests.RegisterScripts();
        HeadlessUiHost.EnsureInputActions();
        var consoleField = AccessTools.Field(typeof(NDevConsole), "_instance");
        var previousConsole = consoleField.GetValue(null);
        var console = new NDevConsole { Visible = false };
        consoleField.SetValue(null, console);
        var previousGame = NGame.Instance;
        var game = new NGame();
        var input = new NInputManager();
        var controller = new NControllerManager();
        AccessTools.Property(typeof(NGame), nameof(NGame.Instance)).SetValue(null, game);
        AccessTools.Property(typeof(NGame), nameof(NGame.InputManager)).SetValue(game, input);
        AccessTools.Property(typeof(NInputManager), "ControllerManager").SetValue(input, controller);
        var harmony = new Harmony("AllMatters.FeedbackUiFixture");
        // Screen decoration needs the whole game UI. Keep dropdown and button scripts native.
        harmony.Patch(AccessTools.Method(typeof(NSendFeedbackScreen), "_Ready"),
            prefix: new HarmonyMethod(typeof(FeedbackUiTests), nameof(SkipDecoration)));
        foreach (string method in new[] { "OnFocus", "OnUnfocus" })
            harmony.Patch(AccessTools.Method(typeof(NFeedbackCategoryDropdown), method),
                prefix: new HarmonyMethod(typeof(FeedbackUiTests), nameof(SkipDecoration)));
        var export = FeedbackUi.ExportAsync;
        var present = FeedbackUi.PresentResult;
        string language = LocManager.Instance.Language;
        var tree = (SceneTree)Engine.GetMainLoop();
        NSendFeedbackScreen? screen = null;
        TaskCompletionSource<FeedbackExportResult>? pending = null;
        try
        {
            string path = (string)AccessTools.Field(typeof(NSendFeedbackScreen), "_scenePath").GetValue(null)!;
            var original = ResourceLoader.Load<PackedScene>(path).Instantiate<NSendFeedbackScreen>();
            var dropdown = original.GetNode<NFeedbackCategoryDropdown>("%CategoryDropdown");
            var button = original.GetNode<NButton>("%SendButton");
            dropdown.GetParent().RemoveChild(dropdown);
            button.GetParent().RemoveChild(button);
            SetOwners(dropdown, dropdown);
            SetOwners(button, button);
            original.Free();
            screen = new NSendFeedbackScreen { Size = new(1280, 720) };
            screen.AddChild(dropdown); screen.AddChild(button);
            button.Position = new(400, 400); button.Size = new(200, 80);
            dropdown.Position = new(100, 100);
            FeedbackUi.Set(screen, "_categoryDropdown", dropdown);
            FeedbackUi.Set(screen, "_sendButton", button);
            FeedbackUi.Set(screen, "_sendLabel", button.GetNode<MegaLabel>("Label"));
            FeedbackUi.Set(screen, "_descriptionText", "");
            button.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(selected =>
                AccessTools.Method(typeof(NSendFeedbackScreen), "SendButtonSelected").Invoke(screen, [selected])));
            await HeadlessUiHost.Mount(screen);
            foreach (string lang in new[] { "eng", "zhs", "eng" })
            {
                LocManager.Instance.SetLanguage(lang);
                for (int count = 0; count < 2; count++)
                {
                    AccessTools.Method(typeof(NFeedbackCategoryDropdown), "PopulateOptions").Invoke(dropdown, null);
                    var items = dropdown.GetNode<Control>("DropdownContainer/VBoxContainer").GetChildren().OfType<NFeedbackCategoryDropdownItem>().ToArray();
                    Check(items.Count(item => item.Text == FeedbackUi.CategoryText) == 1, "Each native population has exactly one visible AllMatters item.");
                    var custom = items.Single(item => item.Text == FeedbackUi.CategoryText);
                    custom.EmitSignal(NDropdownItem.SignalName.Selected, custom);
                    Check(dropdown.CurrentCategory == FeedbackUi.Category && button.GetNode<MegaLabel>("Label").Text == "Export", "Native Selected event selects the internal identifier and Export caption.");
                    var ordinary = items.First(item => item != custom);
                    ordinary.EmitSignal(NDropdownItem.SignalName.Selected, ordinary);
                    Check(dropdown.CurrentCategory != FeedbackUi.Category && button.GetNode<MegaLabel>("Label").Text == new LocString("settings_ui", "FEEDBACK_SEND_BUTTON_LABEL").GetFormattedText(), "Selecting an original item restores the native caption.");
                    custom.EmitSignal(NDropdownItem.SignalName.Selected, custom);
                }
            }
            int exports = 0;
            pending = new TaskCompletionSource<FeedbackExportResult>();
            FeedbackUi.ExportAsync = snapshot => { exports++; Check(snapshot.Description.Length == 0, "Empty feedback reaches export without native empty-input early return."); return pending.Task; };
            bool delivered = false;
            FeedbackUi.PresentResult = result => { delivered = result.Error == "fixture failure"; return true; };
            button.GrabFocus();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Input.ParseInputEvent(new InputEventAction { Action = MegaInput.select, Pressed = true });
            Input.ParseInputEvent(new InputEventAction { Action = MegaInput.select, Pressed = false });
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(exports == 1 && FeedbackUi.Busy && !button.IsEnabled, "Engine action release enters the native send handler once and synchronously disables Export.");
            button.EmitSignal(NClickableControl.SignalName.Released, button);
            Check(exports == 1, "A repeated native release cannot enqueue a second export.");
            screen.Hide();
            pending.SetResult(new(null, "fixture failure"));
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(delivered && !FeedbackUi.Busy && button.IsEnabled, "Hiding feedback preserves result delivery and restores the button after failure.");
            Check(dropdown.CurrentCategory == FeedbackUi.Category && FeedbackUi.Field<string>(screen, "_descriptionText") == "", "Failure retains category and input.");
            pending = new TaskCompletionSource<FeedbackExportResult>();
            delivered = false;
            button.EmitSignal(NClickableControl.SignalName.Released, button);
            Check(exports == 2 && FeedbackUi.Busy, "A fresh operation starts after the earlier export completed.");
            screen.Free();
            screen = null;
            pending.SetResult(new(null, "fixture failure"));
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(delivered && !FeedbackUi.Busy, "Freeing the originating native screen still delivers the pending task result.");
        }
        finally
        {
            if (pending != null && !pending.Task.IsCompleted)
            {
                pending.TrySetResult(new(null, "fixture failure"));
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
            FeedbackUi.ExportAsync = export;
            FeedbackUi.PresentResult = present;
            if (screen != null && GodotObject.IsInstanceValid(screen)) screen.Free();
            harmony.UnpatchAll(harmony.Id);
            AccessTools.Property(typeof(NGame), nameof(NGame.Instance)).SetValue(null, previousGame);
            controller.Free(); input.Free(); game.Free();
            consoleField.SetValue(null, previousConsole);
            console.Free();
            LocManager.Instance.SetLanguage(language);
        }
    }

    private static void SetOwners(Node node, Node root)
    {
        foreach (var child in node.GetChildren())
        {
            child.Owner = root;
            SetOwners(child, root);
        }
    }
}
