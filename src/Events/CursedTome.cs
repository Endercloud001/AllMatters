// SPDX-License-Identifier: GPL-3.0-only
// Adapted from Balance CursedTome (GPLv3); personal, deliberate book selection.
using AllMatters.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Events;

[RegisterSharedEvent]
public sealed class CursedTome : ModEventTemplate
{
    public override bool IsShared => false;
    public override EventAssetProfile AssetProfile => new(InitialPortraitPath: "res://AllMatters/images/events/cursed_tome.png");
    public override bool IsAllowed(IRunState runState) => runState.CurrentActIndex == 1
        && runState.Players.All(player => !player.Relics.Any(relic => relic is Necronomicon or NilrysCodex or Enchiridion));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new(this, () => ShowPage(1), InitialOptionKey("READ"), []),
        new(this, Leave, InitialOptionKey("LEAVE"), []),
    ];

    private Task ShowPage(int page)
    {
        SetEventState(PageDescription($"PAGE_{page}"),
        [
            new EventOption(this, () => Continue(page), $"{Id.Entry}.pages.PAGE_{page}.options.CONTINUE", []).ThatDoesDamage(page),
        ]);
        return Task.CompletedTask;
    }

    private async Task Continue(int page)
    {
        if (!await DamageOwner(page)) return;
        if (page < 3)
        {
            await ShowPage(page + 1);
            return;
        }
        SetEventState(PageDescription("LAST_PAGE"),
        [
            new EventOption(this, Obtain, $"{Id.Entry}.pages.LAST_PAGE.options.OBTAIN", []).ThatDoesDamage(15),
            new EventOption(this, Stop, $"{Id.Entry}.pages.LAST_PAGE.options.STOP", []).ThatDoesDamage(3),
        ]);
    }

    private async Task Obtain()
    {
        if (!await DamageOwner(15)) return;
        SetEventState(PageDescription("CHOOSE"),
        [
            BookOption<Necronomicon>("NECRONOMICON"),
            BookOption<NilrysCodex>("NILRYS_CODEX"),
            BookOption<Enchiridion>("ENCHIRIDION"),
        ]);
    }

    private EventOption BookOption<T>(string key) where T : RelicModel, new() => new(this,
        TakeBook<T>, $"{Id.Entry}.pages.CHOOSE.options.{key}", HoverTipFactory.FromRelic(ModelDb.Relic<T>()).ToArray());

    private async Task TakeBook<T>() where T : RelicModel, new()
    {
        // All three visible options belong to the same choice. Claim before awaiting
        // the native obtain/curse flow so stale options cannot award another book.
        if (IsFinished) return;
        SetEventFinished(PageDescription("OBTAIN"));
        if (!Owner!.Creature.IsDead) await RelicCmd.Obtain<T>(Owner);
    }

    private async Task Stop()
    {
        if (await DamageOwner(3)) SetEventFinished(PageDescription("STOP"));
    }

    private Task Leave()
    {
        SetEventFinished(PageDescription("LEAVE"));
        return Task.CompletedTask;
    }

    private async Task<bool> DamageOwner(int amount)
    {
#if STS2_PUBLIC
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature, amount,
            ValueProp.Unblockable | ValueProp.Unpowered, dealer: null, cardSource: null);
#else
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner!.Creature, amount,
            ValueProp.Unblockable | ValueProp.Unpowered, dealer: null, cardSource: null, cardPlay: null);
#endif
        if (!Owner.Creature.IsDead) return true;
        SetEventFinished(PageDescription("DEAD"));
        return false;
    }
}
