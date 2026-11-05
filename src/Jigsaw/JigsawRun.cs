namespace AllMatters.Jigsaw;

/// <summary>Authoritative accepted-order map state. UI and native adapters submit intentions here.</summary>
public sealed class JigsawRun
{
    private readonly Dictionary<int,JigsawPlacement> placements = [];
    private readonly List<JigsawEdge> external = [];
    private readonly Dictionary<ulong,JigsawHolding> holdings = [];
    private readonly Dictionary<ulong,int> endpoints = [];
    private readonly HashSet<ulong> blankClicks = [];
    private readonly Dictionary<ulong,EndChoice> choices = [];
    private readonly HashSet<ulong> players;
    private readonly HashSet<ulong> ancientCompleted = [], continued = [];
    private readonly HashSet<int> visited = [0], completed = [0];
    private long nextEntry;
    private long? currentEntry;
    public IReadOnlyList<JigsawPiece> Pool { get; }
    public IReadOnlyList<JigsawPlacement> Placements => placements.Values.ToArray();
    public IReadOnlyDictionary<ulong,JigsawHolding> Holdings => new System.Collections.ObjectModel.ReadOnlyDictionary<ulong,JigsawHolding>(holdings);
    public IReadOnlyDictionary<ulong,int> SelectedEndpoints => new System.Collections.ObjectModel.ReadOnlyDictionary<ulong,int>(endpoints);
    public IReadOnlyDictionary<ulong,EndChoice> Choices => new System.Collections.ObjectModel.ReadOnlyDictionary<ulong,EndChoice>(choices);
    public JigsawPhase Phase { get; private set; } = JigsawPhase.Editing;
    public int Budget { get; }
    public int Steps { get; private set; }
    public int FirstProgress { get; private set; }
    public int RemainingSteps => Math.Max(0,Budget-Steps);
    public int? CurrentNode { get; private set; }
    public JigsawEnterAttempt? PendingEntry { get; private set; }
    public IReadOnlyCollection<int> Visited => visited.ToArray();
    public IReadOnlyCollection<int> Completed => completed.ToArray();
    public IReadOnlyList<JigsawNode> Nodes => BuildNodes(placements);
    public IReadOnlyList<JigsawEdge> Edges => BuildEdges(placements,external);
    public static JigsawRun Create(int seed, int budget = 15, bool doubleBoss = false, IEnumerable<ulong>? players = null) => new(JigsawPool.Create(seed,doubleBoss),budget,players);
    public JigsawRun(IEnumerable<JigsawPiece> pool, int budget = 15, IEnumerable<ulong>? players = null)
    {
        if (budget < 0) throw new ArgumentOutOfRangeException(nameof(budget));
        Pool = Array.AsReadOnly(pool.Select(p => p with { Nodes = Array.AsReadOnly(p.Nodes.ToArray()), InternalEdges = Array.AsReadOnly(p.InternalEdges.ToArray()) }).ToArray());
        if (Pool.Select(p => p.Id).Distinct().Count() != Pool.Count || Pool.SelectMany(p => p.Nodes).Select(n => n.Id).Distinct().Count() != Pool.Sum(p => p.Nodes.Count) || Pool.SelectMany(p => p.Nodes).Any(n => n.Id == 0)) throw new ArgumentException("Piece and node IDs must be unique; zero belongs to Ancient.");
        Budget = budget; this.players = (players ?? [1UL]).ToHashSet();
    }
    private List<JigsawNode> BuildNodes(IReadOnlyDictionary<int,JigsawPlacement> layout)
    {
        var result = new List<JigsawNode> { new(0,null,JigsawRoom.Ancient,new(0,0)) };
        foreach (var p in Pool.Where(p => layout.ContainsKey(p.Id)))
        {
            var placement = layout[p.Id];
            foreach (var n in p.Nodes)
            {
                var point = n.Offset;
                for (int r = 0; r < placement.Rotation; r++) point = point.Rotate();
                result.Add(new(n.Id,p.Id,n.Room,point+placement.Anchor));
            }
        }
        return result;
    }
    private List<JigsawEdge> BuildEdges(IReadOnlyDictionary<int,JigsawPlacement> layout,IEnumerable<JigsawEdge> links) => Pool.Where(p => layout.ContainsKey(p.Id)).SelectMany(p => p.InternalEdges).Concat(links).ToList();
    private bool Editable(ulong player) => Phase == JigsawPhase.Editing && players.Contains(player) && !choices.ContainsKey(player);
    private bool Locked(int piece) => holdings.Values.Any(h => h.PieceId == piece || placements.ContainsKey(h.PieceId) && external.Any(e => PieceOf(e.First) == h.PieceId && PieceOf(e.Second) == piece || PieceOf(e.Second) == h.PieceId && PieceOf(e.First) == piece));
    private int? PieceOf(int node) => node == 0 ? null : Pool.FirstOrDefault(p => p.Nodes.Any(n => n.Id == node))?.Id;
    private bool EdgeLocked(JigsawEdge e) => holdings.Values.Any(h => PieceOf(e.First) == h.PieceId || PieceOf(e.Second) == h.PieceId);
    public JigsawResult Take(ulong player, int piece)
    {
        if (!Editable(player) || holdings.ContainsKey(player) || Locked(piece) || !Pool.Any(p => p.Id == piece)) return JigsawResult.Reject(JigsawFailure.PieceUnavailable);
        holdings[player] = new(piece,placements.GetValueOrDefault(piece)?.Rotation ?? 0); ClearEndpoint(player); return JigsawResult.Ok;
    }
    public JigsawResult Rotate(ulong player)
    {
        if (!Editable(player) || !holdings.TryGetValue(player,out var h)) return JigsawResult.Reject(JigsawFailure.NoHeldPiece);
        holdings[player] = h with { Rotation = (h.Rotation+1)%4 }; return JigsawResult.Ok;
    }
    public JigsawResult Drop(ulong player,GridPoint anchor)
    {
        if (!Editable(player) || !holdings.TryGetValue(player,out var h)) return JigsawResult.Reject(JigsawFailure.NoHeldPiece);
        var proposed = new JigsawPlacement(h.PieceId,anchor,h.Rotation);
        if (placements.GetValueOrDefault(h.PieceId) == proposed) return Cancel(player);
        var layout = new Dictionary<int,JigsawPlacement>(placements) { [h.PieceId] = proposed };
        var links = external.Where(e => PieceOf(e.First) != h.PieceId && PieceOf(e.Second) != h.PieceId).ToList();
        var check = JigsawGeometry.Validate(BuildNodes(layout),BuildEdges(layout,links));
        if (!check.Success) return check;
        placements[h.PieceId] = proposed; external.Clear(); external.AddRange(links); holdings.Remove(player); return JigsawResult.Ok;
    }
    public JigsawResult Cancel(ulong player)
    {
        if (!players.Contains(player)) return JigsawResult.Reject(JigsawFailure.UnknownPlayer);
        holdings.Remove(player); ClearEndpoint(player); return JigsawResult.Ok;
    }
    public JigsawResult ReturnToPool(ulong player,int piece)
    {
        if (!Editable(player) || holdings.TryGetValue(player,out var current) && current.PieceId != piece || !Pool.Any(p => p.Id == piece) || Locked(piece) && (!holdings.TryGetValue(player,out var h) || h.PieceId != piece)) return JigsawResult.Reject(JigsawFailure.PieceUnavailable);
        RemovePiece(piece); holdings.Remove(player); return JigsawResult.Ok;
    }
    private void RemovePiece(int piece)
    {
        placements.Remove(piece); external.RemoveAll(e => PieceOf(e.First) == piece || PieceOf(e.Second) == piece);
        foreach (var player in endpoints.Where(e => PieceOf(e.Value) == piece).Select(e => e.Key).ToArray()) ClearEndpoint(player);
    }
    public JigsawResult AddEdge(ulong player,int first,int second)
    {
        if (!Editable(player) || holdings.ContainsKey(player)) return JigsawResult.Reject(JigsawFailure.CannotEditPaths);
        var edge = new JigsawEdge(first,second);
        if (EdgeLocked(edge)) return JigsawResult.Reject(JigsawFailure.ReservedPath);
        if (external.Any(e => e.Connects(first,second))) return JigsawResult.Ok;
        if (Edges.Any(e => e.Connects(first,second))) return JigsawResult.Reject(JigsawFailure.InternalPathFixed);
        var check = JigsawGeometry.Validate(Nodes,Edges.Append(edge).ToArray());
        if (check.Success) external.Add(edge); return check;
    }
    public JigsawResult RemoveEdge(ulong player,int first,int second)
    {
        if (!Editable(player) || holdings.ContainsKey(player)) return JigsawResult.Reject(JigsawFailure.CannotEditPaths);
        if (Edges.Any(e => e.Internal && e.Connects(first,second))) return JigsawResult.Reject(JigsawFailure.InternalPathFixed);
        if (EdgeLocked(new(first,second))) return JigsawResult.Reject(JigsawFailure.ReservedPath);
        external.RemoveAll(e => e.Connects(first,second)); return JigsawResult.Ok;
    }
    public void ClearEndpoint(ulong player) { endpoints.Remove(player); blankClicks.Remove(player); }
    public JigsawResult SelectEndpoint(ulong player,int? node)
    {
        if (!Editable(player) || holdings.ContainsKey(player)) return JigsawResult.Reject(JigsawFailure.CannotSelectPaths);
        if (node == null)
        {
            if (!blankClicks.Add(player)) ClearEndpoint(player);
            return JigsawResult.Ok;
        }
        blankClicks.Remove(player);
        if (!Nodes.Any(n => n.Id == node)) return JigsawResult.Reject(JigsawFailure.UnknownNode);
        if (!endpoints.TryGetValue(player,out int first)) { endpoints[player] = node.Value; return JigsawResult.Ok; }
        if (first == node) { ClearEndpoint(player); return JigsawResult.Ok; }
        var check = external.Any(e => e.Connects(first,node.Value)) ? RemoveEdge(player,first,node.Value) : AddEdge(player,first,node.Value);
        if (check.Success) ClearEndpoint(player); return check;
    }
    public JigsawResult ChooseEnd(ulong player,EndChoice? choice)
    {
        if (Phase != JigsawPhase.Editing || !players.Contains(player) || holdings.ContainsKey(player) || endpoints.ContainsKey(player)) return JigsawResult.Reject(JigsawFailure.EditIncomplete);
        if (choice == null || choices.GetValueOrDefault(player) == choice && choices.ContainsKey(player)) choices.Remove(player); else choices[player] = choice.Value;
        if (players.Count > 0 && choices.Count == players.Count && choices.Values.Distinct().Count() == 1)
        {
            Phase = choices.Values.First() == EndChoice.Manual ? JigsawPhase.Inspecting : JigsawPhase.Generating;
        }
        return JigsawResult.Ok;
    }
    public JigsawResult Inspect()
    {
        if (Phase != JigsawPhase.Inspecting) return JigsawResult.Reject(JigsawFailure.InspectionUnauthorized);
        var reachable = Reachable(0,Edges);
        foreach (int piece in placements.Keys.Where(p => Pool.Single(v => v.Id == p).Nodes.All(n => !reachable.Contains(n.Id))).ToArray()) RemovePiece(piece);
        var check = JigsawGeometry.Validate(Nodes,Edges);
        var bosses = Nodes.Where(n => n.Room == JigsawRoom.Boss).ToArray();
        var boss = bosses.FirstOrDefault();
        int distance = boss == null ? int.MaxValue : Distance(0,boss.Id,Edges);
        if (check.Success && (bosses.Length != 1 || distance == int.MaxValue)) check = JigsawResult.Reject(JigsawFailure.BossUnreachable);
        else if (check.Success && distance > Budget) check = JigsawResult.Reject(JigsawFailure.BossOverBudget);
        if (check.Success && Nodes.Any(n => !Reachable(0,Edges).Contains(n.Id))) check = JigsawResult.Reject(JigsawFailure.DisconnectedLayout);
        if (check.Success && boss != null)
        {
            var second = Nodes.Where(n => n.Room == JigsawRoom.SecondBoss).ToArray();
            var bossEdges = Edges.Where(e => e.Touches(boss.Id)).ToArray();
            bool terminal = second.Length == 0 ? bossEdges.Length == 1 : second.Length == 1 && bossEdges.Length == 2 && Edges.Count(e => e.Touches(second[0].Id)) == 1 && bossEdges.Any(e => e.Touches(second[0].Id));
            terminal &= bossEdges.All(e => e.Internal) && bossEdges.Any(e => Nodes.Single(n => n.Id == (e.First == boss.Id ? e.Second : e.First)).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss));
            if (!terminal) check = JigsawResult.Reject(JigsawFailure.BossNotTerminal);
        }
        if (check.Success)
        {
            var ordinaryReachable = Reachable(0,Edges,Nodes.Where(n => n.Room is JigsawRoom.Boss or JigsawRoom.SecondBoss).Select(n => n.Id).ToHashSet());
            if (Nodes.Any(n => n.Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss) && !ordinaryReachable.Contains(n.Id))) check = JigsawResult.Reject(JigsawFailure.BossBlocksOrdinaryRooms);
        }
        choices.Clear(); Phase = check.Success ? JigsawPhase.Ancient : JigsawPhase.Editing; return check;
    }
    private static HashSet<int> Reachable(int start,IEnumerable<JigsawEdge> edges,HashSet<int>? blocked = null)
    {
        var result = new HashSet<int> { start }; var queue = new Queue<int>(); queue.Enqueue(start); var links = edges.ToArray();
        while (queue.TryDequeue(out int node)) foreach (var e in links.Where(e => e.Touches(node)))
        { int next = e.First == node ? e.Second : e.First; if (blocked?.Contains(next) != true && result.Add(next)) queue.Enqueue(next); }
        return result;
    }
    private static int Distance(int start,int target,IEnumerable<JigsawEdge> edges)
    {
        var seen = new HashSet<int> { start }; var queue = new Queue<(int Node,int Distance)>(); queue.Enqueue((start,0)); var links = edges.ToArray();
        while (queue.TryDequeue(out var current))
        { if (current.Node == target) return current.Distance; foreach (var e in links.Where(e => e.Touches(current.Node))) { int next = e.First == current.Node ? e.Second : e.First; if (seen.Add(next)) queue.Enqueue((next,current.Distance+1)); } }
        return int.MaxValue;
    }
    public JigsawResult ResetLayout()
    {
        if (Phase != JigsawPhase.Editing) return JigsawResult.Reject(JigsawFailure.NotEditable);
        placements.Clear(); external.Clear(); holdings.Clear(); endpoints.Clear(); blankClicks.Clear(); choices.Clear(); return JigsawResult.Ok;
    }
    public JigsawResult CompleteAncient(ulong player)
    {
        if (Phase != JigsawPhase.Ancient || !players.Contains(player)) return JigsawResult.Reject(JigsawFailure.NotAncient);
        ancientCompleted.Add(player); return JigsawResult.Ok;
    }
    public JigsawResult Continue(ulong player)
    {
        if (Phase != JigsawPhase.Ancient || !players.Contains(player) || !players.All(ancientCompleted.Contains)) return JigsawResult.Reject(JigsawFailure.AncientIncomplete);
        continued.Add(player); if (players.All(continued.Contains)) Phase = JigsawPhase.Exploring; return JigsawResult.Ok;
    }
    public IReadOnlyCollection<int> AvailableDestinations => Phase != JigsawPhase.Exploring || CurrentNode != null || PendingEntry != null ? [] : Edges.SelectMany(e => new[] { (From:e.First,To:e.Second),(From:e.Second,To:e.First) }).Where(e => completed.Contains(e.From) && (!visited.Contains(e.To) || Nodes.Single(n => n.Id == e.To).Room == JigsawRoom.Shop) && e.To != 0).Select(e => e.To).Distinct().ToArray();
    public JigsawEnterAttempt? BeginEnter(int node)
    {
        if (!AvailableDestinations.Contains(node)) return null;
        PendingEntry = new(++nextEntry,node); return PendingEntry;
    }
    public JigsawResult AbandonEntry(long attempt)
    {
        if (PendingEntry?.Id != attempt) return JigsawResult.Reject(JigsawFailure.StaleEntry);
        PendingEntry = null; return JigsawResult.Ok;
    }
    public JigsawResult ConfirmEnter(long attempt)
    {
        if (PendingEntry?.Id != attempt || Phase != JigsawPhase.Exploring) return JigsawResult.Reject(JigsawFailure.StaleEntry);
        CurrentNode = PendingEntry.NodeId; currentEntry = attempt; PendingEntry = null; Steps++; if (visited.Add(CurrentNode.Value)) FirstProgress++; return JigsawResult.Ok;
    }
    public JigsawResult CompleteRoom(long attempt)
    {
        if (Phase != JigsawPhase.Exploring || CurrentNode == null || currentEntry != attempt) return JigsawResult.Reject(JigsawFailure.StaleCompletion);
        completed.Add(CurrentNode.Value); CurrentNode = null; currentEntry = null; return JigsawResult.Ok;
    }
    public JigsawSave CreateSave() => new(Pool.Select(p => p with { Nodes = p.Nodes.ToArray(), InternalEdges = p.InternalEdges.ToArray() }).ToArray(),placements.Values.ToArray(),external.ToArray(),Phase,Budget,players.ToArray(),Steps,FirstProgress,visited.ToArray(),completed.ToArray(),CurrentNode,PendingEntry,nextEntry,ancientCompleted.ToArray());
    public static JigsawRun Restore(JigsawSave save)
    {
        JigsawSaveValidation.Validate(save);
        var run = new JigsawRun(save.Pool,save.Budget,save.Players) { Phase=save.Phase,Steps=save.Steps,FirstProgress=save.FirstProgress,CurrentNode=save.CurrentNode,PendingEntry=save.PendingEntry,nextEntry=save.NextEntry,currentEntry=save.CurrentNode == null ? null : save.NextEntry };
        foreach (var p in save.Placements) run.placements.Add(p.PieceId,p); run.external.AddRange(save.ExternalEdges);
        run.visited.UnionWith(save.Visited); run.completed.UnionWith(save.Completed); run.ancientCompleted.UnionWith(save.AncientCompleted);
        var valid = JigsawGeometry.Validate(run.Nodes,run.Edges); if (!valid.Success) throw new ArgumentException(valid.Reason,nameof(save));
        if (save.Phase is JigsawPhase.Ancient or JigsawPhase.Exploring)
        {
            run.Phase=JigsawPhase.Inspecting;
            var inspected=run.Inspect();
            if(!inspected.Success || run.placements.Count!=save.Placements.Length) throw new ArgumentException("Finalized checkpoint map is invalid: "+inspected.Reason,nameof(save));
            run.Phase=save.Phase;
        }
        return run;
    }
}
