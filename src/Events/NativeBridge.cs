using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Events;

namespace AllMatters.Events;

[HarmonyPatch(typeof(SlipperyBridge), "GenerateInitialOptions")]
internal static class NativeBridge
{
    private static bool Prefix(SlipperyBridge __instance, ref IReadOnlyList<EventOption> __result)
    {
        __result =
        [
            NativeEventActions.Option(__instance, "SLIPPERY_BRIDGE.pages.INITIAL.options.AM_RETURN", () =>
            {
                NativeEventActions.Finish(__instance, "SLIPPERY_BRIDGE.pages.AM_RETURN.description");
                return Task.CompletedTask;
            }),
            Forward(__instance)
        ];
        return false;
    }

    private static EventOption Forward(SlipperyBridge model)
    {
        var option = new EventOption(model, model.Owner!.Deck.Cards.Any(c => c.IsRemovable) ? () => Step(model) : null,
            "SLIPPERY_BRIDGE.pages.INITIAL.options.AM_FORWARD");
        return (int)AccessTools.Field(typeof(SlipperyBridge), "_numberOfHoldOns").GetValue(model)! == 2
            ? option.ThatDoesDamage(13) : option;
    }

    private static async Task Step(SlipperyBridge model)
    {
        var count = AccessTools.Field(typeof(SlipperyBridge), "_numberOfHoldOns");
        int step = (int)count.GetValue(model)! + 1;
        count.SetValue(model, step);
        bool remove = model.Rng.NextInt(100) < 10 + step * 10;
        var legal = model.Owner!.Deck.Cards.Where(c => c.IsRemovable).ToArray();
        if (remove && legal.Length > 0)
        {
            var basic = legal.Where(c => c.Rarity == CardRarity.Basic).ToArray();
            var candidates = basic.Length > 0 ? basic : legal;
            var selected = candidates[model.Rng.NextInt(candidates.Length)];
            await CardPileCmd.RemoveFromDeck(selected);
            NativeEventActions.Finish(model, "SLIPPERY_BRIDGE.pages.AM_CLEAR.description");
        }
        else if (step == 3)
        {
            await NativeEventActions.LoseHp(model, 13);
            NativeEventActions.Finish(model, "SLIPPERY_BRIDGE.pages.AM_FALL.description");
        }
        else
        {
            NativeEventActions.State(model, $"SLIPPERY_BRIDGE.pages.HOLD_ON_{step - 1}.description", [Forward(model)]);
        }
    }
}
