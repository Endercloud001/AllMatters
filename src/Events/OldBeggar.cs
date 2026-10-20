// SPDX-License-Identifier: GPL-3.0-only
// Adapted from MT-SUPER-POWER/STS2-Balance-MOD, OldBeggar (GPLv3).
using MegaCrit.Sts2.Core.Events;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Content;

namespace AllMatters.Events;

[RegisterSharedEvent]
public sealed class OldBeggar : ModEventTemplate
{
    public override bool IsShared => false;
    public override EventAssetProfile AssetProfile => new(InitialPortraitPath: "res://AllMatters/images/events/old_beggar.png");
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("GoldCost", 75)];
    public override bool IsAllowed(IRunState runState) => runState.Players.All(player => player.Gold >= 75);

    public override IEnumerable<string> GetAssetPaths(IRunState runState) =>
        base.GetAssetPaths(runState).Append("res://AllMatters/images/events/cleric.png");

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        bool canGive = Owner!.Gold >= 75 && Owner.Deck.Cards.Any(card => card.IsRemovable);
        return
        [
            new(this, canGive ? GiveGold : null, InitialOptionKey(canGive ? "GIVE_GOLD" : "GIVE_GOLD_LOCKED"), []),
            new(this, Leave, InitialOptionKey("LEAVE"), []),
        ];
    }

    private async Task GiveGold()
    {
        await PlayerCmd.LoseGold(75, Owner!, GoldLossType.Spent);
        SetEventState(PageDescription("GAVE_GOLD"),
        [
            new(this, RemoveCard, ModOptionKey("GAVE_GOLD", "REMOVE_CARD"), []),
        ]);
        if (Node?.FindChild("Portrait", true, false) is TextureRect portrait)
            portrait.Texture = PreloadManager.Cache.GetTexture2D("res://AllMatters/images/events/cleric.png");
    }

    private async Task RemoveCard()
    {
        var preferences = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1) { Cancelable = false };
        var selected = await CardSelectCmd.FromDeckForRemoval(Owner!, preferences);
        await CardPileCmd.RemoveFromDeck(selected.ToList());
        SetEventFinished(PageDescription("REMOVE_CARD"));
    }

    private Task Leave()
    {
        SetEventFinished(PageDescription("LEAVE"));
        return Task.CompletedTask;
    }
}
