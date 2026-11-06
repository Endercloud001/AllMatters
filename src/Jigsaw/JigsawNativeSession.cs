using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Models.Events;
using AllMatters.Jigsaw.Presentation;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AllMatters.Tests")]
namespace AllMatters.Jigsaw;

/// <summary>Owns the single-player native lifecycle. Logical coordinates never become native floors.</summary>
public sealed class JigsawNativeSession
{
    private static readonly ConditionalWeakTable<RunState, JigsawNativeSession> sessions = new();
    public static bool SelectedForNewRun { get; set; }
    public static JigsawNativeSession? Current => RunManager.Instance.DebugOnlyGetState() is { } state &&
        sessions.TryGetValue(state, out var session) ? session : null;
    public RunState Native { get; }
    public JigsawRun? Map { get; private set; }
    public event Action? Changed;
    private bool enteringAncient;
    private int act = -1;
    private JigsawScreen? screen;
    private Task saveTask = Task.CompletedTask;
    private Task nativeSaveTask = Task.CompletedTask;
    private bool savingEdits;
    private bool pendingEditSave;
    private Exception? saveFailure;
    private bool blockingHotkeys;
    private bool paused;
    private SerializableRun? entryCheckpoint;
    private bool entryFailed;
    private Task? enteringTask;
    public SerializableRun? BeforeAncient { get; private set; }
    public bool AncientReady => Map?.CreateSave().AncientCompleted.Length == Native.Players.Count;
    public bool CanResetLayout => Map?.Phase == JigsawPhase.Editing && Native.CurrentRoom is MapRoom &&
        !paused && (TestMode.IsOn || screen is { Visible: true } && screen.IsProcessingInput());

    private JigsawNativeSession(RunState native) { Native = native; }
    public static JigsawNativeSession Enable(RunState native)
    {
        if (native.Players.Count != 1) throw new InvalidOperationException("此入口仅用于单人拼图。");
        return sessions.GetValue(native, state => new(state));
    }
    public async Task StartAct()
    {
        if (act != Native.CurrentActIndex || Map == null)
        {
            act = Native.CurrentActIndex;
            // Own deterministic stream: observing/using native content RNG is unnecessary.
            int seed = unchecked((int)Native.Rng.Seed);
            Map = JigsawRun.Create(unchecked(seed + act * 7919), 15 - Math.Min(2, act),
                act == 2 && Native.AscensionLevel >= 10, Native.Players.Select(p => p.NetId));
        }
        if (Native.CurrentRoom is not MapRoom) await RunManager.Instance.EnterRoom(new MapRoom());
        JigsawCheckpoint.Capture(Native, Map);
        Changed?.Invoke();
    }

    public static void Restore(RunState native, bool marked = false, SerializableRun? checkpoint = null)
    {
        if (!JigsawCheckpoint.Slot.TryGet(native, out var data))
        {
            if (marked)
                throw new InvalidDataException("拼图模式的地图数据缺失或不可读，不能按普通跑局恢复。");
            return;
        }
        if (data.Version != 1 || data.Map == null || data.Act != native.CurrentActIndex)
            throw new InvalidDataException("拼图检查点不完整，不能恢复为空地图。");
        var session = Enable(native);
        session.act = data.Act;
        session.Map = JigsawRun.Restore(data.Map);
        if (session.Map.PendingEntry != null && checkpoint != null) session.entryCheckpoint = JigsawSaveSource.Copy(checkpoint);
    }

    public void ShowMap()
    {
        if (Map == null || act != Native.CurrentActIndex || TestMode.IsOn || NRun.Instance == null) return;
        if (screen == null || !GodotObject.IsInstanceValid(screen))
        {
            screen=CreateScreen();
            var parent = NRun.Instance.GlobalUi;
            parent.AddChild(screen);
            parent.MoveChild(screen, parent.SubmenuStack.GetIndex());
            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            screen.TreeExiting += ReleaseHotkeys;
        }
        screen.SetMap(Map);
        if (!blockingHotkeys)
        {
            NHotkeyManager.Instance?.AddBlockingScreen(screen);
            blockingHotkeys = true;
        }
        screen.Show();
    }
    internal JigsawScreen CreateScreen(IJigsawTutorialProfile? profile=null) => screen=JigsawScreen.Create(Map!,Native.Players[0].NetId,OnEdited,Inspect,Continue,Enter,Pause,profile,Automatic,id=>Native.Players.FirstOrDefault(p=>p.NetId==id)?.Character.IconTexture,AbandonEntry,Native.Act,id=>Native.Players.FirstOrDefault(p=>p.NetId==id)?.Character.MapDrawingColor??Colors.White);
    private void ReleaseHotkeys()
    {
        if (!blockingHotkeys) return;
        NHotkeyManager.Instance?.RemoveBlockingScreen(screen!);
        blockingHotkeys = false;
    }
    public void HideMap()
    {
        ReleaseHotkeys();
        if (GodotObject.IsInstanceValid(screen)) screen?.Hide();
    }

    private void Pause()
    {
        SuspendForNativePause();
        var menu = (NPauseMenu)NRun.Instance.GlobalUi.SubmenuStack.ShowScreen(CapstoneSubmenuType.PauseMenu);
        menu.Initialize(Native);
    }
    internal void SuspendForNativePause()
    {
        paused=true; screen?.SetProcessInput(false); ReleaseHotkeys();
    }
    public void ResumeInput()
    {
        paused = false;
        if (screen == null || !GodotObject.IsInstanceValid(screen)) return;
        screen.SetProcessInput(true);
        screen.ResumeFromPause();
        if (Map != null && screen.Visible && !blockingHotkeys) { NHotkeyManager.Instance?.AddBlockingScreen(screen); blockingHotkeys=true; }
    }
    public void OnEdited()
    {
        if (Map == null) return;
        // Reading help during a live room is not a new complete native point.
        // Keep native's pre-roll checkpoint until a native reward save or completion.
        if (Map.CurrentNode != null || Map.PendingEntry != null) { Changed?.Invoke(); return; }
        JigsawCheckpoint.Capture(Native, Map);
        pendingEditSave = true;
        if (!savingEdits)
        {
            savingEdits = true;
            saveTask = SaveEdits();
        }
        Changed?.Invoke();
    }
    private async Task SaveEdits()
    {
        try
        {
            while (pendingEditSave && Current == this)
            {
                pendingEditSave = false;
                if (!RunManager.Instance.ShouldSave) continue;
                try
                {
                    await SaveManager.Instance.SaveRun(Native.CurrentRoom is { IsPreFinished: true } finished ? finished : null);
                }
                catch (Exception error) { saveFailure = error; }
                // Keep draining already accepted newer edits after a failed write.
                // The final outcome belongs to the native write chain, so a later
                // native retry can also supersede this editor attempt's failure.
            }
        }
        finally { savingEdits = false; }
    }
    public async Task WaitForSave()
    {
        try
        {
            while (true)
            {
                var edits = saveTask; var writes = nativeSaveTask;
                Exception? failure = null;
                try
                {
                    await edits;
                    await writes;
                    if (SaveManager.Instance.CurrentRunSaveTask is { } nativeSave) await nativeSave;
                }
                catch (Exception error) { failure = error; }
                // A retry accepted while exit was waiting supersedes the older outcome.
                if (edits != saveTask || writes != nativeSaveTask) continue;
                if (failure != null) throw failure;
                break;
            }
            if (saveFailure != null) throw saveFailure;
        }
        catch (Exception error) { throw new IOException("拼图保存失败，请重试保存后再退出。", error); }
    }
    internal Task TrackNativeSave(Task write) => nativeSaveTask = ObserveSave(write);
    public async Task ObserveSave(Task write)
    {
        try { await write; saveFailure = null; }
        catch (Exception error) { saveFailure = error; throw; }
    }

    internal static Func<Task>? TestAutomaticReveal;
    private Task? automaticTask;
    public Task Automatic()
    {
        if(Map==null)return Task.CompletedTask;
        var chosen=Map.ChooseEnd(Native.Players[0].NetId,EndChoice.Automatic);
        if(!chosen.Success) { screen?.ShowResult(chosen);return Task.CompletedTask; }
        OnEdited();screen?.Refresh();
        return Map.Phase==JigsawPhase.Generating?ResumeAutomatic():Task.CompletedTask;
    }
    public Task ResumeAutomatic() => automaticTask is { IsCompleted:false } ? automaticTask : automaticTask=GenerateAutomatic();
    private async Task GenerateAutomatic()
    {
        var map=Map;
        if(map?.Phase!=JigsawPhase.Generating || map.AutomaticOperation is not {} operation)return;
        bool Active()=>ReferenceEquals(Map,map)&&Current==this&&ReferenceEquals(map.AutomaticOperation,operation)&&map.Phase==JigsawPhase.Generating;
        if(!Active())return;
        JigsawLayout? result=null;
        string? failureReason=null;
        try
        {
            OnEdited();await WaitForSave();if(!Active())return;
            if(!TestMode.IsOn && NGame.Instance!=null)
                await NGame.Instance.Transition.FadeOut(.8f,Native.Players[0].Character.CharacterSelectTransitionPath);
            if(!Active())return;
            screen?.SetGenerating(true);
            if(!TestMode.IsOn && NGame.Instance!=null)await NGame.Instance.Transition.FadeIn(.4f);
            if(!Active())return;
            result=await JigsawAutomaticWorker.Run(map,operation.ExpansionBudget);
            if(!Active())return;
            if(!TestMode.IsOn && NGame.Instance!=null)await NGame.Instance.Transition.FadeOut(.4f);
        }
        catch(Exception error)
        {
            result=null;
            failureReason="自动拼图未能完成，请稍后重试。";
            Godot.GD.PushError("Automatic jigsaw failed: "+error);
            if(Active() && !TestMode.IsOn && NGame.Instance!=null)
                try { await NGame.Instance.Transition.FadeIn(.4f); }
                catch(Exception fadeError) { Godot.GD.PushError("Automatic jigsaw fade recovery failed: "+fadeError); }
        }
        if(!Active())return;
        var accepted=map.FinishAutomatic(operation,result);
        screen?.SetGenerating(false);
        OnEdited();
        if(!accepted.Success)screen?.ShowAutomaticFailure(failureReason??accepted.Reason);
        bool saved=false;
        try { await WaitForSave();saved=true; }
        finally
        {
            if((!accepted.Success || !saved) && Current==this && ReferenceEquals(Map,map) && !TestMode.IsOn && NGame.Instance!=null)
                await NGame.Instance.Transition.FadeIn(.4f);
        }
        if(Current!=this || !ReferenceEquals(Map,map) || !ReferenceEquals(map.AutomaticOperation,operation))return;
        if(!accepted.Success)return;
        BeforeAncient=RunManager.Instance.ToSave(null);
        try { await EnterAncient(); }
        finally
        {
            if(Current==this && ReferenceEquals(Map,map))
            {
                if(TestMode.IsOn) {if(TestAutomaticReveal!=null)await TestAutomaticReveal();}
                else if(NGame.Instance!=null)await NGame.Instance.Transition.FadeIn(.4f);
            }
        }
    }

    public async Task Inspect()
    {
        if (Map == null) return;
        var result = Map.Inspect();
        screen?.ShowResult(result, manual: true);
        OnEdited();
        await WaitForSave();
        if (!result.Success) return;
        BeforeAncient = RunManager.Instance.ToSave(null);
        await EnterAncient();
    }

    public async Task EnterAncient()
    {
        if (Map?.Phase != JigsawPhase.Ancient) return;
        if (AncientReady) { ShowMap(); return; }
        BeforeAncient ??= RunManager.Instance.ToSave(null);
        HideMap();
        enteringAncient = true;
        try
        {
            await RunManager.Instance.EnterMapCoord(Native.Map.StartingMapPoint.coord);
        }
        finally { enteringAncient = false; }
    }

    public void AncientFinished()
    {
        if (Map?.Phase != JigsawPhase.Ancient) return;
        Map.CompleteAncient(Native.Players[0].NetId);
        BeforeAncient = null;
        JigsawCheckpoint.Capture(Native, Map);
    }

    public async Task Continue()
    {
        if(Map is { Phase:JigsawPhase.Ancient } waiting && !AncientReady && Native.CurrentRoom is MapRoom)
        {
            OnEdited();await WaitForSave();
            if(Current!=this || !ReferenceEquals(Map,waiting))return;
            BeforeAncient=RunManager.Instance.ToSave(null);await EnterAncient();return;
        }
        if (Map == null || !Map.Continue(Native.Players[0].NetId).Success) return;
        OnEdited(); await WaitForSave(); ShowMap();
    }

    public Task Enter(int node)
    {
        if (Current != this) return Task.CompletedTask;
        if (enteringTask is { IsCompleted: false }) return enteringTask;
        return enteringTask = EnterCore(node);
    }

    public Task AbandonEntry()
    {
        if (Current != this || enteringTask is { IsCompleted: false }) return Task.CompletedTask;
        return enteringTask = AbandonEntryCore();
    }

    private async Task AbandonEntryCore()
    {
        if (!entryFailed || Map?.PendingEntry is not { } attempt || entryCheckpoint == null) return;
        var checkpoint = JigsawSaveSource.Copy(entryCheckpoint);
        await WaitForSave();
        if (Current != this) return;
        // The before-roll point contains a provisional coordinate, but not its
        // history or rewards. Earlier failures have no provisional coordinate.
        int row = Math.Max(0, (checkpoint.MapPointHistory.ElementAtOrDefault(checkpoint.CurrentActIndex)?.Count ?? 1) - 1);
        if (checkpoint.VisitedMapCoords.Count > 0 && checkpoint.VisitedMapCoords[^1] == new MapCoord(0, row))
            checkpoint.VisitedMapCoords.RemoveAt(checkpoint.VisitedMapCoords.Count - 1);
        var restored = RunState.FromSerializable(checkpoint);
        var selection = sessions.GetValue(restored, state => new(state));
        if (selection.Map == null || !selection.Map.AbandonEntry(attempt.Id).Success)
            throw new InvalidDataException("不能从完整进入点恢复目的地选择。");
        selection.entryCheckpoint = null;
        JigsawCheckpoint.Capture(restored, selection.Map);
        JigsawSaveSource.CaptureDocument(checkpoint, restored);
        HideMap();
        await LoadCheckpoint(checkpoint, restored);
        if (Current != selection) return;
        selection.OnEdited();
        try { await selection.WaitForSave(); }
        catch
        {
            if (Current == selection) selection.screen?.ShowNotice("本次变更尚未保存，请稍后重试。");
            throw;
        }
    }

    private async Task EnterCore(int node)
    {
        if (Map == null) return;
        if (Map.PendingEntry == null) { entryCheckpoint = null; entryFailed = false; }
        if (entryFailed && Map.PendingEntry?.NodeId == node && entryCheckpoint != null)
        {
            await RetryEntry(); return;
        }
        var attempt = Map.PendingEntry ?? Map.BeginEnter(node);
        if (attempt == null || attempt.NodeId != node) return;
        // Also retain a complete point before native AddVisited/exit/sync can fail.
        entryCheckpoint ??= RunManager.Instance.ToSave(null);
        var destination = Map.Nodes.Single(n => n.Id == node);
        var projection = JigsawEntryProjection.Append(Native.Map,
            NextProjectionRow,
            destination.Room);
        Native.Map = projection.Map;
        JigsawCheckpoint.Capture(Native, Map);
        HideMap();
        bool ready = false;
        AbstractRoom? previous = Native.CurrentRoom;
        void RoomReady()
        {
            if (Current == this && Native.CurrentRoom is { } room && room != previous && room is not MapRoom &&
                Native.CurrentMapPoint?.coord == projection.Point.coord)
            {
                // The real native post-EnterInternal event is the confirmation seam.
                ready = Map.ConfirmEnter(attempt.Id).Success;
            }
        }
        RunManager.Instance.RoomEntered += RoomReady;
        try
        {
            await RunManager.Instance.EnterMapCoord(projection.Point.coord);
            if (Current != this) return;
            if (!ready) throw new InvalidOperationException("原生房间尚未确认就绪；本次未计步。");
            entryCheckpoint = null;
            JigsawCheckpoint.Capture(Native, Map);
        }
        catch
        {
            if (Current == this) { entryFailed = !ready; ShowMap(); }
            throw;
        }
        finally { RunManager.Instance.RoomEntered -= RoomReady; }
    }

    internal void CapturePendingEntry(SerializableRun checkpoint)
    {
        if (Map?.PendingEntry != null && Native.CurrentRoomCount == 0) entryCheckpoint = checkpoint;
    }
    private int NextProjectionRow => Math.Max(0, (Native.MapPointHistory.ElementAtOrDefault(Native.CurrentActIndex)?.Count ?? 1) - 1);
    // Before native entry the latest coordinate is still the completed source;
    // the before-roll point has the chronological target coordinate but no new history yet.
    internal bool HasNativeEntryCoordinate => Native.CurrentMapCoord == new MapCoord(0, NextProjectionRow);
    internal SerializableRun? FailedEntryCheckpoint => entryFailed && Map?.PendingEntry != null ? entryCheckpoint : null;

    private async Task RetryEntry()
    {
        var checkpoint = JigsawSaveSource.Copy(entryCheckpoint!);
        await WaitForSave();
        if (Current != this) return;
        HideMap();
        // Reuse native recovery of the whole before-roll point. Do not splice RNG,
        // encounter bags, players or history from a partly loaded room into it.
        var restored = RunState.FromSerializable(checkpoint);
        await LoadCheckpoint(checkpoint, restored);
    }

    private static async Task LoadCheckpoint(SerializableRun checkpoint, RunState restored)
    {
        RunManager.Instance.CleanUp(false);
        try
        {
            await RunManager.Instance.SetUpSavedSingleplayer(restored, checkpoint);
            if (!TestMode.IsOn) await NGame.Instance.LoadRun(restored, null);
            else
            {
                // Existing isolated native host omits NGame's asset/scene ownership.
                await RunManager.Instance.GenerateMap();
                await RunManager.Instance.LoadIntoLatestMapCoord(null);
            }
        }
        catch { if (Current?.Native == restored) Current.RestoredEntryFailed(); throw; }
    }

    internal void RestoredEntryFailed()
    {
        if (Current == this && Map?.PendingEntry != null) { entryFailed = true; ShowMap(); }
    }

    public void CompleteCurrentRoom(bool canTravel)
    {
        if (!canTravel || Map?.CurrentNode == null || Native.CurrentRoomCount > 1) return;
        var save = Map.CreateSave();
        Map.CompleteRoom(save.NextEntry);
        OnEdited();
    }

    public bool ShouldInterceptAncient(MapCoord coord) => !enteringAncient &&
        Native.Map.GetPoint(coord)?.PointType == MapPointType.Ancient &&
          (Map == null || act != Native.CurrentActIndex || Map.Phase is JigsawPhase.Editing or JigsawPhase.Inspecting or JigsawPhase.Generating);

    public void ConfirmRestoredEntry()
    {
        if (Current == this && Map?.PendingEntry is { } pending && Native.CurrentRoom is not (null or MapRoom))
        {
            Map.ConfirmEnter(pending.Id);
            entryCheckpoint = null; entryFailed = false;
            JigsawCheckpoint.Capture(Native, Map);
        }
    }
}

internal static class JigsawAutomaticWorker
{
    internal static Func<JigsawRun,int,Task<JigsawLayout?>>? TestOverride;
    internal static Task<JigsawLayout?> Run(JigsawRun map,int budget) =>
        TestMode.IsOn && TestOverride!=null ? TestOverride(map,budget) : Task.Run(()=>JigsawAutomatic.Solve(map,budget));
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
internal static class JigsawEnableNewRun
{
    private static void Postfix(RunState state, DateTimeOffset? dailyTime)
    {
        if (JigsawNativeSession.SelectedForNewRun && dailyTime == null && state.GameMode == GameMode.Standard)
            JigsawNativeSession.Enable(state);
        JigsawNativeSession.SelectedForNewRun = false;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterMapCoord))]
internal static class JigsawBeforeAncientEntry
{
    private static bool Prefix(MapCoord coord, ref Task __result)
    {
        var session = JigsawNativeSession.Current;
        if (session == null || !session.ShouldInterceptAncient(coord)) return true;
        __result = Start(session);
        return false;
    }
    private static async Task Start(JigsawNativeSession session)
    { await session.StartAct(); session.OnEdited(); session.ShowMap(); }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterAct))]
internal static class JigsawLaterActEntry
{
    private static void Postfix(ref Task __result)
    {
        if (JigsawNativeSession.Current is { } session) __result = Finish(__result, session);
    }
    private static async Task Finish(Task original, JigsawNativeSession session)
    { await original; await session.StartAct(); session.OnEdited(); session.ShowMap(); }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
internal static class JigsawOpenFormalMap
{
    private static bool Prefix(NMapScreen __instance, bool isOpenedFromTopBar, ref NMapScreen __result)
    {
        if (JigsawNativeSession.Current is not { Map: not null } session) return true;
        if (session.Map.Phase == JigsawPhase.Ancient && !session.AncientReady) return true;
        if (!isOpenedFromTopBar && session.Map.CurrentNode != null)
            session.CompleteCurrentRoom(__instance.IsTravelEnabled);
        session.ShowMap(); __result = __instance; return false;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen))]
internal static class JigsawTerminalRoomCompletion
{
    private static void Postfix(ref Task __result)
    {
        if (JigsawNativeSession.Current is { } session) __result = Complete(__result, session);
    }
    private static async Task Complete(Task original, JigsawNativeSession session)
    {
        await original;
        if (session.Native.CurrentRoom is { } room && (room.IsPreFinished || room is TreasureRoom))
        {
            session.CompleteCurrentRoom(true);
            session.ShowMap();
        }
    }
}

[HarmonyPatch(typeof(EventRoom), "OnEventStateChanged")]
internal static class JigsawAncientCompletion
{
    private static void Prefix(EventModel eventModel)
    {
        if (eventModel is AncientEventModel && eventModel.IsFinished)
            JigsawNativeSession.Current?.AncientFinished();
    }
}

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class JigsawRestoreSession
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(SerializableRun save, out List<SerializableModifier>? __state)
    {
        var identity = ModelDb.Modifier<AllMattersJigsawMode>().Id;
        __state = save.Modifiers.Any(modifier => modifier.Id == identity) ? save.Modifiers : null;
        if (__state != null) save.Modifiers = __state.Where(modifier => modifier.Id != identity).ToList();
    }
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(RunState __result, List<SerializableModifier>? __state, SerializableRun save) =>
        JigsawNativeSession.Restore(__result, __state != null, save);
    private static void Finalizer(SerializableRun save, List<SerializableModifier>? __state)
    {
        if (__state != null) save.Modifiers = __state;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
internal static class JigsawCaptureNativePoint
{
    private static void Postfix(SerializableRun __result)
    {
        if (JigsawNativeSession.Current == null) return;
        var identity = ModelDb.Modifier<AllMattersJigsawMode>();
        if (!__result.Modifiers.Any(modifier => modifier.Id == identity.Id))
            __result.Modifiers.Add(identity.ToMutable().ToSerializable());
        JigsawNativeSession.Current.CapturePendingEntry(__result);
    }
    private static bool Prefix(ref SerializableRun __result)
    {
        if (JigsawNativeSession.Current is not { Map: not null } session) return true;
        JigsawCheckpoint.Capture(session.Native, session.Map);
        if (session.FailedEntryCheckpoint is { } pending) { __result = pending; return false; }
        if (session.BeforeAncient == null || session.AncientReady) return true;
        __result = session.BeforeAncient;
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.LoadIntoLatestMapCoord))]
internal static class JigsawResumePhase
{
    private static bool Prefix(ref Task __result, out Action? __state)
    {
        __state = null;
        if (JigsawNativeSession.Current is not { Map: not null } session) return true;
        var map = session.Map;
        if (map.PendingEntry != null && !session.HasNativeEntryCoordinate)
        {
            session.Native.ActFloor = map.FirstProgress;
            __result = RunManager.Instance.EnterRoom(new MapRoom());
            return false;
        }
        if (map.PendingEntry != null)
        {
            var coordinate = session.Native.CurrentMapCoord;
            __state = () => { if (session.Native.CurrentMapCoord == coordinate) session.ConfirmRestoredEntry(); };
            RunManager.Instance.RoomEntered += __state;
        }
        // A completed root room is represented by the logical map. Re-entering
        // native's latest coordinate here would run treasure/shop rewards again.
        if (map.Phase == JigsawPhase.Exploring && map.CurrentNode == null && map.PendingEntry == null ||
            map.Phase is JigsawPhase.Editing or JigsawPhase.Inspecting or JigsawPhase.Generating ||
            map.Phase == JigsawPhase.Ancient && session.AncientReady)
        {
            session.Native.ActFloor = map.FirstProgress;
            __result = RunManager.Instance.EnterRoom(new MapRoom());
            return false;
        }
        return true;
    }
    private static void Postfix(ref Task __result, Action? __state)
    {
        if (JigsawNativeSession.Current is { Map: not null } session) __result = Resume(__result, session, __state);
        else if (__state != null) RunManager.Instance.RoomEntered -= __state;
    }
    private static async Task Resume(Task original, JigsawNativeSession session, Action? observe)
    {
        try { await original; }
        catch { session.RestoredEntryFailed(); throw; }
        finally { if (observe != null) RunManager.Instance.RoomEntered -= observe; }
        if (JigsawNativeSession.Current != session) return;
        if (session.Map?.PendingEntry != null)
        {
            if (!session.HasNativeEntryCoordinate)
            {
                await session.Enter(session.Map.PendingEntry.NodeId);
                return;
            }
            session.RestoredEntryFailed();
            throw new InvalidOperationException("原生房间尚未确认就绪；本次未计步。");
        }
        if (session.Map!.Phase == JigsawPhase.Generating) { session.ShowMap(); await session.ResumeAutomatic(); }
        else if (session.Map.Phase == JigsawPhase.Inspecting) await session.Inspect();
        else if (session.Map.Phase == JigsawPhase.Ancient && !session.AncientReady) await session.EnterAncient();
        else if (session.Map.Phase is JigsawPhase.Editing or JigsawPhase.Ancient ||
                 session.Map.Phase == JigsawPhase.Exploring && session.Map.CurrentNode == null)
            session.ShowMap();
    }
}
