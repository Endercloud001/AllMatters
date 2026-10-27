using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;

namespace AllMatters.Events;

[HarmonyPatch(typeof(SelfHelpBook), "GenerateInitialOptions")]
internal static class NativeSelfHelpBook
{
    private sealed record Reading(EnchantmentModel Enchantment, int Amount, CardType Type, string Key);
    private static readonly ConditionalWeakTable<SelfHelpBook, Reading[]> Readings = new();

    private static Reading[] Roll(SelfHelpBook model)
    {
        int attack = model.Rng.NextInt(100), skill = model.Rng.NextInt(100), power = model.Rng.NextInt(100);
        return
        [
            attack < 50 ? new(ModelDb.Enchantment<Sharp>(), 2, CardType.Attack, "READ_THE_BACK")
                : attack < 90 ? new(ModelDb.Enchantment<Vigorous>(), 4, CardType.Attack, "READ_THE_BACK")
                : attack < 98 ? new(ModelDb.Enchantment<Inky>(), 1, CardType.Attack, "READ_THE_BACK")
                : new(ModelDb.Enchantment<Instinct>(), 1, CardType.Attack, "READ_THE_BACK"),
            skill < 55 ? new(ModelDb.Enchantment<Nimble>(), 2, CardType.Skill, "READ_PASSAGE")
                : skill < 95 ? new(ModelDb.Enchantment<Adroit>(), 3, CardType.Skill, "READ_PASSAGE")
                : new(ModelDb.Enchantment<SlumberingEssence>(), 1, CardType.Skill, "READ_PASSAGE"),
            power < 40 ? new(ModelDb.Enchantment<Swift>(), 2, CardType.Power, "READ_ENTIRE_BOOK")
                : power < 85 ? new(ModelDb.Enchantment<Steady>(), 1, CardType.Power, "READ_ENTIRE_BOOK")
                : power < 98 ? new(ModelDb.Enchantment<Slither>(), 1, CardType.Power, "READ_ENTIRE_BOOK")
                : new(ModelDb.Enchantment<Spiral>(), 1, CardType.Power, "READ_ENTIRE_BOOK")
        ];
    }

    private static bool Prefix(SelfHelpBook __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        var readings = Readings.GetValue(model, Roll);
        var options = readings.Select(reading =>
        {
            string key = "SELF_HELP_BOOK.pages.INITIAL.options." + reading.Key;
            var title = new LocString("events", key + ".title");
            var description = new LocString("events", "ALL_MATTERS_SELF_HELP_READING");
            description.Add("Enchantment", reading.Enchantment.Title);
            description.Add("Amount", reading.Amount);
            description.Add("Type", reading.Type.ToLocString());
            bool legal = model.Owner!.Deck.Cards.Any(c => c.Type == reading.Type && reading.Enchantment.CanEnchant(c));
            var preview = reading.Enchantment.ToMutable();
            preview.Amount = reading.Amount;
            preview.RecalculateValues();
            return new EventOption(model, legal ? () => Read(model, reading) : null, title, description, key,
                preview.HoverTips);
        }).ToList();
        if (options.All(o => o.IsLocked)) options.Add(NativeEventActions.Option(model, "SELF_HELP_BOOK.pages.INITIAL.options.NO_OPTIONS", () =>
        {
            NativeEventActions.Finish(model, "SELF_HELP_BOOK.pages.NO_OPTIONS.description");
            return Task.CompletedTask;
        }));
        __result = options;
        return false;
    }

    private static async Task Read(SelfHelpBook model, Reading reading)
    {
        var selected = await CardSelectCmd.FromDeckForEnchantment(model.Owner!, reading.Enchantment, reading.Amount,
            c => c.Type == reading.Type, new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1));
        foreach (var card in selected) CardCmd.Enchant(reading.Enchantment.ToMutable(), card, reading.Amount);
        NativeEventActions.Finish(model, "SELF_HELP_BOOK.pages." + reading.Key + ".description");
    }
}
