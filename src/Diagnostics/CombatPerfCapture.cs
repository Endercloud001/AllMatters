#if ALLMATTERS_PERF_CAPTURE
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using AllMatters.Jigsaw.Presentation;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Diagnostics;

// This entire type and its patches are absent from normal builds.
internal static class CombatPerfCapture
{
    private readonly record struct Sample(double Ms, double FrameGapMs, int Gen0, int Gen1, int Gen2, long AllocatedBytes, long HeapBytes);
    private readonly record struct Stage(double Ms, string Name, double DurationMs);
    private sealed class Window
    {
        internal readonly long Start = Stopwatch.GetTimestamp();
        internal readonly List<Sample> Frames = [];
        internal readonly List<Stage> Stages = [];
        internal double EndAt = double.PositiveInfinity;
        internal string Outcome = "in-progress";
        internal string Room = "unknown";
    }
    private static readonly Queue<Sample> recent = new();
    private static readonly List<Task> writes = [];
    private static readonly long origin = Stopwatch.GetTimestamp();
    private static Window? current;
    private static bool initialized, writeErrorReported;
    private static long lastFrame;
    private static void Protect(Action action)
    {
        try { action(); }
        catch (Exception error) { ReportError(error); }
    }
    private static void ReportError(Exception error)
    {
        if (writeErrorReported) return;
        writeErrorReported = true;
        try { Console.Error.WriteLine("[AllMatters capture] Diagnostic collection failed: " + error.Message); }
        catch { /* Diagnostic reporting must never interrupt gameplay. */ }
    }
    internal static string OutputDirectory => typeof(CombatPerfCapture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "CombatPerfCaptureOutputDir").Value!;
    private static double Now => Stopwatch.GetElapsedTime(origin).TotalMilliseconds;
    internal static Task PendingWrites => Task.WhenAll(writes.ToArray());

    internal static void Initialize() => Protect(InitializeCore);
    private static void InitializeCore()
    {
        if (initialized || Engine.GetMainLoop() is not SceneTree tree)
            return;
        initialized = true;
        tree.ProcessFrame += Frame;
        GD.Print("[AllMatters capture] Temporary capture enabled; output: " + OutputDirectory);
    }
    private static void Frame() => Protect(FrameCore);
    private static void FrameCore()
    {
        long timestamp = Stopwatch.GetTimestamp();
        double now = Now;
        var sample = new Sample(now, lastFrame == 0 ? 0 : Stopwatch.GetElapsedTime(lastFrame, timestamp).TotalMilliseconds,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), GC.GetTotalAllocatedBytes(), GC.GetTotalMemory(false));
        lastFrame = timestamp;
        recent.Enqueue(sample);
        while (recent.Count > 600 || recent.TryPeek(out var oldest) && now - oldest.Ms > 2000)
            recent.Dequeue();
        if (current is not { } window)
            return;
        window.Frames.Add(sample);
        if (now >= window.EndAt || Stopwatch.GetElapsedTime(window.Start).TotalSeconds >= 60 || window.Frames.Count >= 20000)
            Flush();
    }
    internal static long BeginEntry()
    {
        Protect(BeginEntryCore);
        return Stopwatch.GetTimestamp();
    }
    private static void BeginEntryCore()
    {
        if (current != null)
        {
            current.Outcome = "superseded";
            Flush();
        }
        current = new();
        current.Frames.AddRange(recent);
        Mark("entry-start", 0);
    }
    internal static void Mark(string name, double duration) => Protect(() => Mark(current, name, duration));
    private static void Mark(Window? window, string name, double duration)
    {
        if (window != null && ReferenceEquals(current, window) && window.Stages.Count < 20000)
            window.Stages.Add(new(Now, name, duration));
    }
    internal static async Task ObserveEntry(Task original, long start)
    {
        Window? window = current;
        try { await original; if (window != null) window.Outcome = "completed"; }
        catch (OperationCanceledException) { if (window != null) window.Outcome = "cancelled"; throw; }
        catch { if (window != null) window.Outcome = "faulted"; throw; }
        finally
        {
            Protect(() =>
            {
                if (ReferenceEquals(current, window) && window != null)
                {
                    window.Room = RunManager.Instance.DebugOnlyGetState()?.CurrentRoom?.GetType().Name ?? "unknown";
                    Mark("entry-total", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                    window.EndAt = Now + 3000;
                }
            });
        }
    }
    internal static async Task ObserveStage(Task original, long start, string name)
    {
        var window = current;
        try { await original; }
        finally { Protect(() => Mark(window, name, Stopwatch.GetElapsedTime(start).TotalMilliseconds)); }
    }
    internal static void Flush() => Protect(FlushCore);
    private static void FlushCore()
    {
        if (current is not { } window)
            return;
        current = null;
        // Snapshot on the main thread. No per-frame disk IO or serialization.
        var report = new
        {
            schemaVersion = 1, diagnosticBuild = true, collectorVersion = "1",
#if STS2_PUBLIC
            gameBranch = "public",
#else
            gameBranch = "public-beta",
#endif
            gameAssemblyVersion = typeof(RunManager).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            modVersion = typeof(CombatPerfCapture).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            stopwatchFrequency = Stopwatch.Frequency, timestampUnit = "milliseconds-since-collector-start",
            room = window.Room, outcome = window.Outcome,
            note = "Monotonic ProcessFrame gaps include scheduling; GC counts indicate collections, not precise GC pause attribution. No GPU timing.",
            frames = window.Frames.ToArray(), stages = window.Stages.ToArray()
        };
        writes.RemoveAll(task => task.IsCompleted);
        writes.Add(WriteReport(report, OutputDirectory));
    }
    internal static Task WriteReport(object report, string output) => Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(output);
                string file = Path.Combine(output, $"capture-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{System.Environment.ProcessId}-{Guid.NewGuid():N}.json");
                File.WriteAllText(file, JsonSerializer.Serialize(report));
            }
            catch (Exception error)
            {
                ReportError(error);
            }
        });
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterMapCoord))]
internal static class CaptureNativeEntry
{
    private static void Prefix(out long __state) => __state = CombatPerfCapture.BeginEntry();
    private static void Postfix(ref Task __result, long __state)
    {
        CombatPerfCapture.Mark("entry-sync-return", Stopwatch.GetElapsedTime(__state).TotalMilliseconds);
        __result = CombatPerfCapture.ObserveEntry(__result, __state);
    }
}

[HarmonyPatch(typeof(PreloadManager), "LoadRoomAssets")]
internal static class CaptureNativePreload
{
    private static void Prefix(out long __state) => __state = Stopwatch.GetTimestamp();
    private static void Postfix(ref Task __result, long __state)
    {
        CombatPerfCapture.Mark("preload-sync-return", Stopwatch.GetElapsedTime(__state).TotalMilliseconds);
        __result = CombatPerfCapture.ObserveStage(__result, __state, "preload-total-including-native-GC");
    }
}

[HarmonyPatch(typeof(JigsawScreen), nameof(JigsawScreen.Refresh))]
internal static class CapturePuzzleRefresh
{
    private static void Prefix(out long __state) => __state = Stopwatch.GetTimestamp();
    private static void Postfix(long __state) => CombatPerfCapture.Mark("puzzle-refresh", Stopwatch.GetElapsedTime(__state).TotalMilliseconds);
}
#endif
