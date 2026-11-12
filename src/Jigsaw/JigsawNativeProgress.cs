using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace AllMatters.Jigsaw;

// Native history remains chronological, including every shop visit and transaction.
// Its progress consumers subtract only the explicitly recorded repeated entries.
internal static class JigsawNativeProgress
{
    private sealed record HistoryProgress(Dictionary<int, HashSet<int>> Indices);
    private static readonly ConditionalWeakTable<object, HistoryProgress> histories = new();
    internal static void Bind(RunState native, IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> history)
    {
        if (JigsawCheckpoint.Slot.TryGet(native, out var checkpoint))
            Bind(history, checkpoint.RevisitedHistoryIndices);
    }

    internal static void Bind(SerializableRun save)
    {
        if (JigsawSaveSource.TryReadRevisitedHistoryIndices(save, out var indices))
            Bind(save.MapPointHistory, indices);
    }

    private static void Bind(IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> history, Dictionary<int, HashSet<int>>? indices)
    {
        var copy = indices?.ToDictionary(pair => pair.Key, pair => pair.Value == null
            ? throw new InvalidDataException("拼图重访历史缺少索引集合。") : new HashSet<int>(pair.Value)) ?? [];
        foreach (var (act, entries) in copy)
        {
            if (act < 0 || act >= history.Count)
                throw new InvalidDataException("拼图重访历史的幕不存在。");
            foreach (int index in entries)
                if (index < 0 || index >= history[act].Count ||
                    history[act][index].Rooms.FirstOrDefault()?.RoomType != RoomType.Shop)
                    throw new InvalidDataException("拼图重访索引不对应已保存的原生商店历史。");
        }
        histories.AddOrUpdate(history, new(copy));
    }

    internal static int RevisitCount(IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> history) =>
        histories.TryGetValue(history, out var progress) ? progress.Indices.Sum(pair => pair.Value.Count) : 0;

    internal static bool TryFloorScore(IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> history, out int score)
    {
        score = 0;
        if (!histories.TryGetValue(history, out var progress))
            return false;
        for (int act = 0; act < history.Count; act++)
            score += (history[act].Count - (progress.Indices.GetValueOrDefault(act)?.Count ?? 0)) * 10 * (act + 1);
        return true;
    }

    internal static void RecordRevisit(RunState native)
    {
        if (JigsawNativeSession.TryGet(native)?.Map is not { PendingEntry: { } pending } map || !map.Visited.Contains(pending.NodeId))
            return;
        if (!JigsawCheckpoint.Slot.TryGet(native, out var checkpoint))
            throw new InvalidDataException("拼图重访缺少同来源检查点。");
        var indices = checkpoint.RevisitedHistoryIndices?.ToDictionary(pair => pair.Key, pair => new HashSet<int>(pair.Value)) ?? [];
        if (!indices.TryGetValue(native.CurrentActIndex, out var actEntries))
            indices[native.CurrentActIndex] = actEntries = [];
        var history = native.MapPointHistory;
        int index = history[native.CurrentActIndex].Count - 1;
        if (history[native.CurrentActIndex][index].Rooms.FirstOrDefault()?.RoomType != RoomType.Shop)
            throw new InvalidDataException("已访问节点只能重访原生商店。");
        actEntries.Add(index);
        checkpoint.RevisitedHistoryIndices = indices;
        Bind(native, history);
    }
}

[HarmonyPatch(typeof(RunState), "get_MapPointHistory")]
internal static class JigsawBindNativeProgressHistory
{
    private static void Postfix(RunState __instance, IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> __result) =>
        JigsawNativeProgress.Bind(__instance, __result);
}

[HarmonyPatch(typeof(RunState), "get_TotalFloor")]
internal static class JigsawFirstEntryTotalFloor
{
    private static void Postfix(RunState __instance, ref int __result)
    {
        if (JigsawCheckpoint.Slot.TryGet(__instance, out _))
            __result -= JigsawNativeProgress.RevisitCount(__instance.MapPointHistory);
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.AppendToMapPointHistory))]
internal static class JigsawRecordNativeRevisit
{
    private static void Postfix(RunState __instance) => JigsawNativeProgress.RecordRevisit(__instance);
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterMapPointInternal))]
internal static class JigsawFirstEntryActFloor
{
    private static void Prefix(ref int actFloor)
    {
        if (JigsawNativeSession.Current?.Map is not { Phase: JigsawPhase.Exploring } map)
            return;
        // Native assigns ActFloor before its room roll and before history append.
        // A new pending node gets its first floor now; confirmation later owns Steps.
        actFloor = map.FirstProgress + (map.PendingEntry is { } pending && !map.Visited.Contains(pending.NodeId) ? 1 : 0);
    }
}

[HarmonyPatch(typeof(ScoreUtility), nameof(ScoreUtility.GetScoreForFloor))]
internal static class JigsawFirstEntryFloorScore
{
    private static bool Prefix(IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> history, ref int __result) =>
        !JigsawNativeProgress.TryFloorScore(history, out __result);
}

[HarmonyPatch(typeof(ScoreUtility), nameof(ScoreUtility.CalculateScore), [typeof(SerializableRun), typeof(bool)])]
internal static class JigsawBindSerializedScore
{
    private static void Prefix(SerializableRun run) => JigsawNativeProgress.Bind(run);
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
internal static class JigsawBindSavedProgressHistory
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(SerializableRun __result) => JigsawNativeProgress.Bind(__result);
}

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class JigsawValidateRestoredProgress
{
    [HarmonyPriority(Priority.Low)]
    private static void Postfix(RunState __result) => JigsawNativeProgress.Bind(__result, __result.MapPointHistory);
}

[HarmonyPatch]
internal static class JigsawBindLoadedProgressHistory
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(RunSaveManager), nameof(RunSaveManager.LoadRunSave));
        yield return AccessTools.Method(typeof(RunSaveManager), nameof(RunSaveManager.LoadMultiplayerRunSave));
    }
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ReadSaveResult<SerializableRun> __result)
    {
        if (__result.Success && __result.SaveData is { } save)
            JigsawNativeProgress.Bind(save);
    }
}
