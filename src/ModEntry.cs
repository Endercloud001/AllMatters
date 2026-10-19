using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace AllMatters;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public const string ModId = "AllMatters";

    public static void Initialize()
    {
        new Harmony(ModId).PatchAll(typeof(ModEntry).Assembly);
        GD.Print($"[{ModId}] Hello World upgrade adjustment loaded; event content is not implemented yet.");
    }
}
