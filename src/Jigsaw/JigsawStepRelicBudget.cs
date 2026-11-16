namespace AllMatters.Jigsaw;

public sealed partial class JigsawRun
{
    public int StepRelicBonus { get; private set; }

    // A pickup is valid during Ancient/deferred rewards as well as exploration.
    // Held inventory never calls this operation when a new act begins.
    public bool ApplyStepRelicBonus()
    {
        if (StepRelicBonus != 0)
            return false;
        int budget = checked(Budget + 3);
        // The console permits an int.MaxValue balance. A pickup still increases the budget.
        int remaining = (int)Math.Min((long)RemainingSteps + 3, int.MaxValue);
        Budget = budget;
        RemainingSteps = remaining;
        StepRelicBonus = 3;
        if (!HuntingActive)
            huntingPending = false;
        return true;
    }
}
