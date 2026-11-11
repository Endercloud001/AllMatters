using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Reflection.Emit;
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
        internal readonly Dictionary<ulong, int> NativeRemovalsInFlight = [];
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
        if (state.NativeRemovalsInFlight.Values.Any(count => count > 0))
            throw new InvalidOperationException("A remote native shop purchase is still removing a card; save must be retried.");
    }

    internal static void NativePurchaseStarted(ulong owner, bool removal = false)
    {
        var session = JigsawNativeSession.Current;
        if (session?.Map is not { CurrentNode: not null } map || session.Native.CurrentRoom is not MerchantRoom ||
            owner == RunManager.Instance.NetService.NetId || !map.ActivePlayers.Contains(owner))
            return;
        var state = sequences.GetValue(session, _ => new());
        state.NativePurchases[owner] = state.NativePurchases.GetValueOrDefault(owner) + 1;
        if (removal)
            state.NativeRemovalsInFlight[owner] = state.NativeRemovalsInFlight.GetValueOrDefault(owner) + 1;
    }

    internal static void NativeRemovalFinished(JigsawNativeSession session, ulong owner, bool completed)
    {
        if (!sequences.TryGetValue(session, out var state))
            return;
        if (state.NativeRemovalsInFlight.TryGetValue(owner, out int inFlight) && inFlight > 0)
            state.NativeRemovalsInFlight[owner] = inFlight - 1;
        if (!completed && state.NativePurchases.TryGetValue(owner, out long count) && count > 0)
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
    private static void Prefix(ulong senderId) => JigsawNativeShopSync.NativePurchaseStarted(senderId, removal: true);

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var nativeCall = AccessTools.Method(typeof(MegaCrit.Sts2.Core.Multiplayer.Game.OneOffSynchronizer),
            "DoMerchantCardRemoval", [typeof(Player), typeof(int), typeof(bool)]);
        var trackCall = AccessTools.Method(typeof(JigsawNativeShopRemovalReceived), nameof(Track));
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                Equals(instruction.operand, nativeCall))
            {
                matches++;
                yield return new CodeInstruction(OpCodes.Ldarg_2);
                yield return new CodeInstruction(OpCodes.Call, trackCall);
            }
        }
        if (matches != 1)
            throw new InvalidOperationException($"Expected one native merchant removal task call, found {matches}.");
    }

    private static async Task<bool> Track(Task<bool> purchase, ulong owner)
    {
        var session = JigsawNativeSession.Current;
        bool completed = await purchase;
        if (session != null)
            JigsawNativeShopSync.NativeRemovalFinished(session, owner, completed);
        return completed;
    }
}

[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.InvokePurchaseCompleted))]
internal static class JigsawNativeShopPurchaseCompleted
{
    private static void Postfix(MerchantEntry __instance) => JigsawNativeShopSync.Purchased(__instance);
}
