namespace AllMatters.Jigsaw;

/// <summary>Authoritative accepted-order map state. UI and native adapters submit intentions here.</summary>
public sealed class JigsawRun
{
    private readonly Dictionary<int, JigsawPlacement> placements = [];
    private readonly List<JigsawEdge> external = [];
    private readonly Dictionary<ulong, JigsawHolding> holdings = [];
    private readonly Dictionary<ulong, int> endpoints = [];
    private readonly HashSet<ulong> blankClicks = [];
    private readonly Dictionary<ulong, EndChoice> choices = [];
    private readonly HashSet<ulong> players;
    private readonly HashSet<ulong> originalPlayers;
    public IReadOnlyCollection<ulong> OriginalPlayers => originalPlayers.ToArray();
    public IReadOnlyCollection<ulong> ActivePlayers => players.ToArray();
    private readonly HashSet<ulong> ancientCompleted = [], continued = [];
    private readonly HashSet<int> visited = [0], completed = [0];
    private readonly Dictionary<(int nodeId, ulong playerId), JigsawShopInventory> shopInventories = [];
    private (int nodeId, ulong playerId, JigsawShopInventory? checkpoint)? shopTransactionCheckpoint;
    private long nextEntry;
    private long? currentEntry;
    public JigsawLayout? AutomaticWitness { get; private set; }
    public JigsawAutomaticOperation? AutomaticOperation { get; private set; }
    private ulong generationRandomState = 1;
    public IReadOnlyList<JigsawPiece> Pool { get; }
    public IReadOnlyList<JigsawPlacement> Placements => placements.Values.ToArray();
    public IReadOnlyDictionary<ulong, JigsawHolding> Holdings => new System.Collections.ObjectModel.ReadOnlyDictionary<ulong, JigsawHolding>(holdings);
    public IReadOnlyDictionary<ulong, int> SelectedEndpoints => new System.Collections.ObjectModel.ReadOnlyDictionary<ulong, int>(endpoints);
    public IReadOnlyDictionary<ulong, EndChoice> Choices => new System.Collections.ObjectModel.ReadOnlyDictionary<ulong, EndChoice>(choices);
    public JigsawPhase Phase { get; private set; } = JigsawPhase.Editing;
    public long EditRevision { get; private set; }
    public int Budget { get; }
    public int Steps { get; private set; }
    public int FirstProgress { get; private set; }
    public int RemainingSteps => Math.Max(0, Budget - Steps);
    public int? CurrentNode { get; private set; }
    public JigsawEnterAttempt? PendingEntry { get; private set; }
    public IReadOnlyCollection<int> Visited => visited.ToArray();
    public IReadOnlyCollection<int> Completed => completed.ToArray();
    public IReadOnlyList<JigsawNode> Nodes => BuildNodes(placements);
    public IReadOnlyList<JigsawEdge> Edges => BuildEdges(placements, external);
    public static JigsawRun Create(int seed, int budget = 15, bool doubleBoss = false, IEnumerable<ulong>? players = null, IEnumerable<ulong>? originalPlayers = null)
    {
        var run = new JigsawRun(JigsawPool.Create(seed, doubleBoss), budget, players, originalPlayers);
        run.generationRandomState = (ulong)(uint)seed + 1;
        run.AutomaticWitness = JigsawAutomatic.Witness(run);
        return run;
    }
    public JigsawRun(IEnumerable<JigsawPiece> pool, int budget = 15, IEnumerable<ulong>? players = null)
        : this(pool, budget, players, null) { }
    private JigsawRun(IEnumerable<JigsawPiece> pool, int budget, IEnumerable<ulong>? players, IEnumerable<ulong>? originalPlayers)
    {
        if (budget < 0)
            throw new ArgumentOutOfRangeException(nameof(budget));
        Pool = Array.AsReadOnly(pool.Select(p => p with { Nodes = Array.AsReadOnly(p.Nodes.ToArray()), InternalEdges = Array.AsReadOnly(p.InternalEdges.ToArray()) }).ToArray());
        if (Pool.Select(p => p.Id).Distinct().Count() != Pool.Count || Pool.SelectMany(p => p.Nodes).Select(n => n.Id).Distinct().Count() != Pool.Sum(p => p.Nodes.Count) || Pool.SelectMany(p => p.Nodes).Any(n => n.Id == 0))
            throw new ArgumentException("Piece and node IDs must be unique; zero belongs to Ancient.");
        Budget = budget;
        this.players = (players ?? [1UL]).ToHashSet();
        this.originalPlayers = (originalPlayers ?? this.players).ToHashSet();
        if (this.players.Count == 0 || !this.players.IsSubsetOf(this.originalPlayers))
            throw new ArgumentException("Active players must be a non-empty subset of the original roster.", nameof(players));
    }
    private List<JigsawNode> BuildNodes(IReadOnlyDictionary<int, JigsawPlacement> layout)
    {
        var result = new List<JigsawNode> { new(0, null, JigsawRoom.Ancient, new(0, 0)) };
        foreach (var p in Pool.Where(p => layout.ContainsKey(p.Id)))
        {
            var placement = layout[p.Id];
            foreach (var n in p.Nodes)
            {
                var point = n.Offset;
                for (int r = 0; r < placement.Rotation; r++)
                    point = point.Rotate();
                result.Add(new(n.Id, p.Id, n.Room, point + placement.Anchor));
            }
        }
        return result;
    }
    private List<JigsawEdge> BuildEdges(IReadOnlyDictionary<int, JigsawPlacement> layout, IEnumerable<JigsawEdge> links) => Pool.Where(p => layout.ContainsKey(p.Id)).SelectMany(p => p.InternalEdges).Concat(links).ToList();
    private bool Editable(ulong player) => Phase == JigsawPhase.Editing && players.Contains(player) && !choices.ContainsKey(player);
    private bool Locked(int piece) => holdings.Values.Any(h => h.PieceId == piece || placements.ContainsKey(h.PieceId) && external.Any(e => PieceOf(e.First) == h.PieceId && PieceOf(e.Second) == piece || PieceOf(e.Second) == h.PieceId && PieceOf(e.First) == piece));
    private int? PieceOf(int node) => node == 0 ? null : Pool.FirstOrDefault(p => p.Nodes.Any(n => n.Id == node))?.Id;
    private bool EdgeLocked(JigsawEdge e) => holdings.Values.Any(h => PieceOf(e.First) == h.PieceId || PieceOf(e.Second) == h.PieceId);
    public JigsawResult Take(ulong player, int piece)
    {
        if (!Editable(player) || holdings.ContainsKey(player) || Locked(piece) || !Pool.Any(p => p.Id == piece))
            return JigsawResult.Reject(JigsawFailure.PieceUnavailable);
        holdings[player] = new(piece, placements.GetValueOrDefault(piece)?.Rotation ?? 0);
        ClearEndpoint(player);
        return JigsawResult.Ok;
    }
    public JigsawResult Rotate(ulong player)
    {
        if (!Editable(player) || !holdings.TryGetValue(player, out var h))
            return JigsawResult.Reject(JigsawFailure.NoHeldPiece);
        holdings[player] = h with { Rotation = (h.Rotation + 1) % 4 };
        return JigsawResult.Ok;
    }
    public JigsawResult Drop(ulong player, GridPoint anchor)
    {
        if (!Editable(player) || !holdings.TryGetValue(player, out var h))
            return JigsawResult.Reject(JigsawFailure.NoHeldPiece);
        var proposed = new JigsawPlacement(h.PieceId, anchor, h.Rotation);
        if (placements.GetValueOrDefault(h.PieceId) == proposed)
            return Cancel(player);
        var layout = new Dictionary<int, JigsawPlacement>(placements) { [h.PieceId] = proposed };
        var links = external.Where(e => PieceOf(e.First) != h.PieceId && PieceOf(e.Second) != h.PieceId).ToList();
        var check = JigsawGeometry.Validate(BuildNodes(layout), BuildEdges(layout, links));
        if (!check.Success)
            return check;
        placements[h.PieceId] = proposed;
        external.Clear();
        external.AddRange(links);
        holdings.Remove(player);
        return JigsawResult.Ok;
    }
    public JigsawResult Cancel(ulong player)
    {
        if (!players.Contains(player))
            return JigsawResult.Reject(JigsawFailure.UnknownPlayer);
        holdings.Remove(player);
        ClearEndpoint(player);
        return JigsawResult.Ok;
    }
    public JigsawResult ReturnToPool(ulong player, int piece)
    {
        if (!Editable(player) || holdings.TryGetValue(player, out var current) && current.PieceId != piece || !Pool.Any(p => p.Id == piece) || Locked(piece) && (!holdings.TryGetValue(player, out var h) || h.PieceId != piece))
            return JigsawResult.Reject(JigsawFailure.PieceUnavailable);
        RemovePiece(piece);
        holdings.Remove(player);
        return JigsawResult.Ok;
    }
    private void RemovePiece(int piece)
    {
        placements.Remove(piece);
        external.RemoveAll(e => PieceOf(e.First) == piece || PieceOf(e.Second) == piece);
        foreach (var player in endpoints.Where(e => PieceOf(e.Value) == piece).Select(e => e.Key).ToArray())
            ClearEndpoint(player);
    }
    public JigsawResult AddEdge(ulong player, int first, int second)
    {
        if (!Editable(player) || holdings.ContainsKey(player))
            return JigsawResult.Reject(JigsawFailure.CannotEditPaths);
        var edge = new JigsawEdge(first, second);
        if (EdgeLocked(edge))
            return JigsawResult.Reject(JigsawFailure.ReservedPath);
        if (external.Any(e => e.Connects(first, second)))
            return JigsawResult.Ok;
        if (Edges.Any(e => e.Connects(first, second)))
            return JigsawResult.Reject(JigsawFailure.InternalPathFixed);
        var check = JigsawGeometry.Validate(Nodes, Edges.Append(edge).ToArray());
        if (check.Success)
            external.Add(edge);
        return check;
    }
    public JigsawResult RemoveEdge(ulong player, int first, int second)
    {
        if (!Editable(player) || holdings.ContainsKey(player))
            return JigsawResult.Reject(JigsawFailure.CannotEditPaths);
        if (Edges.Any(e => e.Internal && e.Connects(first, second)))
            return JigsawResult.Reject(JigsawFailure.InternalPathFixed);
        if (EdgeLocked(new(first, second)))
            return JigsawResult.Reject(JigsawFailure.ReservedPath);
        external.RemoveAll(e => e.Connects(first, second));
        return JigsawResult.Ok;
    }
    public void ClearEndpoint(ulong player) { endpoints.Remove(player); blankClicks.Remove(player); }
    public JigsawResult SelectEndpoint(ulong player, int? node, bool? desiredAdd = null)
    {
        if (!Editable(player) || holdings.ContainsKey(player))
            return JigsawResult.Reject(JigsawFailure.CannotSelectPaths);
        if (node == null)
        {
            if (!blankClicks.Add(player))
                ClearEndpoint(player);
            return JigsawResult.Ok;
        }
        blankClicks.Remove(player);
        if (!Nodes.Any(n => n.Id == node))
            return JigsawResult.Reject(JigsawFailure.UnknownNode);
        if (!endpoints.TryGetValue(player, out int first))
        { endpoints[player] = node.Value; return JigsawResult.Ok; }
        if (first == node)
        { ClearEndpoint(player); return JigsawResult.Ok; }
        var check = (desiredAdd ?? !external.Any(e => e.Connects(first, node.Value))) ? AddEdge(player, first, node.Value) : RemoveEdge(player, first, node.Value);
        if (check.Success)
            ClearEndpoint(player);
        return check;
    }
    public JigsawResult ChooseEnd(ulong player, EndChoice? choice)
    {
        if (Phase != JigsawPhase.Editing || !players.Contains(player) || holdings.ContainsKey(player) || endpoints.ContainsKey(player))
            return JigsawResult.Reject(JigsawFailure.EditIncomplete);
        if (choice == null || choices.GetValueOrDefault(player) == choice && choices.ContainsKey(player))
            choices.Remove(player);
        else
            choices[player] = choice.Value;
        AuthorizeIfUnanimous();
        return JigsawResult.Ok;
    }
    public void Disconnect(ulong player)
    {
        holdings.Remove(player);
        ClearEndpoint(player);
        choices.Remove(player);
        players.Remove(player);
        if (Phase == JigsawPhase.Editing)
            AuthorizeIfUnanimous();
        if (Phase == JigsawPhase.Ancient && players.Count > 0 && players.All(ancientCompleted.Contains) && players.All(continued.Contains))
            Phase = JigsawPhase.Exploring;
    }
    public JigsawResult Reconnect(ulong player)
    {
        if (!originalPlayers.Contains(player))
            return JigsawResult.Reject(JigsawFailure.UnknownPlayer);
        players.Add(player);
        return JigsawResult.Ok;
    }
    private void AuthorizeIfUnanimous()
    {
        if (players.Count > 0 && choices.Count == players.Count && choices.Values.Distinct().Count() == 1)
        {
            EditRevision++;
            Phase = choices.Values.First() == EndChoice.Manual ? JigsawPhase.Inspecting : JigsawPhase.Generating;
            if (Phase == JigsawPhase.Inspecting)
                AutomaticOperation = null;
            if (Phase == JigsawPhase.Generating)
            {
                ulong initial = generationRandomState;
                var order = Pool.Select(p => p.Id).Order().ToArray();
                for (int i = order.Length - 1; i > 0; i--)
                {
                    generationRandomState ^= generationRandomState << 13;
                    generationRandomState ^= generationRandomState >> 7;
                    generationRandomState ^= generationRandomState << 17;
                    int j = (int)(generationRandomState % (ulong)(i + 1));
                    (order[i], order[j]) = (order[j], order[i]);
                }
                AutomaticOperation = new(JigsawAutomatic.Algorithm, 2000, order, initial);
            }
        }
    }
    public JigsawResult FinishAutomatic(JigsawAutomaticOperation operation, JigsawLayout? result)
    {
        if (Phase != JigsawPhase.Generating || !ReferenceEquals(operation, AutomaticOperation))
            return JigsawResult.Reject("旧自动任务不能提交。");
        var check = result == null ? JigsawResult.Reject("预算内未找到合格布局；不能判断无解。") : JigsawAutomatic.Validate(this, result);
        if (check.Success)
        {
            placements.Clear();
            foreach (var p in result!.Placements)
                placements.Add(p.PieceId, p);
            external.Clear();
            external.AddRange(result.Edges);
        }
        choices.Clear();
        holdings.Clear();
        endpoints.Clear();
        blankClicks.Clear();
        Phase = check.Success ? JigsawPhase.Ancient : JigsawPhase.Editing;
        return check;
    }
    public JigsawResult Inspect()
    {
        if (Phase != JigsawPhase.Inspecting)
            return JigsawResult.Reject(JigsawFailure.InspectionUnauthorized);
        var reachable = Reachable(0, Edges);
        foreach (int piece in placements.Keys.Where(p => Pool.Single(v => v.Id == p).Nodes.All(n => !reachable.Contains(n.Id))).ToArray())
            RemovePiece(piece);
        var check = JigsawGeometry.Validate(Nodes, Edges);
        var bosses = Nodes.Where(n => n.Room == JigsawRoom.Boss).ToArray();
        var boss = bosses.FirstOrDefault();
        int distance = boss == null ? int.MaxValue : Distance(0, boss.Id, Edges);
        if (check.Success && (bosses.Length != 1 || distance == int.MaxValue))
            check = JigsawResult.Reject(JigsawFailure.BossUnreachable);
        else if (check.Success && distance > Budget)
            check = JigsawResult.Reject(JigsawFailure.BossOverBudget);
        if (check.Success && Nodes.Any(n => !Reachable(0, Edges).Contains(n.Id)))
            check = JigsawResult.Reject(JigsawFailure.DisconnectedLayout);
        if (check.Success && boss != null)
        {
            var second = Nodes.Where(n => n.Room == JigsawRoom.SecondBoss).ToArray();
            var bossEdges = Edges.Where(e => e.Touches(boss.Id)).ToArray();
            bool terminal = second.Length == 0 ? bossEdges.Length == 1 : second.Length == 1 && bossEdges.Length == 2 && Edges.Count(e => e.Touches(second[0].Id)) == 1 && bossEdges.Any(e => e.Touches(second[0].Id));
            terminal &= bossEdges.All(e => e.Internal) && bossEdges.Any(e => Nodes.Single(n => n.Id == (e.First == boss.Id ? e.Second : e.First)).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss));
            if (!terminal)
                check = JigsawResult.Reject(JigsawFailure.BossNotTerminal);
        }
        if (check.Success)
        {
            var ordinaryReachable = Reachable(0, Edges, Nodes.Where(n => n.Room is JigsawRoom.Boss or JigsawRoom.SecondBoss).Select(n => n.Id).ToHashSet());
            if (Nodes.Any(n => n.Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss) && !ordinaryReachable.Contains(n.Id)))
                check = JigsawResult.Reject(JigsawFailure.BossBlocksOrdinaryRooms);
        }
        choices.Clear();
        Phase = check.Success ? JigsawPhase.Ancient : JigsawPhase.Editing;
        return check;
    }
    private static HashSet<int> Reachable(int start, IEnumerable<JigsawEdge> edges, HashSet<int>? blocked = null)
    {
        var result = new HashSet<int> { start };
        var queue = new Queue<int>();
        queue.Enqueue(start);
        var links = edges.ToArray();
        while (queue.TryDequeue(out int node))
            foreach (var e in links.Where(e => e.Touches(node)))
            { int next = e.First == node ? e.Second : e.First; if (blocked?.Contains(next) != true && result.Add(next)) queue.Enqueue(next); }
        return result;
    }
    private static int Distance(int start, int target, IEnumerable<JigsawEdge> edges)
    {
        var seen = new HashSet<int> { start };
        var queue = new Queue<(int Node, int Distance)>();
        queue.Enqueue((start, 0));
        var links = edges.ToArray();
        while (queue.TryDequeue(out var current))
        { if (current.Node == target) return current.Distance; foreach (var e in links.Where(e => e.Touches(current.Node))) { int next = e.First == current.Node ? e.Second : e.First; if (seen.Add(next)) queue.Enqueue((next, current.Distance + 1)); } }
        return int.MaxValue;
    }
    public JigsawResult ResetLayout()
    {
        if (Phase != JigsawPhase.Editing)
            return JigsawResult.Reject(JigsawFailure.NotEditable);
        EditRevision++;
        placements.Clear();
        external.Clear();
        holdings.Clear();
        endpoints.Clear();
        blankClicks.Clear();
        choices.Clear();
        return JigsawResult.Ok;
    }
    public JigsawResult CompleteAncient(ulong player)
    {
        if (Phase != JigsawPhase.Ancient || !players.Contains(player))
            return JigsawResult.Reject(JigsawFailure.NotAncient);
        ancientCompleted.Add(player);
        return JigsawResult.Ok;
    }
    internal JigsawResult RecordNativeAncientCompletion(ulong player)
    {
        if (Phase is not (JigsawPhase.Ancient or JigsawPhase.Exploring) || !originalPlayers.Contains(player))
            return JigsawResult.Reject(JigsawFailure.NotAncient);
        ancientCompleted.Add(player);
        return JigsawResult.Ok;
    }
    public JigsawResult Continue(ulong player)
    {
        if (Phase != JigsawPhase.Ancient || !players.Contains(player) || !players.All(ancientCompleted.Contains))
            return JigsawResult.Reject(JigsawFailure.AncientIncomplete);
        continued.Add(player);
        if (players.All(continued.Contains))
            Phase = JigsawPhase.Exploring;
        return JigsawResult.Ok;
    }
    public IReadOnlyCollection<int> AvailableDestinations
    {
        get
        {
            if (Phase != JigsawPhase.Exploring || CurrentNode != null || PendingEntry != null)
                return [];
            var candidates = Edges.SelectMany(e => new[] { (From: e.First, To: e.Second), (From: e.Second, To: e.First) })
                .Where(e => completed.Contains(e.From) && (!visited.Contains(e.To) || Nodes.Single(n => n.Id == e.To).Room == JigsawRoom.Shop) && e.To != 0)
                .Select(e => e.To).Distinct().ToArray();

            // Dual boss sequence: second boss only available after first boss completed
            var secondBoss = Nodes.FirstOrDefault(n => n.Room == JigsawRoom.SecondBoss);
            var firstBoss = Nodes.FirstOrDefault(n => n.Room == JigsawRoom.Boss);
            if (secondBoss != null && firstBoss != null && !completed.Contains(firstBoss.Id))
                candidates = candidates.Where(id => id != secondBoss.Id).ToArray();

            return candidates;
        }
    }
    public bool IsEndgame
    {
        get
        {
            if (CurrentNode == null)
                return false;
            var currentRoom = Nodes.Single(n => n.Id == CurrentNode.Value).Room;
            var hasSecondBoss = Nodes.Any(n => n.Room == JigsawRoom.SecondBoss);
            // Endgame is at Boss (if no second boss) or at SecondBoss (if dual boss layout)
            return hasSecondBoss ? currentRoom == JigsawRoom.SecondBoss : currentRoom == JigsawRoom.Boss;
        }
    }
    public JigsawEnterAttempt? BeginEnter(int node)
    {
        if (!AvailableDestinations.Contains(node))
            return null;
        PendingEntry = new(++nextEntry, node);
        return PendingEntry;
    }
    public JigsawResult AbandonEntry(long attempt)
    {
        if (PendingEntry?.Id != attempt)
            return JigsawResult.Reject(JigsawFailure.StaleEntry);
        PendingEntry = null;
        return JigsawResult.Ok;
    }
    public JigsawResult ConfirmEnter(long attempt)
    {
        if (PendingEntry?.Id != attempt || Phase != JigsawPhase.Exploring)
            return JigsawResult.Reject(JigsawFailure.StaleEntry);
        CurrentNode = PendingEntry.NodeId;
        currentEntry = attempt;
        PendingEntry = null;
        Steps++;
        if (visited.Add(CurrentNode.Value))
            FirstProgress++;
        return JigsawResult.Ok;
    }
    public JigsawResult CompleteRoom(long attempt)
    {
        if (Phase != JigsawPhase.Exploring || CurrentNode == null || currentEntry != attempt)
            return JigsawResult.Reject(JigsawFailure.StaleCompletion);
        completed.Add(CurrentNode.Value);
        CurrentNode = null;
        currentEntry = null;
        // Commit any pending shop transaction on room completion
        if (shopTransactionCheckpoint != null)
            CommitShopTransaction();
        return JigsawResult.Ok;
    }
    // T05: Shop inventory tracking
    public void RecordShopInventory(int nodeId, ulong playerId, JigsawShopInventory inventory)
    {
        shopInventories[(nodeId, playerId)] = inventory;
    }
    public JigsawShopInventory? GetShopInventory(int nodeId, ulong playerId)
    {
        return shopInventories.TryGetValue((nodeId, playerId), out var inventory) ? inventory : null;
    }
    public void CreateShopTransactionCheckpoint(int nodeId, ulong playerId)
    {
        var existing = GetShopInventory(nodeId, playerId);
        shopTransactionCheckpoint = (nodeId, playerId, existing);
    }
    public void CommitShopTransaction()
    {
        shopTransactionCheckpoint = null;
    }
    public void RollbackShopTransaction()
    {
        if (shopTransactionCheckpoint is var (nodeId, playerId, checkpoint))
        {
            if (checkpoint != null)
                shopInventories[(nodeId, playerId)] = checkpoint;
            else
                shopInventories.Remove((nodeId, playerId));
        }
        shopTransactionCheckpoint = null;
    }
    // Legacy method for backward compatibility with tests
    public void RecordShopState(int nodeId, ulong playerId, object state)
    {
        // Convert generic object to inventory if needed
        if (state is JigsawShopInventory inventory)
            RecordShopInventory(nodeId, playerId, inventory);
    }
    public object? GetShopState(int nodeId, ulong playerId)
    {
        return GetShopInventory(nodeId, playerId);
    }
    // T06: Act transition
    public JigsawResult TransitionToNextAct()
    {
        if (Phase != JigsawPhase.Exploring)
            return JigsawResult.Reject("Act transition only allowed during exploration.");
        Steps = 0;
        FirstProgress = 0;
        visited.Clear();
        visited.Add(0);
        completed.Clear();
        completed.Add(0);
        CurrentNode = null;
        currentEntry = null;
        PendingEntry = null;
        ancientCompleted.Clear();
        continued.Clear();
        Phase = JigsawPhase.Ancient;
        ClearHuntingData();
        return JigsawResult.Ok;
    }
    /// <summary>Stub for hunting data cleanup. To be implemented in #84.</summary>
    private void ClearHuntingData()
    {
        // TODO #84: Clear hunting state when transitioning between acts
    }
    /// <summary>Stub for mask restoration before endgame rewards. To be implemented in #09.</summary>
    public void RestoreMaskForEndgame()
    {
        // TODO #09: Restore full mask points before final boss reward
    }
    public JigsawSave CreateSave() => new(Pool.Select(p => p with { Nodes = p.Nodes.ToArray(), InternalEdges = p.InternalEdges.ToArray() }).ToArray(), placements.Values.ToArray(), external.ToArray(), Phase, Budget, players.ToArray(), Steps, FirstProgress, visited.ToArray(), completed.ToArray(), CurrentNode, PendingEntry, nextEntry, ancientCompleted.ToArray()) { OriginalPlayers = originalPlayers.ToArray(), AutomaticWitness = AutomaticWitness is { } witness ? new(witness.Placements.ToArray(), witness.Edges.ToArray()) : null, AutomaticOperation = AutomaticOperation is { } operation ? operation with { CandidateOrder = operation.CandidateOrder.ToArray() } : null, GenerationRandomState = generationRandomState, ShopStates = shopInventories.Count > 0 ? shopInventories.ToDictionary(kv => $"{kv.Key.nodeId}_{kv.Key.playerId}", kv => (object)kv.Value) : null };
    public static JigsawRun Restore(JigsawSave save, IEnumerable<ulong>? activePlayers = null)
    {
        JigsawSaveValidation.Validate(save);
        var run = new JigsawRun(save.Pool, save.Budget, save.OriginalPlayers ?? save.Players) { Phase = save.Phase, Steps = save.Steps, FirstProgress = save.FirstProgress, CurrentNode = save.CurrentNode, PendingEntry = save.PendingEntry, nextEntry = save.NextEntry, currentEntry = save.CurrentNode == null ? null : save.NextEntry };
        run.players.IntersectWith(save.Players);
        foreach (var p in save.Placements)
            run.placements.Add(p.PieceId, p);
        run.external.AddRange(save.ExternalEdges);
        run.generationRandomState = save.GenerationRandomState;
        run.AutomaticWitness = save.AutomaticWitness;
        run.AutomaticOperation = save.AutomaticOperation;
        if (run.AutomaticWitness is { } witness && !JigsawAutomatic.Validate(run, witness).Success)
            throw new ArgumentException("Saved automatic witness is invalid.", nameof(save));
        run.visited.UnionWith(save.Visited);
        run.completed.UnionWith(save.Completed);
        run.ancientCompleted.UnionWith(save.AncientCompleted);
        if (save.ShopStates != null)
        {
            foreach (var kv in save.ShopStates)
            {
                var parts = kv.Key.Split('_');
                if (parts.Length == 2 && int.TryParse(parts[0], out var nodeId) && ulong.TryParse(parts[1], out var playerId))
                {
                    if (kv.Value is JigsawShopInventory inventory)
                        run.shopInventories[(nodeId, playerId)] = inventory;
                    else if (kv.Value is System.Text.Json.JsonElement json && json.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        // Deserialize from JSON if needed
                        var deserialized = System.Text.Json.JsonSerializer.Deserialize<JigsawShopInventory>(json.GetRawText());
                        if (deserialized != null)
                            run.shopInventories[(nodeId, playerId)] = deserialized;
                    }
                }
            }
        }
        var valid = JigsawGeometry.Validate(run.Nodes, run.Edges);
        if (!valid.Success)
            throw new ArgumentException(valid.Reason, nameof(save));
        if (save.Phase is JigsawPhase.Ancient or JigsawPhase.Exploring)
        {
            run.Phase = JigsawPhase.Inspecting;
            var inspected = run.Inspect();
            if (!inspected.Success || run.placements.Count != save.Placements.Length)
                throw new ArgumentException("Finalized checkpoint map is invalid: " + inspected.Reason, nameof(save));
            run.Phase = save.Phase;
            if (run.AutomaticOperation != null && !JigsawAutomatic.Validate(run, new(save.Placements, save.ExternalEdges)).Success)
                throw new ArgumentException("Finalized automatic checkpoint does not satisfy automatic requirements.", nameof(save));
        }
        if (activePlayers != null)
        {
            var connected = activePlayers.ToHashSet();
            if (!connected.IsSubsetOf(run.originalPlayers))
                throw new ArgumentException("A new player cannot replace an original saved character.", nameof(activePlayers));
            if (run.Phase == JigsawPhase.Exploring && !connected.All(run.ancientCompleted.Contains))
                throw new ArgumentException("Returning players must complete Ancient before exploration can resume.", nameof(activePlayers));
            run.players.Clear();
            run.players.UnionWith(connected);
        }
        return run;
    }
}
