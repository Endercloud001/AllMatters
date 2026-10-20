using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;

namespace AllMatters;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public const string ModId = "AllMatters";

    public static void Initialize()
    {
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, typeof(ModEntry).Assembly);
        RitsuLibFramework.EnsureGodotScriptsRegistered(typeof(ModEntry).Assembly,
            RitsuLibFramework.CreateLogger(ModId));
        new Harmony(ModId).PatchAll(typeof(ModEntry).Assembly);
        GD.Print($"[{ModId}] Hello World adjustment and returning events loaded.");
    }
}
