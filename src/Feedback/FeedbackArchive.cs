using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Logging;

namespace AllMatters.Feedback;

internal sealed record FeedbackFile(string Entry, string? Source, bool Save = false);
internal sealed record FeedbackSnapshot(DateTimeOffset Started, string OutputDirectory, string Description,
    JsonObject Environment, JsonObject Context, List<FeedbackFile> Files, byte[]? Screenshot,
    JsonArray Collection);
internal sealed record FeedbackExportResult(string? Path, string? Error);

internal static class FeedbackArchive
{
    internal const string SuccessMessage = "诊断文件已生成完毕。请将此文件发给模组作者。";
    internal const string CollisionMessage = "同名诊断文件已存在，请稍后重试。";
    private const string PartialPrefix = ".AllMatters-feedback-";
    private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    internal static string FileName(DateTimeOffset started) =>
        "AllMatters_diagnosis_pack_" + started.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + ".zip";

    // Sanitize values rather than serialized JSON: paths can contain escaped backslashes.
    internal static JsonNode? SanitizeJson(JsonNode? node, bool numericIds = false)
    {
        if (node is JsonObject obj)
            return new JsonObject(obj.Select(pair => KeyValuePair.Create(pair.Key, SanitizeJson(pair.Value, numericIds))));
        if (node is JsonArray array)
            return new JsonArray(array.Select(value => SanitizeJson(value, numericIds)).ToArray());
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return JsonValue.Create(LogSanitizer.Sanitize(text));
        if (numericIds && node is JsonValue number)
        {
            string raw = number.ToJsonString();
            if (raw.Length == 17 && raw.StartsWith("76561", StringComparison.Ordinal) && ulong.TryParse(raw, out _))
            {
#if STS2_PUBLIC
                string anonymized = LogSanitizer.Sanitize(raw);
                if (anonymized.StartsWith('A')) anonymized = anonymized[1..];
                return JsonValue.Create(ulong.Parse(anonymized, CultureInfo.InvariantCulture));
#else
                return JsonNode.Parse(LogSanitizer.AnonymizeSteamIds(Encoding.UTF8.GetBytes(raw)));
#endif
            }
        }
        return node?.DeepClone();
    }

    internal static List<FeedbackFile> SelectLogs(string directory, JsonArray collection)
    {
        string? previous = null;
        try
        {
            // Godot rotates godot.log to godotYYYY-MM-DDTHH.MM.SS.log.
            if (Directory.Exists(directory))
                previous = Directory.EnumerateFiles(directory, "godot*.log")
                    .Where(path => IsArchiveLog(System.IO.Path.GetFileName(path)))
                    .OrderByDescending(path => System.IO.Path.GetFileName(path), StringComparer.Ordinal)
                    .ThenByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        }
        catch (Exception exception) { collection.Add(Record("logs/previous.log", "selection_failed", exception.Message)); }
        return [new("logs/current.log", System.IO.Path.Combine(directory, "godot.log")), new("logs/previous.log", previous)];
    }

    private static bool IsArchiveLog(string name) => name.StartsWith("godot", StringComparison.Ordinal)
        && name.EndsWith(".log", StringComparison.Ordinal)
        && DateTime.TryParseExact(name[5..^4], "yyyy-MM-dd'T'HH.mm.ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    internal static JsonObject Record(string entry, string status, string? reason = null) =>
        new() { ["entry"] = entry, ["status"] = status, ["reason"] = reason };

    internal static byte[] ReadSnapshot(string source)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long remaining = input.Length;
        using var output = new MemoryStream();
        byte[] buffer = new byte[65536];
        while (remaining > 0)
        {
            int count = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (count == 0)
                break;
            output.Write(buffer, 0, count);
            remaining -= count;
        }
        return output.ToArray();
    }

    internal static FeedbackExportResult Export(FeedbackSnapshot snapshot)
    {
        string destination = System.IO.Path.Combine(snapshot.OutputDirectory, FileName(snapshot.Started));
        string partial = System.IO.Path.Combine(snapshot.OutputDirectory,
            $"{PartialPrefix}{System.Environment.ProcessId}-{Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks}-{Guid.NewGuid():N}.partial");
        string? failure = null;
        try
        {
            if (File.Exists(destination))
                throw new IOException(CollisionMessage);
            using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    WriteText(archive, "feedback.txt", LogSanitizer.Sanitize(snapshot.Description));
                    WriteJson(archive, "environment.json", snapshot.Environment);
                    WriteJson(archive, "context.json", snapshot.Context);
                    foreach (string entry in new[] { "feedback.txt", "environment.json", "context.json" })
                    {
                        var collected = Record(entry, "collected");
                        collected["collected_at"] = snapshot.Started.ToString("O", CultureInfo.InvariantCulture);
                        snapshot.Collection.Add(collected);
                    }
                    foreach (var file in snapshot.Files)
                    {
                        var record = Record(file.Entry, "missing", "No applicable source exists.");
                        record["source"] = file.Source == null ? null : System.IO.Path.GetFileName(file.Source);
                        snapshot.Collection.Add(record);
                        if (file.Source == null)
                            continue;
                        if (!File.Exists(file.Source))
                        {
                            record["reason"] = "Selected source does not exist or cannot be inspected.";
                            continue;
                        }
                        string text;
                        try
                        {
                            record["last_modified"] = File.GetLastWriteTimeUtc(file.Source).ToString("O", CultureInfo.InvariantCulture);
                            byte[] bytes = ReadSnapshot(file.Source);
                            if (file.Save)
                            {
                                var save = JsonNode.Parse(bytes) ?? throw new InvalidDataException("Empty save JSON.");
                                text = SanitizeJson(save, true)!.ToJsonString(jsonOptions);
                            }
                            else
                                text = LogSanitizer.Sanitize(Encoding.UTF8.GetString(bytes));
                            record["status"] = "collected";
                            record["reason"] = null;
                            record["collected_at"] = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
                        }
                        catch (Exception exception) { record["status"] = "unavailable"; record["reason"] = exception.Message; continue; }
                        WriteText(archive, file.Entry, text);
                    }
                    var screenshot = Record("attachments/screenshot.png", snapshot.Screenshot == null ? "missing" : "collected",
                        snapshot.Screenshot == null ? "Native feedback screen has no screenshot." : null);
                    snapshot.Collection.Add(screenshot);
                    if (snapshot.Screenshot != null)
                    {
                        using var image = archive.CreateEntry("attachments/screenshot.png").Open();
                        image.Write(snapshot.Screenshot);
                    }
                    WriteJson(archive, "collection.json", snapshot.Collection);
                }
                output.Flush(true);
                output.Position = 0;
                using var verified = new ZipArchive(output, ZipArchiveMode.Read, true);
                foreach (var entry in verified.Entries)
                {
                    using var stream = entry.Open();
                    stream.CopyTo(Stream.Null);
                }
            }
            try
            { File.Move(partial, destination, false); }
            catch (IOException) when (File.Exists(destination)) { throw new IOException(CollisionMessage); }
            return new(destination, null);
        }
        catch (Exception exception) { failure = exception.Message; }
        finally
        {
            try
            { if (File.Exists(partial)) File.Delete(partial); }
            catch (Exception exception) { failure = $"{failure}\n临时文件未能清理：{partial}\n{exception.Message}"; }
        }
        return new(null, failure);
    }

    internal static List<string> Cleanup(string directory)
    {
        var failures = new List<string>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, PartialPrefix + "*.partial"))
            {
                var match = Regex.Match(System.IO.Path.GetFileName(path), @"^\.AllMatters-feedback-(\d+)-(\d+)-[a-f0-9]{32}\.partial$");
                if (!match.Success)
                    continue;
                try
                {
                    try
                    {
                        using var owner = Process.GetProcessById(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
                        if (owner.StartTime.ToUniversalTime().Ticks == long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))
                            continue;
                    }
                    catch (ArgumentException) { /* Owner exited. */ }
                    using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    { }
                    File.Delete(path);
                }
                catch (Exception exception) { failures.Add(path + ": " + exception.Message); }
            }
        }
        catch (Exception exception) { failures.Add(exception.Message); }
        return failures;
    }

    private static void WriteJson(ZipArchive archive, string entry, JsonNode node) =>
        WriteText(archive, entry, SanitizeJson(node)!.ToJsonString(jsonOptions));
    private static void WriteText(ZipArchive archive, string entry, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(entry).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }
}
