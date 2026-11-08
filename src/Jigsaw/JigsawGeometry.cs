namespace AllMatters.Jigsaw;

internal static class JigsawGeometry
{
    public static JigsawResult Validate(IReadOnlyList<JigsawNode> nodes, IReadOnlyList<JigsawEdge> edges)
    {
        var byId = nodes.ToDictionary(n => n.Id);
        for (int i = 0; i < nodes.Count; i++)
            for (int j = i + 1; j < nodes.Count; j++)
            {
                var a = nodes[i];
                var b = nodes[j];
                if (Math.Max(Math.Abs((long)a.Center.X - b.Center.X), Math.Abs((long)a.Center.Y - b.Center.Y)) < a.Radius + b.Radius + 2)
                    return JigsawResult.Reject(JigsawFailure.InsufficientClearance);
            }
        var paths = new List<(long Left, long Right, long Bottom, long Top)>();
        var ports = new HashSet<(int Id, int X, int Y)>();
        foreach (var edge in edges)
        {
            if (!byId.TryGetValue(edge.First, out var a) || !byId.TryGetValue(edge.Second, out var b))
                return JigsawResult.Reject(JigsawFailure.MissingEndpoint);
            int dx = Math.Sign(b.Center.X - a.Center.X), dy = Math.Sign(b.Center.Y - a.Center.Y);
            if (a.Id == b.Id || dx != 0 && dy != 0)
                return JigsawResult.Reject(JigsawFailure.MisalignedPath);
            if (edge.Internal && (a.PieceId == null || a.PieceId != b.PieceId))
                return JigsawResult.Reject(JigsawFailure.InvalidInternalPath);
            if (!edge.Internal)
            {
                if (a.PieceId == b.PieceId || a.Room is JigsawRoom.Boss or JigsawRoom.SecondBoss || b.Room is JigsawRoom.Boss or JigsawRoom.SecondBoss)
                    return JigsawResult.Reject(JigsawFailure.InvalidExternalEndpoint);
                if (Math.Abs((long)a.Center.X - b.Center.X) + Math.Abs((long)a.Center.Y - b.Center.Y) != a.Radius + b.Radius + 2)
                    return JigsawResult.Reject(JigsawFailure.InvalidExternalGap);
            }
            if (!ports.Add((a.Id, dx, dy)) || !ports.Add((b.Id, -dx, -dy)))
                return JigsawResult.Reject(JigsawFailure.OccupiedPort);
            long ax = (long)a.Center.X + dx * (a.Radius + 1), ay = (long)a.Center.Y + dy * (a.Radius + 1);
            long bx = (long)b.Center.X - dx * (b.Radius + 1), by = (long)b.Center.Y - dy * (b.Radius + 1);
            var path = (Left: Math.Min(ax, bx), Right: Math.Max(ax, bx), Bottom: Math.Min(ay, by), Top: Math.Max(ay, by));
            if (paths.Any(p => p.Left <= path.Right && path.Left <= p.Right && p.Bottom <= path.Top && path.Bottom <= p.Top))
                return JigsawResult.Reject(JigsawFailure.IntersectingPaths);
            if (nodes.Any(n => n.Id != a.Id && n.Id != b.Id && path.Left <= (long)n.Center.X + n.Radius && path.Right >= (long)n.Center.X - n.Radius && path.Bottom <= (long)n.Center.Y + n.Radius && path.Top >= (long)n.Center.Y - n.Radius))
                return JigsawResult.Reject(JigsawFailure.PathThroughNode);
            paths.Add(path);
        }
        foreach (var group in edges.Where(e => !e.Internal).Select(e => (A: byId[e.First].PieceId, B: byId[e.Second].PieceId)).Where(p => p.A != null && p.B != null)
            .GroupBy(p => (Math.Min(p.A!.Value, p.B!.Value), Math.Max(p.A!.Value, p.B!.Value))))
            if (group.Count() > 2)
                return JigsawResult.Reject(JigsawFailure.TooManyPairPaths);
        return JigsawResult.Ok;
    }
}
