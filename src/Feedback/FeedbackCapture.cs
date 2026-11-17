using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace AllMatters.Feedback;

internal static class FeedbackCapture
{
    internal static FeedbackSnapshot Capture(string description, byte[]? screenshot)
    {
        var collection = new JsonArray();
        var started = DateTimeOffset.Now;
        string directory = System.IO.Path.GetDirectoryName(OS.GetExecutablePath())!;
        var files = FeedbackArchive.SelectLogs(System.IO.Path.Combine(OS.GetUserDataDir(), "logs"), collection);
        var environment = new JsonObject
        {
            ["exported_at"] = started.ToString("O"),
            ["game"] = Get("game", () =>
            {
                var release = ReleaseInfoManager.Instance.ReleaseInfo;
                if (release == null)
                    collection.Add(FeedbackArchive.Record("environment.game", "unavailable", "No native release info is loaded."));
                return new JsonObject { ["version"] = release?.Version, ["commit"] = release?.Commit, ["branch"] = Get("game.branch", () => JsonValue.Create(PlatformUtil.GetPlatformBranch().ToName()), collection) };
            }, collection),
            ["allmatters"] = new JsonObject
            {
                ["id"] = ModEntry.ModId,
                ["version"] = ModManager.GetLoadedMods().FirstOrDefault(mod => mod.manifest?.id == ModEntry.ModId)?.version?.ToString()
                    ?? ModManager.GetLoadedMods().FirstOrDefault(mod => mod.manifest?.id == ModEntry.ModId)?.manifest?.version
            },
            ["platform"] = new JsonObject
            {
                ["name"] = OS.GetName(),
                ["version"] = RuntimeInformation.OSDescription,
                ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString()
            },
            ["language"] = Get("language", () => JsonValue.Create(LocManager.Instance.Language), collection),
            ["mods"] = Get("mods", () => new JsonArray(ModManager.GetLoadedMods().Select(mod => (JsonNode)new JsonObject
            {
                ["id"] = mod.manifest?.id,
                ["name"] = mod.manifest?.name,
                ["version"] = mod.version?.ToString() ?? mod.manifest?.version,
                ["source"] = mod.modSource.ToString()
            }).ToArray()), collection),
            ["settings"] = Get("settings", () => new JsonObject { ["jigsaw.enabled"] = Jigsaw.JigsawPreferences.Enabled }, collection)
        };
        var context = new JsonObject
        {
            ["in_run"] = RunManager.Instance.IsInProgress,
            ["mode"] = null,
            ["role"] = null,
            ["character"] = null,
            ["seed"] = null,
            ["ascension"] = null,
            ["act"] = null,
            ["floor"] = null,
            ["room"] = null,
            ["combat_round"] = null
        };
        var run = RunManager.Instance.DebugOnlyGetState();
        if (run != null)
        {
            var net = RunManager.Instance.NetService;
            string type = net.Type.ToString();
            context["mode"] = RunManager.Instance.IsSingleplayerOrFakeMultiplayer ? "singleplayer" : "multiplayer";
            context["role"] = type;
            context["character"] = run.Players.FirstOrDefault(player => player.NetId == net.NetId)?.Character.Id.ToString();
            context["seed"] = run.Rng.StringSeed;
            context["ascension"] = run.AscensionLevel;
            context["act"] = run.CurrentActIndex + 1;
            context["floor"] = run.TotalFloor;
            context["room"] = run.CurrentRoom?.GetType().Name;
            context["combat_round"] = CombatManager.Instance.DebugOnlyGetState()?.RoundNumber;
            try
            {
                string name = RunManager.Instance.IsSingleplayerOrFakeMultiplayer ? "current_run.save" : "current_run_mp.save";
                string account = ProjectSettings.GlobalizePath(UserDataPathProvider.GetAccountScopedBasePath(null));
                string relative = RunSaveManager.GetRunSavePath(SaveManager.Instance.CurrentProfileId, name);
                files.Add(new("attachments/" + name, System.IO.Path.Combine(account, relative), true));
            }
            catch (Exception exception) { collection.Add(FeedbackArchive.Record("attachments/current_run.save", "unavailable", exception.Message)); }
        }
        else
        {
            collection.Add(FeedbackArchive.Record("context", "not_applicable", "No run is in progress; all run-specific fields are unavailable."));
            collection.Add(FeedbackArchive.Record("attachments/current_run.save", "not_applicable", "No run is in progress."));
            screenshot = null;
        }
        if (screenshot == null)
            collection.Add(run == null
                ? FeedbackArchive.Record("attachments/screenshot.png", "not_applicable", "No run is in progress.")
                : FeedbackArchive.Record("attachments/screenshot.png", "missing", "Native feedback screen has no screenshot."));
        if (environment["allmatters"]?["version"] == null)
            collection.Add(FeedbackArchive.Record("environment.allmatters.version", "unavailable", "No loaded AllMatters manifest version is available."));
        if (run != null)
            foreach (var pair in context.Where(pair => pair.Value == null))
                collection.Add(FeedbackArchive.Record("context." + pair.Key, "unavailable", "No value exists for the captured scene."));
        collection.Add(FeedbackArchive.Record("attachments/exception.txt", "skipped", "No recent AllMatters exception snapshot is available."));
        collection.Add(FeedbackArchive.Record("attachments/divergence", "skipped", "No existing attachment can be verified as belonging to the current session with a supported format."));
        return new(started, directory, description, environment, context, files, screenshot?.ToArray(), collection);
    }

    private static JsonNode? Get(string field, Func<JsonNode?> read, JsonArray collection)
    {
        try
        { return read(); }
        catch (Exception exception)
        {
            collection.Add(FeedbackArchive.Record("environment." + field, "unavailable", exception.Message));
            return null;
        }
    }
}
