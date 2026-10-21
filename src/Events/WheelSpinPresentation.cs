// SPDX-License-Identifier: GPL-3.0-only
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace AllMatters.Events;

// Built-in nodes only: no custom Godot script registration or copied minigame assets.
internal static class WheelSpinPresentation
{
    public static async Task Play(Control eventNode, ModelId eventId, int result)
    {
        var parent = (Control?)NOverlayStack.Instance ?? eventNode;
        var root = new Control { Name = "AllMattersWheel" };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var done = new TaskCompletionSource();
        root.TreeExiting += () => done.TrySetResult();
        parent.AddChild(root);
        var backdrop = new ColorRect { Color = new Color(0.025f, 0.02f, 0.05f, 0.94f) };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(backdrop);

        float radius = Math.Min(root.Size.X * 0.34f, root.Size.Y * 0.36f);
        var center = root.Size / 2;
        var wheel = new Node2D { Position = center };
        root.AddChild(wheel);
        Color[] colors = [new("94722b"), new("435e89"), new("346c54"), new("65447e"), new("6a6643"), new("883c43")];
        for (int sector = 0; sector < 6; sector++)
        {
            var points = new List<Vector2> { Vector2.Zero };
            for (int step = 0; step <= 20; step++)
            {
                float angle = Mathf.DegToRad(-120 + sector * 60 + step * 3);
                points.Add(Vector2.FromAngle(angle) * radius);
            }
            wheel.AddChild(new Polygon2D { Polygon = points.ToArray(), Color = colors[sector] });
            var label = new Label
            {
                Text = new LocString("events", $"{eventId.Entry}.pages.RESULT.options.{WheelOfChange.Outcomes[sector]}.title").GetFormattedText(),
                Position = Vector2.FromAngle(Mathf.DegToRad(-90 + sector * 60)) * (radius * 0.66f) - new Vector2(radius * 0.3f, 18),
                Size = new Vector2(radius * 0.6f, 40),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", Math.Max(16, (int)(radius * 0.075f)));
            wheel.AddChild(label);
        }
        // Segment centers begin at twelve o'clock; the fixed pointer points down.
        root.AddChild(new Polygon2D
        {
            Polygon = [center + new Vector2(-18, -radius - 18), center + new Vector2(18, -radius - 18), center + new Vector2(0, -radius + 22)],
            Color = new Color("ffde8a"),
        });
        var tween = root.CreateTween();
        tween.TweenProperty(wheel, "rotation_degrees", 1800f - result * 60f, 3)
            .SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(0.8);
        tween.TweenCallback(Callable.From(() => done.TrySetResult()));
        // Leaving a room or closing its scene must also release the pending option.
        eventNode.TreeExiting += OnEventExit;
        try { await done.Task; }
        finally
        {
            eventNode.TreeExiting -= OnEventExit;
            if (GodotObject.IsInstanceValid(tween)) tween.Kill();
            if (GodotObject.IsInstanceValid(root)) root.QueueFree();
        }
        void OnEventExit() => done.TrySetResult();
    }
}
