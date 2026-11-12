using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Migrations;
using STS2RitsuLib.RunData;

namespace AllMatters.Jigsaw;

public sealed class JigsawCheckpointData
{
    [JsonRequired] public int Version { get; set; }
    [JsonRequired] public int Act { get; set; }
    [JsonRequired] public JigsawSave? Map { get; set; }
    public Dictionary<int, HashSet<int>>? RevisitedHistoryIndices { get; set; }
}

/// <summary>Run data lives inside the native save, never in a second run file.</summary>
public static class JigsawCheckpoint
{
    public static RunSavedData<JigsawCheckpointData> Slot { get; } =
        RunSavedDataStore.For(ModEntry.ModId).Register<JigsawCheckpointData>("jigsaw");
    public static void Initialize() { _ = Slot; _ = JigsawNewRunMode.Slot; }

    public static void Capture(RunState native, JigsawRun map) =>
        Slot.Set(native, new()
        {
            Version = 1,
            Act = native.CurrentActIndex,
            Map = map.CreateSave(),
            RevisitedHistoryIndices = Slot.TryGet(native, out var previous) && previous.RevisitedHistoryIndices is { } indices
                ? indices.ToDictionary(pair => pair.Key, pair => new HashSet<int>(pair.Value)) : null
        });
}

// RitsuLib 0.6.7 otherwise reopens the primary path after native recovery. Bind the
// extension to native's successfully parsed object instead. Both target parsers
// have this signature; no independent deserializer decides the recovery source.
internal static class JigsawSaveSource
{
    private sealed record Source(string Json, bool IsJigsaw);
    private static readonly ConditionalWeakTable<SerializableRun, Source> sources = new();
    private static readonly MethodInfo attach = typeof(RunSavedDataStore).Assembly
        .GetType("STS2RitsuLib.RunData.RunSavedDataRegistry", true)!
        .GetMethod("AttachDocumentFromJson")!;
    private static readonly MethodInfo inject = attach.DeclaringType!.GetMethod("InjectIntoUtf8Json")!;
    private static readonly MethodInfo build = attach.DeclaringType!.GetMethod("BuildDocumentFromRun")!;
    private static readonly MethodInfo attachDocument = attach.DeclaringType!.GetMethod("AttachDocument")!;

    private static readonly MethodInfo tryGetDocument = typeof(RunSavedDataStore).Assembly
        .GetType("STS2RitsuLib.RunData.RunSavedDataRuntime", true)!.GetMethod("TryGetDocument")!;
    private static readonly MethodInfo documentEntries = typeof(RunSavedDataStore).Assembly
        .GetType("STS2RitsuLib.RunData.RunSavedDataDocument", true)!.GetMethod("Entries")!;

    internal static bool TryReadRevisitedHistoryIndices(SerializableRun save, out Dictionary<int, HashSet<int>>? indices)
    {
        indices = null;
        Attach(save);
        object?[] arguments = [save, null];
        if (!(bool)tryGetDocument.Invoke(null, arguments)!)
            return false;
        var entries = (IEnumerable<(string ModId, string Key, JsonObject Entry)>)documentEntries.Invoke(arguments[1], null)!;
        foreach (var (modId, key, entry) in entries)
        {
            // The old JSON property lookup was ordinal; TryGetRaw is case-insensitive.
            if (!string.Equals(modId, ModEntry.ModId, StringComparison.Ordinal) || !string.Equals(key, "jigsaw", StringComparison.Ordinal))
                continue;
            if (entry["data"] is not JsonObject checkpoint)
                throw new InvalidDataException("拼图检查点缺少原生扩展数据。");
            if (checkpoint[nameof(JigsawCheckpointData.RevisitedHistoryIndices)] is { } value)
            {
                try
                { indices = value.Deserialize<Dictionary<int, HashSet<int>>>(); }
                catch (JsonException error) { throw new InvalidDataException("拼图重访历史不可读。", error); }
            }
            return true;
        }
        return false;
    }

    public static void Observe(string content, ReadSaveResult<SerializableRun> result)
    {
        if (!result.Success || result.SaveData is not { } save)
            return;
        JsonDocument parsed;
        try
        { parsed = JsonDocument.Parse(content); }
        catch (JsonException) { return; } // Native recovery may salvage non-JSON saves; do not break that result.
        using var json = parsed;
        bool hasJigsaw = json.RootElement.TryGetProperty("_ritsulib", out var root) &&
            root.ValueKind == JsonValueKind.Object && root.TryGetProperty("run_saved_data", out var data) &&
            data.ValueKind == JsonValueKind.Object && data.TryGetProperty(ModEntry.ModId, out var mod) &&
            mod.ValueKind == JsonValueKind.Object && mod.TryGetProperty("jigsaw", out _);
        bool marked = save.Modifiers.Any(modifier => modifier.Id == ModelDb.Modifier<AllMattersJigsawMode>().Id);
        sources.Remove(save);
        sources.Add(save, new(content, hasJigsaw || marked));
    }

    internal static bool IsJigsaw(SerializableRun save) =>
        save.Modifiers.Any(m => m.Id == ModelDb.Modifier<AllMattersJigsawMode>().Id) ||
        sources.TryGetValue(save, out var source) && source.IsJigsaw;

    public static void Attach(SerializableRun save)
    {
        if (sources.TryGetValue(save, out var source) && source.IsJigsaw)
            attach.Invoke(null, [save, source.Json]);
    }

    // Native restoration reuses some mutable history collections from its input.
    // A retry must keep an independent complete point for another failed load.
    public static SerializableRun Copy(SerializableRun save)
    {
        return FromJson(ToJson(save));
    }

    internal static string Serialize(SerializableRun save)
    {
        using var output = new MemoryStream();
        using (var compressed = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(ToJson(save));
            compressed.Write(bytes);
        }
        return Convert.ToBase64String(output.ToArray());
    }

    internal static SerializableRun Deserialize(string payload)
    {
        using var input = new MemoryStream(Convert.FromBase64String(payload));
        using var compressed = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        compressed.CopyTo(output);
        return FromJson(System.Text.Encoding.UTF8.GetString(output.ToArray()));
    }

    private static string ToJson(SerializableRun save)
    {
        var type = JsonSerializationUtility.GetTypeInfo<SerializableRun>();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(save, type);
        bytes = (byte[])inject.Invoke(null, [bytes, save])!;
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static SerializableRun FromJson(string json)
    {
        var type = JsonSerializationUtility.GetTypeInfo<SerializableRun>();
        var copy = JsonSerializer.Deserialize(json, type) ?? throw new InvalidDataException("进入检查点无法复制。");
        var identity = ModelDb.Modifier<AllMattersJigsawMode>();
        if (!copy.Modifiers.Any(m => m.Id == identity.Id))
            copy.Modifiers.Add(identity.ToMutable().ToSerializable());
        attach.Invoke(null, [copy, json]);
        return copy;
    }

    public static void CaptureDocument(SerializableRun save, RunState native) =>
        attachDocument.Invoke(null, [save, build.Invoke(null, [native])]);
}

// Never detour a closed generic parser: reference-type instantiations share CLR
// code. A Run-only detour can corrupt Profile/Prefs/Progress generic contexts.
[HarmonyPatch]
internal static class JigsawObserveNativeSaveSource
{
    internal sealed class ReadScope
    {
        public ReadScope? Previous;
        public string? Content;
    }
    [ThreadStatic] internal static ReadScope? Current;
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(MegaCrit.Sts2.Core.Saves.Managers.RunSaveManager), "LoadRunSave");
        yield return AccessTools.Method(typeof(MegaCrit.Sts2.Core.Saves.Managers.RunSaveManager), "LoadMultiplayerRunSave");
    }
    private static void Prefix(out ReadScope __state) => Current = __state = new() { Previous = Current };
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ReadSaveResult<SerializableRun> __result, ReadScope __state)
    {
        if (__state.Content is { } json && __result.Success && __result.SaveData is { } save)
        {
            JigsawSaveSource.Observe(json, __result);
            JigsawSaveSource.Attach(save);
        }
    }
    private static void Finalizer(ReadScope __state) => Current = __state.Previous;
}

[HarmonyPatch]
internal static class JigsawObserveNativeRead
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in new[] { typeof(GodotFileIo), typeof(CloudSaveStore), typeof(MegaCrit.Sts2.Core.Saves.Test.MockGodotFileIo) })
            yield return AccessTools.Method(type, "ReadFile", [typeof(string)]);
    }
    private static void Postfix(string __result)
    {
        if (JigsawObserveNativeSaveSource.Current is not { } scope || string.IsNullOrWhiteSpace(__result))
            return;
        // Ignore library post-load primary rereads. Only native migration reads
        // determine the source of the successfully returned run object.
        if (new System.Diagnostics.StackTrace().GetFrames().Any(f => f.GetMethod()?.DeclaringType == typeof(MigrationManager)))
            scope.Content = __result;
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class JigsawRestoreNativeSaveSource
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(SerializableRun save) => JigsawSaveSource.Attach(save);
}
