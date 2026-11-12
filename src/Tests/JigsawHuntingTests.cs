using AllMatters.Jigsaw;

internal static class JigsawHuntingTests
{
    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static JigsawRun Ready(bool dual = false)
    {
        var map = JigsawRun.Create(75, doubleBoss: dual, players: [1UL, 2UL]);
        foreach (ulong player in map.ActivePlayers) Ok(map.ChooseEnd(player, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        foreach (ulong player in map.ActivePlayers) Ok(map.CompleteAncient(player));
        foreach (ulong player in map.ActivePlayers) Ok(map.Continue(player));
        return map;
    }
    private static Task SafeRoot()
    {
        var map = Ready();
        Ok(map.SetRemainingSteps(1));
        int node = map.AvailableDestinations.First(id => map.Nodes.Single(n => n.Id == id).Room != JigsawRoom.Boss);
        var attempt = map.BeginEnter(node)!;
        Ok(map.ConfirmEnter(attempt.Id));
        Check(map.RemainingSteps == 0 && map.Steps == 1 && !map.HuntingActive && !map.CurrentRoomHunted,
            "The last non-Boss entry is safe and counts exactly once.");
        Ok(map.RecordRoomCompletion(1));
        Check(!map.HuntingActive, "A late teammate keeps the exhausted root safe.");
        Ok(map.RecordRoomCompletion(2));
        Check(map.HuntingActive && !map.CurrentRoomHunted,
            "Hunting starts at all-player completion, before destination voting, without changing the root snapshot.");
        var restored = JigsawRun.Restore(System.Text.Json.JsonSerializer.Deserialize<JigsawSave>(System.Text.Json.JsonSerializer.Serialize(map.CreateSave()))!);
        Check(restored.HuntingActive && !restored.CurrentRoomHunted && restored.RemainingSteps == 0,
            "The completed safe root and active hunting restore as separate facts.");
        Ok(restored.SetRemainingSteps(100));
        Check(restored.HuntingActive && restored.Steps == 1 && restored.FirstProgress == 1,
            "Setting a larger balance preserves hunting, exploration and first progress.");
        Ok(restored.CompleteRoom(attempt.Id));
        int next = restored.AvailableDestinations.First();
        Ok(restored.ConfirmEnter(restored.BeginEnter(next)!.Id));
        Check(restored.CurrentRoomHunted && restored.RemainingSteps == 99 && restored.Steps == 2,
            "Only the next root is punished and the new balance decrements normally.");
        return Task.CompletedTask;
    }
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw hunting safe root waits for every player and restores", SafeRoot),
      ("Jigsaw hunting final step Boss is timely and dual Boss keeps result", TimelyBoss),
      ("Jigsaw hunting zero control balance punishes the next root", ZeroBalance),
      ("Jigsaw hunting zero before first Boss restores and enters punished", ZeroBoss) ];

    private static Task TimelyBoss()
    {
        var map = Ready(true);
        int boss = map.Nodes.Single(n => n.Room == JigsawRoom.Boss).Id;
        var queue = new Queue<int>(); queue.Enqueue(0);
        var previous = new Dictionary<int, int> { [0] = -1 };
        while (queue.TryDequeue(out int current) && !previous.ContainsKey(boss))
            foreach (var edge in map.Edges.Where(e => e.Touches(current)))
            { int next = edge.First == current ? edge.Second : edge.First; if (previous.TryAdd(next, current)) queue.Enqueue(next); }
        var path = new List<int>();
        for (int node = boss; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        foreach (int node in path)
        {
            if (node == boss) Ok(map.SetRemainingSteps(1));
            var entry = map.BeginEnter(node)!; Ok(map.ConfirmEnter(entry.Id));
            if (node != boss) Ok(map.CompleteRoom(entry.Id));
        }
        Check(map.BossReachedOnTime && map.RemainingSteps == 0 && !map.HuntingActive, "The final step into the first Boss is timely.");
        Check(!map.CompleteMapBossVictory(), "The first of two Bosses is not the final map victory.");
        Ok(map.CompleteRoom(map.CreateSave().NextEntry));
        var second = map.BeginEnter(map.Nodes.Single(n => n.Room == JigsawRoom.SecondBoss).Id)!;
        Ok(map.ConfirmEnter(second.Id));
        Check(map.BossReachedOnTime && !map.HuntingActive && !map.CurrentRoomHunted, "The second Boss counts an entry without reevaluating timeout.");
        Ok(map.TransitionToNextAct());
        Check(map.RemainingSteps == map.Budget && !map.HuntingActive && !map.BossReachedOnTime, "The next act resets all hunting facts.");
        return Task.CompletedTask;
    }

    private static Task ZeroBalance()
    {
        var map = Ready();
        Ok(map.SetRemainingSteps(0));
        Check(!map.HuntingActive && map.Steps == 0, "Setting zero is not a hunting switch or a fake entry.");
        var entry = map.BeginEnter(map.AvailableDestinations.First(id => map.Nodes.Single(n => n.Id == id).Room != JigsawRoom.Boss))!;
        Ok(map.ConfirmEnter(entry.Id));
        Check(map.HuntingActive && map.CurrentRoomHunted, "The next root entered with zero balance is punished immediately.");
        Ok(map.RecordRoomCompletion(1));
        map.Disconnect(2);
        Check(map.HuntingActive && map.OriginalPlayers.Count == 2, "A departed laggard no longer blocks completion, without deleting the original recipient.");
        Check(!map.SetRemainingSteps(-1).Success && map.RemainingSteps == 0, "Negative balance is rejected without a partial update.");
        return Task.CompletedTask;
    }

    private static Task ZeroBoss()
    {
        foreach (bool completedRoot in new[] { false, true })
        {
            var map = Ready();
            int boss = map.Nodes.Single(node => node.Room == JigsawRoom.Boss).Id;
            var queue = new Queue<int>(); queue.Enqueue(0);
            var previous = new Dictionary<int, int> { [0] = -1 };
            while (queue.TryDequeue(out int current) && !previous.ContainsKey(boss))
                foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
                { int next = edge.First == current ? edge.Second : edge.First; if (previous.TryAdd(next, current)) queue.Enqueue(next); }
            var path = new List<int>();
            for (int node = boss; node != 0; node = previous[node]) path.Add(node);
            path.Reverse();
            Ok(map.SetRemainingSteps(100));
            foreach (int node in path.Take(path.Count - 1))
            {
                var entry = map.BeginEnter(node)!; Ok(map.ConfirmEnter(entry.Id));
                if (completedRoot && node == path[^2])
                { Ok(map.RecordRoomCompletion(1)); Ok(map.RecordRoomCompletion(2)); }
                else Ok(map.CompleteRoom(entry.Id));
            }
            Ok(map.SetRemainingSteps(0));
            Check(!map.HuntingActive, "The zero command at a completed destination-selection point does not activate hunting.");
            map = JigsawRun.Restore(System.Text.Json.JsonSerializer.Deserialize<JigsawSave>(System.Text.Json.JsonSerializer.Serialize(map.CreateSave()))!);
            if (completedRoot)
            {
                // Completion acknowledgements are transient and are observed again on restore.
                Ok(map.RecordRoomCompletion(1)); Ok(map.RecordRoomCompletion(2));
                Check(!map.HuntingActive, "Re-observing the saved completed root still waits for the next entry.");
                Ok(map.VoteDestination(1, boss)); Ok(map.VoteDestination(2, boss)); Ok(map.FinalizeDestination(boss));
            }
            else Check(map.BeginEnter(boss) != null, "The first map Boss remains an available destination.");
            Ok(map.ConfirmEnter(map.PendingEntry!.Id));
            Check(map.HuntingActive && map.CurrentRoomHunted && !map.BossReachedOnTime && map.RemainingSteps == 0,
                "Both saved selection representations enter the first Boss punished without a timely arrival.");
        }
        return Task.CompletedTask;
    }
}
