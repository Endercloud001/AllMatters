using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace AllMatters.Events;

internal static class NativePotionPayment
{
    private sealed record Payment(PotionModel? Potion, bool PreviouslyAllowed);
    private static readonly ConditionalWeakTable<EventModel, Payment> Payments = new();
    private static readonly ConditionalWeakTable<Player, EventModel> FrozenPlayers = new();

    internal static void Freeze(EventModel model, bool choosePotion = true)
    {
        if (Payments.TryGetValue(model, out _)) return;
        var potions = model.Owner!.Potions.ToArray();
        Payments.Add(model, new(!choosePotion || potions.Length == 0 ? null : potions[model.Rng.NextInt(potions.Length)], IsAllowed(model)));
        FrozenPlayers.Remove(model.Owner);
        FrozenPlayers.Add(model.Owner, model);
        SetAllowed(model, false);
    }

    internal static PotionModel? Potion(EventModel model) => Payments.GetValue(model, _ => throw new InvalidOperationException("Payment must be fixed on entry.")).Potion;
    internal static void Restore(EventModel model)
    {
        if (Payments.TryGetValue(model, out var payment))
        {
            SetAllowed(model, payment.PreviouslyAllowed);
            FrozenPlayers.Remove(model.Owner!);
        }
    }

    internal static bool IsFrozen(Player player) => FrozenPlayers.TryGetValue(player, out var model) && !model.IsFinished;

    private static bool IsAllowed(EventModel model)
#if STS2_PUBLIC
        => model.Owner!.CanRemovePotions;
#else
        => model.Owner!.CanUseOrRemovePotions;
#endif
    private static void SetAllowed(EventModel model, bool value)
    {
#if STS2_PUBLIC
        model.Owner!.CanRemovePotions = value;
#else
        model.Owner!.CanUseOrRemovePotions = value;
#endif
    }

    internal static EventOption Option(EventModel model, string key, Func<Task> action, bool legal = true)
    {
        var potion = Potion(model)!;
        var description = new LocString("events", key + ".description");
        description.Add("Potion", potion.Title);
        return new EventOption(model, legal ? async () =>
        {
            if (model.IsFinished || !model.Owner!.Potions.Contains(potion)) return;
            await PotionCmd.Discard(potion);
            await action();
        } : null, new LocString("events", key + ".title"), description, key, potion.HoverTips);
    }
}

[HarmonyPatch(typeof(TheFutureOfPotions), "BeforeEventStarted")]
internal static class FreezeFuturePayment
{
    private static void Prefix(TheFutureOfPotions __instance) => NativePotionPayment.Freeze(__instance, false);
}

// public's CanRemovePotions only controls discarding. Block manual use at the
// action boundary as well; normal event payment still calls PotionCmd.Discard.
[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.EnqueueManualUse))]
internal static class FrozenPotionManualUse
{
    private static bool Prefix(PotionModel __instance) => !NativePotionPayment.IsFrozen(__instance.Owner);
}

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.OnUseWrapper))]
internal static class FrozenPotionUse
{
    private static bool Prefix(PotionModel __instance, ref Task __result)
    {
        if (!NativePotionPayment.IsFrozen(__instance.Owner)) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(EventModel), "BeforeEventStarted")]
internal static class FreezeNativePayment
{
    private static void Postfix(EventModel __instance)
    {
        if (__instance is BrainLeech or SapphireSeed or DrowningBeacon) NativePotionPayment.Freeze(__instance);
    }
}

[HarmonyPatch(typeof(EventModel), nameof(EventModel.EnsureCleanup))]
internal static class RestoreNativePayment
{
    private static void Postfix(EventModel __instance) => NativePotionPayment.Restore(__instance);
}
