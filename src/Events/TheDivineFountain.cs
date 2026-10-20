// SPDX-License-Identifier: GPL-3.0-only
// Adapted from MT-SUPER-POWER/STS2-Balance-MOD, TheDivineFountain (GPLv3).
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Content;

namespace AllMatters.Events;

[RegisterSharedEvent]
public sealed class TheDivineFountain : ModEventTemplate
{
    public override bool IsShared => false;
    public override EventAssetProfile AssetProfile => new(InitialPortraitPath: "res://AllMatters/images/events/the_divine_fountain.png");

    public override bool IsAllowed(IRunState runState) => runState.Players.All(player =>
        player.Deck.Cards.Any(card => card.Type == CardType.Curse && card.IsRemovable));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new(this, Drink, InitialOptionKey("DRINK"), []),
        new(this, Leave, InitialOptionKey("LEAVE"), []),
    ];

    private async Task Drink()
    {
        var curses = Owner!.Deck.Cards.Where(card => card.Type == CardType.Curse && card.IsRemovable).ToList();
        await CardPileCmd.RemoveFromDeck(curses);
        SetEventFinished(PageDescription("DRINK"));
    }

    private Task Leave()
    {
        SetEventFinished(PageDescription("LEAVE"));
        return Task.CompletedTask;
    }
}
