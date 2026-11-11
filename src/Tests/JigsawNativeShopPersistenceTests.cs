using System.Text.Json;
using System.Text.Json.Nodes;
using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawNativeShopPersistenceTests
{
    internal const string Name = "Jigsaw native shop purchases and complete stock survive a new session revisit";
    internal const string FaultName = "Jigsaw native shop rejects missing and incomplete persisted stock without rerolling";
    internal const string AdapterName = "Jigsaw native shop reconstruction preserves full stock and actual purchase hooks";
    internal const string SyncFailureName = "Jigsaw native shop refuses a save after ordered purchase submission fails";

    internal static Task Revisit() => Revisit(null);
    internal static Task SyncFailure() => Revisit("failed-request");
    internal static async Task Faults()
    {
        foreach (string fault in new[] { "missing-stock", "missing-price" })
            await Revisit(fault);
    }

    internal static async Task Reconstruct()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas");
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
        var native = RunState.CreateForTest(players: [player], acts: [ModelDb.Act<Overgrowth>()], seed: "REBUILD_SHOP_93");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        MegaCrit.Sts2.Core.Context.LocalContext.NetId = player.NetId;
        try
        {
            await RunManager.Instance.EnterRoom(new MerchantRoom());
            player.Gold = 20000;
            var original = ((MerchantRoom)native.CurrentRoom!).Inventories.Single();
            CardCmd.Upgrade(original.CardEntries.ElementAt(1).CreationResult!.Card);
            CardCmd.Enchant<Sharp>(original.CardEntries.ElementAt(1).CreationResult!.Card, 1);
            await original.CardEntries.First().OnTryPurchaseWrapper(original);
            string expected = Stock(original);
            ulong rng = HeadlessTestSetup.RngPosition(player.PlayerRng.Shops);
            var saved = JsonSerializer.Deserialize<JigsawShopInventory>(JsonSerializer.Serialize(JigsawNativeShopPersistence.Capture(original)))!;
            string runBeforeInvalid = JsonSerializer.Serialize(RunManager.Instance.ToSave(null));
            var invalidRelics = saved.NativeStock!.Relics.ToArray();
            invalidRelics[0] = invalidRelics[0] with { Slot = -1 };
            bool rejectedInvalid = false;
            try { JigsawNativeShopPersistence.Apply(original, saved with
                { NativeStock = saved.NativeStock with { Relics = invalidRelics } }); }
            catch (InvalidDataException) { rejectedInvalid = true; }
            Require(rejectedInvalid && JsonSerializer.Serialize(RunManager.Instance.ToSave(null)) == runBeforeInvalid &&
                ReferenceEquals(((MerchantRoom)native.CurrentRoom!).Inventories.Single(), original),
                "A rejected late shop slot leaves no restored stock cards or replaced inventory.");
            var restored = JigsawNativeShopPersistence.Restore(player, saved);
            Require(Stock(restored) == expected && HeadlessTestSetup.RngPosition(player.PlayerRng.Shops) == rng,
                "Rebuilding actual native stock retains all serialized properties without drawing new stock or prices.");
            Require(await restored.CardEntries.ElementAt(1).OnTryPurchaseWrapper(restored),
                "A rebuilt native entry supports an actual card purchase and original update callbacks.");
            var synchronized = JigsawNativeShopPersistence.Capture(restored);
            ulong beforeApply = HeadlessTestSetup.RngPosition(player.PlayerRng.Shops);
            var replacedStockCard = original.CardEntries.ElementAt(1).CreationResult!.Card;
            JigsawNativeShopPersistence.Apply(original, synchronized);
            Require(Stock(((MerchantRoom)native.CurrentRoom!).Inventories.Single()) == Stock(restored) &&
                HeadlessTestSetup.RngPosition(player.PlayerRng.Shops) == beforeApply &&
                !native.ContainsCard(replacedStockCard),
                "Applying an owner's completed native purchase replaces remote stock without another shop roll or orphan card.");
            player.ExtraFields.CardShopRemovalsUsed++;
            var repriced = JigsawNativeShopPersistence.Restore(player, saved);
            Require(repriced.CardRemovalEntry!.Cost == new MerchantCardRemovalEntry(player).Cost,
                "Unused removal service follows the native player's current global removal count.");
            original.CardRemovalEntry!.SetUsed();
            var used = JigsawNativeShopPersistence.Restore(player, JigsawNativeShopPersistence.Capture(original));
            Require(used.CardRemovalEntry!.Used && !used.CardRemovalEntry.IsStocked,
                "A consumed removal service remains consumed when its stock is reconstructed.");
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static async Task Revisit(string? fault)
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas");
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
        var native = RunState.CreateForTest(players: [player], acts: [ModelDb.Act<Overgrowth>()], seed: "PERSISTENT_SHOP_93");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        MegaCrit.Sts2.Core.Context.LocalContext.NetId = player.NetId;
        var io = new MockGodotFileIo("jigsaw-shop93-memory-only");
        var saves = new RunSaveManager(93, io, new MigrationManager(io), forceSynchronous: true);
        JigsawNativeSaveHost.Save = room => saves.SaveRun(RunManager.Instance.ToSave(room), false);
        try
        {
            native.ExtraFields.StartedWithNeow = true;
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            await RunManager.Instance.EnterMapCoord(native.Map.StartingMapPoint.coord);
            await RunManager.Instance.EnterRoom(new MapRoom());
            var map = Enumerable.Range(0, 40).Select(seed => JigsawRun.Create(seed))
                .First(candidate => candidate.Pool[0].Nodes[0].Room == JigsawRoom.Shop);
            Require(map.Take(1, 0).Success && map.Drop(1, new(3, 0)).Success && map.AddEdge(1, 0, 1).Success &&
                map.ChooseEnd(1, EndChoice.Manual).Success && map.Inspect().Success &&
                map.CompleteAncient(1).Success && map.Continue(1).Success, "Prepare a valid generated pool with an adjacent shop.");
            JigsawCheckpoint.Capture(native, map);
            JigsawNativeSession.Restore(native);
            var session = JigsawNativeSession.Current!;
            player.Gold = 20000;
            await RelicCmd.Obtain(ModelDb.Relic<MembershipCard>().ToMutable(), player);
            await session.Enter(1);
            Require(native.CurrentRoom is MerchantRoom, "The logical shop enters a real native MerchantRoom.");
            var inventory = ((MerchantRoom)native.CurrentRoom).Inventories.Single();
            var cards = inventory.CardEntries.ToArray();
            if (fault == "failed-request")
            {
                int goldBefore = player.Gold, deckBefore = player.Deck.Cards.Count;
                JigsawShopStockUpdate? update = null;
                JigsawNativeShopSync.RequestOverride = message => { update = message; return false; };
                try
                {
                    try { await cards[0].OnTryPurchaseWrapper(inventory); }
                    catch (InvalidOperationException) { }
                    Require(player.Gold < goldBefore && player.Deck.Cards.Count == deckBefore + 1 &&
                        !cards[0].IsStocked && update != null,
                        "A failed stock submission follows an already completed native purchase with its slot sold out.");
                    bool rejected = false;
                    try { _ = RunManager.Instance.ToSave(null); }
                    catch (InvalidOperationException) { rejected = true; }
                    Require(rejected, "A failed submission cannot be saved as a complete shop transaction.");
                    JigsawNativeShopSync.RequestOverride = message => { update = message; return true; };
                    rejected = false;
                    try { _ = RunManager.Instance.ToSave(null); }
                    catch (InvalidOperationException) { rejected = true; }
                    Require(rejected && update != null, "Retry remains unsettled until its local ordered echo arrives.");
                    JigsawNativeShopSync.Apply(player, update!);
                    _ = RunManager.Instance.ToSave(null);
                }
                finally { JigsawNativeShopSync.RequestOverride = null; }
                return;
            }
            CardCmd.Upgrade(cards[1].CreationResult!.Card);
            CardCmd.Enchant<Sharp>(cards[1].CreationResult!.Card, 1);
            inventory.RelicEntries[0].Model!.FloorAddedToDeck = 17;
            Require(await cards[0].OnTryPurchaseWrapper(inventory), "Complete an actual native card purchase.");
            Require(await inventory.PotionEntries[0].OnTryPurchaseWrapper(inventory), "Complete an actual native potion purchase.");
            Require(!cards[0].IsStocked, "The native transaction clears its purchased slot.");
            string expected = Stock(inventory);
            int gold = player.Gold, deck = player.Deck.Cards.Count;
            // Headless rooms have no NMerchantRoom button; deliver its accepted
            // HideScreen exit signal before completing the formal map transition.
            JigsawNativeExploration.MerchantExited(session);
            session.CompleteCurrentRoom(true);
            Require(session.Map!.CurrentNode == null && session.Map.Completed.Contains(1), "The shop is completed before saving for a later revisit.");
            await saves.SaveRun(RunManager.Instance.ToSave(null), false);
            ulong savedShopRandom = HeadlessTestSetup.RngPosition(player.PlayerRng.Shops);
            if (fault != null)
            {
                string path = RunSaveManager.GetRunSavePath(93, "current_run.save");
                var document = JsonNode.Parse(io.ReadFile(path))!;
                var persistedShop = Find(document, "ShopStates")!.AsObject().First().Value!.AsObject();
                if (fault == "missing-stock") persistedShop.Remove("NativeStock");
                else persistedShop["NativeStock"]!["CharacterCards"]![0]!.AsObject().Remove("Cost");
                io.SetFileContent(path, document.ToJsonString());
            }
            var loaded = saves.LoadRunSave();
            Require(loaded.Success && loaded.SaveData != null, "Read the real complete native save after purchasing.");
            RunManager.Instance.CleanUp(false);
            if (fault != null)
            {
                bool rejected = false;
                RunState? invalid = null;
                try
                {
                    invalid = RunState.FromSerializable(loaded.SaveData!);
                    await RunManager.Instance.SetUpSavedSingleplayer(invalid, loaded.SaveData!);
                    await RunManager.Instance.GenerateMap();
                    await RunManager.Instance.LoadIntoLatestMapCoord(null);
                    await JigsawNativeSession.Current!.Enter(1);
                }
                catch (Exception error) when (error is InvalidDataException or ArgumentException or JsonException) { rejected = true; }
                Require(rejected, $"Invalid persisted shop ({fault}) must fail instead of generating replacement items.");
                Require(invalid == null || HeadlessTestSetup.RngPosition(invalid.Players.Single().PlayerRng.Shops) == savedShopRandom,
                    "Rejected stock must not consume a replacement shop roll.");
                return;
            }
            var restored = RunState.FromSerializable(loaded.SaveData!);
            await RunManager.Instance.SetUpSavedSingleplayer(restored, loaded.SaveData!);
            MegaCrit.Sts2.Core.Context.LocalContext.NetId = restored.Players[0].NetId;
            await RunManager.Instance.GenerateMap();
            await RunManager.Instance.LoadIntoLatestMapCoord(null);
            var restoredPlayer = restored.Players.Single();
            var recovered = JigsawNativeSession.Current!;
            Require(restoredPlayer.Gold == gold && restoredPlayer.Deck.Cards.Count == deck,
                "The native player restores the actual gold and purchased card from the same point.");
            ulong shops = HeadlessTestSetup.RngPosition(restoredPlayer.PlayerRng.Shops);
            await recovered.Enter(1);
            var revisited = ((MerchantRoom)restored.CurrentRoom!).Inventories.Single();
            Require(!revisited.CardEntries.First().IsStocked, "A purchased card slot remains sold out after native loading and revisiting.");
            Require(!revisited.PotionEntries[0].IsStocked, "A purchased potion slot remains sold out.");
            if (Stock(revisited) != expected && Environment.GetEnvironmentVariable("JIGSAW_SHOP_EVIDENCE") is { } evidence)
            {
                File.WriteAllText(Path.Combine(evidence, "expected.json"), expected);
                File.WriteAllText(Path.Combine(evidence, "actual.json"), Stock(revisited));
            }
            Require(Stock(revisited) == expected, "Every card, relic, potion, price, sale and removal state matches the saved native shop.");
            Require(HeadlessTestSetup.RngPosition(restoredPlayer.PlayerRng.Shops) == shops,
                "Revisiting a persisted shop consumes no shop RNG.");
            Require(recovered.Map!.Steps == 2 && recovered.Map.FirstProgress == 1,
                $"The revisit costs one step and retains first-entry progress (steps={recovered.Map.Steps}, progress={recovered.Map.FirstProgress}).");
            var upgraded = revisited.CardEntries.ElementAt(1);
            var purchasedCard = upgraded.CreationResult!.Card;
            var cardState = purchasedCard.ToSerializable();
            Require(await upgraded.OnTryPurchaseWrapper(revisited) && !upgraded.IsStocked,
                "A reconstructed entry completes another real native transaction.");
            // Native deck insertion records the acquisition floor on this same card.
            cardState.FloorAddedToDeck = purchasedCard.FloorAddedToDeck;
            Require(restoredPlayer.Deck.Cards.Contains(purchasedCard) &&
                JsonSerializer.Serialize(purchasedCard.ToSerializable()) == JsonSerializer.Serialize(cardState),
                "Purchasing restored stock grants the exact upgraded and enchanted card.");
        }
        finally { JigsawNativeSaveHost.Save = null; RunManager.Instance.CleanUp(false); }
    }

    private static string Stock(MerchantInventory inventory) => JsonSerializer.Serialize(new
    {
        Cards = inventory.CardEntries.Select(entry => new { Card = entry.CreationResult?.Card.ToSerializable(), entry.Cost, entry.IsOnSale, entry.IsStocked }),
        Relics = inventory.RelicEntries.Select(entry => new { Model = entry.Model?.ToSerializable(), entry.Cost, entry.IsStocked }),
        Potions = inventory.PotionEntries.Select((entry, slot) => new { Model = entry.Model?.ToSerializable(slot), entry.Cost, entry.IsStocked }),
        Purge = new { inventory.CardRemovalEntry!.Used, inventory.CardRemovalEntry.Cost }
    });

    private static JsonNode? Find(JsonNode node, string property)
    {
        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue(property, out var found)) return found;
            foreach (var value in obj.Select(pair => pair.Value).OfType<JsonNode>())
                if (Find(value, property) is { } nested) return nested;
        }
        else if (node is JsonArray array)
            foreach (var value in array.OfType<JsonNode>())
                if (Find(value, property) is { } nested) return nested;
        return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
