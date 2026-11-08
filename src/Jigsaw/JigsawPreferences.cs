using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace AllMatters.Jigsaw;

public sealed class JigsawPreferencesData { public bool Enabled { get; set; } = true; }
internal static class JigsawPreferences
{
    private static readonly ModDataStore store = ModDataStore.For(ModEntry.ModId);
    private static bool registered;
    internal static void Register()
    {
        if (registered) return;
        registered = true;
        store.Register<JigsawPreferencesData>("jigsaw_settings", "jigsaw_settings", SaveScope.Global);
        RitsuLibFramework.RegisterModSettings(ModEntry.ModId, page => page
            .WithTitle(ModSettingsText.Literal("万物皆有意义"))
            .WithModDisplayName(ModSettingsText.Literal("AllMatters"))
            .AddSection("jigsaw", section => section.WithTitle(ModSettingsText.Literal("拼图模式"))
                .AddToggle("enabled", ModSettingsText.Literal("新单人及由我主持的多人标准跑局默认启用拼图模式"),
                    new ModSettingsValueBinding<JigsawPreferencesData, bool>(ModEntry.ModId, "jigsaw_settings", SaveScope.Global,
                        settings => settings.Enabled, (settings, value) => settings.Enabled = value))));
    }
    internal static bool Enabled
    {
        get { Register(); store.InitializeGlobal(); return store.Get<JigsawPreferencesData>("jigsaw_settings").Enabled; }
        set { Register(); store.InitializeGlobal(); store.Get<JigsawPreferencesData>("jigsaw_settings").Enabled = value; store.Save("jigsaw_settings"); }
    }
}
