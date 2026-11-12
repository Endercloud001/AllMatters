using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

// Extends the existing authenticated three-process ENet host. Only animation
// completion is substituted; ordinary actions, room entry and confirmation stay native.
internal static class JigsawEntryTransitionNetworkProbe
{
    private static TaskCompletionSource<bool>? slowFade;
    private static bool delayRoom;
    private static bool failImmediately;
    private static int reveals;
    private static int nativeReveals;
    private static void NativeReveal() => nativeReveals++;
    private static string? nativeEntryMarker;
    private static string? fadeMarker;
    private static string? failureMarker;
    private static void FailureAccepted() => File.WriteAllText(failureMarker!, "accepted");
    private static void NativeStarting() => File.WriteAllText(nativeEntryMarker!, "started");
    private static void HoldRoom(ref Task __result)
    { if (slowFade != null) __result = DelayRoom(__result); }
    private static async Task DelayRoom(Task original)
    { await original; await slowFade!.Task; }
    private static bool FadeOut(ref Task __result)
    { File.WriteAllText(fadeMarker!, "fading"); __result = failImmediately ? Task.FromException(new IOException("Injected immediate fade failure")) : delayRoom ? Task.CompletedTask : slowFade?.Task ?? Task.CompletedTask; return false; }
    private static bool FadeIn(ref Task __result)
    { reveals++; __result = Task.CompletedTask; return false; }

    internal static async Task Run(JigsawNativeSession session, JigsawScreen view, string role, string directory,
        int target, Func<Func<bool>, string, Task> until, string mode)
    {
        var harmony = new Harmony("AllMatters.Tests.EntryTransitionNetwork");
        var transition = new NTransition();
        transition.AddChild(new Control { Name = "GradientTransition" });
        transition.AddChild(new Control { Name = "SimpleTransition" });
        void Ready()
        {
            File.WriteAllText(Path.Combine(directory, role + ".room-ready"), "ready");
            if (mode == "takeover" && role == "host") _ = transition.RoomFadeIn();
        }
        try
        {
            reveals = nativeReveals = 0;
            bool failing = mode is "failure" or "earlyfailure";
            delayRoom = !failing;
            failImmediately = mode == "earlyfailure" && role == "host";
            slowFade = (mode == "earlyfailure" ? role != "host" : role == "3") ? new(TaskCreationOptions.RunContinuationsAsynchronously) : null;
            nativeEntryMarker = Path.Combine(directory, role + ".native-starting");
            fadeMarker = Path.Combine(directory, role + ".fade-starting");
            failureMarker = Path.Combine(directory, role + ".failure-accepted");
            AccessTools.Property(typeof(NGame), nameof(NGame.Transition)).SetValue(NGame.Instance, transition);
            AccessTools.Property(typeof(NSceneContainer), nameof(NSceneContainer.CurrentScene)).SetValue(NGame.Instance!.RootSceneContainer, null);
            await HeadlessUiHost.Mount(transition);
            harmony.Patch(AccessTools.Method(typeof(NTransition), nameof(NTransition.RoomFadeOut)), prefix: new HarmonyMethod(typeof(JigsawEntryTransitionNetworkProbe), nameof(FadeOut)));
            harmony.Patch(AccessTools.Method(typeof(NTransition), nameof(NTransition.RoomFadeIn)), prefix: new HarmonyMethod(typeof(JigsawEntryTransitionNetworkProbe), nameof(FadeIn)));
            harmony.Patch(AccessTools.Method(typeof(RunManager), nameof(RunManager.EnterMapPointInternal)), prefix: new HarmonyMethod(typeof(JigsawEntryTransitionNetworkProbe), nameof(NativeStarting)));
            harmony.Patch(AccessTools.Method(typeof(JigsawNativeSession), nameof(JigsawNativeSession.AcceptEntryFailure)), postfix: new HarmonyMethod(typeof(JigsawEntryTransitionNetworkProbe), nameof(FailureAccepted)));
#if STS2_PUBLIC
            harmony.Patch(AccessTools.Method(typeof(RunManager), "FadeIn", [typeof(bool)]), prefix: new HarmonyMethod(typeof(JigsawEntryTransitionNetworkProbe), nameof(NativeReveal)));
#endif
            if (!failing)
            {
                // Native combat sync requires all endpoints to enter before any can
                // mount a room. Delay real room completion after that common sync.
                foreach (var type in new[] { typeof(CombatRoom), typeof(MerchantRoom), typeof(TreasureRoom), typeof(EventRoom) })
                    harmony.Patch(AccessTools.Method(type, nameof(CombatRoom.EnterInternal)), postfix: new HarmonyMethod(typeof(JigsawEntryTransitionNetworkProbe), nameof(HoldRoom)));
            }
            RunManager.Instance.RoomEntered += Ready;
            File.WriteAllText(Path.Combine(directory, role + ".transition-probe"), "ready");
            await until(() => new[] { "host", "2", "3" }.All(peer => File.Exists(Path.Combine(directory, peer + ".transition-probe"))), "transition probes mounted");
            if (role != "2")
                JigsawEditorAction.Submit(session, JigsawEditorOperation.VoteDestination, nodeId: target);
            if (mode == "earlyfailure")
            {
                await until(() => File.Exists(failureMarker!), "reliable EntryFailed is applied while other endpoints still await fade completion");
                if (role != "host")
                {
                    if (session.EntryRecoveryTask?.IsCompleted == true)
                        throw new InvalidOperationException("Construct the failure while the endpoint is still fading, before it creates its local readiness decision.");
                    slowFade!.SetResult(true);
                }
            }
            else if (role == "3")
            {
                await until(() => File.Exists(Path.Combine(directory, "release-fade")), "fast endpoints observe native readiness under the cover");
                if (reveals != 0)
                    throw new InvalidOperationException("The slow endpoint must not reveal before its room is ready.");
                if (mode == "failure") slowFade!.SetException(new IOException("Injected native fade-out failure"));
                else slowFade!.SetResult(true);
            }
            else
            {
                await until(() => File.Exists(Path.Combine(directory, role + (mode == "failure" ? ".fade-starting" : ".room-ready"))), "fast endpoint reaches transition boundary");
                if (reveals != (mode == "takeover" && role == "host" ? 1 : 0) || session.Map!.PendingEntry == null || session.Map.Steps != 0 || !view.Visible)
                    throw new InvalidOperationException("Local RoomEntered must not reveal or confirm while the slow endpoint is still fading.");
                if (role == "host") File.WriteAllText(Path.Combine(directory, "release-fade"), "release");
            }
            await until(() => session.EntryRecoveryTask?.IsCompleted == true, "every native ordinary entry returns after common decision");
            if (failing)
            {
                if (session.Map!.PendingEntry == null || session.Map.Steps != 0 || !view.Visible || !view.IsProcessingInput() || reveals != 1)
                    throw new InvalidOperationException("A fade failure must release every endpoint to the uncharged production map with input and one reveal.");
                if (session.Native.VisitedMapCoords.Count != 1 || session.FailedEntryCheckpoint == null)
                    throw new InvalidOperationException("Fade failure retains a complete before-entry point without native projection/history or RNG entry work.");
            }
            else if (session.Map!.PendingEntry != null || session.Map.Steps != 1 || view.Visible || reveals != 1)
                throw new InvalidOperationException("All endpoints must confirm once and reveal exactly once after the real slow room readiness.");
#if STS2_PUBLIC
            if (nativeReveals != 0)
                throw new InvalidOperationException("A committed ordinary native entry must still defer its private reveal after another visual caller took ownership.");
#endif
            Console.WriteLine($"TRANSITION ENET mode={mode} role={role} steps={session.Map.Steps} reveals={reveals}");
            File.WriteAllText(Path.Combine(directory, role + ".transition-checked"), "checked");
            await until(() => new[] { "host", "2", "3" }.All(peer => File.Exists(Path.Combine(directory, peer + ".transition-checked"))), "all endpoint transition assertions");
            if (failing)
            {
                slowFade = null; // The injected animation fault affects this one attempt.
                failImmediately = false;
                var expectedPlayers = session.Native.Players.Select(player => (player.NetId, player.Gold, Deck: string.Join(',', player.Deck.Cards.Select(card => card.Id)))).ToArray();
                if (role == "host") await session.Enter(target);
                await until(() => JigsawNativeSession.Current is { } current && current != session && current.Map is { PendingEntry: null, Steps: 1 }, "reliable whole-checkpoint retry confirms the native room on all endpoints");
                var recovered = JigsawNativeSession.Current!;
                if (recovered.Map!.CurrentNode != target || recovered.Native.VisitedMapCoords.Count != 2 || recovered.EntryRound != session.EntryRound + 1 ||
                    !recovered.Native.Players.Select(player => (player.NetId, player.Gold, Deck: string.Join(',', player.Deck.Cards.Select(card => card.Id)))).SequenceEqual(expectedPlayers))
                    throw new InvalidOperationException("Recovery must enter the same target once, retain complete player effects and advance only the recovery round.");
                File.WriteAllText(Path.Combine(directory, role + ".transition-recovered"), "recovered");
                await until(() => new[] { "host", "2", "3" }.All(peer => File.Exists(Path.Combine(directory, peer + ".transition-recovered"))), "all endpoints observe the recovered entry once");
                Console.WriteLine($"TRANSITION RECOVERED role={role} round={recovered.EntryRound} target={target} steps={recovered.Map.Steps}");
            }
        }
        finally
        {
            RunManager.Instance.RoomEntered -= Ready;
            slowFade?.TrySetResult(true); harmony.UnpatchAll(harmony.Id); transition.Free();
        }
    }
}
