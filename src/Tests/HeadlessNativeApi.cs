using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

// Version dispatch only: all selection, purchase and reward behavior remains native.
internal static class HeadlessNativeApi
{
    internal static IDisposable UseSelector(IRunState run, ICardSelector selector)
    {
#if STS2_PUBLIC
        return UseSelector(selector);
#else
        var property = HarmonyLib.AccessTools.Property(typeof(RunManager), nameof(RunManager.CardSelectorStack));
        var previous = property.GetValue(RunManager.Instance);
        var collection = previous == null ? null : HarmonyLib.AccessTools.Field(previous.GetType(), "_playerCollection").GetValue(previous);
        if (ReferenceEquals(collection, run))
            return UseSelector(selector);
        property.SetValue(RunManager.Instance, new MegaCrit.Sts2.Core.CardSelection.CardSelectorStack(run));
        try { return new SelectionScope(UseSelector(selector), () => property.SetValue(RunManager.Instance, previous)); }
        catch { property.SetValue(RunManager.Instance, previous); throw; }
#endif
    }

    private sealed class SelectionScope(IDisposable selection, Action restore) : IDisposable
    {
        public void Dispose()
        {
            try { selection.Dispose(); }
            finally { restore(); }
        }
    }

    internal static IDisposable UseSelector(ICardSelector selector)
    {
#if STS2_PUBLIC
        return CardSelectCmd.UseSelector(selector);
#else
        return MegaCrit.Sts2.Core.CardSelection.CardSelectorStack.SetTestSelector(selector);
#endif
    }

    internal static List<MerchantInventory> Inventories(this MerchantRoom room) => JigsawNativeShopPersistence.Inventories(room);

    internal static Task Proceed(RunManager manager)
    {
#if STS2_PUBLIC
        return manager.ProceedFromTerminalRewardsScreen();
#else
        return manager.ProceedFromTerminalRewardsScreenAsync();
#endif
    }

    internal static Task<bool> LocalRemoval(MerchantInventory inventory)
    {
#if STS2_PUBLIC
        return RunManager.Instance.OneOffSynchronizer.DoLocalMerchantCardRemoval(0);
#else
        return RunManager.Instance.MerchantSynchronizer.PurchaseLocalEntry(inventory.CardRemovalEntry!);
#endif
    }

    internal static Task<bool> PurchaseLocal(this MerchantEntry entry, MerchantInventory inventory)
    {
#if STS2_PUBLIC
        return entry is MerchantCardRemovalEntry removal
            ? removal.OnTryPurchaseWrapper(inventory, false, true)
            : entry.OnTryPurchaseWrapper(inventory);
#else
        return RunManager.Instance.MerchantSynchronizer.PurchaseLocalEntry(entry);
#endif
    }

    internal static Task<bool> PurchaseUnsynchronized(this MerchantEntry entry, MerchantInventory inventory,
        bool ignoreCost = false, bool cancelable = true)
    {
#if STS2_PUBLIC
        return entry is MerchantCardRemovalEntry removal
            ? removal.OnTryPurchaseWrapper(inventory, ignoreCost, cancelable)
            : entry.OnTryPurchaseWrapper(inventory, ignoreCost);
#else
        if (entry is MerchantCardRemovalEntry removal && !cancelable)
            removal.SetPurchaseUncancelable();
        return entry.OnTryPurchaseUnsynchronized(inventory, ignoreCost);
#endif
    }
}
