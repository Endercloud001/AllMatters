// SPDX-License-Identifier: GPL-3.0-only
// Adapted from MT-SUPER-POWER/STS2-Balance-MOD, Cleric (GPLv3).
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Content;

namespace AllMatters.Events;

[RegisterSharedEvent]
public sealed class Cleric : ModEventTemplate
{
    public override bool IsShared => false;

    public override EventAssetProfile AssetProfile => new(InitialPortraitPath: "res://AllMatters/images/events/cleric.png");

    public override bool IsAllowed(IRunState runState) => runState.Players.All(player => player.Gold >= 35);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(0),
        new IntVar("HealCost", 35),
        new IntVar("PurifyCost", 75),
    ];

    public override void CalculateVars() => DynamicVars.Heal.BaseValue = Math.Floor((Owner?.Creature.MaxHp ?? 0) * 0.25m);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var owner = Owner!;
        bool canHeal = owner.Gold >= 35;
        bool canPurify = owner.Gold >= 75 && owner.Deck.Cards.Any(card => card.IsRemovable);
        return
        [
            new(this, canHeal ? Heal : null, InitialOptionKey(canHeal ? "HEAL" : "HEAL_LOCKED"), []),
            new(this, canPurify ? Purify : null, InitialOptionKey(canPurify ? "PURIFY" : "PURIFY_LOCKED"), []),
            new(this, Leave, InitialOptionKey("LEAVE"), []),
        ];
    }

    private async Task Heal()
    {
        var owner = Owner!;
        await PlayerCmd.LoseGold(35, owner, GoldLossType.Spent);
        await CreatureCmd.Heal(owner.Creature, DynamicVars.Heal.BaseValue);
        SetEventFinished(PageDescription("HEAL"));
    }

    private async Task Purify()
    {
        var owner = Owner!;
        await PlayerCmd.LoseGold(75, owner, GoldLossType.Spent);
        var preferences = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1) { Cancelable = false };
        var selected = await CardSelectCmd.FromDeckForRemoval(owner, preferences);
        await CardPileCmd.RemoveFromDeck(selected.ToList());
        SetEventFinished(PageDescription("PURIFY"));
    }

    private Task Leave()
    {
        SetEventFinished(PageDescription("LEAVE"));
        return Task.CompletedTask;
    }
}
