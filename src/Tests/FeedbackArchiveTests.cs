using System.Globalization;
using System.IO.Compression;
using System.Text.Json.Nodes;
using AllMatters.Feedback;
using MegaCrit.Sts2.Core.Logging;

internal static class FeedbackArchiveTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Feedback export sanitizes archive JSON and preserves numeric save IDs", Contents),
        ("Feedback export selects current and latest native archive excluding mod logs", Logs),
        ("Feedback export records missing unreadable logs without fake entries", Missing),
        ("Feedback export collision preserves existing report and cleans partial files", Collision),
        ("Feedback export cleanup protects live process partial files", Cleanup)
    ];

    private static void Check(bool condition, string message) => NativeEventTests.Check(condition, message);
    private static string Temporary()
    {
        string directory = Path.Combine(Path.GetTempPath(), "AllMatters-feedback-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
    private static FeedbackSnapshot Snapshot(string directory, List<FeedbackFile>? files = null) =>
        new(new DateTimeOffset(2026, 11, 18, 14, 32, 5, TimeSpan.FromHours(8)), directory, "",
            new JsonObject { ["path"] = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ["enabled"] = true, ["number"] = 3 },
            new JsonObject { ["in_run"] = false }, files ?? [], null, []);
    private static string Read(ZipArchive archive, string entry)
    {
        using var reader = new StreamReader(archive.GetEntry(entry)!.Open());
        return reader.ReadToEnd();
    }

    private static Task Contents()
    {
        string directory = Temporary();
        try
        {
            string source = Path.Combine(directory, "save.json");
            const string id = "76561198000000001";
            File.WriteAllText(source, "{\"id\":" + id + ",\"owner\":\"" + id + "\",\"flag\":true}");
            var result = FeedbackArchive.Export(Snapshot(directory, [new("attachments/current_run.save", source, true)]) with { Description = id });
            Check(result.Error == null && result.Path != null, "Export succeeds for empty-safe feedback and save.");
            using var archive = ZipFile.OpenRead(result.Path!);
            Check(Path.GetFileName(result.Path) == "AllMatters_diagnosis_pack_2026-11-18_14-32-05.zip", "Fixed timestamp uses local calendar fields.");
            Check(Read(archive, "feedback.txt") == LogSanitizer.Sanitize(id), "Native sanitizer handles feedback.");
            var save = JsonNode.Parse(Read(archive, "attachments/current_run.save"))!;
            string anonymous = LogSanitizer.Sanitize(id).TrimStart('A');
            Check(save["id"]!.GetValue<ulong>().ToString(CultureInfo.InvariantCulture) == anonymous, "Numeric ID stays numeric with matching native anonymization.");
            Check(save["owner"]!.GetValue<string>() == LogSanitizer.Sanitize(id), "String ID uses the native text representation.");
            var environment = JsonNode.Parse(Read(archive, "environment.json"))!;
            Check(environment["path"]!.GetValue<string>() == LogSanitizer.Sanitize(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)), "JSON string value is sanitized before escaping.");
            Check(environment["enabled"]!.GetValue<bool>() && environment["number"]!.GetValue<int>() == 3, "JSON primitive types survive sanitization.");
            var collected = JsonNode.Parse(Read(archive, "collection.json"))!.AsArray();
            foreach (string entry in new[] { "feedback.txt", "environment.json", "context.json" })
                Check(collected.Any(node => node!["entry"]!.GetValue<string>() == entry && node["status"]!.GetValue<string>() == "collected"), "Required text entries have explicit successful collection records.");
            Check(!Directory.EnumerateFiles(directory, "*.partial").Any(), "Published archives leave no partial.");
        }
        finally { Directory.Delete(directory, true); }
        return Task.CompletedTask;
    }

    private static Task Logs()
    {
        string directory = Temporary();
        try
        {
            File.WriteAllText(Path.Combine(directory, "godot.log"), "current");
            File.WriteAllText(Path.Combine(directory, "godot2026-11-17T22.41.17.log"), "older");
            File.WriteAllText(Path.Combine(directory, "godot2026-11-18T00.24.52.log"), "previous");
            File.WriteAllText(Path.Combine(directory, "godot-mod.log"), "exclude");
            var snapshot = Snapshot(directory, FeedbackArchive.SelectLogs(directory, []));
            File.WriteAllText(Path.Combine(directory, "godot2026-11-18T01.00.00.log"), "late");
            var result = FeedbackArchive.Export(snapshot);
            using var archive = ZipFile.OpenRead(result.Path!);
            Check(Read(archive, "logs/current.log") == "current" && Read(archive, "logs/previous.log") == "previous", "Selections are frozen before worker export.");
            Check(archive.Entries.Count(entry => entry.FullName.StartsWith("logs/", StringComparison.Ordinal)) == 2, "Only two selected native logs are exported.");
        }
        finally { Directory.Delete(directory, true); }
        return Task.CompletedTask;
    }

    private static Task Missing()
    {
        string directory = Temporary();
        try
        {
            string lockedPath = Path.Combine(directory, "locked.log");
            File.WriteAllText(lockedPath, "locked");
            using var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var result = FeedbackArchive.Export(Snapshot(directory, [new("logs/current.log", lockedPath), new("logs/previous.log", null)]));
            using var archive = ZipFile.OpenRead(result.Path!);
            Check(archive.GetEntry("logs/current.log") == null && archive.GetEntry("logs/previous.log") == null, "Missing/unreadable sources do not produce empty logs.");
            Check(Read(archive, "feedback.txt") == "", "Empty feedback is an actual empty archive entry.");
            var records = JsonNode.Parse(Read(archive, "collection.json"))!.AsArray();
            Check(records.Any(node => node!["entry"]!.GetValue<string>() == "logs/current.log" && node["status"]!.GetValue<string>() == "unavailable"), "Unreadable source is recorded.");
            Check(records.Any(node => node!["entry"]!.GetValue<string>() == "logs/previous.log" && node["status"]!.GetValue<string>() == "missing"), "Absent source is recorded.");
            foreach (bool hasCurrent in new[] { true, false })
            {
                string scenario = Path.Combine(directory, hasCurrent ? "single-log" : "no-logs");
                Directory.CreateDirectory(scenario);
                if (hasCurrent) File.WriteAllText(Path.Combine(scenario, "godot.log"), "only current");
                var outcome = FeedbackArchive.Export(Snapshot(scenario, FeedbackArchive.SelectLogs(scenario, [])));
                using var pack = ZipFile.OpenRead(outcome.Path!);
                Check((pack.GetEntry("logs/current.log") != null) == hasCurrent && pack.GetEntry("logs/previous.log") == null,
                    "One/no existing native logs exports exactly the available sources.");
                var collected = JsonNode.Parse(Read(pack, "collection.json"))!.AsArray();
                Check(collected.Count(node => node!["status"]!.GetValue<string>() == "missing" && node["entry"]!.GetValue<string>().StartsWith("logs/", StringComparison.Ordinal)) == (hasCurrent ? 1 : 2),
                    "Missing log count matches disk sources.");
            }
        }
        finally { Directory.Delete(directory, true); }
        return Task.CompletedTask;
    }

    private static Task Collision()
    {
        string directory = Temporary();
        try
        {
            var snapshot = Snapshot(directory);
            var first = FeedbackArchive.Export(snapshot);
            byte[] original = File.ReadAllBytes(first.Path!);
            var second = FeedbackArchive.Export(Snapshot(directory) with { Description = "second" });
            Check(second.Error == FeedbackArchive.CollisionMessage && second.Path == null, "Same-second export reports conflict.");
            Check(File.ReadAllBytes(first.Path!).SequenceEqual(original), "Existing successful report remains intact.");
            Check(!Directory.EnumerateFiles(directory, "*.partial").Any(), "Conflict leaves no temporary file.");
            var failed = FeedbackArchive.Export(Snapshot(Path.Combine(directory, "does-not-exist")));
            Check(failed.Error != null && failed.Path == null, "Write failure cannot report success.");
        }
        finally { Directory.Delete(directory, true); }
        return Task.CompletedTask;
    }

    private static Task Cleanup()
    {
        string directory = Temporary();
        try
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            string live = Path.Combine(directory, $".AllMatters-feedback-{process.Id}-{process.StartTime.ToUniversalTime().Ticks}-{Guid.NewGuid():N}.partial");
            string stale = Path.Combine(directory, $".AllMatters-feedback-{process.Id}-1-{Guid.NewGuid():N}.partial");
            string foreign = Path.Combine(directory, "foreign.partial");
            File.WriteAllText(live, "live"); File.WriteAllText(stale, "stale"); File.WriteAllText(foreign, "foreign");
            Check(FeedbackArchive.Cleanup(directory).Count == 0, "Cleanup reports no unexpected failures.");
            Check(File.Exists(live) && File.Exists(foreign) && !File.Exists(stale), "Owner start time protects live instance and unrelated partial names.");
        }
        finally { Directory.Delete(directory, true); }
        return Task.CompletedTask;
    }
}
