using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Powers;

[RegisterPower]
public sealed class TightGripPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/powers/tight_grip.png", BigIconPath: "res://AllMatters/images/powers/tight_grip.png");

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        WeightedCardPresentation.RefreshViews();
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        WeightedCardPresentation.RefreshViews();
        return Task.CompletedTask;
    }

    public override Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature == Owner) WeightedCardPresentation.RefreshViews();
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.IsDead || cardPlay.Card.Owner.Creature.CombatState != Owner.CombatState) return;
        Flash();
        await PlayerCmd.LoseEnergy(1, cardPlay.Card.Owner);
    }
}

// Only the event encounter gets the cycle; the native magistrate remains unchanged elsewhere.
[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.BeforeSideTurnStart))]
internal static class TrialGripCycle
{
    private static void Postfix(AbstractModel __instance, PlayerChoiceContext choiceContext, CombatSide side,
        ICombatState combatState, ref Task __result)
    {
        if (__instance is OwlMagistrate monster && monster.Creature.IsAlive && side == CombatSide.Player
            && combatState.Encounter is Encounters.TrialEncounter)
            __result = Update(__result, choiceContext, monster, combatState.RoundNumber);
    }

    private static async Task Update(Task original, PlayerChoiceContext context, OwlMagistrate monster, int round)
    {
        await original;
        var grip = monster.Creature.GetPower<TightGripPower>();
        if (round % 3 == 0 && round > 0)
        {
            if (grip == null) await PowerCmd.Apply<TightGripPower>(context, monster.Creature, 1, monster.Creature, null);
        }
        else if (grip != null) await PowerCmd.Remove(grip);
    }
}
