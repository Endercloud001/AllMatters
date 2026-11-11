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
        internal long Next;
    }

    private static readonly ConditionalWeakTable<JigsawNativeSession, SequenceState> sequences = new();
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
        if (!RitsuLibManagedNetActions.Request(RunManager.Instance, descriptor, update))
            throw new InvalidOperationException("Could not synchronize the completed native shop purchase.");
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
    }
}

[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.InvokePurchaseCompleted))]
internal static class JigsawNativeShopPurchaseCompleted
{
    private static void Postfix(MerchantEntry __instance) => JigsawNativeShopSync.Purchased(__instance);
}
