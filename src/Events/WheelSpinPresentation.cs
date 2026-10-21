// SPDX-License-Identifier: GPL-3.0-only
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;

namespace AllMatters.Events;

// Built-in nodes with Balance's attributed wheel artwork; no custom Godot scripts.
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
        var backdrop = new ColorRect { Color = new Color(0.025f, 0.02f, 0.05f, 0.76f) };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(backdrop);

        float size = Math.Min(root.Size.X * 0.68f, root.Size.Y * 0.94f);
        var center = root.Size / 2;
        var wheel = new Node2D { Position = center };
        root.AddChild(wheel);
        wheel.AddChild(new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Texture = GD.Load<Texture2D>("res://AllMatters/images/event_extras/wheel.png"),
            Position = -Vector2.One * size / 2,
            Size = Vector2.One * size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        // In the artwork, centers run clockwise from three o'clock:
        // gold, chest, heart, curse, crossed-out card, blood. Point from the right.
        float arrowSize = size * 0.30f;
        root.AddChild(new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Texture = GD.Load<Texture2D>("res://AllMatters/images/event_extras/wheel_arrow.png"),
            Position = center + new Vector2(size * 0.415f - arrowSize * 0.146f, -arrowSize / 2),
            Size = Vector2.One * arrowSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        var caption = new Label
        {
            Text = new LocString("events", $"{eventId.Entry}.pages.INITIAL.options.SPIN.title").GetFormattedText(),
            Position = new Vector2(0, root.Size.Y - 64),
            Size = new Vector2(root.Size.X, 48),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color("ffde8a"),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        caption.AddThemeFontSizeOverride("font_size", Math.Max(22, (int)(root.Size.Y * 0.037f)));
        root.AddChild(caption);
        var tween = root.CreateTween();
        tween.TweenProperty(wheel, "rotation_degrees", 1800f - result * 60f, 3)
            .SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() => caption.Text = new LocString("events",
            $"{eventId.Entry}.pages.RESULT.options.{WheelOfChange.Outcomes[result]}.title").GetFormattedText()));
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
