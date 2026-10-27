// SPDX-License-Identifier: GPL-3.0-only
// Recreates STS1 SmallLaserEffect using Godot; original assets remain Mega Crit's.
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using STS2RitsuLib.Audio;

namespace AllMatters.Vfx;

public static class SmallLaserEffect
{
    public const string TexturePath = "res://AllMatters/vfx/sentry_laser.png";
    public const string SoundPath = "res://AllMatters/sfx/sentry_beam_short.ogg";
    public const string SoundEvent = "event:/AllMatters/sentry/beam_short";
    public const float Duration = 0.5f;

    public static void Play(Creature attacker, IReadOnlyList<Creature> targets)
    {
        var room = NCombatRoom.Instance;
        var source = room?.GetCreatureNode(attacker);
        if (room == null || source == null) return;
        var endpoints = targets.Where(target => target.IsAlive)
            .Select(target => room.GetCreatureNode(target))
            .Where(node => node != null).Select(node => node!.VfxSpawnPosition).ToArray();
        PlayAt(room.CombatVfxContainer, source.VfxSpawnPosition, endpoints);
    }

    public static void PlayAt(Node container, Vector2 source, IReadOnlyList<Vector2> targets)
    {
        var effect = Create(source, targets);
        if (effect == null) return;
        container.AddChild(effect);
        // File playback is outside the FMOD graph: apply master explicitly;
        // RitsuLib additionally samples the SFX bus when playback starts.
        // Godot's cosmetic RNG does not consume any run/combat RNG.
        float master = SaveManager.Instance.SettingsSave.VolumeMaster;
        VirtualFmodEventRegistry.RegisterOneShot(SoundEvent, SoundPath,
            volume: master * master, pitch: (float)GD.RandRange(0.5, 1.5));
        SfxCmd.Play(SoundEvent);
    }

    public static Node2D? Create(Vector2 source, IReadOnlyList<Vector2> targets)
    {
        if (targets.Count == 0) return null;
        var texture = ResourceLoader.Load<Texture2D>(TexturePath);
        if (texture == null) return null;
        // The mod uses Microsoft.NET.Sdk, without Godot script generators.
        // Built-in nodes/signals/tweens work without registering custom scripts.
        var effect = new Node2D { TopLevel = true, Modulate = new Color(1, 1, 1, 0) };
        var beams = new List<(Sprite2D Primary, Sprite2D Secondary, float Length)>();
        effect.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        foreach (var target in targets)
        {
            var pivot = new Node2D { Position = source, Rotation = (target - source).Angle() };
            var primary = new Sprite2D
            {
                Name = "Primary", Texture = texture, Centered = false,
                Position = new Vector2(0, -35), Scale = new Vector2(source.DistanceTo(target) / texture.GetWidth(), 50f / texture.GetHeight()),
                Modulate = Colors.Cyan, UseParentMaterial = true,
            };
            var secondary = new Sprite2D
            {
                Name = "Secondary", Texture = texture, Centered = false,
                Position = new Vector2(0, -35), Scale = new Vector2(primary.Scale.X, 70f / texture.GetHeight()),
                Modulate = new Color(0.3f, 0.3f, 1), UseParentMaterial = true,
            };
            pivot.UseParentMaterial = true;
            pivot.AddChild(primary); pivot.AddChild(secondary); effect.AddChild(pivot);
            beams.Add((primary, secondary, primary.Scale.X));
        }
        effect.Draw += () => DrawBorder(effect);
        effect.TreeEntered += () =>
        {
            var tween = effect.CreateTween();
            tween.TweenMethod(Callable.From<float>(elapsed => UpdatePulse(effect, beams, elapsed)), 0f, Duration, Duration);
            tween.TweenCallback(Callable.From(effect.QueueFree));
        };
        return effect;
    }

    private static void UpdatePulse(Node2D effect, List<(Sprite2D Primary, Sprite2D Secondary, float Length)> beams, float elapsed)
    {
        float remaining = Duration - elapsed;
        float alpha = remaining > 0.25f ? 1 - Mathf.Pow((remaining - 0.25f) * 4, 2) : 1 - BounceOut(1 - remaining * 4);
        effect.Modulate = new Color(1, 1, 1, alpha);
        foreach (var (primary, secondary, length) in beams)
        {
            primary.Scale = new Vector2(length + (float)GD.RandRange(-0.01, 0.01), primary.Scale.Y);
            float height = (float)GD.RandRange(50, 90);
            secondary.Scale = new Vector2(length + (float)GD.RandRange(-0.02, 0.02), height / secondary.Texture.GetHeight());
            secondary.Position = new Vector2(0, -height / 2);
        }
        effect.QueueRedraw();
    }

    private static void DrawBorder(Node2D effect)
    {
        // A short sky-blue border flash accompanies the pulse in STS1.
        var viewport = effect.GetViewportRect();
        for (int i = 0; i < 12; i++)
            effect.DrawRect(viewport.Grow(-i * 6), new Color(0.53f, 0.81f, 0.92f, 0.22f * (1 - i / 12f)), false, 6);
    }

    private static float BounceOut(float value)
    {
        // libGDX's default four-bounce curve, used by STS1's bounceIn fade.
        ReadOnlySpan<float> widths = [0.68f, 0.34f, 0.2f, 0.15f];
        ReadOnlySpan<float> heights = [1, 0.26f, 0.11f, 0.03f];
        if (value == 1) return 1;
        value += widths[0] / 2;
        float width = widths[^1], height = heights[^1];
        for (int i = 0; i < widths.Length; i++)
        {
            width = widths[i];
            if (value <= width) { height = heights[i]; break; }
            value -= width;
        }
        value /= width;
        float z = 4 / width * height * value;
        return 1 - (z - z * value) * width;
    }
}
