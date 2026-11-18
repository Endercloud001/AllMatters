using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
#if !STS2_PUBLIC
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Dread.Dominions;
using MegaCrit.Sts2.Core.TestSupport;
#endif

// Extends the existing authenticated three-process ENet host. Only animation
// completion is substituted; ordinary actions, room entry and confirmation stay native.
internal static class JigsawEntryTransitionNetworkProbe
{
    private static TaskCompletionSource<bool>? slowFade;
    private static bool delayRoom;
    private static TaskCompletionSource<bool>? hostFade;
    private static bool failPreparation;
    private static int reveals;
    private static int nativeReveals;
    private static void NativeReveal() => nativeReveals++;
    private static string? nativeEntryMarker;
    private static string? fadeMarker;
    private static string? failureMarker;
    private static void FailureAccepted() => File.WriteAllText(failureMarker!, "accepted");
    private static void NativeStarting() => File.WriteAllText(nativeEntryMarker!, "started");
    private static void EnterCombat(ref MapPointType pointType) => pointType = MapPointType.Monster;
    private static void PrepareProjection()
    {
        if (!failPreparation)
            return;
        failPreparation = false;
        throw new IOException("Injected local projection preparation failure.");
    }
    private static void HoldRoom(ref Task __result)
    { if (slowFade != null) __result = DelayRoom(__result); }
    private static async Task DelayRoom(Task original)
    { await original; await slowFade!.Task; }
    private static bool FadeOut(ref Task __result)
    { File.WriteAllText(fadeMarker!, "fading"); __result = hostFade?.Task ?? (delayRoom ? Task.CompletedTask : slowFade?.Task ?? Task.CompletedTask); return false; }
    private static bool FadeIn(ref Task __result)
    { reveals++; __result = Task.CompletedTask; return false; }

#if !STS2_PUBLIC
    private sealed class OpeningChoice : ICardSelector
    {
        internal Sloth? Offered { get; private set; }
        private readonly TaskCompletionSource<IEnumerable<CardModel>> choice = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int selectedIndex;

        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            var cards = options.ToArray();
            Offered = cards.OfType<Sloth>().Single();
            selectedIndex = Array.IndexOf(cards, Offered);
            return choice.Task;
        }

        internal void Select(RunState run)
        {
            var selected = Offered!;
            var synchronizer = RunManager.Instance.PlayerChoiceSynchronizer;
            uint id = synchronizer.ChoiceIds[run.GetPlayerSlotIndex(selected.Owner)] - 1;
            // The test local selector supplies the same sync the native UI submits.
            synchronizer.SyncLocalChoice(selected.Owner, id, PlayerChoiceResult.FromIndex(selectedIndex));
            choice.SetResult([selected]);
        }

        internal void Cancel() => choice.TrySetCanceled();

        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives) =>
            throw new InvalidOperationException("Expected a native opening choice.");
    }
#endif

    internal static async Task Run(JigsawNativeSession session, JigsawScreen view, string role, string directory,
        int target, Func<Func<bool>, string, Task> until, string mode)
    {
        var harmony = new Harmony("AllMatters.Tests.EntryTransitionNetwork");
        var transition = new NTransition { Material = new ShaderMaterial() };
        transition.AddChild(new Control { Name = "GradientTransition" });
        transition.AddChild(new Control { Name = "SimpleTransition" });
#if !STS2_PUBLIC
        var opening = mode == "combat" ? new OpeningChoice() : null;
        using var suspendTestSelector = opening == null ? null : CardSelectorStack.SuspendTestSelector();
        using var localSelector = opening == null ? null : RunManager.Instance.CardSelectorStack.UseLocalSelector(opening);
        var inProgress = AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress));
        var mockInProgress = AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix");
        var testHost = new Harmony("AllMatters.TestHost");
        if (opening != null)
        {
            testHost.Unpatch(inProgress, mockInProgress);
            session.Native.SetDominion(ModelDb.Dominion<KnowledgeDemonDominion>());
        }
#endif
        void Ready()
        {
            File.WriteAllText(Path.Combine(directory, role + ".room-ready"), "ready");
            if (mode == "takeover" && role == "host") _ = transition.RoomFadeIn();
        }
        try
        {
            reveals = nativeReveals = 0;
            bool failing = mode is "failure" or "earlyfailure" or "preparation";
            delayRoom = !failing;
            hostFade = role == "host" && mode is "earlyfailure" or "preparation" ? new(TaskCreationOptions.RunContinuationsAsynchronously) : null;
            slowFade = (mode == "earlyfailure" ? role != "host" : mode != "preparation" && role == "3") ? new(TaskCreationOptions.RunContinuationsAsynchronously) : null;
            failPreparation = mode == "preparation" && role == "host";
            nativeEntryMarker = Path.Combine(directory, role + ".native-starting");
            fadeMarker = Path.Combine(directory, role + ".fade-starting");
            failureMarker = Path.Combine(directory, role + ".failure-accepted");
            AccessTools.Property(typeof(NGame), nameof(NGame.Transition)).SetValue(NGame.Instance, transition);
            AccessTools.Property(typeof(NSceneContainer), nameof(NSceneContainer.CurrentScene)).SetValue(NGame.Instance!.RootSceneContainer, null);
            await HeadlessUiHost.Mount(transition);
            harmony.Patch(AccessTools.Method(typeof(NTransition), nameof(NTransition.RoomFadeOut)), prefix: new HarmonyMethod(typeof(JigsawEntryTransitionNetworkProbe), nameof(FadeOut)));
            harmony.Patch(AccessTools.Method(typeof(NTransition), nameof(NTransition.RoomFadeIn)), prefix: new HarmonyMethod(typeof(JigsawEntryTransitionNetworkProbe), nameof(FadeIn)));
            harmony.Patch(AccessTools.Method(typeof(RunManager), nameof(RunManager.EnterMapPointInternal)), prefix: new HarmonyMethod(typeof(JigsawEntryTransitionNetworkProbe), nameof(NativeStarting)));
            if (mode == "combat")
                harmony.Patch(AccessTools.Method(typeof(RunManager), nameof(RunManager.EnterMapPointInternal)), prefix: new HarmonyMethod(typeof(JigsawEntryTransitionNetworkProbe), nameof(EnterCombat)));
            if (mode == "preparation")
                harmony.Patch(AccessTools.Method(typeof(JigsawEntryProjection), nameof(JigsawEntryProjection.Append)), prefix: new HarmonyMethod(typeof(JigsawEntryTransitionNetworkProbe), nameof(PrepareProjection)));
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
            if (mode is "earlyfailure" or "preparation")
            {
                if (hostFade != null)
                {
                    await until(() => new[] { "host", "2", "3" }.All(peer => File.Exists(Path.Combine(directory, peer + ".fade-starting"))),
                        "all endpoint fades start before releasing the host fade");
                    if (mode == "preparation")
                        hostFade.SetResult(true);
                    else
                        hostFade.SetException(new IOException("Injected host fade failure while peers are fading."));
                }
                await until(() => File.Exists(failureMarker!), "reliable EntryFailed is applied while other endpoints still await fade completion");
                if (mode == "earlyfailure" && role != "host")
                    await until(() => session.EntryRecoveryTask?.IsCompleted == true && reveals == 1,
                        "a shared failure reveals the recovery map even when the old local fade never completes");
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
                if (mode == "combat" && session.Native.CurrentRoom is not CombatRoom)
                    throw new InvalidOperationException($"The fixture must enter a native combat room, got {session.Native.CurrentRoom?.GetType().Name}.");
#if !STS2_PUBLIC
                if (opening != null && role == "host")
                {
                    var held = System.Diagnostics.Stopwatch.StartNew();
                    await until(() => opening.Offered != null || held.Elapsed >= TimeSpan.FromSeconds(2),
                        "give the native opening choice time to run before the slow endpoint reports readiness");
                    if (opening.Offered != null)
                        throw new InvalidOperationException("The native opening choice reached the host while the room was still covered.");
                }
#endif
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
                if (File.Exists(nativeEntryMarker))
                    throw new InvalidOperationException("A failed preparation or fade must never send an endpoint into native room sync.");
            }
            else if (session.Map!.PendingEntry != null || session.Map.Steps != 1 || view.Visible || reveals != 1)
                throw new InvalidOperationException("All endpoints must confirm once and reveal exactly once after the real slow room readiness.");
#if !STS2_PUBLIC
            if (opening != null)
            {
                await until(() => opening.Offered != null, "native Knowledge Demon Dominion reaches the local player choice");
                File.WriteAllText(Path.Combine(directory, role + ".opening-choice"), "waiting");
                await until(() => new[] { "host", "2", "3" }.All(peer => File.Exists(Path.Combine(directory, peer + ".opening-choice"))),
                    "all endpoints can reach their opening choices under the shared room transition");
                opening.Select(session.Native);
                if (role == "host")
                    await until(() => session.Native.Players.All(player => player.PlayerCombatState?.Hand.Cards.Count > 0),
                        "the host receives every opening choice and draws the native hands");
            }
#endif
#if STS2_PUBLIC
            if (nativeReveals != 0)
                throw new InvalidOperationException("A committed ordinary native entry must still defer its private reveal after another visual caller took ownership.");
#endif
            Console.WriteLine($"TRANSITION ENET mode={mode} role={role} steps={session.Map.Steps} reveals={reveals}");
            File.WriteAllText(Path.Combine(directory, role + ".transition-checked"), "checked");
            await until(() => new[] { "host", "2", "3" }.All(peer => File.Exists(Path.Combine(directory, peer + ".transition-checked"))), "all endpoint transition assertions");
            if (failing)
            {
                slowFade?.TrySetResult(true);
                slowFade = null; // The injected animation fault affects this one attempt.
                hostFade = null;
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
#if !STS2_PUBLIC
            opening?.Cancel();
            if (opening != null)
                testHost.Patch(inProgress, prefix: new HarmonyMethod(mockInProgress));
#endif
            RunManager.Instance.RoomEntered -= Ready;
            slowFade?.TrySetResult(true); hostFade?.TrySetResult(true);
            harmony.UnpatchAll(harmony.Id); transition.Free();
        }
    }
}
