using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using Godot;

internal static class JigsawTranslatedButtonTests
{
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Jigsaw presentation translated buttons fit 720p and receive engine clicks", TranslatedButtons)
    ];

    private static async Task TranslatedButtons()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("ui_atlas", "compressed");
        HeadlessUiHost.EnsureInputActions();
        var tree = (SceneTree)Engine.GetMainLoop();
        var originalSize = tree.Root.Size;
        string originalLocale = TranslationServer.GetLocale();
        const string locale = "eo";
        Require(TranslationServer.GetTranslationObject(locale) == null, "The temporary locale must not replace an existing translation resource.");
        using var translation = new Translation { Locale = locale };
        string[] keys = ["由先古之民审视", "令先古之民决定", "帮助"];
        string[] texts = ["请先古之民审视并确认当前地图布局", "将地图整体布局交由先古之民重新决定", "查看拼图操作说明与规则"];
        JigsawScreen? view = null;
        bool added = false;
        try
        {
            for (int i = 0; i < keys.Length; i++) translation.AddMessage(keys[i], texts[i], "");
            TranslationServer.AddTranslation(translation);
            added = true;
            TranslationServer.SetLocale(locale);
            for (int i = 0; i < keys.Length; i++)
                Require(TranslationServer.Translate(keys[i]).ToString() == texts[i], "TranslationServer resolves the exact alternate caption before constructing production controls.");
            tree.Root.Size = new(1280, 720);
            int automaticRequests = 0;
            var map = JigsawRun.Create(74, players: [1UL, 2UL]);
            view = JigsawScreen.Create(map, 1, () => {}, () => Task.CompletedTask, () => Task.CompletedTask,
                _ => Task.CompletedTask, () => {}, new MemoryJigsawTutorialProfile { Sentence = 6 },
                automaticRequested: () => { automaticRequests++; return Task.CompletedTask; });
            await HeadlessUiHost.Mount(view);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var buttons = texts.Select(text => view.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == text)).ToArray();
            void CheckLayout()
            {
                foreach (var button in buttons)
                {
                    Require(button.IsVisibleInTree() && !button.Disabled, $"Translated action is visible and enabled: {button.Text}");
                    int size = button.GetThemeFontSize("font_size");
                    Require(size >= 16, $"720p translated action retains readable font size: {button.Text} ({size}).");
                    var rect = button.GetGlobalRect();
                    Require(rect.Position.X >= 0 && rect.Position.Y >= 0 && rect.End.X <= 1281 && rect.End.Y <= 721,
                        $"Translated button stays inside the viewport: {button.Text}, {rect}.");
                    // Focus is an overlay, not the content style used to lay out Button text.
                    foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed" })
                    {
                        var available = button.Size - button.GetThemeStylebox(state).GetMinimumSize();
                        var flags = TextServer.LineBreakFlag.Mandatory;
                        float width = -1;
                        if (button.AutowrapMode == TextServer.AutowrapMode.WordSmart)
                        {
                            flags |= TextServer.LineBreakFlag.WordBound | TextServer.LineBreakFlag.Adaptive;
                            width = available.X;
                        }
                        else Require(button.AutowrapMode == TextServer.AutowrapMode.Off,
                            "The oracle explicitly supports the actual Off and WordSmart button modes.");
                        var envelope = button.GetThemeFont("font").GetMultilineStringSize(button.Text,
                            HorizontalAlignment.Left, width, size, -1, flags,
                            TextServer.JustificationFlag.None, TextServer.Direction.Auto, TextServer.Orientation.Horizontal);
                        Console.WriteLine($"TRANSLATED BUTTON locale={locale} text={button.Text} wrap={button.AutowrapMode} state={state} rect={rect} font={size} glyphs={envelope} content={available}");
                        Require(envelope.X <= available.X + 1 && envelope.Y <= available.Y + 1,
                            $"Full translated glyph envelope fits {state}: {button.Text}; glyphs={envelope}, content={available}, rect={rect}, font={size}.");
                    }
                }
                for (int i = 0; i < buttons.Length; i++)
                for (int j = i + 1; j < buttons.Length; j++)
                    Require(!buttons[i].GetGlobalRect().Intersects(buttons[j].GetGlobalRect()), "Translated buttons do not cover each other.");
            }
            CheckLayout();
            if (System.Environment.GetEnvironmentVariable("ALLMATTERS_UI_CAPTURE") is {} capture && DisplayServer.GetName() != "headless")
            {
                var texture = tree.Root.GetTexture();
                var frame = texture?.GetImage();
                if (frame != null && !frame.IsEmpty())
                {
                    using (frame) frame.SavePng(System.IO.Path.ChangeExtension(capture, "translated-buttons.png"));
                }
                else Console.WriteLine("Translated buttons: headless viewport has no readable image; visual QA remains unverified.");
            }
            else if (DisplayServer.GetName() == "headless")
                Console.WriteLine("Translated buttons: headless display has no readable viewport image; visual QA remains unverified.");
            async Task Click(Button button)
            {
                var at = button.GetGlobalRect().GetCenter();
                tree.Root.PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
                tree.Root.PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                tree.Root.PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = false }, true);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
            await Click(buttons[1]);
            Require(automaticRequests == 1 && map.Choices.Count == 0, "The real translated automatic button invokes its production request exactly once.");
            await Click(buttons[0]);
            Require(map.Choices[1] == EndChoice.Manual, "The real translated manual button records the local player's domain vote.");
            // Help remains reachable after a vote; it must preserve that vote.
            await Click(buttons[2]);
            Require(view.Reading && view.GetNode<ColorRect>("JigsawHelpShade").IsVisibleInTree() && map.Choices[1] == EndChoice.Manual,
                "The translated Help button opens the actual modal shade and preserves the recorded vote.");
        }
        finally
        {
            view?.Free();
            if (added) TranslationServer.RemoveTranslation(translation);
            TranslationServer.SetLocale(originalLocale);
            tree.Root.Size = originalSize;
        }
    }
}
