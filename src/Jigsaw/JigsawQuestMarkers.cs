namespace AllMatters.Jigsaw;

public sealed record JigsawQuestMarkers(bool SpoilsFinalized, int? SpoilsNode, int[]? FurNodes, ulong[] PaidOwners, bool NoChestRemovalPending = false);

public sealed partial class JigsawRun
{
    private bool spoilsFinalized;
    private int? spoilsNode;
    private int[]? furNodes;
    internal bool NoChestRemovalPending { get; private set; }
    internal void CompleteNoChestRemoval() => NoChestRemovalPending = false;
    private readonly HashSet<ulong> spoilsPaidOwners = [];
    internal int? SpoilsNode => spoilsNode;
    internal long QuestEntry => nextEntry;
    internal bool HasFurMarker(int node) => furNodes?.Contains(node) == true;
    internal bool FinalizeSpoils(bool hasCards, int actIndex)
    {
        if (spoilsFinalized || Phase is not (JigsawPhase.Ancient or JigsawPhase.Exploring))
            return false;
        spoilsFinalized = true;
        if (actIndex == 1 && hasCards)
            spoilsNode = SelectMarkers(Nodes.Where(n => n.Room == JigsawRoom.Treasure), 1, 0x53504f494c53UL).Cast<int?>().FirstOrDefault();
        return NoChestRemovalPending = actIndex == 1 && hasCards && spoilsNode == null;
    }
    internal void MarkFur(int count)
    {
        if (furNodes != null || Phase is not (JigsawPhase.Ancient or JigsawPhase.Exploring))
            return;
        furNodes = SelectMarkers(Nodes.Where(n => n.Room is JigsawRoom.Combat or JigsawRoom.Elite), count, 0x465552UL);
    }
    private int[] SelectMarkers(IEnumerable<JigsawNode> candidates, int count, ulong salt)
    {
        var ids = candidates.Select(n => n.Id).Order().ToArray();
        ulong random = generationRandomState ^ salt;
        for (int i = ids.Length - 1; i > 0; i--)
        {
            random ^= random << 13;
            random ^= random >> 7;
            random ^= random << 17;
            int j = (int)(random % (ulong)(i + 1));
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }
        return ids.Take(count).ToArray();
    }
    internal bool IsSpoilsDestination => spoilsNode != null && (PendingEntry?.NodeId ?? CurrentNode) == spoilsNode;
    internal bool ClaimSpoils(ulong owner) => IsSpoilsDestination && spoilsPaidOwners.Add(owner);
    private JigsawQuestMarkers? SaveQuestMarkers() => !spoilsFinalized && furNodes == null ? null : new(spoilsFinalized, spoilsNode, furNodes?.ToArray(), spoilsPaidOwners.Order().ToArray(), NoChestRemovalPending);
    private void RestoreQuestMarkers(JigsawQuestMarkers? state)
    {
        if (state == null)
            return;
        spoilsFinalized = state.SpoilsFinalized;
        spoilsNode = state.SpoilsNode;
        furNodes = state.FurNodes?.ToArray();
        spoilsPaidOwners.UnionWith(state.PaidOwners);
        NoChestRemovalPending = state.NoChestRemovalPending;
    }
}
