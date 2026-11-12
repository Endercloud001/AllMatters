using AllMatters.Jigsaw;

internal static class JigsawExplorationTests
{
    private static void Check(bool condition, string message) => NativeEventTests.Check(condition, message);
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);

    private static JigsawRun Ready()
    {
        var run = JigsawRun.Create(75, players: [1UL, 2UL, 3UL]);
        Ok(run.ChooseEnd(1, EndChoice.Automatic));
        Ok(run.ChooseEnd(2, EndChoice.Automatic));
        Ok(run.ChooseEnd(3, EndChoice.Automatic));
        Ok(run.FinishAutomatic(run.AutomaticOperation!, JigsawAutomatic.Solve(run, 0)));
        foreach (ulong player in run.ActivePlayers)
            Ok(run.CompleteAncient(player));
        foreach (ulong player in run.ActivePlayers)
            Ok(run.Continue(player));
        return run;
    }

    internal static Task Ballots()
    {
        var run = JigsawRun.Restore(Ready().CreateSave() with { DestinationRandomState = 1 });
        var destinations = run.AvailableDestinations.Order().ToArray();
        Check(destinations.Length >= 2, "The generated witness offers distinct first destinations.");
        Ok(run.VoteDestination(1, destinations[0]));
        Ok(run.VoteDestination(1, destinations[0]));
        Ok(run.VoteDestination(2, destinations[0]));
        Check(run.DestinationVotes.Count == 2 && run.ProposedDestination == null,
            "Repeated clicks keep one ballot and a missing player prevents entry.");
        Check(!run.FinalizeDestination(destinations[0]).Success && run.PendingEntry == null,
            "A Host result cannot finalize while an active player has not voted.");
        Ok(run.VoteDestination(3, destinations[1]));
        int selected = run.ProposedDestination!.Value;
        Check(destinations.Contains(selected), "All ballots choose exactly one of the voted nodes.");
        Check(run.ProposedDestination == selected && run.CreateSave().DestinationRandomState == 1,
            "Observing the drawn candidate neither rerolls it nor consumes the saved random stream.");
        Check(!run.FinalizeDestination(destinations.First(node => node != selected)).Success && run.PendingEntry == null,
            "A forged target cannot replace the one result drawn from the players' ballots.");
        Ok(run.FinalizeDestination(selected));
        Check(run.PendingEntry?.NodeId == selected && run.Steps == 0 && run.DestinationVotes.Count == 0,
            "Finalization records one retryable target without counting entry or retaining ballots.");
        Check(!run.FinalizeDestination(selected).Success, "A delayed duplicate cannot finalize again.");
        return Task.CompletedTask;
    }

    internal static Task RoomAndRecovery()
    {
        var run = Ready();
        int first = run.AvailableDestinations.First();
        foreach (ulong player in run.ActivePlayers)
            Ok(run.VoteDestination(player, first));
        Ok(run.FinalizeDestination(first));
        var pending = run.PendingEntry!;
        ulong advancedState = run.CreateSave().DestinationRandomState;
        var restoredPending = JigsawRun.Restore(System.Text.Json.JsonSerializer.Deserialize<JigsawSave>(
            System.Text.Json.JsonSerializer.Serialize(run.CreateSave()))!);
        Check(restoredPending.PendingEntry == pending && restoredPending.CreateSave().DestinationRandomState == advancedState &&
            restoredPending.DestinationVotes.Count == 0, "Recovery retains the already drawn target and RNG position without transient ballots.");
        var legacyJson = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(run.CreateSave()))!;
        legacyJson.AsObject().Remove(nameof(JigsawSave.DestinationRandomState));
        var legacyPending = JigsawRun.Restore(System.Text.Json.JsonSerializer.Deserialize<JigsawSave>(legacyJson.ToJsonString())!);
        Check(legacyPending.PendingEntry == pending && legacyPending.CreateSave().DestinationRandomState != 0,
            "An older version-one checkpoint reuses its pending target and receives a valid future draw state.");
        Check(!restoredPending.VoteDestination(1, first).Success, "A pending destination cannot accept a new ballot.");
        Ok(restoredPending.ConfirmEnter(pending.Id));
        Check(restoredPending.Steps == 1, "Only confirmed native entry counts the step.");

        int next = restoredPending.VotingDestinations.First();
        Check(!restoredPending.VoteDestination(1, next).Success, "A player cannot vote before finishing their room flow.");
        Check(!restoredPending.VoteDestination(2, next).Success && restoredPending.DestinationVotes.Count == 0,
            "An unfinished teammate cannot submit a destination through the same domain boundary.");
        Ok(restoredPending.RecordRoomCompletion(1));
        Ok(restoredPending.VoteDestination(1, next));
        Check(restoredPending.ProposedDestination == null && restoredPending.CurrentNode == first,
            "An early ballot waits while teammates finish the actual room.");
        Ok(restoredPending.RecordRoomCompletion(2));
        Ok(restoredPending.VoteDestination(2, next));
        restoredPending.Disconnect(2);
        Check(!restoredPending.DestinationVotes.ContainsKey(2) && restoredPending.DestinationVotes.ContainsKey(1),
            "Disconnect clears only the departed player's ballot.");
        Ok(restoredPending.RecordRoomCompletion(3));
        Ok(restoredPending.VoteDestination(3, next));
        Check(restoredPending.ProposedDestination == next, "Remaining completed players can finalize their votes.");
        var transientRestored = JigsawRun.Restore(restoredPending.CreateSave());
        Check(transientRestored.DestinationVotes.Count == 0 && transientRestored.RoomCompletedPlayers.Count == 0 &&
            transientRestored.CurrentNode == first, "Recovery rechecks personal room completion and votes.");
        Ok(restoredPending.FinalizeDestination(next));
        Check(restoredPending.PendingEntry?.NodeId == next && restoredPending.CurrentNode == null && restoredPending.Steps == 1,
            "Finalizing leaves a retryable target without a second step.");
        return Task.CompletedTask;
    }

    internal static Task ResolvedShop()
    {
        var run = Ready();
        int first = run.AvailableDestinations.First(node => run.ResolvedRoom(node) != JigsawRoom.Shop);
        foreach (ulong player in run.ActivePlayers)
            Ok(run.VoteDestination(player, first));
        Ok(run.FinalizeDestination(first));
        Ok(run.ConfirmEnter(run.PendingEntry!.Id));
        Ok(run.RecordResolvedRoom(first, JigsawRoom.Shop));
        Ok(run.CompleteRoom(run.CreateSave().NextEntry));
        Check(run.AvailableDestinations.Contains(first), "A native MerchantRoom permits a later visit even when its original node was not Shop.");
        var restored = JigsawRun.Restore(run.CreateSave());
        Check(restored.ResolvedRoom(first) == JigsawRoom.Shop && restored.AvailableDestinations.Contains(first),
            "The native shop identity survives recovery and remains a legal destination.");
        return Task.CompletedTask;
    }
}
