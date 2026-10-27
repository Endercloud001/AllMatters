using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

internal static class NativeLockedOptionTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Native locked options explain actual missing requirements in both languages and keep previews", Reasons),
        ("Native locked options distinguish un-enchantable cards and current insufficient-gold costs", NativeRequirements),
        ("Native locked options focus shows reasons and unfocus removes them without enabling selection", Focus)
    ];

    private static IHoverTip[] Tips(EventModel model, EventOption option)
    {
        // NEventOptionButton._Ready binds these before a player can focus it.
        model.DynamicVars.AddTo(option.Description);
        model.DynamicVars.AddTo(option.Title);
        return ((IEnumerable<IHoverTip>)AccessTools.Method(typeof(AllMatters.ModEntry).Assembly.GetType("AllMatters.Events.NativeLockedOptions"), "Tips")
            .Invoke(null, [model, option])!).ToArray();
    }

    private static async Task Reasons()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var player = NativeEventTests.Player("LOCK_REASONS");
        player.Gold = 0;
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        await PotionCmd.TryToProcure<BlockPotion>(player);
        var models = new[]
        {
            await NativeEventTests.Begin<SlipperyBridge>(player),
            await NativeEventTests.Begin<SelfHelpBook>(player),
            await NativeEventTests.Begin<SapphireSeed>(player),
            await NativeEventTests.Begin<Amalgamator>(player),
            await NativeEventTests.Begin<LostWisp>(player),
            await NativeEventTests.Begin<WhisperingHollow>(player),
            await NativeEventTests.Begin<ZenWeaver>(player),
            await NativeEventTests.Begin<MorphicGrove>(player)
        };
        foreach (string suffix in new[] { ".AM_UPGRADE_1", ".AM_HUG_1", ".AM_HUG_2" })
        {
            if (models.Any(m => m.CurrentOptions.Any(o => o.TextKey.EndsWith(suffix)))) continue;
            EventModel? matching = null;
            for (int i = 0; i < 20 && matching is null; i++)
            {
                var candidatePlayer = NativeEventTests.Player("LOCK_VARIANT" + i);
                candidatePlayer.Gold = 0;
                foreach (var card in candidatePlayer.Deck.Cards.ToArray()) candidatePlayer.Deck.RemoveInternal(card);
                var candidate = suffix.Contains("UPGRADE")
                    ? await NativeEventTests.Begin<ZenWeaver>(candidatePlayer)
                    : await NativeEventTests.Begin<WhisperingHollow>(candidatePlayer);
                if (candidate.CurrentOptions.Any(o => o.TextKey.EndsWith(suffix))) matching = candidate;
            }
            NativeEventTests.Check(matching is not null, "Exercise random branch " + suffix);
            models = models.Append(matching!).ToArray();
        }
        var cleric = await NativeEventTests.Begin<AllMatters.Events.Cleric>(player);
        var checkedKeys = new List<string>();
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                foreach (var model in models)
                foreach (var option in model.CurrentOptions.Where(o => o.IsLocked &&
                    (o.TextKey.Contains(".AM_") || model is SelfHelpBook)))
                {
                    var previews = option.HoverTips.ToArray();
                    var tips = Tips(model, option);
                    var reason = (HoverTip)tips[0];
                    NativeEventTests.Check(reason.Title == (language == "zhs" ? "无法选择" : "Unavailable") &&
                        !string.IsNullOrWhiteSpace(reason.Description), "Every locked branch needs a localized reason.");
                    NativeEventTests.Check(tips.Skip(1).SequenceEqual(previews) && option.HoverTips.SequenceEqual(previews),
                        "Adding a reason must preserve existing previews without mutating or accumulating them.");
                    string[] expected = model switch
                    {
                        SelfHelpBook => ["附魔", "enchantment"],
                        SapphireSeed => ["2", "2"],
                        Amalgamator => ["2", "2"],
                        LostWisp or SlipperyBridge => ["移除", "removable"],
                        WhisperingHollow => ["变化", "transformable"],
                        ZenWeaver => [option.TextKey.EndsWith("_1") ? "125" : "250", option.TextKey.EndsWith("_1") ? "125" : "250"],
                        _ => ["2", "2"]
                    };
                    NativeEventTests.Check(reason.Description.Contains(expected[language == "zhs" ? 0 : 1]),
                        "The reason must name this branch's missing requirement: " + option.TextKey);
                    await option.Chosen();
                    NativeEventTests.Check(option.IsLocked && !option.WasChosen && model.Owner!.Gold == 0 && model.Owner.Deck.Cards.Count == 0,
                        "Inspecting and choosing a locked option must never pay or mutate the Deck.");
                    checkedKeys.Add(language + ":" + option.TextKey);
                }
                var bridge = models[0];
                var leave = bridge.CurrentOptions.Single(o => !o.IsLocked);
                NativeEventTests.Check(Tips(bridge, leave).SequenceEqual(leave.HoverTips), "Enabled options retain native hover behavior.");
                var refresh = new EventOption(bridge, null, leave.Title, leave.Description,
                    "THE_FUTURE_OF_POTIONS.pages.INITIAL.options.AM_REFRESH", leave.HoverTips);
                NativeEventTests.Check(((HoverTip)Tips(bridge, refresh)[0]).Description.Contains("50"), "Potion refresh explains its 50 Gold requirement.");
                // Transplanted locked descriptions already explain their payment and target requirements.
                foreach (var locked in cleric.CurrentOptions.Where(o => o.IsLocked))
                {
                    string text = ((HoverTip)Tips(cleric, locked)[0]).Description;
                    NativeEventTests.Check(text == locked.Description.GetFormattedText() &&
                        text.Contains(locked.TextKey.Contains("HEAL") ? "35" : "75"),
                        "Existing locked descriptions remain the reason source, including actual Cleric prices.");
                }
            }
            foreach (string language in new[] { "eng", "zhs" })
            foreach (string suffix in new[] { ".AM_FORWARD", ".READ_THE_BACK", ".READ_PASSAGE", ".READ_ENTIRE_BOOK",
                ".AM_CATALYZE", ".AM_FUSE", ".AM_BURN", ".AM_HUG_1", ".AM_HUG_2", ".AM_UPGRADE_1", ".AM_UPGRADE_2", ".AM_GROUP_LOCKED" })
                NativeEventTests.Check(checkedKeys.Any(k => k.StartsWith(language + ":") && k.EndsWith(suffix)),
                    "Exercise the actual locked branch in each language: " + language + suffix);
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
    }

    private static Control? CaptureOwner;
    private static IHoverTip[]? Captured;
    private static bool Removed;

    private static async Task NativeRequirements()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var player = NativeEventTests.Player("NATIVE_LOCK_REQUIREMENTS");
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        var attack = player.RunState.CreateCard<FiendFire>(player);
        await CardPileCmd.Add(attack, PileType.Deck);
        CardCmd.Enchant<Sharp>(attack, 1);
        NativeEventTests.Check(attack.Type == CardType.Attack && attack.Keywords.Contains(CardKeyword.Exhaust) &&
            !ModelDb.Enchantment<Vigorous>().CanEnchant(attack) && !ModelDb.Enchantment<Corrupted>().CanEnchant(attack) &&
            !ModelDb.Enchantment<SoulsPower>().CanEnchant(attack),
            "Keep an Attack with Exhaust whose existing enchantment prevents all three offered enchantments.");
        var models = new[]
        {
            await NativeEventTests.Begin<StoneOfAllTime>(player),
            await NativeEventTests.Begin<Symbiote>(player),
            await NativeEventTests.Begin<GraveOfTheForgotten>(player)
        };
        player.Gold = 1;
        var conveyor = await NativeEventTests.Begin<EndlessConveyor>(player);
        var conveyorLock = conveyor.CurrentOptions.Single(o => o.TextKey.EndsWith(".LOCKED"));
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                foreach (var model in models)
                {
                    var option = model.CurrentOptions.Single(o => o.TextKey.EndsWith(".PUSH_LOCKED") ||
                        o.TextKey.EndsWith(".CONFRONT_LOCKED") || o.TextKey.EndsWith(".APPROACH_LOCKED"));
                    var tip = (HoverTip)Tips(model, option)[0];
                    NativeEventTests.Check(tip.Description.Contains(language == "zhs" ? "附魔" : "enchantment"),
                        "An existing card of the right type cannot satisfy an enchantment legality condition: " + option.TextKey);
                    await option.Chosen();
                    NativeEventTests.Check(!option.WasChosen && attack.Enchantment is Sharp, "Locked enchantment options must remain inert.");
                }
                foreach (int cost in new[] { 40, 65 })
                {
                    conveyor.DynamicVars.Gold.BaseValue = cost;
                    string text = ((HoverTip)Tips(conveyor, conveyorLock)[0]).Description;
                    NativeEventTests.Check(text.Contains(cost.ToString()) && text.Contains(language == "zhs" ? "金币" : "Gold"),
                        "Conveyor explains the current fee even when the player has some Gold.");
                }
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
    }

    private static async Task Focus()
    {
        var player = NativeEventTests.Player("LOCK_FOCUS");
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        var model = await NativeEventTests.Begin<SlipperyBridge>(player);
        var option = model.CurrentOptions.Single(o => o.IsLocked);
        var button = new NEventOptionButton();
        AccessTools.PropertySetter(typeof(NEventOptionButton), nameof(NEventOptionButton.Event)).Invoke(button, [model]);
        AccessTools.PropertySetter(typeof(NEventOptionButton), nameof(NEventOptionButton.Option)).Invoke(button, [option]);
        CaptureOwner = button;
        Captured = null;
        Removed = false;
        try
        {
            AccessTools.Method(typeof(NEventOptionButton), "OnFocus").Invoke(button, null);
            NativeEventTests.Check(Captured is { Length: > 0 } && ((HoverTip)Captured[0]).Description.Contains("removable"),
                "Native locked button focus must request the reason hover tip.");
            AccessTools.Method(typeof(NEventOptionButton), "OnUnfocus").Invoke(button, null);
            NativeEventTests.Check(Removed, "Native locked button unfocus must remove its hover tips.");
            await option.Chosen();
            NativeEventTests.Check(option.IsLocked && !option.WasChosen && player.Deck.Cards.Count == 0,
                "Hover handling must preserve the original locked callback boundary.");
        }
        finally { CaptureOwner = null; Captured = null; button.Free(); }
    }

    // Capture only this test's button; all other hover rendering keeps its native path.
    [HarmonyPatch]
    private static class CaptureHover
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(NHoverTipSet), nameof(NHoverTipSet.CreateAndShow),
            [typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment)]);
        private static bool Prefix(Control owner, IEnumerable<IHoverTip> hoverTips)
        {
            if (CaptureOwner is null || owner != CaptureOwner) return true;
            Captured = hoverTips.ToArray();
            return false;
        }
    }

    [HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.Remove))]
    private static class CaptureRemove
    {
        private static bool Prefix(Control owner)
        {
            if (CaptureOwner is null || owner != CaptureOwner) return true;
            Removed = true;
            return false;
        }
    }
}
