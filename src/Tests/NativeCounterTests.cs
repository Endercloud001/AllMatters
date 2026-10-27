using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Powers;

internal static class NativeCounterTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Native counters bone tea upgrades opening hands in two combats and restores remaining uses", Bone),
        ("Native counters ember tea grants Strength and Dexterity for five event combats", Ember),
        ("Native counters ink bottle counts actual automatic plays and restores across combats", Ink),
        ("Native counters virus draws each turn and inserts Dazed every five non attacks", Virus),
        ("Native counters discourtesy freezes its displayed next combat effect and expires", Discourtesy),
        ("Native counters history replays last own turn skill and never falls back to older turns", History),
        ("Native counters presentation retains scriptorium narrative and loads native virus artwork", Presentation),
    ];

    private static async Task Bone()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player();
        var e = await NativeEventTests.Begin<TeaMaster>(p);
        int gold = p.Gold;
        await e.CurrentOptions[0].Chosen();
        var tea = p.Relics.OfType<BoneTea>().Single();
        NativeEventTests.Check(p.Gold == gold - 50 && tea.CombatsLeft == 2, "50 gold buys two opening hands.");
        scope.Dispose();
        var combat = new MegaCrit.Sts2.Core.Combat.CombatState(runState: p.RunState); combat.AddPlayer(p); p.ResetCombatState();
        var card = combat.CreateCard<StrikeIronclad>(p);
        await CardPileCmd.Add(card, PileType.Hand);
#if STS2_PUBLIC
        await tea.AfterSideTurnStart(MegaCrit.Sts2.Core.Combat.CombatSide.Player, [p.Creature], combat);
#else
        await tea.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), p);
#endif
        NativeEventTests.Check(card.IsUpgraded && tea.CombatsLeft == 1, "First opening hand upgrades and consumes one combat.");
        var restored = MegaCrit.Sts2.Core.Entities.Players.Player.FromSerializable(p.ToSerializable());
        NativeEventTests.Check(restored.Relics.OfType<BoneTea>().Single().CombatsLeft == 1, "Native saved combat count restores.");
    }

    private static async Task History()
    {
        var (p, combat) = NativeEventTests.Combat();
        var relic = await RelicCmd.Obtain<HistoryCourse>(p);
        var history = MegaCrit.Sts2.Core.Combat.CombatManager.Instance.History;
        history.Clear();
        p.PlayerCombatState!.IncrementTurnNumber();
        var card = combat.CreateCard<DefendIronclad>(p);
        history.CardPlayFinished(combat, new CardPlay
        {
            Card = card, Target = null, ResultPile = PileType.Discard, Resources = default,
            IsAutoPlay = true, PlayIndex = 0, PlayCount = 1,
#if !STS2_PUBLIC
            Player = p,
#endif
        });
        p.PlayerCombatState.IncrementTurnNumber();
        int block = p.Creature.Block;
        await relic.AfterAutoPrePlayPhaseEntered(new ThrowingPlayerChoiceContext(), p);
        NativeEventTests.Check(p.Creature.Block == block + 5, "Last own turn skill is actually replayed.");
        p.PlayerCombatState.IncrementTurnNumber();
        block = p.Creature.Block;
        await relic.AfterAutoPrePlayPhaseEntered(new ThrowingPlayerChoiceContext(), p);
        NativeEventTests.Check(p.Creature.Block == block, "An empty previous turn cannot fall back to an older card or replay its dupe.");
        history.Clear();
    }

    private static Task Presentation()
    {
        var manager = MegaCrit.Sts2.Core.Localization.LocManager.Instance;
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                manager.SetLanguage(language);
                var table = manager.GetTable("events");
                string native = table.GetRawText("WATERLOGGED_SCRIPTORIUM.pages.BLOODY_INK.description");
                string ink = table.GetRawText("WATERLOGGED_SCRIPTORIUM.pages.AM_INK.description");
                NativeEventTests.Check(ink == native.Replace("[red]bright red ink[/red]", "[blue]bright blue ink[/blue]")
                    .Replace("[red]亮红色墨水[/red]", "[blue]亮蓝色墨水[/blue]") && ink.Contains("[blue]"), "Only the original ink color phrase changes; the entire remaining narrative stays intact.");
                var virus = ModelDb.Relic<SymbioticVirus>();
                NativeEventTests.Check(virus.Icon != null && virus.IconOutline != null && virus.BigIcon != null, "All native replacement artwork loads.");
            }
        }
        finally { manager.SetLanguage("eng"); }
        return Task.CompletedTask;
    }

    private static async Task Ember()
    {
        var p = NativeEventTests.Player();
        p.Gold = 200;
        EmberTea tea;
        using (HeadlessCombatInProgress.EnterEvent())
        {
            var e = await NativeEventTests.Begin<TeaMaster>(p);
            int gold = p.Gold;
            await e.CurrentOptions[1].Chosen();
            tea = p.Relics.OfType<EmberTea>().Single();
            NativeEventTests.Check(p.Gold == gold - 150, "Ember costs150.");
        }
        for (int i = 0; i < 6; i++)
        {
            p.Creature.Reset();
            var combat = new MegaCrit.Sts2.Core.Combat.CombatState(runState: p.RunState); combat.AddPlayer(p); p.ResetCombatState();
            await MegaCrit.Sts2.Core.Hooks.Hook.BeforeCombatStart(p.RunState, combat);
            NativeEventTests.Check((p.Creature.GetPower<StrengthPower>()?.Amount ?? 0) == (i < 5 ? 1 : 0)
                && (p.Creature.GetPower<DexterityPower>()?.Amount ?? 0) == (i < 5 ? 1 : 0), "Each of five combats grants1 each, then expires.");
        }
        NativeEventTests.Check(tea.IsUsedUp, "Tea expires after five combats.");
    }

    private static async Task Ink()
    {
        var (p, combat) = NativeEventTests.Combat();
        var ink = await RelicCmd.Obtain<AllMatters.Relics.InkBottle>(p);
        for (int i = 0; i < 9; i++) await PlayDefend(p, combat);
        NativeEventTests.Check(ink.DisplayAmount == 9, "Nine actual auto plays retain remainder9.");
        p = MegaCrit.Sts2.Core.Entities.Players.Player.FromSerializable(p.ToSerializable());
        ink = p.Relics.OfType<AllMatters.Relics.InkBottle>().Single();
        p.Creature.Reset();
        combat = new MegaCrit.Sts2.Core.Combat.CombatState(runState: p.RunState); combat.AddPlayer(p); p.ResetCombatState();
        var draw = combat.CreateCard<StrikeIronclad>(p); await CardPileCmd.Add(draw, PileType.Draw);
        await PlayDefend(p, combat);
        NativeEventTests.Check(ink.DisplayAmount == 0 && p.PlayerCombatState!.Hand.Cards.Contains(draw), "Next combat tenth play draws1 and resets before draw reentry.");
    }

    private static async Task PlayDefend(MegaCrit.Sts2.Core.Entities.Players.Player p, MegaCrit.Sts2.Core.Combat.CombatState combat)
    {
        var card = combat.CreateCard<DefendIronclad>(p);
        await CardPileCmd.Add(card, PileType.Hand);
        await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), null, true, default, skipCardPileVisuals: true);
    }

    private static async Task Virus()
    {
        var (p, combat) = NativeEventTests.Combat();
        var virus = await RelicCmd.Obtain<SymbioticVirus>(p);
        var draw = combat.CreateCard<StrikeIronclad>(p); await CardPileCmd.Add(draw, PileType.Draw);
        await virus.BeforeHandDraw(p, new ThrowingPlayerChoiceContext(), combat);
        NativeEventTests.Check(p.PlayerCombatState!.Hand.Cards.Contains(draw), "Virus draws one at own turn start.");
        for (int i = 0; i < 4; i++) await PlayDefend(p, combat);
        NativeEventTests.Check(!p.PlayerCombatState.DrawPile.Cards.Any(c => c is Dazed), "Four non attacks do not create Dazed.");
        p = MegaCrit.Sts2.Core.Entities.Players.Player.FromSerializable(p.ToSerializable());
        p.Creature.Reset();
        combat = new MegaCrit.Sts2.Core.Combat.CombatState(runState: p.RunState); combat.AddPlayer(p); p.ResetCombatState();
        await PlayDefend(p, combat);
        NativeEventTests.Check(p.PlayerCombatState!.DrawPile.Cards.Count(c => c is Dazed) == 1, "Saved remainder survives into next combat and fifth actual non attack adds exactly one Dazed.");
    }

    private static async Task Discourtesy()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player("DISCOURTESY");
        var e = await NativeEventTests.Begin<TeaMaster>(p);
        var option = e.CurrentOptions.Last(); int gold = p.Gold;
        string key = option.TextKey;
        await option.Chosen();
        NativeEventTests.Check(p.Gold == gold, "Discourtesy is free.");
        p = MegaCrit.Sts2.Core.Entities.Players.Player.FromSerializable(p.ToSerializable());
        scope.Dispose();
        var combat = new MegaCrit.Sts2.Core.Combat.CombatState(runState: p.RunState); combat.AddPlayer(p); p.ResetCombatState();
        var tea = p.Relics.OfType<TeaOfDiscourtesy>().Single();
        await tea.BeforeCombatStart();
        NativeEventTests.Check(tea.IsUsedUp && (key.EndsWith("_2") ? p.Creature.GetPower<ConfusedPower>()?.Amount == 1
            : p.PlayerCombatState!.DrawPile.Cards.Count(c => key.EndsWith("_1") ? c is Wound : c is Dazed) == 2), "Restored effect agrees with displayed option.");
        await tea.BeforeCombatStart();
        NativeEventTests.Check(p.PlayerCombatState!.DrawPile.Cards.Count <= 2, "Spent tea does not reapply.");
    }
}
