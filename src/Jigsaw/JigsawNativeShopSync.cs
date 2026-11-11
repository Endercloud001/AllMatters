using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace AllMatters.Jigsaw;

internal sealed record JigsawShopStockUpdate(int Act, int Node, long Entry, long Round, long Sequence,
    JigsawShopInventory Stock);

internal static class JigsawNativeShopSync
{
    private sealed class SequenceState
    {
        internal readonly Dictionary<ulong, long> Applied = [];
        internal readonly Dictionary<ulong, long> NativePurchases = [];
        internal long Next;
        internal JigsawShopStockUpdate? Pending;
        internal bool Submitted;
    }

    private static readonly ConditionalWeakTable<JigsawNativeSession, SequenceState> sequences = new();
    internal static Func<JigsawShopStockUpdate, bool>? RequestOverride;
    private static readonly RitsuLibManagedNetActionDescriptor<JigsawShopStockUpdate> descriptor = new(
        ModEntry.ModId, "jigsaw-shop-stock",
        message => JsonSerializer.SerializeToUtf8Bytes(message),
        bytes => JsonSerializer.Deserialize<JigsawShopStockUpdate>(bytes) ?? throw new InvalidDataException("Missing shop stock update."),
        context =>
        {
            Apply(context.Player, context.Message);
            return Task.CompletedTask;
        }, GameActionType.NonCombat);

    internal static void Initialize() => RitsuLibManagedNetActions.Register(descriptor);

    private static bool Request(JigsawShopStockUpdate update) => RequestOverride?.Invoke(update) ??
        RitsuLibManagedNetActions.Request(RunManager.Instance, descriptor, update);

    internal static void ThrowIfUnsettled(JigsawNativeSession session)
    {
        if (!sequences.TryGetValue(session, out var state))
            return;
        if (state.Pending is { } pending)
        {
            if (!state.Submitted)
                state.Submitted = Request(pending);
            if (state.Pending != null)
                throw new InvalidOperationException("The completed native shop purchase has not reached the ordered action on this endpoint; save must be retried.");
        }
        if (state.NativePurchases.Any(purchase => purchase.Value > state.Applied.GetValueOrDefault(purchase.Key)))
            throw new InvalidOperationException("A remote native shop purchase has not reached its complete stock update; save must be retried.");
    }

    internal static void NativePurchaseStarted(ulong owner)
    {
        var session = JigsawNativeSession.Current;
        if (session?.Map is not { CurrentNode: not null } map || session.Native.CurrentRoom is not MerchantRoom ||
            owner == RunManager.Instance.NetService.NetId || !map.ActivePlayers.Contains(owner))
            return;
        var state = sequences.GetValue(session, _ => new());
        state.NativePurchases[owner] = state.NativePurchases.GetValueOrDefault(owner) + 1;
    }

    internal static void NativePurchaseCancelled(ulong owner)
    {
        var session = JigsawNativeSession.Current;
        if (session == null || !sequences.TryGetValue(session, out var state))
            return;
        if (state.NativePurchases.TryGetValue(owner, out long count) && count > 0)
            state.NativePurchases[owner] = count - 1;
    }

    internal static void Purchased(MerchantEntry entry)
    {
        var session = JigsawNativeSession.Current;
        if (session?.Map is not { CurrentNode: int node } map || session.Native.CurrentRoom is not MerchantRoom room)
            return;
        var inventory = room.Inventories.FirstOrDefault(candidate => candidate.AllEntries.Contains(entry));
        if (inventory == null || inventory.Player.NetId != RunManager.Instance.NetService.NetId)
            return;
        if (entry is MerchantCardRemovalEntry removal)
            removal.SetUsed();
        var stock = JigsawNativeShopPersistence.Capture(inventory);
        map.RecordShopInventory(node, inventory.Player.NetId, stock);
        var state = sequences.GetValue(session, _ => new());
        var update = new JigsawShopStockUpdate(session.Native.CurrentActIndex, node, map.CreateSave().NextEntry,
            session.EntryRound, ++state.Next, stock);
        state.Pending = update;
        state.Submitted = false;
        if (!Request(update))
            throw new InvalidOperationException("Could not synchronize the completed native shop purchase.");
        if (state.Pending == update)
            state.Submitted = true;
    }

    internal static void Apply(Player sender, JigsawShopStockUpdate update)
    {
        var session = JigsawNativeSession.Current;
        if (session?.Map is not { CurrentNode: int node } map || session.Native.CurrentRoom is not MerchantRoom room ||
            !ReferenceEquals(sender.RunState, session.Native) || !map.ActivePlayers.Contains(sender.NetId) ||
            session.Native.CurrentActIndex != update.Act || node != update.Node ||
            map.CreateSave().NextEntry != update.Entry || session.EntryRound != update.Round)
            return;
        var state = sequences.GetValue(session, _ => new());
        if (update.Sequence <= state.Applied.GetValueOrDefault(sender.NetId))
            return;
        if (sender.NetId != RunManager.Instance.NetService.NetId)
        {
            var index = room.Inventories.FindIndex(inventory => inventory.Player.NetId == sender.NetId);
            if (index < 0)
                throw new InvalidDataException("Shop stock owner is missing from the native room.");
            JigsawNativeShopPersistence.Apply(room.Inventories[index], update.Stock);
            session.CaptureCurrentShop();
        }
        state.Applied[sender.NetId] = update.Sequence;
        if (sender.NetId == RunManager.Instance.NetService.NetId &&
            state.Pending is { } pending && update.Sequence >= pending.Sequence)
        {
            state.Pending = null;
            state.Submitted = false;
        }
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Multiplayer.Game.RewardSynchronizer), "HandleGoldLostMessage")]
internal static class JigsawNativeShopGoldReceived
{
    private static void Prefix(ulong senderId) => JigsawNativeShopSync.NativePurchaseStarted(senderId);
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Multiplayer.Game.OneOffSynchronizer), "HandleMerchantCardRemoval")]
internal static class JigsawNativeShopRemovalReceived
{
    private static void Prefix(ulong senderId) => JigsawNativeShopSync.NativePurchaseStarted(senderId);
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Multiplayer.Game.OneOffSynchronizer), "DoMerchantCardRemoval")]
internal static class JigsawNativeShopRemovalSettled
{
    private static void Postfix(Player player, ref Task<bool> __result)
    {
        if (player.NetId != RunManager.Instance.NetService.NetId)
            __result = Track(__result, player.NetId);
    }

    private static async Task<bool> Track(Task<bool> purchase, ulong owner)
    {
        bool completed = await purchase;
        if (!completed)
            JigsawNativeShopSync.NativePurchaseCancelled(owner);
        return completed;
    }
}

[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.InvokePurchaseCompleted))]
internal static class JigsawNativeShopPurchaseCompleted
{
    private static void Postfix(MerchantEntry __instance) => JigsawNativeShopSync.Purchased(__instance);
}
