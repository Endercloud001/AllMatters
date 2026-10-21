// SPDX-License-Identifier: GPL-3.0-only
// Adapted from Balance MindBloom; single double-Lagavulin fight and specified curses.
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using AllMatters.Relics;
using AllMatters.Encounters;

namespace AllMatters.Events;

[RegisterSharedEvent]
public sealed class MindBloom : ModEventTemplate
{
    private bool _choosing;
    public override bool IsShared => true;
    public override bool IsAllowed(IRunState runState) => runState.CurrentActIndex == 2;
    public override EventAssetProfile AssetProfile => new(InitialPortraitPath: "res://AllMatters/images/events/mind_bloom.png");

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new(this, Fight, InitialOptionKey("FIGHT"), []),
        new(this, Upgrade, InitialOptionKey("UPGRADE"), []),
        Owner!.RunState.TotalFloor < 38
            ? new(this, Gold, InitialOptionKey("GOLD"), [])
            : new(this, Heal, InitialOptionKey("HEAL"), []),
    ];

    private Task Fight()
    {
        if (IsFinished || _choosing) return Task.CompletedTask;
        _choosing = true;
        EnterCombatWithoutExitingEvent<MindBloomEncounter>([], false);
        return Task.CompletedTask;
    }
    private async Task Upgrade()
    {
        if (IsFinished || _choosing) return;
        _choosing = true;
        foreach (var card in Owner!.Deck.Cards.Where(c => c.IsUpgradable).ToArray())
            CardCmd.Upgrade(card);
        await RelicCmd.Obtain(ModelDb.Relic<MarkOfTheBloom>().ToMutable(), Owner);
        SetEventFinished(PageDescription("UPGRADE"));
    }

    private async Task Gold()
    {
        if (IsFinished || _choosing) return;
        _choosing = true;
        await PlayerCmd.GainGold(999, Owner!);
        await AddCurses(ModelDb.Card<Normality>(), ModelDb.Card<Greed>());
        SetEventFinished(PageDescription("GOLD"));
    }

    private async Task Heal()
    {
        if (IsFinished || _choosing) return;
        _choosing = true;
        await CreatureCmd.Heal(Owner!.Creature, Owner.Creature.MaxHp);
        await AddCurses(ModelDb.Card<Enthralled>(), ModelDb.Card<Regret>());
        SetEventFinished(PageDescription("HEAL"));
    }

    private async Task AddCurses(params CardModel[] curses)
    {
        foreach (var curse in curses)
        {
            var card = Owner!.RunState.CreateCard(curse, Owner);
            var result = await CardPileCmd.Add(card, PileType.Deck);
            CardCmd.PreviewCardPileAdd([result], 2f);
        }
    }
}
