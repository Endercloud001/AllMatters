using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
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
    internal static JigsawNativeSession? TryGet(RunState native) =>
        sessions.TryGetValue(native, out var session) && !session.closed ? session : null;
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
    internal CancellationToken ClosedToken => closedCancellation.Token;
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
        entryDecision?.TrySetResult(false);
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
    private bool entryRecoveryRequested;
    private bool entryConfirmQueued;
    internal Task? EntryRecoveryTask { get; private set; }
    private readonly HashSet<ulong> entryReadyPlayers = [];
    private long nativeReadyAttempt;
    private TaskCompletionSource<bool>? entryDecision;
    private readonly HashSet<ulong> entryFadePlayers = [];
    private long entryFadeAttempt;
    private TaskCompletionSource<bool>? entryFadeDecision;
    private Task? enteringTask;
    internal long EntryRound { get; private set; }
    internal void SetEntryRound(long round) => EntryRound = round;
    private RunLobby? boundLobby;
    public SerializableRun? BeforeAncient { get; private set; }
    internal JigsawDeferredAncient? DeferredAncient { get; private set; }
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
        if (RunManager.Instance.NetService is NetSingleplayerGameService || lobby == null)
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
        var lobby = netService is NetSingleplayerGameService ? null : RunManager.Instance.RunLobby;
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
        entryReadyPlayers.Remove(player);
        entryFadePlayers.Remove(player);
        CompleteEntryFadeIfReady();
        MaybeConfirmSharedEntry();
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
        bool validating = JigsawLobbyCheckpoint.IsValidating || native.Players.Count > 1 &&
            !ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), native);
        var restoredMap = validating ? JigsawRun.Restore(mapData) : JigsawRun.RestoreForDeferredAncient(mapData, ActivePlayers(native));
        // The native lobby validates/rebuilds saves before a live session exists.
        // Validate the document here without activating multiplayer gameplay.
        if (validating)
            return;
        var session = Enable(native);
        session.act = data.Act;
        session.Map = restoredMap;
        if (restoredMap.HasDeferredAncient)
        {
            if (checkpoint == null)
                throw new InvalidDataException("补领 Ancient 需要同一原生存档的完整检查点。");
            session.DeferredAncient = new(session, checkpoint);
        }
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
        screen.CanVoteRequested = () => Map?.RoomCompletedPlayers.Contains(LocalPlayerId) == true || CanLeaveMerchant;
        screen.VoteRequested = VoteDestination;
        return screen;
    }
    private bool CanLeaveMerchant => Native.CurrentRoom is MerchantRoom && NMapScreen.Instance?.IsTravelEnabled == true;
    private JigsawResult VoteDestination(int node)
    {
        if (Current != this)
            return JigsawResult.Reject("拼图会话已结束。");
        if (Map?.CurrentNode != null && Map.VotingDestinations.Contains(node) && CanLeaveMerchant)
        {
            JigsawNativeExploration.MerchantExited(this);
            CompleteCurrentRoom(true);
            // In singleplayer, completion removes CurrentNode. Update the preview
            // only after an actual destination choice, preserving return-to-shop.
            ShowMap();
        }
        return JigsawEditorAction.Submit(this, JigsawEditorOperation.VoteDestination, nodeId: node);
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
        Callable.From((Action)(async () => { try { if (Current == this && ReferenceEquals(Map, map)) await work(); completion.TrySetResult(); } catch (Exception error) { completion.TrySetException(error); } })).CallDeferred();
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
        if (DeferredAncient is { } deferred)
        {
            await deferred.Resume();
            return;
        }
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

    internal bool NativeRoomFinished(ulong player) => JigsawNativeExploration.LocalCompletionWasObserved(this, player);

    internal void OnDestinationBallotChanged()
    {
        if (!JigsawEditorAction.IsHost(LocalPlayerId) || Map?.ProposedDestination is not { } selected)
            return;
        _ = Defer(() =>
        {
            if (Map?.ProposedDestination == selected)
                JigsawEditorAction.Submit(this, JigsawEditorOperation.FinalizeDestination, nodeId: selected);
            return Task.CompletedTask;
        });
    }

    internal void OnDestinationFinalized(int node)
    {
        OnEdited();
        TrackEntryRecovery(() => Enter(node));
    }

    public Task AbandonEntry()
    {
        if (Current != this || enteringTask is { IsCompleted: false })
            return Task.CompletedTask;
        if (netService is not NetSingleplayerGameService)
            return enteringTask = RequestSharedEntryRecovery(abandon: true);
        return enteringTask = AbandonEntryCore();
    }

    private Task RequestSharedEntryRecovery(bool abandon)
    {
        if (!entryFailed || Map?.PendingEntry == null || entryRecoveryRequested)
            return Task.CompletedTask;
        if (!JigsawEditorAction.IsHost(LocalPlayerId))
        {
            screen?.ShowNotice("请等待 Host 决定全队进入恢复。");
            return Task.CompletedTask;
        }
        var result = JigsawEditorAction.Submit(this, abandon ? JigsawEditorOperation.AbandonEntry : JigsawEditorOperation.RetryEntry);
        if (!result.Success)
            throw new InvalidOperationException(result.Reason);
        return Task.CompletedTask;
    }

    internal JigsawResult AcceptEntryReady(ulong player)
    {
        if (Map?.PendingEntry == null || entryFailed || !Map.ActivePlayers.Contains(player))
            return JigsawResult.Reject(JigsawFailure.StaleEntry);
        entryReadyPlayers.Add(player);
        MaybeConfirmSharedEntry();
        return JigsawResult.Ok;
    }

    private void MaybeConfirmSharedEntry()
    {
        if (Map?.PendingEntry is not { } pending || entryFailed || entryConfirmQueued || !JigsawEditorAction.IsHost(LocalPlayerId) ||
            Map.ActivePlayers.Count == 0 || !Map.ActivePlayers.All(entryReadyPlayers.Contains))
            return;
        entryConfirmQueued = true;
        _ = Defer(() =>
        {
            if (Map?.PendingEntry?.Id == pending.Id && !entryFailed)
            {
                var submitted = JigsawEditorAction.Submit(this, JigsawEditorOperation.ConfirmEntry);
                if (!submitted.Success)
                    entryConfirmQueued = false;
            }
            return Task.CompletedTask;
        });
    }

    internal JigsawResult AcceptEntryFailure(ulong player)
    {
        if (Map?.PendingEntry == null || !Map.ActivePlayers.Contains(player))
            return JigsawResult.Reject(JigsawFailure.StaleEntry);
        entryFailed = true;
        entryConfirmQueued = false;
        entryReadyPlayers.Clear();
        entryDecision?.TrySetResult(false);
        entryFadeDecision?.TrySetResult(false);
        ShowMap();
        return JigsawResult.Ok;
    }

    private TaskCompletionSource<bool> EntryFadeDecision()
    {
        if (entryFadeAttempt != Map!.PendingEntry!.Id)
        {
            entryFadeAttempt = Map.PendingEntry.Id;
            entryFadePlayers.Clear();
            entryFadeDecision = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        // EntryFailed can arrive while this endpoint is still awaiting its fade,
        // before it has created a local decision. Never wait for a rejected ready.
        if (entryFailed)
            entryFadeDecision!.TrySetResult(false);
        return entryFadeDecision!;
    }
    internal JigsawResult AcceptEntryFadeReady(ulong player)
    {
        if (Map?.PendingEntry == null || entryFailed || !Map.ActivePlayers.Contains(player))
            return JigsawResult.Reject(JigsawFailure.StaleEntry);
        _ = EntryFadeDecision();
        entryFadePlayers.Add(player);
        CompleteEntryFadeIfReady();
        return JigsawResult.Ok;
    }
    private void CompleteEntryFadeIfReady()
    {
        if (!entryFailed && Map is { PendingEntry: { } pending } && pending.Id == entryFadeAttempt &&
            Map.ActivePlayers.Count > 0 && Map.ActivePlayers.All(entryFadePlayers.Contains))
            entryFadeDecision?.TrySetResult(true);
    }

    internal JigsawResult ConfirmSharedEntry()
    {
        if (Map?.PendingEntry is not { } pending || entryFailed || nativeReadyAttempt != pending.Id ||
            JigsawEditorAction.IsHost(LocalPlayerId) && !Map.ActivePlayers.All(entryReadyPlayers.Contains) ||
            Native.CurrentRoom is null or MapRoom)
            return JigsawResult.Reject(JigsawFailure.StaleEntry);
        var declared = Map.Nodes.Single(node => node.Id == pending.NodeId).Room;
        if (!Map.ConfirmEnter(pending.Id).Success)
            return JigsawResult.Reject(JigsawFailure.StaleEntry);
        Map.RecordResolvedRoom(pending.NodeId, ActualRoom(Native.CurrentRoom, declared));
        entryCheckpoint = null;
        entryFailed = false;
        entryDecision?.TrySetResult(true);
        JigsawCheckpoint.Capture(Native, Map);
        HideMap();
        OnEdited();
        return JigsawResult.Ok;
    }

    internal JigsawResult RequestEntryRecovery(bool abandon)
    {
        if (!entryFailed || Map?.PendingEntry == null || entryRecoveryRequested)
            return JigsawResult.Reject(JigsawFailure.StaleEntry);
        entryRecoveryRequested = true;
        if (JigsawEditorAction.IsHost(LocalPlayerId))
            TrackEntryRecovery(() => SendEntryRecovery(abandon));
        return JigsawResult.Ok;
    }

    private void TrackEntryRecovery(Func<Task> work)
    {
        var task = EntryRecoveryTask = Defer(work);
        _ = ReportEntryRecoveryFailure(task);
    }

    private async Task ReportEntryRecoveryFailure(Task task)
    {
        try
        { await task; }
        catch (Exception error)
        {
            entryRecoveryRequested = false;
            if (Current == this)
                screen?.ShowNotice("进入恢复失败，请检查联机连接后重试。");
            Console.Error.WriteLine($"Jigsaw entry recovery failed: {error}");
        }
    }

    private async Task SendEntryRecovery(bool abandon)
    {
        if (entryCheckpoint == null || Map?.PendingEntry is not { } attempt)
            throw new InvalidDataException("进入前检查点缺失。");
        var checkpoint = JigsawSaveSource.Copy(entryCheckpoint);
        await WaitForSave();
        if (Current != this)
            return;
        if (abandon)
        {
            int row = Math.Max(0, (checkpoint.MapPointHistory.ElementAtOrDefault(checkpoint.CurrentActIndex)?.Count ?? 1) - 1);
            if (checkpoint.VisitedMapCoords.Count > 0 && checkpoint.VisitedMapCoords[^1] == new MapCoord(0, row))
                checkpoint.VisitedMapCoords.RemoveAt(checkpoint.VisitedMapCoords.Count - 1);
            var restored = RunState.FromSerializable(checkpoint);
            if (!JigsawCheckpoint.Slot.TryGet(restored, out var data) || data.Map == null)
                throw new InvalidDataException("进入前检查点缺少拼图地图。");
            var selection = JigsawRun.Restore(data.Map);
            if (!selection.AbandonEntry(attempt.Id).Success)
                throw new InvalidDataException("不能从完整进入点恢复目的地选择。");
            JigsawCheckpoint.Capture(restored, selection);
            JigsawSaveSource.CaptureDocument(checkpoint, restored);
        }
        var submitted = JigsawEditorAction.Submit(this, JigsawEditorOperation.RecoverEntry, entryCheckpoint: JigsawSaveSource.Serialize(checkpoint));
        if (!submitted.Success)
            throw new IOException(submitted.Reason);
    }

    internal JigsawResult AcceptEntryRecovery(string json)
    {
        if (!entryFailed || Map?.PendingEntry == null || !entryRecoveryRequested)
            return JigsawResult.Reject(JigsawFailure.StaleEntry);
        SerializableRun checkpoint;
        RunState restored;
        try
        {
            checkpoint = JigsawSaveSource.Deserialize(json);
            restored = RunState.FromSerializable(checkpoint);
            if (!JigsawCheckpoint.Slot.TryGet(restored, out var data) || data.Map?.NextEntry != Map.CreateSave().NextEntry ||
                data.Map.OriginalPlayers == null || !data.Map.OriginalPlayers.ToHashSet().SetEquals(Map.OriginalPlayers))
                return JigsawResult.Reject("恢复检查点与当前进入不匹配。");
        }
        catch (Exception error) { return JigsawResult.Reject(error.Message); }
        var active = Map.ActivePlayers.ToArray();
        long round = EntryRound + 1;
        TrackEntryRecovery(() => LoadCheckpoint(checkpoint, restored, active, round));
        return JigsawResult.Ok;
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
            if (netService is NetSingleplayerGameService)
                await RetryEntry();
            else
                await RequestSharedEntryRecovery(abandon: false);
            return;
        }
        if (Map.ActivePlayers.Count > 1 && Map.PendingEntry?.NodeId != node)
            return;
        var attempt = Map.PendingEntry ?? Map.BeginEnter(node);
        if (attempt == null || attempt.NodeId != node)
            return;
        var view = GodotObject.IsInstanceValid(screen) ? screen : null;
        var processMode = view?.ProcessMode;
        JigsawEntryTransition? transition = null;
        Exception? failure = null;
        bool nativeStarted = false;
        try
        {
            if (GodotObject.IsInstanceValid(view))
            { view!.ProcessMode = Node.ProcessModeEnum.Disabled; view.SetProcessInput(false); }
            transition = JigsawEntryTransition.Begin(this);
            if (transition != null)
                await transition.FadeOut();
            if (Current != this || Map.PendingEntry?.Id != attempt.Id || paused || BeforeAncient != null || entryRecoveryRequested)
                throw new OperationCanceledException("进入目标或会话已改变。");
            if (netService is not NetSingleplayerGameService)
            {
                if (entryFailed)
                    throw new IOException("队友房间过场失败；本次未计步。");
                var decision = EntryFadeDecision().Task;
                var submitted = JigsawEditorAction.Submit(this, JigsawEditorOperation.EntryFadeReady, completedOwner: LocalPlayerId);
                if (!submitted.Success || !await decision.WaitAsync(ClosedToken))
                    throw new IOException("队友房间过场失败；本次未计步。");
                // Once local fade readiness was sent, visual ownership no longer
                // retracts this endpoint from native sync. A later visual takeover
                // only prevents our reveal; disconnects remain native lifecycle.
                if (Current != this || Map.PendingEntry?.Id != attempt.Id || entryFailed || entryRecoveryRequested)
                    throw new OperationCanceledException("进入目标或过场已改变。");
            }
            await EnterPreparedCore(node, attempt, transition, () => nativeStarted = true);
        }
        catch (Exception error)
        {
            failure = error;
            if (!nativeStarted && Current == this && Map.PendingEntry?.Id == attempt.Id && netService is not NetSingleplayerGameService)
            {
                // Before fade readiness, no native state has changed. Preparation
                // failures after readiness already retain their before-entry point.
                entryCheckpoint ??= JigsawSaveSource.Copy(RunManager.Instance.ToSave(null));
                if (!entryFailed)
                {
                    entryFailed = true;
                    entryFadeDecision?.TrySetResult(false);
                    RunManager.Instance.RunLocationTargetedBuffer.OnLocationChanged(Native.RunLocation);
                    JigsawEditorAction.Submit(this, JigsawEditorOperation.EntryFailed, completedOwner: LocalPlayerId);
                    RunManager.Instance.ActionExecutor.Unpause();
                }
            }
            if (Current == this && (transition?.IsCurrent == true || NGame.Instance?.Transition == null))
                ShowMap();
            throw;
        }
        finally
        {
            try
            { if (transition != null) await transition.Reveal(); }
            catch (Exception error) when (failure != null)
            { Console.Error.WriteLine($"Jigsaw room reveal failed after entry failure: {error}"); }
            finally
            {
                transition?.Dispose();
                if (Current == this && GodotObject.IsInstanceValid(view))
                { view!.ProcessMode = processMode!.Value; view.SetProcessInput(!paused); }
            }
        }
    }

    private async Task EnterPreparedCore(int node, JigsawEnterAttempt attempt, JigsawEntryTransition? transition, Action startingNative)
    {
        if (Map == null)
            return;
        entryDecision = null;
        entryReadyPlayers.Clear();
        entryConfirmQueued = false;
        nativeReadyAttempt = 0;
        entryRecoveryRequested = false;
        // Also retain a complete point before native AddVisited/exit/sync can fail.
        entryCheckpoint ??= JigsawSaveSource.Copy(RunManager.Instance.ToSave(null));
        var destination = Map.Nodes.Single(n => n.Id == node);
        var projection = JigsawEntryProjection.Append(Native.Map,
            NextProjectionRow,
            Map.ResolvedRoom(node));
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
                if (ready)
                    return;
                // RoomEntered is the native readiness seam on each endpoint.
                if (netService is NetSingleplayerGameService)
                {
                    ready = Map.ConfirmEnter(attempt.Id).Success;
                    if (ready)
                    {
                        Map.RecordResolvedRoom(node, ActualRoom(room, destination.Room));
                        HideMap();
                    }
                }
                else
                {
                    nativeReadyAttempt = attempt.Id;
                    entryDecision ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                    ready = JigsawEditorAction.Submit(this, JigsawEditorOperation.EntryReady, completedOwner: LocalPlayerId).Success;
                }
            }
        }
        RunManager.Instance.RoomEntered += RoomReady;
        bool shop = Map.ResolvedRoom(node) is JigsawRoom.Shop or JigsawRoom.Unknown;
        if (shop)
        {
            Map.CreateShopTransactionCheckpoint(node, LocalPlayerId);
            enteringShopNode = node;
        }
        try
        {
            startingNative();
            using (transition?.NativeEntry())
                await RunManager.Instance.EnterMapCoord(projection.Point.coord);
            if (Current != this)
                return;
            if (!ready)
                throw new InvalidOperationException("原生房间尚未确认就绪；本次未计步。");
            if (netService is not NetSingleplayerGameService && !await WaitForEntryDecision())
                throw new InvalidOperationException("队友进入失败；本次未计步。");
            if (shop)
                CaptureShopInventory(node);
            entryCheckpoint = null;
            JigsawCheckpoint.Capture(Native, Map);
        }
        catch
        {
            if (shop)
                Map?.RollbackShopTransaction();
            if (Current == this && netService is not NetSingleplayerGameService)
            {
                if (ready)
                    await WaitForEntryDecision();
                else if (!entryFailed)
                {
                    entryFailed = true;
                    RunManager.Instance.RunLocationTargetedBuffer.OnLocationChanged(Native.RunLocation);
                    JigsawEditorAction.Submit(this, JigsawEditorOperation.EntryFailed, completedOwner: LocalPlayerId);
                    RunManager.Instance.ActionExecutor.Unpause();
                }
                if (Map?.PendingEntry != null)
                    ShowMap();
            }
            else if (Current == this)
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

    internal bool NativeEntryWasReady => Map?.PendingEntry is { } pending && nativeReadyAttempt == pending.Id;

    internal Task<bool> WaitForEntryDecision() =>
        (entryDecision ??= new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;

    internal void CapturePendingEntry(SerializableRun checkpoint)
    {
        if (Map?.PendingEntry != null && Native.CurrentRoomCount == 0)
            entryCheckpoint = JigsawSaveSource.Copy(checkpoint);
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

    private static async Task LoadCheckpoint(SerializableRun checkpoint, RunState restored,
        IReadOnlyCollection<ulong>? active = null, long round = 0)
    {
        if (active != null)
        {
            await JigsawNativeEntryReload.Load(checkpoint, restored, active, round);
            return;
        }
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
        {
            if (!NativeEntryWasReady)
            {
                entryFailed = true;
                if (netService is not NetSingleplayerGameService)
                {
                    RunManager.Instance.RunLocationTargetedBuffer.OnLocationChanged(Native.RunLocation);
                    JigsawEditorAction.Submit(this, JigsawEditorOperation.EntryFailed, completedOwner: LocalPlayerId);
                    RunManager.Instance.ActionExecutor.Unpause();
                }
            }
            ShowMap();
        }
    }

    // Live entries retain native callbacks within a session; a new session
    // rebuilds the same complete stock from the native save's jigsaw document.
    private readonly Dictionary<(int nodeId, ulong playerId), MerchantInventory> nativeShops = new();
    private int? enteringShopNode;

    internal MerchantInventory? SavedShopFor(Player player)
    {
        if (Map == null || (enteringShopNode ?? Map.PendingEntry?.NodeId ?? Map.CurrentNode) is not { } node)
            return null;
        if (nativeShops.TryGetValue((node, player.NetId), out var inventory) && ReferenceEquals(inventory.Player, player))
            return inventory;
        if (Map.GetShopInventory(node, player.NetId) is { } stock)
        {
            inventory = JigsawNativeShopPersistence.Restore(player, stock);
            nativeShops[(node, player.NetId)] = inventory;
            return inventory;
        }
        if (Map.Visited.Contains(node) && Map.ResolvedRoom(node) == JigsawRoom.Shop)
            throw new InvalidDataException("已访问商店的库存缺失，不能重新抽取；请恢复完整备份。");
        return null;
    }

    internal void CaptureCurrentShop()
    {
        if (Map?.CurrentNode is { } node && Native.CurrentRoom is MerchantRoom)
            CaptureShopInventory(node);
    }

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
            Map.RecordShopInventory(nodeId, inventory.Player.NetId, JigsawNativeShopPersistence.Capture(inventory));
        }
        Map.CommitShopTransaction();
    }

    public void CompleteCurrentRoom(bool canTravel)
    {
        if (BeforeAncient != null || DeferredAncient is { IsResuming: false } || Map?.CurrentNode == null || !JigsawNativeExploration.MarkLocalExit(this, canTravel))
            return;
        CaptureCurrentShop();
        if (Map.ActivePlayers.Count == 1)
        {
            Map.CompleteRoom(Map.CreateSave().NextEntry);
            OnEdited();
        }
        else
            JigsawEditorAction.Submit(this, JigsawEditorOperation.CompleteRoom, completedOwner: LocalPlayerId);
    }

    internal void BeginDeferredAncient(SerializableRun point)
    {
        BeforeAncient = JigsawSaveSource.Copy(point);
        HideMap();
    }

    internal void FinishDeferredAncient() => DeferredAncient = null;

    public bool ShouldInterceptAncient(MapCoord coord) => !enteringAncient &&
        Native.Map.GetPoint(coord)?.PointType == MapPointType.Ancient &&
          (Map == null || act != Native.CurrentActIndex || Map.Phase is JigsawPhase.Editing or JigsawPhase.Inspecting or JigsawPhase.Generating);

    public void ConfirmRestoredEntry()
    {
        if (Current == this && Map?.PendingEntry is { } pending && Native.CurrentRoom is not (null or MapRoom))
        {
            if (netService is not NetSingleplayerGameService)
            {
                nativeReadyAttempt = pending.Id;
                entryDecision ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
                JigsawEditorAction.Submit(this, JigsawEditorOperation.EntryReady, completedOwner: LocalPlayerId);
                return;
            }
            if (!Map.ConfirmEnter(pending.Id).Success)
                return;
            var declared = Map.Nodes.Single(node => node.Id == pending.NodeId).Room;
            Map.RecordResolvedRoom(pending.NodeId, ActualRoom(Native.CurrentRoom, declared));
            entryCheckpoint = null;
            entryFailed = false;
            JigsawCheckpoint.Capture(Native, Map);
        }
    }

    private static JigsawRoom ActualRoom(AbstractRoom room, JigsawRoom declared) => room switch
    {
        MerchantRoom => JigsawRoom.Shop,
        CombatRoom => JigsawRoom.Combat,
        RestSiteRoom => JigsawRoom.Rest,
        TreasureRoom => JigsawRoom.Treasure,
        EventRoom => JigsawRoom.Unknown,
        _ => declared
    };
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom), "HideScreen")]
internal static class JigsawMerchantExitSignal
{
    private static void Prefix()
    {
        if (JigsawNativeSession.Current is { } session)
            JigsawNativeExploration.MerchantExited(session);
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Rooms.NTreasureRoom), "OpenChest")]
internal static class JigsawTreasureOpenSignal
{
    private static void Prefix()
    {
        if (JigsawNativeSession.Current is { } session)
            JigsawNativeExploration.TreasureOpened(session);
    }
}

[HarmonyPatch(typeof(TreasureRoom), nameof(TreasureRoom.DoNormalRewards))]
internal static class JigsawTreasureRewardSignal
{
    private static void Prefix()
    {
        if (JigsawNativeSession.Current is { } session)
            JigsawNativeExploration.TreasureOpened(session);
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
        if (session.Map?.CurrentNode != null)
        {
            if (!await JigsawNativeExploration.WaitForStartedRewards(session, RunManager.Instance.NetService.NetId))
                return;
            JigsawNativeExploration.TerminalRewardsFinished(session);
        }
        if (session.Native.CurrentRoom is { } room && (room.IsPreFinished || room is TreasureRoom))
        {
            session.CompleteCurrentRoom(true);
            session.ShowMap();
        }
    }
}

// Native NRewardsScreen returns to the map after the first of two bosses only when
// CurrentMapCoord == BossMapPoint.coord. Jigsaw projects every entry to (0,row), so
// that check never matches and the first boss fell through to ActChangeSynchronizer
// (EnterNextAct → ending). Route the logical first boss of a dual-boss act back to the map.
[HarmonyPatch(typeof(NRewardsScreen), "OnProceedButtonPressed")]
internal static class JigsawFirstBossReturnsToMap
{
    private static bool Prefix(bool ____isTerminal)
    {
        if (!____isTerminal || JigsawNativeSession.Current is not { Map: { CurrentNode: int node } map } session)
            return true;
        if (session.Native.CurrentRoom?.RoomType != RoomType.Boss)
            return true;
        if (map.Nodes.Single(n => n.Id == node).Room != JigsawRoom.Boss)
            return true;
        if (!map.Nodes.Any(n => n.Room == JigsawRoom.SecondBoss))
            return true;
        TaskHelper.RunSafely(RunManager.Instance.ProceedFromTerminalRewardsScreen());
        return false;
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
    [HarmonyPriority(Priority.VeryLow)]
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
    [HarmonyPriority(Priority.VeryLow)]
    private static void Postfix(ref SerializableRun __result)
    {
        if (JigsawNativeSession.SaveSession is not { } session)
            return;
        if (session.BeforeAncient is { } before)
            __result = JigsawSaveSource.Copy(before);
        else if (session.FailedEntryCheckpoint is { } failed)
            __result = JigsawSaveSource.Copy(failed);
        else if (session.DeferredAncient is { IsRunning: true } deferred)
            deferred.RestoreSaveRoom(__result);
        var identity = ModelDb.Modifier<AllMattersJigsawMode>();
        if (!__result.Modifiers.Any(modifier => modifier.Id == identity.Id))
            __result.Modifiers.Add(identity.ToMutable().ToSerializable());
        session.CapturePendingEntry(__result);
    }
    private static bool Prefix(ref SerializableRun __result)
    {
        if (JigsawNativeSession.SaveSession is not { Map: not null } session)
            return true;
        JigsawNativeShopSync.ThrowIfUnsettled(session);
        session.CaptureCurrentShop();
        JigsawCheckpoint.Capture(session.Native, session.Map);
        if (session.FailedEntryCheckpoint is { } pending)
        { __result = JigsawSaveSource.Copy(pending); return false; }
        if (session.BeforeAncient == null)
            return true;
        __result = JigsawSaveSource.Copy(session.BeforeAncient);
        return false;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.LoadIntoLatestMapCoord))]
internal static class JigsawResumePhase
{
    private static bool Prefix(AbstractRoom? preFinishedRoom, ref Task __result, out Action? __state)
    {
        __state = null;
        if (JigsawNativeSession.Current is not { Map: not null } session)
            return true;
        var map = session.Map;
        if (session.DeferredAncient is { IsResuming: false } deferred)
        {
            __result = deferred.Enter(preFinishedRoom);
            return false;
        }
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
        {
            if (session.DeferredAncient is not { IsResuming: false })
                __result = Resume(__result, session, __state);
        }
        else if (__state != null)
            RunManager.Instance.RoomEntered -= __state;
    }
    private static async Task Resume(Task original, JigsawNativeSession session, Action? observe)
    {
        try
        { await original; }
        catch
        {
            if (session.NativeEntryWasReady)
                await session.WaitForEntryDecision();
            else
                session.RestoredEntryFailed();
            throw;
        }
        finally { if (observe != null) RunManager.Instance.RoomEntered -= observe; }
        if (JigsawNativeSession.Current != session)
            return;
        if (session.Map?.PendingEntry != null)
        {
            if (session.NativeEntryWasReady && await session.WaitForEntryDecision())
                return;
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
        if (JigsawNativeSession.Current?.SavedShopFor(player) is not { } saved)
            return true;
        __result = saved;
        return false;
    }
}
