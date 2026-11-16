using AllMatters.Monsters;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

internal static class JigsawHuntingTorchFlameTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Jigsaw Torch flame native bone API probe", BoneProbe),
        ("Jigsaw Torch flame production visuals contain live green emitter", Emitter)
    ];

    // Discovery only: do not call guessed native bone methods. Capture their
    // actual signatures on both matching Spine extensions before implementation.
    private static async Task BoneProbe()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var scene = ResourceLoader.Load<PackedScene>("res://AllMatters/monsters/torch_head/torch_head.tscn").Instantiate<Node2D>();
        try
        {
            scene.Position = new Vector2(321, 417);
            scene.Scale = new Vector2(0.8f, 1.2f);
            tree.Root.AddChild(scene);
            for (int frame = 0; frame < 5; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var spriteNode = scene.GetNode<Node2D>("Visuals");
            var sprite = new MegaSprite(spriteNode);
            var skeleton = sprite.GetSkeleton();
            NativeEventTests.Check(skeleton != null, "Matching native Torch skeleton initializes.");
            var bone = skeleton!.FindBone("fireslot");
            NativeEventTests.Check(bone != null, "Source Torch contains the actual fireslot bone.");
            Dump("sprite", sprite.BoundObject);
            Dump("skeleton", skeleton.BoundObject);
            Dump("fireslot", bone!.BoundObject);
            Console.WriteLine($"TORCH_BONE_TRANSFORM sprite={spriteNode.GlobalTransform}");
            var before = bone.BoundObject.Call("get_global_transform").AsTransform2D();
            scene.Position += new Vector2(73, 41);
            for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var after = bone.BoundObject.Call("get_global_transform").AsTransform2D();
            Console.WriteLine($"TORCH_BONE_COORDINATES before={before} after={after} delta={after.Origin - before.Origin} spriteAfter={spriteNode.GlobalTransform}");
        }
        finally
        {
            scene.Free();
        }
    }

    private static void Dump(string role, GodotObject value)
    {
        foreach (var method in value.GetMethodList())
        {
            string name = method["name"].AsString();
            if (name.Contains("world") || name.Contains("transform") || name.Contains("position") || name.Contains("bone"))
                Console.WriteLine($"TORCH_BONE_API role={role} method={name} args={method["args"].AsGodotArray().Count} return={method["return"].AsGodotDictionary()["type"].AsInt32()}");
        }
        foreach (var property in value.GetPropertyList())
        {
            string name = property["name"].AsString();
            if (name.Contains("world") || name.Contains("transform") || name.Contains("position"))
                Console.WriteLine($"TORCH_BONE_API role={role} property={name} type={property["type"].AsInt32()}");
        }
    }

    private static async Task Emitter()
    {
        var visuals = (NCreatureVisuals)AccessTools.Method(typeof(TorchHead), "TryCreateCreatureVisuals")
            .Invoke(ModelDb.Monster<TorchHead>().ToMutable(), null)!;
        try
        {
            var emitters = visuals.FindChildren("*", "CPUParticles2D", true, false).OfType<CpuParticles2D>().ToArray();
            NativeEventTests.Check(emitters.Length == 1,
                "Production Torch visuals create a flame emitter; a correct PNG alone cannot supply the flame.");
            var flame = emitters.Single();
            var second = (NCreatureVisuals)AccessTools.Method(typeof(TorchHead), "TryCreateCreatureVisuals")
                .Invoke(ModelDb.Monster<TorchHead>().ToMutable(), null)!;
            try
            {
                var other = second.FindChildren("*", "CPUParticles2D", true, false).OfType<CpuParticles2D>().Single();
                NativeEventTests.Check(flame.Material.GetInstanceId() != other.Material.GetInstanceId() &&
                    flame.ColorRamp.GetInstanceId() != other.ColorRamp.GetInstanceId() &&
                    flame.Texture.GetInstanceId() != other.Texture.GetInstanceId(), "Two summons own independent mutable flame resources.");
            }
            finally { second.Free(); }

            var tree = (SceneTree)Engine.GetMainLoop();
            var viewport = new SubViewport { Size = new Vector2I(640, 480), TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            tree.Root.AddChild(viewport);
            try
            {
                visuals.Position = new Vector2(320, 420);
                viewport.AddChild(visuals);
                for (int frame = 0; frame < 10; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                var bone = new MegaSprite(visuals.GetNode<Node2D>("Visuals")).GetSkeleton()!.FindBone("fireslot")!;
                var expected = bone.BoundObject.Call("get_global_transform").AsTransform2D().Origin;
                NativeEventTests.Check(flame.Visible && flame.Emitting && flame.GlobalPosition.DistanceTo(expected) < 0.1f,
                    "Live native fireslot drives the emitter position.");
                Console.WriteLine($"TORCH_FLAME_GEOMETRY emitter={flame.GlobalPosition} intent={visuals.GetNode<Marker2D>("IntentPos").GlobalPosition} lifetime={flame.Lifetime} maxSpeed={flame.InitialVelocityMax}");
                string? capture = System.Environment.GetEnvironmentVariable("ALLMATTERS_TORCH_FLAME_CAPTURE");
                if (!string.IsNullOrWhiteSpace(capture))
                {
                    // Allow a full cold-start lifetime in real elapsed time;
                    // ten fast headless process frames do not fill the plume.
                    await tree.ToSignal(tree.CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
                    await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var image = viewport.GetTexture().GetImage();
                    NativeEventTests.Check(image != null && !image.IsEmpty(), "A real rendering backend supplies the offscreen image.");
                    image!.SavePng(capture);
                    int green = 0;
                    int minX = image.GetWidth(), minY = image.GetHeight(), maxX = -1, maxY = -1;
                    var origin = flame.GlobalPosition;
                    for (int y = Math.Max(0, (int)origin.Y - 85); y < Math.Min(image.GetHeight(), (int)origin.Y + 12); y++)
                        for (int x = Math.Max(0, (int)origin.X - 35); x < Math.Min(image.GetWidth(), (int)origin.X + 35); x++)
                        {
                            var color = image.GetPixel(x, y);
                            if (color.G > 0.15f && color.G > color.R * 1.25f && color.G > color.B * 1.5f)
                            {
                                green++;
                                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                            }
                        }
                    Console.WriteLine($"TORCH_FLAME_PIXELS green={green} extent=({minX},{minY})..({maxX},{maxY})");
                    NativeEventTests.Check(green > 250 && maxX - minX >= 22 && maxY - minY >= 35,
                        $"Head region contains a visible green plume rather than a tiny dot: {green} pixels.");
                    for (int y = 0; y < 32; y++)
                        for (int x = 0; x < 32; x++)
                        {
                            var color = image.GetPixel(x, y);
                            NativeEventTests.Check(!(color.G > 0.15f && color.G > color.R * 1.25f && color.G > color.B * 1.5f),
                                "Cold start leaves no green particle at the world origin.");
                        }
                }
                viewport.RemoveChild(visuals);
                for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                // Re-entry must subscribe once again after TreeExiting unhooks.
                viewport.AddChild(visuals);
                for (int frame = 0; frame < 2; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                NativeEventTests.Check(flame.Visible, "The existing emitter survives tree exit/re-entry.");
                viewport.RemoveChild(visuals);
            }
            finally { viewport.Free(); }
        }
        finally
        {
            visuals.Free();
        }
    }
}
