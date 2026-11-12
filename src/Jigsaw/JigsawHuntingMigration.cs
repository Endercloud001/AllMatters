namespace AllMatters.Jigsaw;

internal static class JigsawHuntingMigration
{
    internal static JigsawSave FromLegacy(JigsawSave save)
    {
        if (save.Hunting != null)
            return save;
        // Older complete points had no independent balance or punished-root fact.
        // Before exhaustion, the old public entry count determines both exactly.
        if (save.Steps < 0 || save.Steps >= save.Budget)
            throw new InvalidDataException("旧拼图检查点已到追猎边界，无法确定当前根房是否受罚；请恢复耗尽前的完整备份。");
        int? firstBoss = save.Pool.SelectMany(piece => piece.Nodes).FirstOrDefault(node => node.Room == JigsawRoom.Boss)?.Id;
        bool reachedBoss = firstBoss is { } boss && save.Visited.Contains(boss);
        return save with { Hunting = new(save.Budget - save.Steps, false, false, false, reachedBoss, reachedBoss) };
    }
}
