// SPDX-License-Identifier: GPL-3.0-only
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace AllMatters.Vfx;

public static class TorchHeadFlame
{
    public static void Attach(NCreatureVisuals visuals)
    {
        var sprite = new MegaSprite(visuals.GetNode<Node2D>("Visuals"));
        // All mutable resources belong to this summon. No game RNG, imported
        // fire texture, custom Godot script registration or persistent state.
        var particles = new CpuParticles2D
        {
            Name = "TorchHeadFlame",
            Amount = 36,
            Lifetime = 0.7,
            // World-space prewarming runs before the first fireslot sample and
            // leaves stray particles at (0, 0). Start cold after positioning.
            Preprocess = 0,
            LocalCoords = false,
            Emitting = false,
            Visible = false,
            Direction = Vector2.Up,
            Spread = 17,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(12, 6),
            Gravity = Vector2.Zero,
            InitialVelocityMin = 20,
            InitialVelocityMax = 100,
            ScaleAmountMin = 0.75f,
            ScaleAmountMax = 1.1f,
            Texture = new GradientTexture2D
            {
                Width = 32,
                Height = 48,
                Gradient = new Gradient { Colors = [Colors.White, Colors.White] }
            },
            ColorRamp = new Gradient
            {
                Offsets = [0, 0.25f, 1],
                Colors = [new Color(0.65f, 1, 0.12f, 0.85f), new Color(0.4f, 1, 0, 0.7f), new Color(0.15f, 0.8f, 0, 0)]
            },
            Material = new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = """
                        shader_type canvas_item;
                        render_mode blend_add;
                        void fragment() {
                            vec2 p = UV * 2.0 - 1.0;
                            float edge = 1.0 - smoothstep(0.25, 1.0, length(p));
                            COLOR.a *= edge;
                        }
                        """
                }
            }
        };
        visuals.AddChild(particles);
        SceneTree? tree = null;
        void Follow()
        {
            if (visuals.GetParent() is NCreature { Entity.IsDead: true })
            {
                particles.Emitting = false;
                return;
            }
            var bone = sprite.GetSkeleton()?.FindBone("fireslot");
            if (bone == null)
                return;
            // Both matching native extensions expose this zero-argument
            // Transform2D getter; the public managed MegaBone omits its wrapper.
            var transform = bone.BoundObject.Call("get_global_transform").AsTransform2D();
            particles.GlobalPosition = transform.Origin;
            particles.Visible = true;
            particles.Emitting = true;
        }
        visuals.TreeEntered += () =>
        {
            tree = visuals.GetTree();
            tree.ProcessFrame += Follow;
            Follow();
        };
        visuals.TreeExiting += () =>
        {
            if (tree != null)
                tree.ProcessFrame -= Follow;
            tree = null;
            particles.Emitting = false;
        };
    }
}
