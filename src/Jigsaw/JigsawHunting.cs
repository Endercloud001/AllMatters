namespace AllMatters.Jigsaw;

public sealed record JigsawHuntingState(
    [property: System.Text.Json.Serialization.JsonRequired] int RemainingSteps,
    [property: System.Text.Json.Serialization.JsonRequired] bool Active,
    [property: System.Text.Json.Serialization.JsonRequired] bool RootHunted,
    [property: System.Text.Json.Serialization.JsonRequired] bool Pending,
    [property: System.Text.Json.Serialization.JsonRequired] bool BossChecked,
    [property: System.Text.Json.Serialization.JsonRequired] bool BossOnTime);

public sealed partial class JigsawRun
{
    public bool HuntingActive { get; private set; }
    public bool CurrentRoomHunted { get; private set; }
    public bool BossReachedOnTime { get; private set; }
    private bool huntingPending;
    private bool bossChecked;

    public JigsawResult SetRemainingSteps(int remaining)
    {
        if (remaining < 0 || Phase != JigsawPhase.Exploring || PendingEntry != null || HasDeferredAncient)
            return JigsawResult.Reject("当前阶段不能设置步数，或步数超出0至2147483647范围。");
        RemainingSteps = remaining;
        if (!bossChecked && !HuntingActive && CurrentNode is { } node &&
            Nodes.Single(n => n.Id == node).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss))
            huntingPending = remaining == 0 && !players.All(roomCompleted.Contains);
        return JigsawResult.Ok;
    }

    private void EnterHuntingRoot(int node)
    {
        CurrentRoomHunted = HuntingActive;
        bool hadStep = RemainingSteps > 0;
        RemainingSteps = Math.Max(0, RemainingSteps - 1);
        var room = Nodes.Single(n => n.Id == node).Room;
        if (room == JigsawRoom.Boss && !bossChecked)
        {
            bossChecked = true;
            BossReachedOnTime = !HuntingActive && hadStep;
        }
        if (!bossChecked && !HuntingActive && RemainingSteps == 0 && room != JigsawRoom.SecondBoss)
            huntingPending = true;
    }

    private void ActivateHuntingAfterCompletion()
    {
        if (huntingPending)
        {
            HuntingActive = true;
            huntingPending = false;
        }
    }

    public bool CompleteMapBossVictory()
    {
        if (!IsEndgame)
            return false;
        bool cleared = HuntingActive;
        HuntingActive = CurrentRoomHunted = false;
        huntingPending = false;
        return cleared;
    }

    private void ClearHuntingData()
    {
        HuntingActive = CurrentRoomHunted = huntingPending = bossChecked = BossReachedOnTime = false;
        RemainingSteps = Budget;
    }

    private JigsawHuntingState SaveHunting() => new(RemainingSteps, HuntingActive, CurrentRoomHunted, huntingPending, bossChecked, BossReachedOnTime);

    private void RestoreHunting(JigsawHuntingState state)
    {
        RemainingSteps = state.RemainingSteps;
        HuntingActive = state.Active;
        CurrentRoomHunted = state.RootHunted;
        huntingPending = state.Pending;
        bossChecked = state.BossChecked;
        BossReachedOnTime = state.BossOnTime;
    }
}
