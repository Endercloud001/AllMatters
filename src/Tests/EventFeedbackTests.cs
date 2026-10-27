using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

internal static class EventFeedbackTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Event feedback card keywords appear once in bilingual descriptions and previews", Descriptions),
        ("Event feedback relic artwork fills its icon and insect tip matches its duration", Relics),
        ("Event feedback Alpha and Beta preview the actual generated draw-pile card", ChainPreview),
    ];

    private static int Count(string text, string word) => text.Split(word).Length - 1;

    private static Task Descriptions()
    {
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                string exhaust = language == "eng" ? "Exhaust" : "消耗";
                string innate = language == "eng" ? "Innate" : "固有";
                foreach (var model in new CardModel[] { ModelDb.Card<AllMatters.Cards.Alpha>(), ModelDb.Card<AllMatters.Cards.Beta>(), ModelDb.Card<AllMatters.Cards.Omniscience>() })
                {
                    var card = model.ToMutable();
                    int expectedExhaust = card is AllMatters.Cards.Omniscience ? 2 : 1;
                    foreach (bool upgraded in new[] { false, true })
                    {
                        if (upgraded) CardCmd.Upgrade(card);
                        foreach (string text in new[] { card.GetDescriptionForPile(PileType.None), card.GetDescriptionForUpgradePreview() })
                        {
                            NativeEventTests.Check(Count(text, exhaust) == expectedExhaust, $"Only native source Exhaust plus selected-card exhaust: {text}");
                            NativeEventTests.Check(Count(text, innate) <= 1, $"Innate is not duplicated: {text}");
                        }
                    }
                }
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
        return Task.CompletedTask;
    }

    private static Task Relics()
    {
        foreach (var relic in new RelicModel[] { ModelDb.Relic<AllMatters.Relics.Omamori>(), ModelDb.Relic<AllMatters.Relics.PreservedInsect>(), ModelDb.Relic<AllMatters.Relics.Inserter>() })
        {
            var image = relic.Icon.GetImage();
            var bounds = image.GetUsedRect().Size;
            NativeEventTests.Check(Math.Max(bounds.X, bounds.Y) >= image.GetWidth() * 0.85, $"Relic artwork fills its canvas: {relic.Id}, {bounds}");
            NativeEventTests.Check(relic.Icon.GetSize() == relic.IconOutline.GetSize(), "Icon and outline canvases align.");
        }
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                var relic = ModelDb.Relic<AllMatters.Relics.PreservedInsect>();
                var tip = relic.HoverTipsExcludingRelic.OfType<HoverTip>().Single(t => t.Id == ModelDb.Power<ShrinkPower>().Id.ToString());
                NativeEventTests.Check(tip.Description.Contains("99") && !tip.Description.Contains("{"), $"Shrink tip states actual 99-turn duration: {tip.Description}");
                string description = relic.DynamicDescription.GetFormattedText();
                NativeEventTests.Check(!description.Contains("99") && !description.Contains("Artifact") && !description.Contains("人工制品"), "Relic text describes damage reduction without implementation details.");
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
        return Task.CompletedTask;
    }

    private static async Task ChainPreview()
    {
        var (player, combat) = NativeEventTests.Combat();
        var previews = new List<CardPileAddResult>();
        EventFeedbackPreviewObserver.Results = previews;
        try
        {
            var alpha = combat.CreateCard<AllMatters.Cards.Alpha>(player);
            await alpha.OnPlayWrapper(new ThrowingPlayerChoiceContext(), null, true, default, skipCardPileVisuals: true);
            var beta = player.PlayerCombatState!.DrawPile.Cards.OfType<AllMatters.Cards.Beta>().Single();
            NativeEventTests.Check(previews.Count == 1 && previews[0].success && ReferenceEquals(previews[0].cardAdded, beta), "Alpha previews the generated Beta.");
            await beta.OnPlayWrapper(new ThrowingPlayerChoiceContext(), null, true, default, skipCardPileVisuals: true);
            var omega = player.PlayerCombatState.DrawPile.Cards.OfType<AllMatters.Cards.Omega>().Single();
            NativeEventTests.Check(previews.Count == 2 && previews[1].success && ReferenceEquals(previews[1].cardAdded, omega), "Beta previews the generated Omega.");
        }
        finally { EventFeedbackPreviewObserver.Results = null; }
    }
}

// Observe the real preview command; native TestMode skips UI creation in this host.
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.PreviewCardPileAdd), [typeof(CardPileAddResult), typeof(float), typeof(CardPreviewStyle)])]
internal static class EventFeedbackPreviewObserver
{
    internal static List<CardPileAddResult>? Results;
    private static void Prefix(CardPileAddResult result) => Results?.Add(result);
}
