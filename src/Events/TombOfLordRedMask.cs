// SPDX-License-Identifier: GPL-3.0-only
// Adapted from Balance TombOfLordRedMask (GPLv3); fixed offering and Act 3 admission.
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Events;

[RegisterSharedEvent]
public sealed class TombOfLordRedMask : ModEventTemplate
{
    public override bool IsShared => false;
    public override bool IsAllowed(IRunState runState) => runState.CurrentActIndex == 2;
    public override EventAssetProfile AssetProfile => new(InitialPortraitPath: "res://AllMatters/images/events/tomb_of_lord_red_mask.png");

    protected override IEnumerable<DynamicVar> CanonicalVars => [new GoldVar(222), new IntVar("Offering", 150)];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>();
        if (Owner!.Relics.Any(relic => relic is RedMask))
        {
            options.Add(new(this, WearMask, InitialOptionKey("WEAR_MASK"), []));
        }
        else
        {
            options.Add(new(this, null, InitialOptionKey("WEAR_MASK_LOCKED"), []));
            bool canPay = Owner.Gold >= 150;
            // Balance omits the native Red Mask hover tip here because its energy-icon
            // pool lookup is unsafe in a mod event. Keep the explicit option text.
            options.Add(new(this, canPay ? PayRespects : null, InitialOptionKey(canPay ? "PAY_RESPECTS" : "PAY_RESPECTS_LOCKED"), []));
        }
        options.Add(new(this, Leave, InitialOptionKey("LEAVE"), []));
        return options;
    }

    private async Task WearMask()
    {
        if (IsFinished) return;
        await PlayerCmd.GainGold(222, Owner!);
        SetEventFinished(PageDescription("WEAR_MASK"));
    }

    private async Task PayRespects()
    {
        if (IsFinished) return;
        await PlayerCmd.LoseGold(150, Owner!, GoldLossType.Spent);
        await RelicCmd.Obtain(ModelDb.Relic<RedMask>().ToMutable(), Owner!);
        SetEventFinished(PageDescription("PAY_RESPECTS"));
    }

    private Task Leave()
    {
        if (!IsFinished) SetEventFinished(PageDescription("LEAVE"));
        return Task.CompletedTask;
    }
}
