using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace AllMatters.Events;

internal static class NativeLockedOptions
{
    // Construct at focus time so language changes and current card previews are retained.
    internal static IEnumerable<IHoverTip> Tips(EventModel model, EventOption option)
    {
        if (!option.IsLocked) return option.HoverTips;
        bool zh = LocManager.Instance.Language == "zhs";
        string? reason = option.TextKey switch
        {
            "SELF_HELP_BOOK.pages.INITIAL.options.READ_THE_BACK" => zh ? "没有能够接受本次附魔的攻击牌。" : "No Attack card can receive the offered enchantment.",
            "SELF_HELP_BOOK.pages.INITIAL.options.READ_PASSAGE" => zh ? "没有能够接受本次附魔的技能牌。" : "No Skill card can receive the offered enchantment.",
            "SELF_HELP_BOOK.pages.INITIAL.options.READ_ENTIRE_BOOK" => zh ? "没有能够接受本次附魔的能力牌。" : "No Power card can receive the offered enchantment.",
            "SLIPPERY_BRIDGE.pages.INITIAL.options.AM_FORWARD" or "LOST_WISP.pages.INITIAL.options.AM_BURN" => zh ? "牌组中没有能够移除的牌。" : "Your Deck has no removable cards.",
            "SAPPHIRE_SEED.pages.INITIAL.options.AM_CATALYZE" => zh ? "需要至少2张能够接受播种附魔的牌。" : "Requires at least 2 cards that can receive the Sown enchantment.",
            "AMALGAMATOR.pages.INITIAL.options.AM_FUSE" => model.Owner!.Deck.Cards.Count(c => c.IsRemovable) < 2
                ? zh ? "牌组中需要至少2张能够移除的牌。" : "Requires at least 2 removable cards in your Deck."
                : zh ? "角色与无色牌池中没有可获得的稀有牌。" : "No eligible Rare card is available from the character and Colorless card pools.",
            "ZEN_WEAVER.pages.INITIAL.options.AM_UPGRADE_1" => zh ? "需要125金币。" : "Requires 125 Gold.",
            "ZEN_WEAVER.pages.INITIAL.options.AM_UPGRADE_2" => zh ? "需要250金币。" : "Requires 250 Gold.",
            "WHISPERING_HOLLOW.pages.INITIAL.options.AM_HUG_1" => zh ? "牌组中需要至少1张能够变化的牌。" : "Requires at least 1 transformable card in your Deck.",
            "WHISPERING_HOLLOW.pages.INITIAL.options.AM_HUG_2" => zh ? "牌组中需要至少2张能够变化的牌。" : "Requires at least 2 transformable cards in your Deck.",
            "THE_FUTURE_OF_POTIONS.pages.INITIAL.options.AM_REFRESH" => zh ? "需要50金币。" : "Requires 50 Gold.",
            "STONE_OF_ALL_TIME.pages.INITIAL.options.PUSH_LOCKED" => EnchantmentReason(ModelDb.Enchantment<Vigorous>(), zh),
            "GRAVE_OF_THE_FORGOTTEN.pages.INITIAL.options.CONFRONT_LOCKED" => EnchantmentReason(ModelDb.Enchantment<SoulsPower>(), zh),
            "SYMBIOTE.pages.INITIAL.options.APPROACH_LOCKED" => EnchantmentReason(ModelDb.Enchantment<Corrupted>(), zh),
            "ENDLESS_CONVEYOR.pages.ALL.options.LOCKED" => zh ? $"需要{model.DynamicVars.Gold.IntValue}金币。" : $"Requires {model.DynamicVars.Gold.IntValue} Gold.",
            _ => null
        };
        var title = new LocString("events", "ALL_MATTERS_LOCKED_OPTION.title");
        IHoverTip tip = reason is null ? new HoverTip(title, option.Description) : new HoverTip(title, reason);
        return new[] { tip }.Concat(option.HoverTips);
    }

    private static string EnchantmentReason(EnchantmentModel enchantment, bool zh) => zh
        ? $"牌组中没有能够接受{enchantment.Title.GetFormattedText()}附魔的牌。"
        : $"No card in your Deck can receive the {enchantment.Title.GetFormattedText()} enchantment.";
}

[HarmonyPatch(typeof(NEventOptionButton), "OnFocus")]
internal static class NativeLockedOptionFocus
{
    private static void Postfix(NEventOptionButton __instance)
    {
        if (__instance.Option.IsLocked)
            NHoverTipSet.CreateAndShow(__instance, NativeLockedOptions.Tips(__instance.Event, __instance.Option),
                __instance.Event.LayoutType != EventLayoutType.Combat ? HoverTipAlignment.Left : HoverTipAlignment.Right);
    }
}

[HarmonyPatch(typeof(NEventOptionButton), "OnUnfocus")]
internal static class NativeLockedOptionUnfocus
{
    private static void Postfix(NEventOptionButton __instance)
    {
        if (__instance.Option.IsLocked) NHoverTipSet.Remove(__instance);
    }
}

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class NativeLockedOptionLocalization
{
    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        if (name == "events") __result.MergeWith(new Dictionary<string, string>
        {
            ["ALL_MATTERS_LOCKED_OPTION.title"] = __instance.Language == "zhs" ? "无法选择" : "Unavailable"
        });
    }
}
