using MegaCrit.Sts2.Core.Map;

namespace AllMatters.Jigsaw;

/// <summary>A chronological native history projection, with no cyclic logical Children graph.</summary>
internal sealed class JigsawEntryProjection : ActMap
{
    private readonly MapPoint?[,] grid;
    public override MapPoint StartingMapPoint { get; }
    public override MapPoint BossMapPoint { get; }
    public override MapPoint? SecondBossMapPoint { get; }
    protected override MapPoint?[,] Grid => grid;

    private JigsawEntryProjection(ActMap original, int row, MapPoint point)
    {
        StartingMapPoint = original.StartingMapPoint;
        BossMapPoint = original.BossMapPoint;
        SecondBossMapPoint = original.SecondBossMapPoint;
        grid = new MapPoint?[Math.Max(original.GetColumnCount(), 1), Math.Max(original.GetRowCount(), row + 1)];
        foreach (var old in original.GetAllMapPoints())
            if (old.coord.col >= 0 && old.coord.col < grid.GetLength(0) && old.coord.row >= 0 && old.coord.row < grid.GetLength(1))
                grid[old.coord.col, old.coord.row] = old;
        grid[point.coord.col, point.coord.row] = point;
        startMapPoints.UnionWith(original.startMapPoints);
    }

    public static (ActMap Map, MapPoint Point) Append(ActMap original, int row, JigsawRoom room)
    {
        var point = new MapPoint(0, row)
        {
            PointType = room switch
            {
                JigsawRoom.Combat => MapPointType.Monster,
                JigsawRoom.Unknown => MapPointType.Unknown,
                JigsawRoom.Elite => MapPointType.Elite,
                JigsawRoom.Shop => MapPointType.Shop,
                JigsawRoom.Rest => MapPointType.RestSite,
                JigsawRoom.Treasure => MapPointType.Treasure,
                JigsawRoom.Boss or JigsawRoom.SecondBoss => MapPointType.Boss,
                _ => throw new InvalidOperationException("This is not an exploration destination.")
            }
        };
        return (new JigsawEntryProjection(original, row, point), point);
    }
}
