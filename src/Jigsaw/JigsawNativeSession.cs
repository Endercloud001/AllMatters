using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
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
    internal static JigsawNativeSession? SaveSession => RunManager.Instance.DebugOnlyGetState() is { } state &&
        sessions.TryGetValue(state, out var session) ? session : null;
    public static JigsawNativeSession? Current
    {
        get
        {
            var session = SaveSession;
            if (session == null)
                return null;
            if (session.netService is not NetSingleplayerGameService && !session.netService.IsConnected)
                session.Close();
            return session.closed ? null : session;
        }
    }
    private bool closed;
    private readonly MegaCrit.Sts2.Core.Multiplayer.Game.INetGameService netService;
    private readonly CancellationTokenSource closedCancellation = new();
    private void OnDisconnected(NetErrorInfo info) => Close();
    internal void Close()
    {
        if (closed)
            return;
        closed = true;
        netService.Disconnected -= OnDisconnected;
        closedCancellation.Cancel();
        if (boundLobby != null)
        {
            boundLobby.RemotePlayerDisconnected -= OnPlayerDisconnected;
#if !STS2_PUBLIC
            boundLobby.PlayerRejoined -= OnPlayerRejoined;
#endif
            boundLobby = null;
        }
        // TODO(cleanup): TaskCompletionSource lifecycle - Consider adding explicit cleanup
        // or bounded allocation for pending completions. Currently orphaned if session
        // closes mid-operation, though practically benign due to `closed` flag guard.
        pendingAutomaticVote?.TrySetResult(JigsawResult.Reject("会话已结束。"));
        automaticApplied?.TrySetResult(JigsawResult.Reject("会话已结束。"));
        if (Map != null)
            JigsawEditorAction.Stop(Map);
        if (GodotObject.IsInstanceValid(screen))
            screen?.SetProcessInput(false);
        ReleaseHotkeys();
        // TODO(cleanup): ConditionalWeakTable entry cleanup - Sessions WeakTable entry
        // not explicitly removed. Benign: `closed` flag prevents reuse, RunState GC
        // triggers auto-cleanup, and Current property guards with reference check.
    }
    public RunState Native { get; }
    private ulong LocalPlayerId => netService.NetId;
    public JigsawRun? Map { get; private set; }
    public event Action? Changed;
    private bool enteringAncient;
    private int act = -1;
    private JigsawScreen? screen;
    private Task saveTask = Task.CompletedTask;
    private Task nativeSaveTask = Task.CompletedTask;
    private TaskCompletionSource<JigsawResult>? pendingAutomaticVote;
    private bool savingEdits;
    private bool pendingEditSave;
    private Exception? saveFailure;
    private bool blockingHotkeys;
    private bool paused;
    private SerializableRun? entryCheckpoint;
    private bool entryFailed;
    private Task? enteringTask;
    private RunLobby? boundLobby;
    public SerializableRun? BeforeAncient { get; private set; }
    public bool AncientReady => Map is { } map && ActivePlayerIds.All(map.CreateSave().AncientCompleted.Contains);
    public bool CanResetLayout => Map?.Phase == JigsawPhase.Editing && Native.CurrentRoom is MapRoom &&
        !paused && (TestMode.IsOn || screen is { Visible: true } && screen.IsProcessingInput());

    private JigsawNativeSession(RunState native) { Native = native; netService = RunManager.Instance.NetService; netService.Disconnected += OnDisconnected; }
    public static JigsawNativeSession Enable(RunState native)
    {
        if (native.Players.Count != 1)
        {
            var net = RunManager.Instance.NetService;
            var roster = native.Players.Select(p => p.NetId).ToHashSet();
            var active = ActivePlayers(native).ToHashSet();
            bool connected = net switch
            {
                NetHostGameService host => host.IsConnected && active.SetEquals(host.ConnectedPeers.Select(p => p.peerId).Append(host.NetId)),
                NetClientGameService client => client.IsConnected && active.Contains(client.NetId) && active.Contains(client.HostNetId),
                _ => false
            };
            if (!connected || active.Count == 0 || !active.IsSubsetOf(roster) || !ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), native))
                throw new InvalidOperationException("多人拼图需要已连接且角色名单匹配的原生会话。");
        }
        var session = sessions.GetValue(native, state => new(state));
        session.AttachLobby();
        return session;
    }
    private static IEnumerable<ulong> ActivePlayers(RunState native)
    {
        var lobby = RunManager.Instance.RunLobby;
        if (lobby == null)
            return native.Players.Select(player => player.NetId);
#if STS2_PUBLIC
        return lobby.ConnectedPlayerIds;
#else
        return lobby.PlayerIds;
#endif
    }
    private ulong[] ActivePlayerIds => (Map?.ActivePlayers ?? ActivePlayers(Native)).ToArray();
    private void AttachLobby()
    {
        var lobby = RunManager.Instance.RunLobby;
        if (ReferenceEquals(boundLobby, lobby))
            return;
        if (boundLobby != null)
        {
            boundLobby.RemotePlayerDisconnected -= OnPlayerDisconnected;
#if !STS2_PUBLIC
            boundLobby.PlayerRejoined -= OnPlayerRejoined;
#endif
        }
        boundLobby = lobby;
        if (boundLobby == null)
            return;
        boundLobby.RemotePlayerDisconnected += OnPlayerDisconnected;
#if !STS2_PUBLIC
        boundLobby.PlayerRejoined += OnPlayerRejoined;
#endif
    }
    private void OnPlayerDisconnected(ulong player)
    {
        if (Current != this || Map?.ActivePlayers.Contains(player) != true || !JigsawEditorAction.IsHost(LocalPlayerId))
            return;
        JigsawEditorAction.Submit(this, JigsawEditorOperation.Disconnect, completedOwner: player);
    }
    internal JigsawResult ApplyDisconnect(ulong player)
    {
        if (Map == null || !Map.OriginalPlayers.Contains(player))
            return JigsawResult.Reject(JigsawFailure.UnknownPlayer);
        Map.Disconnect(player);
        OnAncientCompleted();
        return JigsawResult.Ok;
    }
#if !STS2_PUBLIC
    private void OnPlayerRejoined(RunLobbyPlayer player)
    {
        if (Map?.Reconnect(player.id).Success == true)
        { OnEdited(); RefreshEditor(); }
    }
#endif
    public async Task StartAct()
    {
        AttachLobby();
        if (act != Native.CurrentActIndex || Map == null)
        {
            act = Native.CurrentActIndex;
            // Own deterministic stream: observing/using native content RNG is unnecessary.
            int seed = unchecked((int)Native.Rng.Seed);
            Map = JigsawRun.Create(unchecked(seed + act * 7919), 15 - Math.Min(2, act),
                act == 2 && Native.AscensionLevel >= 10, ActivePlayerIds, Native.Players.Select(p => p.NetId));
        }
        SyncActivePlayers();
        if (Native.CurrentRoom is not MapRoom)
            await RunManager.Instance.EnterRoom(new MapRoom());
        JigsawCheckpoint.Capture(Native, Map);
        Changed?.Invoke();
    }
    private void SyncActivePlayers()
    {
        if (Map == null)
            return;
        var active = ActivePlayers(Native).ToHashSet();
        foreach (ulong player in Map.ActivePlayers.Where(player => !active.Contains(player)).ToArray())
            Map.Disconnect(player);
        foreach (ulong player in active.Where(player => !Map.ActivePlayers.Contains(player)))
            Map.Reconnect(player);
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
        var originals = native.Players.Select(p => p.NetId).ToArray();
        var mapData = data.Map.OriginalPlayers == null ? data.Map with { OriginalPlayers = originals } : data.Map;
        if (!originals.ToHashSet().SetEquals(mapData.OriginalPlayers!))
            throw new InvalidDataException("拼图原队员与原生存档角色名单不一致，不能替换角色恢复。");
        var restoredMap = JigsawRun.Restore(mapData, JigsawLobbyCheckpoint.IsValidating ? null : originals);
        // The native lobby validates/rebuilds saves before a live session exists.
        // Validate the document here without activating multiplayer gameplay.
        if (JigsawLobbyCheckpoint.IsValidating)
            return;
        var session = Enable(native);
        session.act = data.Act;
        session.Map = restoredMap;
        session.BeginExploration();
        if (session.Map.PendingEntry != null && checkpoint != null)
            session.entryCheckpoint = JigsawSaveSource.Copy(checkpoint);
    }

    public void ShowMap()
    {
        if (Map == null || act != Native.CurrentActIndex)
            return;
        if (screen == null || !GodotObject.IsInstanceValid(screen))
        {
            if (TestMode.IsOn || NRun.Instance == null)
                return;
            screen = CreateScreen();
            var parent = NRun.Instance.GlobalUi;
            parent.AddChild(screen);
            parent.MoveChild(screen, parent.SubmenuStack.GetIndex());
            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            screen.TreeExiting += ReleaseHotkeys;
        }
        screen.SetMap(Map, Native.Act);
        screen.SetRoomPreview(Map.CurrentNode != null || Map.Phase == JigsawPhase.Ancient && !AncientReady && Native.CurrentRoom is EventRoom ? HideMap : null);
        if (!blockingHotkeys)
        {
            NHotkeyManager.Instance?.AddBlockingScreen(screen);
            blockingHotkeys = true;
        }
        screen.Show();
    }
    internal JigsawScreen CreateScreen(IJigsawTutorialProfile? profile = null)
    {
        screen = JigsawScreen.Create(Map!, RunManager.Instance.NetService.NetId, () => { }, Inspect, Continue, Enter, Pause, profile, Automatic, id => Native.Players.FirstOrDefault(p => p.NetId == id)?.Character.IconTexture, AbandonEntry, Native.Act, id => Native.Players.FirstOrDefault(p => p.NetId == id)?.Character.MapDrawingColor ?? Colors.White);
        screen.ReturnRequested = piece => JigsawEditorAction.Submit(this, JigsawEditorOperation.ReturnToPool, piece);
        screen.TakeRequested = piece => JigsawEditorAction.Submit(this, JigsawEditorOperation.Take, piece);
        screen.CancelRequested = () => JigsawEditorAction.Submit(this, JigsawEditorOperation.Cancel);
        screen.RotateRequested = () => JigsawEditorAction.Submit(this, JigsawEditorOperation.Rotate);
        screen.DropRequested = point => JigsawEditorAction.Submit(this, JigsawEditorOperation.Drop, point: point);
        screen.EndpointRequested = node => JigsawEditorAction.Submit(this, JigsawEditorOperation.SelectEndpoint, nodeId: node);
        screen.ChoiceRequested = choice => JigsawEditorAction.Submit(this, JigsawEditorOperation.ChooseEnd, choice: choice);
        return screen;
    }
    internal void RefreshEditor()
    {
        if (GodotObject.IsInstanceValid(screen) && screen!.IsInsideTree())
            screen.Refresh();
    }
    internal void EditorResult(ulong issuer, JigsawEditorOperation operation, JigsawResult result)
    {
        if (issuer == LocalPlayerId && operation == JigsawEditorOperation.ChooseEnd)
            pendingAutomaticVote?.TrySetResult(result);
        if (issuer == RunManager.Instance.NetService.NetId && GodotObject.IsInstanceValid(screen) && screen!.IsInsideTree())
            screen.EditorResult(operation, result);
    }
    internal void OnOrderedConsensus()
    {
        if (Map?.Phase == JigsawPhase.Inspecting)
            _ = Defer(Inspect);
        else if (Map?.Phase == JigsawPhase.Generating)
            _ = ResumeAutomatic();
    }
    internal bool CloseRoomPreview()
    {
        if (!GodotObject.IsInstanceValid(screen) || screen is not { Visible: true, CanReturnToRoom: true })
            return false;
        HideMap();
        return true;
    }
    private void ReleaseHotkeys()
    {
        if (!blockingHotkeys)
            return;
        NHotkeyManager.Instance?.RemoveBlockingScreen(screen!);
        blockingHotkeys = false;
    }
    public void HideMap()
    {
        ReleaseHotkeys();
        if (GodotObject.IsInstanceValid(screen))
            screen?.Hide();
    }

    private void Pause()
    {
        SuspendForNativePause();
        var menu = (NPauseMenu?)NRun.Instance!.GlobalUi.SubmenuStack.ShowScreen(CapstoneSubmenuType.PauseMenu);
        menu!.Initialize(Native);
    }
    internal void SuspendForNativePause()
    {
        paused = true;
        screen?.SetProcessInput(false);
        ReleaseHotkeys();
    }
    public void ResumeInput()
    {
        paused = false;
        if (screen == null || !GodotObject.IsInstanceValid(screen))
            return;
        screen.SetProcessInput(true);
        screen.ResumeFromPause();
        if (Map != null && screen.Visible && !blockingHotkeys)
        { NHotkeyManager.Instance?.AddBlockingScreen(screen); blockingHotkeys = true; }
    }
    public void OnEdited()
    {
        if (Map == null)
            return;
        // Reading help during a live room is not a new complete native point.
        // Keep native's pre-roll checkpoint until a native reward save or completion.
        if (Map.CurrentNode != null || Map.PendingEntry != null)
        { Changed?.Invoke(); return; }
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
                if (!RunManager.Instance.ShouldSave)
                    continue;
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
                var edits = saveTask;
                var writes = nativeSaveTask;
                Exception? failure = null;
                try
                {
                    await edits;
                    await writes;
                    if (SaveManager.Instance.CurrentRunSaveTask is { } nativeSave)
                        await nativeSave;
                }
                catch (Exception error) { failure = error; }
                // A retry accepted while exit was waiting supersedes the older outcome.
                if (edits != saveTask || writes != nativeSaveTask)
                    continue;
                if (failure != null)
                    throw failure;
                break;
            }
            if (saveFailure != null)
                throw saveFailure;
        }
        catch (Exception error) { throw new IOException("拼图保存失败，请重试保存后再退出。", error); }
    }
    internal Task TrackNativeSave(Task write) => nativeSaveTask = ObserveSave(write);
    public async Task ObserveSave(Task write)
    {
        try
        { await write; saveFailure = null; }
        catch (Exception error) { saveFailure = error; throw; }
    }

    internal static Func<Task>? TestAutomaticReveal;
    private Task? automaticTask;
    private TaskCompletionSource<JigsawResult>? automaticApplied;
    private Task Defer(Func<Task> work)
    {
        var map = Map;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Callable.From(async () => { try { if (Current == this && ReferenceEquals(Map, map)) await work(); completion.TrySetResult(); } catch (Exception error) { completion.TrySetException(error); } }).CallDeferred();
        return completion.Task;
    }
    public Task Automatic()
    {
        if (Map == null)
            return Task.CompletedTask;
        if (pendingAutomaticVote != null)
            return pendingAutomaticVote.Task;
        var completion = pendingAutomaticVote = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var submitted = JigsawEditorAction.Submit(this, JigsawEditorOperation.ChooseEnd, choice: EndChoice.Automatic);
        if (!submitted.Success)
        {
            pendingAutomaticVote = null;
            screen?.ShowResult(submitted);
            return Task.CompletedTask;
        }
        return CompleteAutomaticVote(completion);
    }
    private async Task CompleteAutomaticVote(TaskCompletionSource<JigsawResult> completion)
    {
        var result = await completion.Task;
        if (ReferenceEquals(pendingAutomaticVote, completion))
            pendingAutomaticVote = null;
        if (result.Success && (Map?.Phase is JigsawPhase.Generating or JigsawPhase.Ancient) && automaticTask is { } generation)
            await generation;
    }
    private JigsawAutomaticOperation? runningAutomatic;
    public Task ResumeAutomatic()
    {
        if (Map?.AutomaticOperation is not { } operation)
            return Task.CompletedTask;
        if (automaticTask is { IsCompleted: false } && ReferenceEquals(runningAutomatic, operation))
            return automaticTask;
        runningAutomatic = operation;
        automaticApplied = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return automaticTask = Defer(GenerateAutomatic);
    }
    private async Task GenerateAutomatic()
    {
        var map = Map;
        if (map == null || (map.Phase != JigsawPhase.Generating && automaticApplied?.Task.IsCompleted != true) || map.AutomaticOperation is not { } operation)
            return;
        var applied = automaticApplied ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool Active() => ReferenceEquals(Map, map) && Current == this && ReferenceEquals(map.AutomaticOperation, operation) && (map.Phase == JigsawPhase.Generating || applied.Task.IsCompleted);
        if (!Active())
            return;
        JigsawLayout? result = null;
        string? failureReason = null;
        try
        {
            OnEdited();
            await WaitForSave();
            if (!Active())
                return;
            if (!TestMode.IsOn && NGame.Instance != null)
                await NGame.Instance.Transition.FadeOut(.8f, Native.Players.Single(player => player.NetId == LocalPlayerId).Character.CharacterSelectTransitionPath);
            if (!Active())
                return;
            if (GodotObject.IsInstanceValid(screen))
                screen?.SetGenerating(true);
            if (!TestMode.IsOn && NGame.Instance != null)
                await NGame.Instance.Transition.FadeIn(.4f);
            if (!Active())
                return;
            automaticApplied ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            if (JigsawEditorAction.IsHost(LocalPlayerId))
                result = await JigsawAutomaticWorker.Run(map, operation.ExpansionBudget).WaitAsync(closedCancellation.Token);
            if (!Active())
                return;
            if (!TestMode.IsOn && NGame.Instance != null)
                await NGame.Instance.Transition.FadeOut(.4f);
        }
        catch (OperationCanceledException) when (closed) { return; }
        catch (Exception error)
        {
            result = null;
            failureReason = "自动拼图未能完成，请稍后重试。";
            Godot.GD.PushError("Automatic jigsaw failed: " + error);
            if (Active() && !TestMode.IsOn && NGame.Instance != null)
                try
                { await NGame.Instance.Transition.FadeIn(.4f); }
                catch (Exception fadeError) { Godot.GD.PushError("Automatic jigsaw fade recovery failed: " + fadeError); }
        }
        if (!Active())
            return;
        if (JigsawEditorAction.IsHost(LocalPlayerId))
        {
            var submitted = JigsawEditorAction.Submit(this, JigsawEditorOperation.FinalizeAutomatic, layout: result, automaticFailure: failureReason);
            if (!submitted.Success)
                throw new InvalidOperationException(submitted.Reason);
        }
        var accepted = await applied.Task;
        if (GodotObject.IsInstanceValid(screen))
            screen?.SetGenerating(false);
        if (ReferenceEquals(automaticApplied, applied))
            automaticApplied = null;
        bool saved = false;
        try
        { await WaitForSave(); saved = true; }
        finally
        {
            if ((!accepted.Success || !saved) && Current == this && ReferenceEquals(Map, map) && !TestMode.IsOn && NGame.Instance != null)
                await NGame.Instance.Transition.FadeIn(.4f);
        }
        if (Current != this || !ReferenceEquals(Map, map) || !ReferenceEquals(map.AutomaticOperation, operation))
            return;
        if (!accepted.Success)
            return;
        BeforeAncient = JigsawSaveSource.Copy(RunManager.Instance.ToSave(null));
        try
        { await EnterAncient(); }
        finally
        {
            if (Current == this && ReferenceEquals(Map, map))
            {
                if (TestMode.IsOn)
                { if (TestAutomaticReveal != null) await TestAutomaticReveal(); }
                else if (NGame.Instance != null)
                    await NGame.Instance.Transition.FadeIn(.4f);
            }
        }
    }

    internal JigsawResult ApplyAutomatic(JigsawLayout? layout, string? failure)
    {
        if (Map is not { } map || map.Phase != JigsawPhase.Generating || map.AutomaticOperation is not { } operation)
            return JigsawResult.Reject("旧自动任务不能提交。");
        automaticApplied ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
        var accepted = map.FinishAutomatic(operation, layout);
        if (GodotObject.IsInstanceValid(screen))
        {
            screen?.SetGenerating(false);
            if (!accepted.Success)
                screen?.ShowAutomaticFailure(failure ?? accepted.Reason);
        }
        automaticApplied.TrySetResult(accepted);
        return JigsawResult.Ok;
    }

    public async Task Inspect()
    {
        if (Map == null)
            return;
        var result = Map.Inspect();
        screen?.ShowResult(result, manual: true);
        OnEdited();
        await WaitForSave();
        if (!result.Success)
            return;
        BeforeAncient = JigsawSaveSource.Copy(RunManager.Instance.ToSave(null));
        await EnterAncient();
    }

    public async Task EnterAncient()
    {
        if (Map?.Phase != JigsawPhase.Ancient)
            return;
        if (AncientReady)
        { ShowMap(); return; }
        BeforeAncient ??= JigsawSaveSource.Copy(RunManager.Instance.ToSave(null));
        HideMap();
        enteringAncient = true;
        try
        {
            await RunManager.Instance.EnterMapCoord(Native.Map.StartingMapPoint.coord);
        }
        finally { enteringAncient = false; }
    }

    internal bool NativeAncientFinished(ulong player) =>
        RunManager.Instance.EventSynchronizer.Events.Any(e => e.Owner?.NetId == player && e is AncientEventModel && e.IsFinished);

    public void AncientFinished(ulong player)
    {
        if (Map?.Phase is not (JigsawPhase.Ancient or JigsawPhase.Exploring) || !NativeAncientFinished(player))
            return;
        if (JigsawEditorAction.IsHost(LocalPlayerId))
            JigsawEditorAction.Submit(this, JigsawEditorOperation.CompleteAncient, completedOwner: player);
        OnAncientCompleted();
    }

    private bool settlingAncientOptions;
    private bool HasUnfinishedChosenAncient() => RunManager.Instance.EventSynchronizer.Events
        .OfType<AncientEventModel>().Any(e => !e.IsFinished && e.CurrentOptions.Any(option => option.WasChosen));

    private async Task SettleAncientOptions()
    {
        var synchronizer = RunManager.Instance.EventSynchronizer;
        var events = synchronizer.Events.ToArray();
        var map = Map;
        try
        {
            // Run outside the managed action and native StateChanged callback.
            await synchronizer.AwaitPendingOptionTasks();
        }
        finally { settlingAncientOptions = false; }
        // AwaitPendingOptionTasks swallows failures: a chosen unfinished event is
        // still unsafe after its task ends. Keep the whole rollback checkpoint.
        if (Current != this || !ReferenceEquals(Map, map) || !ReferenceEquals(synchronizer, RunManager.Instance.EventSynchronizer)
            || !events.SequenceEqual(synchronizer.Events) || HasUnfinishedChosenAncient())
            return;
        OnAncientCompleted();
    }

    internal void OnAncientCompleted()
    {
        if (Map?.Phase is not (JigsawPhase.Ancient or JigsawPhase.Exploring) || !AncientReady || !ActivePlayerIds.All(NativeAncientFinished))
            return;
        if (settlingAncientOptions)
            return;
        if (HasUnfinishedChosenAncient())
        {
            settlingAncientOptions = true;
            _ = Defer(SettleAncientOptions);
            return;
        }
        if (Native.CurrentRoom is EventRoom room)
            room.MarkPreFinished();
        BeforeAncient = null;
        BeginExploration();
        JigsawCheckpoint.Capture(Native, Map);
        OnEdited();
    }

    // Native reward completion owns this transition so its normal save already
    // contains exploration. Also accepts completed Ancient saves from older builds.
    private void BeginExploration()
    {
        if (Map?.Phase == JigsawPhase.Ancient && AncientReady && Map.ActivePlayers.Count == 1)
            Map.Continue(LocalPlayerId);
    }

    public async Task Continue()
    {
        if (Map is { Phase: JigsawPhase.Ancient } waiting && !AncientReady && Native.CurrentRoom is MapRoom)
        {
            OnEdited();
            await WaitForSave();
            if (Current != this || !ReferenceEquals(Map, waiting))
                return;
            BeforeAncient = JigsawSaveSource.Copy(RunManager.Instance.ToSave(null));
            await EnterAncient();
            return;
        }
        if (Map == null)
            return;
        var submitted = JigsawEditorAction.Submit(this, JigsawEditorOperation.ContinueAncient);
        if (!submitted.Success)
            screen?.ShowResult(submitted);
        await Task.CompletedTask;
    }

    public Task Enter(int node)
    {
        if (Current != this)
            return Task.CompletedTask;
        if (enteringTask is { IsCompleted: false })
            return enteringTask;
        return enteringTask = EnterCore(node);
    }

    public Task AbandonEntry()
    {
        if (Current != this || enteringTask is { IsCompleted: false })
            return Task.CompletedTask;
        return enteringTask = AbandonEntryCore();
    }

    private async Task AbandonEntryCore()
    {
        if (!entryFailed || Map?.PendingEntry is not { } attempt || entryCheckpoint == null)
            return;
        var checkpoint = JigsawSaveSource.Copy(entryCheckpoint);
        await WaitForSave();
        if (Current != this)
            return;
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
        if (Current != selection)
            return;
        selection.OnEdited();
        try
        { await selection.WaitForSave(); }
        catch
        {
            if (Current == selection)
                selection.screen?.ShowNotice("本次变更尚未保存，请稍后重试。");
            throw;
        }
    }

    private async Task EnterCore(int node)
    {
        if (Map == null || BeforeAncient != null)
            return;
        if (Map.PendingEntry == null)
        { entryCheckpoint = null; entryFailed = false; }
        if (entryFailed && Map.PendingEntry?.NodeId == node && entryCheckpoint != null)
        {
            await RetryEntry();
            return;
        }
        var attempt = Map.PendingEntry ?? Map.BeginEnter(node);
        if (attempt == null || attempt.NodeId != node)
            return;
        // Also retain a complete point before native AddVisited/exit/sync can fail.
        entryCheckpoint ??= RunManager.Instance.ToSave(null);
        var destination = Map.Nodes.Single(n => n.Id == node);
        var projection = JigsawEntryProjection.Append(Native.Map,
            NextProjectionRow,
            destination.Room);
        Native.Map = projection.Map;
        JigsawCheckpoint.Capture(Native, Map);
        // Keep the opaque map over the previous room until native has mounted
        // the destination. Hiding it before EnterMapCoord exposed stale scenery.
        if (GodotObject.IsInstanceValid(screen))
            screen?.SetProcessInput(false);
        bool ready = false;
        AbstractRoom? previous = Native.CurrentRoom;
        void RoomReady()
        {
            if (Current == this && Native.CurrentRoom is { } room && room != previous && room is not MapRoom &&
                Native.CurrentMapPoint?.coord == projection.Point.coord)
            {
                // The real native post-EnterInternal event is the confirmation seam.
                ready = Map.ConfirmEnter(attempt.Id).Success;
                if (ready)
                    HideMap();
            }
        }
        RunManager.Instance.RoomEntered += RoomReady;
        bool shop = destination.Room == JigsawRoom.Shop;
        if (shop)
        {
            Map.CreateShopTransactionCheckpoint(node, LocalPlayerId);
            enteringShopNode = node;
        }
        try
        {
            await RunManager.Instance.EnterMapCoord(projection.Point.coord);
            if (Current != this)
                return;
            if (!ready)
                throw new InvalidOperationException("原生房间尚未确认就绪；本次未计步。");
            if (shop)
                CaptureShopInventory(node);
            entryCheckpoint = null;
            JigsawCheckpoint.Capture(Native, Map);
        }
        catch
        {
            if (shop)
                Map?.RollbackShopTransaction();
            if (Current == this)
            { entryFailed = !ready; ShowMap(); }
            throw;
        }
        finally
        {
            enteringShopNode = null;
            RunManager.Instance.RoomEntered -= RoomReady;
            if (Current == this && GodotObject.IsInstanceValid(screen))
                screen?.SetProcessInput(!paused);
        }
    }

    internal void CapturePendingEntry(SerializableRun checkpoint)
    {
        if (Map?.PendingEntry != null && Native.CurrentRoomCount == 0)
            entryCheckpoint = checkpoint;
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
        if (Current != this)
            return;
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
            if (!TestMode.IsOn)
                await NGame.Instance!.LoadRun(restored, null);
            else
            {
                // Existing isolated native host omits NGame's asset/scene ownership.
                await RunManager.Instance.GenerateMap()!;
                await RunManager.Instance.LoadIntoLatestMapCoord(null);
            }
        }
        catch { if (Current?.Native == restored) Current.RestoredEntryFailed(); throw; }
    }

    internal void RestoredEntryFailed()
    {
        if (Current == this && Map?.PendingEntry != null)
        { entryFailed = true; ShowMap(); }
    }

    // T05: Shop persistence. Native MerchantRoom rolls a fresh MerchantInventory on
    // every EnterInternal. The live native inventory objects (entries, sale flag,
    // IsStocked/Used) are kept per logical node and player and handed back to the
    // revisited room, so no new RNG roll happens and purchases stay cleared.
    private readonly Dictionary<(int nodeId, ulong playerId), MerchantInventory> nativeShops = new();
    private int? enteringShopNode;

    internal MerchantInventory? SavedShopFor(Player player) =>
        enteringShopNode is { } node && nativeShops.TryGetValue((node, player.NetId), out var inventory) &&
        ReferenceEquals(inventory.Player, player) ? inventory : null;

    /// <summary>Record the mounted MerchantRoom inventories after native entry, then commit.</summary>
    private void CaptureShopInventory(int nodeId)
    {
        if (Map == null || Native.CurrentRoom is not MerchantRoom room)
        {
            Map?.RollbackShopTransaction();
            return;
        }
        foreach (var inventory in room.Inventories)
        {
            nativeShops[(nodeId, inventory.Player.NetId)] = inventory;
            Map.RecordShopInventory(nodeId, inventory.Player.NetId, Describe(inventory));
        }
        Map.CommitShopTransaction();
    }

    private static JigsawShopInventory Describe(MerchantInventory inventory)
    {
        var cards = inventory.CardEntries.Select((entry, slot) =>
            new JigsawShopCard(entry.CreationResult?.Card?.Id.ToString() ?? "", slot, false, null, entry.Cost)).ToArray();
        var relics = inventory.RelicEntries.Select((entry, slot) =>
            new JigsawShopRelic(entry.Model?.Id.ToString() ?? "", slot, null, entry.Cost)).ToArray();
        var soldOut = new HashSet<string>();
        int index = 0;
        foreach (var entry in inventory.AllEntries)
        {
            if (!entry.IsStocked) soldOut.Add($"{entry.GetType().Name}:{index}");
            index++;
        }
        var promotions = inventory.CardEntries.Select((entry, slot) => (entry, slot))
            .Where(x => x.entry.IsOnSale).ToDictionary(x => $"card:{x.slot}", _ => true);
        return new JigsawShopInventory(cards, relics, inventory.CardRemovalEntry?.Used == true ? 1 : 0,
            inventory.CardRemovalEntry?.IsStocked == true, new Dictionary<string, int>(), promotions, soldOut);
    }

    public void CompleteCurrentRoom(bool canTravel)
    {
        if (!canTravel || Map?.CurrentNode == null || Native.CurrentRoomCount > 1)
            return;
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
            entryCheckpoint = null;
            entryFailed = false;
            JigsawCheckpoint.Capture(Native, Map);
        }
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class JigsawCloseNativeSession
{
    private static void Prefix() => JigsawNativeSession.SaveSession?.Close();
}

internal static class JigsawAutomaticWorker
{
    internal static Func<JigsawRun, int, Task<JigsawLayout?>>? TestOverride;
    internal static Task<JigsawLayout?> Run(JigsawRun map, int budget) =>
        TestMode.IsOn && TestOverride != null ? TestOverride(map, budget) : Task.Run(() => JigsawAutomatic.Solve(map, budget));
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpNewSingleplayer))]
internal static class JigsawEnableNewRun
{
    private static void Postfix(RunState state, DateTimeOffset? dailyTime)
    {
        if (JigsawPreferences.Enabled && dailyTime == null && state.GameMode == GameMode.Standard)
            JigsawNativeSession.Enable(state);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterMapCoord))]
internal static class JigsawBeforeAncientEntry
{
    private static bool Prefix(MapCoord coord, ref Task __result)
    {
        var session = JigsawNativeSession.Current;
        if (session == null || !session.ShouldInterceptAncient(coord))
            return true;
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
        if (JigsawNativeSession.Current is { } session)
            __result = Finish(__result, session);
    }
    private static async Task Finish(Task original, JigsawNativeSession session)
    { await original; await session.StartAct(); session.OnEdited(); session.ShowMap(); }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
internal static class JigsawOpenFormalMap
{
    private static bool Prefix(NMapScreen __instance, bool isOpenedFromTopBar, ref NMapScreen __result)
    {
        if (JigsawNativeSession.Current is not { Map: not null } session)
            return true;
        if (isOpenedFromTopBar && session.CloseRoomPreview())
        { __result = __instance; return false; }
        if (!isOpenedFromTopBar && session.Map.CurrentNode != null)
            session.CompleteCurrentRoom(__instance.IsTravelEnabled);
        session.ShowMap();
        __result = __instance;
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ProceedFromTerminalRewardsScreen))]
internal static class JigsawTerminalRoomCompletion
{
    private static void Postfix(ref Task __result)
    {
        if (JigsawNativeSession.Current is { } session)
            __result = Complete(__result, session);
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
            JigsawNativeSession.Current?.AncientFinished(eventModel.Owner!.NetId);
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
        if (__state != null)
            save.Modifiers = __state.Where(modifier => modifier.Id != identity).ToList();
    }
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(RunState __result, List<SerializableModifier>? __state, SerializableRun save) =>
        JigsawNativeSession.Restore(__result, __state != null || JigsawSaveSource.IsJigsaw(save), save);
    private static void Finalizer(SerializableRun save, List<SerializableModifier>? __state)
    {
        if (__state != null)
            save.Modifiers = __state;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
internal static class JigsawCaptureNativePoint
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ref SerializableRun __result)
    {
        if (JigsawNativeSession.SaveSession is not { } session)
            return;
        if (session.BeforeAncient is { } before)
            __result = JigsawSaveSource.Copy(before);
        var identity = ModelDb.Modifier<AllMattersJigsawMode>();
        if (!__result.Modifiers.Any(modifier => modifier.Id == identity.Id))
            __result.Modifiers.Add(identity.ToMutable().ToSerializable());
        session.CapturePendingEntry(__result);
    }
    private static bool Prefix(ref SerializableRun __result)
    {
        if (JigsawNativeSession.SaveSession is not { Map: not null } session)
            return true;
        JigsawCheckpoint.Capture(session.Native, session.Map);
        if (session.FailedEntryCheckpoint is { } pending)
        { __result = pending; return false; }
        if (session.BeforeAncient == null)
            return true;
        __result = JigsawSaveSource.Copy(session.BeforeAncient);
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.LoadIntoLatestMapCoord))]
internal static class JigsawResumePhase
{
    private static bool Prefix(ref Task __result, out Action? __state)
    {
        __state = null;
        if (JigsawNativeSession.Current is not { Map: not null } session)
            return true;
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
        if (JigsawNativeSession.Current is { Map: not null } session)
            __result = Resume(__result, session, __state);
        else if (__state != null)
            RunManager.Instance.RoomEntered -= __state;
    }
    private static async Task Resume(Task original, JigsawNativeSession session, Action? observe)
    {
        try
        { await original; }
        catch { session.RestoredEntryFailed(); throw; }
        finally { if (observe != null) RunManager.Instance.RoomEntered -= observe; }
        if (JigsawNativeSession.Current != session)
            return;
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
        if (session.Map!.Phase == JigsawPhase.Generating)
        { session.ShowMap(); await session.ResumeAutomatic(); }
        else if (session.Map.Phase == JigsawPhase.Inspecting)
            await session.Inspect();
        else if (session.Map.Phase == JigsawPhase.Ancient && !session.AncientReady)
            await session.EnterAncient();
        else if (session.Map.Phase is JigsawPhase.Editing or JigsawPhase.Ancient ||
                 session.Map.Phase == JigsawPhase.Exploring && session.Map.CurrentNode == null)
            session.ShowMap();
    }
}

[HarmonyPatch(typeof(MerchantInventory), nameof(MerchantInventory.CreateForNormalMerchant))]
internal static class JigsawRestoreShopInventory
{
    // Runs inside MerchantRoom.EnterInternal before NMerchantRoom exposes entries to UI.
    private static bool Prefix(Player player, ref MerchantInventory __result)
    {
        if (JigsawNativeSession.Current?.SavedShopFor(player) is not { } saved) return true;
        __result = saved;
        return false;
    }
}
