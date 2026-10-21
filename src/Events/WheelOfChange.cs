// SPDX-License-Identifier: GPL-3.0-only
// Adapted from Balance WheelOfChange (GPLv3); AllMatters outcomes and restricted relic draw.
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Events;

[RegisterSharedEvent]
public sealed class WheelOfChange : ModEventTemplate
{
    internal static readonly string[] Outcomes = ["GOLD", "RELIC", "HEAL", "CURSE", "REMOVE", "DAMAGE"];

    public override bool IsShared => false;
    public override bool IsAllowed(IRunState runState) => true;
    public override EventAssetProfile AssetProfile => new(InitialPortraitPath: "res://AllMatters/images/events/wheel_of_change.png");

    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("GoldAmount", 0)];
    public override void CalculateVars() => DynamicVars["GoldAmount"].BaseValue = Owner!.RunState.CurrentActIndex switch
    {
        0 => 100,
        1 => 200,
        _ => 300,
    };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new(this, Spin, InitialOptionKey("SPIN"), []),
    ];

    private async Task Spin()
    {
        // Balance advances the per-event stream by act so recurring wheels need
        // not repeat their first-act outcome. NextInt(1) preserves uniform odds.
        for (int act = 0; act < Owner!.RunState.CurrentActIndex; act++) Rng.NextInt(1);
        int result = Rng.NextInt(6);
        // Visuals consume no gameplay randomness and only run for the local owner.
        if (Node != null && LocalContext.IsMe(Owner!))
            await WheelSpinPresentation.Play(Node, Id, result);
        string key = Outcomes[result];
        var option = new EventOption(this, () => ApplyResult(result), $"{Id.Entry}.pages.RESULT.options.{key}", []);
        if (result == 5) option.ThatDoesDamage(7);
        SetEventState(PageDescription(key), [option]);
    }

    private async Task ApplyResult(int result)
    {
        var owner = Owner!;
        switch (result)
        {
            case 0:
                await PlayerCmd.GainGold(DynamicVars["GoldAmount"].IntValue, owner);
                break;
            case 1:
                var relic = DrawRelic();
                if (relic == null)
                {
                    SetEventFinished(PageDescription("NO_RELIC"));
                    return;
                }
                await RelicCmd.Obtain(relic.ToMutable(), owner);
                break;
            case 2:
                await CreatureCmd.Heal(owner.Creature, owner.Creature.MaxHp);
                break;
            case 3:
                await CardPileCmd.AddCurseToDeck<BadLuck>(owner);
                break;
            case 4:
                if (!owner.Deck.Cards.Any(card => card.IsRemovable))
                {
                    SetEventFinished(PageDescription("NO_TARGET"));
                    return;
                }
                var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1) { Cancelable = false };
                var selected = await CardSelectCmd.FromDeckForRemoval(owner, prefs);
                await CardPileCmd.RemoveFromDeck(selected.ToList());
                break;
            case 5:
#if STS2_PUBLIC
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), owner.Creature, 7,
                    ValueProp.Unblockable | ValueProp.Unpowered, dealer: null, cardSource: null);
#else
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), owner.Creature, 7,
                    ValueProp.Unblockable | ValueProp.Unpowered, dealer: null, cardSource: null, cardPlay: null);
#endif
                break;
        }
        SetEventFinished(PageDescription("FINISHED"));
    }

    private RelicModel? DrawRelic()
    {
        var owner = Owner!;
        // Only unlocked general-drop pools qualify. Event-only relics (including
        // library bottles) cannot enter this draw even if present in a grab bag.
        var candidates = ModelDb.RelicPool<SharedRelicPool>().GetUnlockedRelics(owner.UnlockState)
            .Concat(owner.Character.RelicPool.GetUnlockedRelics(owner.UnlockState))
            .Where(relic => relic.Rarity is RelicRarity.Common or RelicRarity.Uncommon
                && relic.IsAllowed(owner.RunState)
                && (relic.IsStackable || owner.Relics.All(held => held.Id != relic.Id)))
            .DistinctBy(relic => relic.Id).ToArray();
        var rarity = Rng.NextInt(2) == 0 ? RelicRarity.Common : RelicRarity.Uncommon;
        return DrawFromTier(candidates, rarity)
            ?? DrawFromTier(candidates, rarity == RelicRarity.Common ? RelicRarity.Uncommon : RelicRarity.Common);
    }

    private RelicModel? DrawFromTier(RelicModel[] candidates, RelicRarity rarity)
    {
        var owner = Owner!;
        var tier = candidates.Where(relic => relic.Rarity == rarity).ToArray();
        var primary = tier.Where(owner.RelicGrabBag.Contains).ToArray();
        if (primary.Length > 0) return primary[Rng.NextInt(primary.Length)];

        // Contains only sees primary queues. The native multiplayer chest fallback
        // remains eligible when those are exhausted. Probe in random order through
        // the public draw API, filtering to one exact ID AND rarity: automatic
        // escalation cannot return a Rare, event relic, or Circlet compensation.
        Rng.Shuffle(tier);
        foreach (var candidate in tier)
        {
            var relic = owner.RelicGrabBag.PullFromFront(rarity,
                relic => relic.Rarity == rarity && relic.Id == candidate.Id, owner.RunState);
            if (relic != null) return relic;
        }
        return null;
    }
}
