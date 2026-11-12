using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace AllMatters.Jigsaw;

public sealed record JigsawNativeShopCard(
    [property: JsonRequired] int Slot,
    [property: JsonRequired] int Cost,
    [property: JsonRequired] bool OnSale,
    [property: JsonRequired] SerializableCard? Original,
    [property: JsonRequired] SerializableCard? Modified,
    [property: JsonRequired] SerializableRelic[] ModifyingRelics,
    [property: JsonRequired] ModelId[] Pool,
    [property: JsonRequired] CardType? Type,
    [property: JsonRequired] CardRarity? Rarity);
public sealed record JigsawNativeShopRelic(
    [property: JsonRequired] int Slot,
    [property: JsonRequired] int Cost,
    [property: JsonRequired] SerializableRelic? Model);
public sealed record JigsawNativeShopPotion(
    [property: JsonRequired] int Slot,
    [property: JsonRequired] int Cost,
    [property: JsonRequired] SerializablePotion? Model);
public sealed record JigsawNativeShopRemoval(
    [property: JsonRequired] int Cost,
    [property: JsonRequired] bool Used);
public sealed record JigsawNativeShopStock(
    [property: JsonRequired] JigsawNativeShopCard[] CharacterCards,
    [property: JsonRequired] JigsawNativeShopCard[] ColorlessCards,
    [property: JsonRequired] JigsawNativeShopRelic[] Relics,
    [property: JsonRequired] JigsawNativeShopPotion[] Potions,
    [property: JsonRequired] JigsawNativeShopRemoval? Removal);

// The complete shop lives in the jigsaw extension of the same native run save.
// Constructors which Populate, CalcCost or MarkSeen are deliberately avoided
// when rebuilding an existing item; they are only for creating new shop stock.
internal static class JigsawNativeShopPersistence
{
    private static int Cost(MerchantEntry entry) => (int)AccessTools.Field(typeof(MerchantEntry), "_cost").GetValue(entry)!;

    internal static JigsawShopInventory Capture(MerchantInventory inventory)
    {
        JigsawNativeShopCard Card(MerchantCardEntry entry, int slot) => new(slot, Cost(entry), entry.IsOnSale,
            entry.CreationResult?.originalCard.ToSerializable(),
            entry.CreationResult is { HasBeenModified: true } creation ? creation.Card.ToSerializable() : null,
            entry.CreationResult?.ModifyingRelics.Select(relic => relic.ToSerializable()).ToArray() ?? [],
            ((IEnumerable<CardModel>)AccessTools.Field(typeof(MerchantCardEntry), "_cardPool").GetValue(entry)!).Select(card => card.Id).ToArray(),
            (CardType?)AccessTools.Field(typeof(MerchantCardEntry), "_cardType").GetValue(entry),
            (CardRarity?)AccessTools.Field(typeof(MerchantCardEntry), "_cardRarity").GetValue(entry));
        var stock = new JigsawNativeShopStock(
            inventory.CharacterCardEntries.Select(Card).ToArray(),
            inventory.ColorlessCardEntries.Select(Card).ToArray(),
            inventory.RelicEntries.Select((entry, slot) => new JigsawNativeShopRelic(slot, Cost(entry), entry.Model?.ToSerializable())).ToArray(),
            inventory.PotionEntries.Select((entry, slot) => new JigsawNativeShopPotion(slot, Cost(entry), entry.Model?.ToSerializable(slot))).ToArray(),
            inventory.CardRemovalEntry is { } removal ? new(Cost(removal), removal.Used) : null);
        var cards = inventory.CardEntries.Select((entry, slot) => new JigsawShopCard(
            entry.CreationResult?.Card.Id.ToString() ?? "", slot, entry.CreationResult?.Card.IsUpgraded == true,
            entry.CreationResult?.Card.Enchantment?.Id.ToString(), Cost(entry))).ToArray();
        var relics = inventory.RelicEntries.Select((entry, slot) => new JigsawShopRelic(
            entry.Model?.Id.ToString() ?? "", slot, null, Cost(entry))).ToArray();
        var prices = inventory.AllEntries.Select((entry, slot) => (entry, slot)).ToDictionary(
            value => $"{value.entry.GetType().Name}:{value.slot}", value => Cost(value.entry));
        var sales = inventory.CardEntries.Select((entry, slot) => (entry, slot)).Where(value => value.entry.IsOnSale)
            .ToDictionary(value => $"card:{value.slot}", _ => true);
        var sold = inventory.AllEntries.Select((entry, slot) => (entry, slot)).Where(value => !value.entry.IsStocked)
            .Select(value => $"{value.entry.GetType().Name}:{value.slot}").ToHashSet();
        return new(cards, relics, stock.Removal?.Used == true ? 1 : 0, stock.Removal?.Used == false, prices, sales, sold)
        { NativeStock = stock };
    }

    internal static MerchantInventory Restore(Player player, JigsawShopInventory saved)
    {
        var stock = saved.NativeStock ?? throw new InvalidDataException("商店检查点缺少完整原生库存，不能重新抽取。");
        if (stock.CharacterCards == null || stock.ColorlessCards == null || stock.Relics == null || stock.Potions == null)
            throw new InvalidDataException("商店检查点的库存列表不完整。");
        var addedCards = new List<CardModel>();
        try
        { return RestoreCore(player, stock, addedCards); }
        catch
        {
            foreach (var card in addedCards)
                if (!player.Deck.Cards.Contains(card) && player.RunState.ContainsCard(card))
                    player.RunState.RemoveCard(card);
            throw;
        }
    }

    private static MerchantInventory RestoreCore(Player player, JigsawNativeShopStock stock, List<CardModel> addedCards)
    {
        var inventory = new MerchantInventory(player);
        MerchantCardEntry Card(JigsawNativeShopCard value, int index)
        {
            Require(value.Slot == index && value.Cost >= 0 && value.Pool != null &&
                value.Type.HasValue != value.Rarity.HasValue && (value.Original != null || value.Modified == null), "卡牌槽位数据无效。");
            var pool = value.Pool!.Select(id => ModelDb.GetById<CardModel>(id)).ToArray();
            var entry = value.Type is { } type ? new MerchantCardEntry(player, inventory, pool, type)
                : new MerchantCardEntry(player, inventory, pool, value.Rarity!.Value);
            CardCreationResult? creation = null;
            if (value.Original is { } original)
            {
                ValidateCard(original);
                var card = CardModel.FromSerializable(original);
                player.RunState.AddCard(card, player);
                addedCards.Add(card);
                creation = new(card);
                if (value.Modified is { } modified)
                {
                    ValidateCard(modified);
                    var result = CardModel.FromSerializable(modified);
                    player.RunState.AddCard(result, player);
                    addedCards.Add(result);
                    creation.ModifyCard(result);
                }
                Require(value.ModifyingRelics != null, "卡牌修改来源缺失。");
                foreach (var relic in value.ModifyingRelics!)
                {
                    ValidateRelic(relic);
                    var ownerRelic = player.Relics.FirstOrDefault(candidate => candidate.Id == relic.Id) ?? RelicModel.FromSerializable(relic);
                    creation.ModifyCard(creation.Card, ownerRelic);
                }
            }
            Set(entry, nameof(MerchantCardEntry.CreationResult), creation);
            Set(entry, nameof(MerchantCardEntry.IsOnSale), value.OnSale);
            SetCost(entry, value.Cost);
            return entry;
        }
        var character = (List<MerchantCardEntry>)AccessTools.Field(typeof(MerchantInventory), "_characterCardEntries").GetValue(inventory)!;
        var colorless = (List<MerchantCardEntry>)AccessTools.Field(typeof(MerchantInventory), "_colorlessCardEntries").GetValue(inventory)!;
        character.AddRange(stock.CharacterCards.Select(Card));
        colorless.AddRange(stock.ColorlessCards.Select(Card));
        foreach (var (value, index) in stock.Relics.Select((value, index) => (value, index)))
        {
            Require(value.Slot == index && value.Cost >= 0, "遗物槽位数据无效。");
            var entry = Empty<MerchantRelicEntry>(player);
            if (value.Model != null)
                ValidateRelic(value.Model);
            Set(entry, nameof(MerchantRelicEntry.Model), value.Model == null ? null : RelicModel.FromSerializable(value.Model));
            SetCost(entry, value.Cost);
            inventory.AddRelicEntry(entry);
        }
        var potions = (List<MerchantPotionEntry>)AccessTools.Field(typeof(MerchantInventory), "_potionEntries").GetValue(inventory)!;
        foreach (var (value, index) in stock.Potions.Select((value, index) => (value, index)))
        {
            Require(value.Slot == index && value.Cost >= 0 && (value.Model == null || value.Model.SlotIndex == index), "药水槽位数据无效。");
            var entry = Empty<MerchantPotionEntry>(player);
            if (value.Model != null)
            {
                Require(value.Model.Id != null, "药水标识缺失。");
                _ = ModelDb.GetById<PotionModel>(value.Model.Id!);
            }
            Set(entry, nameof(MerchantPotionEntry.Model), value.Model == null ? null : PotionModel.FromSerializable(value.Model));
            SetCost(entry, value.Cost);
            potions.Add(entry);
        }
        if (stock.Removal is { } removal)
        {
            Require(removal.Cost >= 0, "删牌服务价格无效。");
            var entry = new MerchantCardRemovalEntry(player);
            if (removal.Used)
                entry.SetUsed();
            // An unused service follows the native player's current global removal
            // count. Cost applies relic discounts dynamically when the room is live.
            if (removal.Used)
                SetCost(entry, removal.Cost);
            Set(inventory, nameof(MerchantInventory.CardRemovalEntry), entry);
        }
        var update = AccessTools.Method(typeof(MerchantInventory), "UpdateEntries")
            .CreateDelegate<Action<PurchaseStatus, MerchantEntry>>(inventory);
        foreach (var entry in inventory.AllEntries)
            entry.PurchaseCompleted += update;
        return inventory;
    }

    internal static void Apply(MerchantInventory current, JigsawShopInventory saved)
    {
        if (current.Player.RunState.CurrentRoom is not MegaCrit.Sts2.Core.Rooms.MerchantRoom room)
            throw new InvalidOperationException("The native shop is no longer active.");
        int index = room.Inventories.IndexOf(current);
        if (index < 0)
            throw new InvalidOperationException("The current native shop inventory is missing.");
        var replacement = Restore(current.Player, saved);
        foreach (var card in current.CardEntries.SelectMany(entry => entry.CreationResult is { } result
            ? new CardModel[] { result.originalCard, result.Card } : []).Distinct())
        {
            if (!current.Player.Deck.Cards.Contains(card) && current.Player.RunState.ContainsCard(card))
                current.Player.RunState.RemoveCard(card);
        }
        room.Inventories[index] = replacement;
    }

    private static void ValidateCard(SerializableCard card)
    {
        Require(card.Id != null && card.CurrentUpgradeLevel >= 0, "卡牌原生数据无效。");
        _ = ModelDb.GetById<CardModel>(card.Id!);
    }
    private static void ValidateRelic(SerializableRelic relic)
    {
        Require(relic.Id != null, "遗物标识缺失。");
        _ = ModelDb.GetById<RelicModel>(relic.Id!);
    }
    private static T Empty<T>(Player player) where T : MerchantEntry
    {
        var entry = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        AccessTools.Field(typeof(MerchantEntry), "_player").SetValue(entry, player);
        return entry;
    }
    private static void Set(object target, string property, object? value) =>
        AccessTools.Property(target.GetType(), property).SetValue(target, value);
    private static void SetCost(MerchantEntry entry, int cost) => AccessTools.Field(typeof(MerchantEntry), "_cost").SetValue(entry, cost);
    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidDataException(reason);
    }
}
