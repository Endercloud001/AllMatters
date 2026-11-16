#if !STS2_PUBLIC
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;

internal static class NativeMuseumOfTortureTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Museum of Torture take grants the displayed relic and exactly one previewed Guilty", Take),
        ("Museum of Torture escape loses 10 current HP and finishes without rewards", Escape)
    ];

    private static async Task<(EventModel Museum, EventOption Take, EventOption Escape)> ViewDisplay(Player player)
    {
        var museum = await NativeEventTests.Begin<MuseumOfTorture>(player);
        await museum.CurrentOptions.Single().Chosen();
        await museum.CurrentOptions.Single().Chosen();
        var take = museum.CurrentOptions.Single(option => option.TextKey.EndsWith(".TAKE"));
        var escape = museum.CurrentOptions.Single(option => option.TextKey.EndsWith(".BREAK_OUT"));
        return (museum, take, escape);
    }

    private static async Task Take()
    {
        using var eventScope = HeadlessCombatInProgress.EnterEvent();
        string previousLanguage = LocManager.Instance.Language;
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                var player = NativeEventTests.Player("MUSEUM_TAKE");
                var (museum, take, escape) = await ViewDisplay(player);
                var relic = take.HoverTips.OfType<HoverTip>().Select(tip => tip.CanonicalModel).OfType<RelicModel>().Single();
                var curse = take.HoverTips.OfType<CardHoverTip>().Single().Card;
                string curseName = language == "zhs" ? "愧疚" : "Guilty";
                string escapeText = language == "zhs" ? "失去[red]10[/red]点生命。" : "Lose [red]10[/red] HP.";
                string takeDescription = take.Description.GetFormattedText();
                NativeEventTests.Check(curse is Guilty && takeDescription.Contains($"[red]{curseName}[/red]") &&
                    takeDescription.Contains(relic.Title.GetFormattedText()) && escape.Description.GetFormattedText() == escapeText,
                    "Both options display their actual effects, including the red localized Guilty name and its card preview.");

                int deckSize = player.Deck.Cards.Count;
                int relicCount = player.Relics.Count();
                await take.Chosen();
                await take.Chosen();
                NativeEventTests.Check(museum.IsFinished && player.Relics.Count() == relicCount + 1 &&
                    player.Relics.Count(owned => owned.Id == relic.Id) == 1 && player.Deck.Cards.Count == deckSize + 1 &&
                    player.Deck.Cards.OfType<Guilty>().Count() == 1,
                    "Taking the displayed relic and one permanent Guilty ends the event; reusing the option adds nothing.");
            }
        }
        finally
        {
            LocManager.Instance.SetLanguage(previousLanguage);
        }
    }

    private static async Task Escape()
    {
        using var eventScope = HeadlessCombatInProgress.EnterEvent();
        var player = NativeEventTests.Player("MUSEUM_ESCAPE");
        player.Creature.SetCurrentHpInternal(25);
        int maxHp = player.Creature.MaxHp;
        int deckSize = player.Deck.Cards.Count;
        int relicCount = player.Relics.Count();

        var (museum, _, escape) = await ViewDisplay(player);
        await escape.Chosen();
        await escape.Chosen();
        NativeEventTests.Check(museum.IsFinished && player.Creature.CurrentHp == 15 &&
            player.Creature.MaxHp == maxHp && player.Deck.Cards.Count == deckSize && player.Relics.Count() == relicCount,
            "Escaping costs exactly 10 current HP and finishes even if the old choice is invoked again.");
    }
}
#endif
