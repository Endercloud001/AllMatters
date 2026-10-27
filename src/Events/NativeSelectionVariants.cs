using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace AllMatters.Events;

internal static class NativeSelectionVariant
{
    private sealed record Branch(bool Alternative);
    private static readonly ConditionalWeakTable<EventModel, Branch> Branches = new();
    internal static bool Alternative(EventModel model) => Branches.GetValue(model, e => new(e.Rng.NextInt(2) == 0)).Alternative;
}

[HarmonyPatch(typeof(ZenWeaver), "GenerateInitialOptions")]
internal static class NativeZenUpgrades
{
    private static void Postfix(ZenWeaver __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        if (!NativeSelectionVariant.Alternative(model)) return;
        var options = __result.ToList();
        for (int i = 1; i <= 2; i++)
        {
            int count = i, cost = i == 1 ? 125 : 250;
            options[i] = new EventOption(model, model.Owner!.Gold >= cost ? async () =>
            {
                var selected = await CardSelectCmd.FromDeckForUpgrade(model.Owner, new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, count));
                CardCmd.Upgrade(selected, MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.MessyLayout);
                await PlayerCmd.LoseGold(cost, model.Owner, GoldLossType.Spent);
                NativeEventActions.Finish(model, "ZEN_WEAVER.pages.AM_UPGRADE.description");
            } : null, "ZEN_WEAVER.pages.INITIAL.options.AM_UPGRADE_" + count);
        }
        __result = options;
    }
}
