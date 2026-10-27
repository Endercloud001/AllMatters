using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

internal static class NativeRadianceTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Native radiance damage counts positive gains after removal and regain, not remaining layers", Damage),
        ("Native radiance ignores natural restoration and counts single buffs once per acquisition", Restoration),
        ("Native radiance lighthouse pays its displayed potion, adds Brilliance and restores inventory", Lighthouse),
        ("Native radiance counts teammate-applied buffs only for recipient and resets in a new combat", Ownership),
    ];

    private static async Task Damage()
    {
        var (p, combat) = NativeEventTests.Combat();
        var canonical = ModelDb.AllCards.SingleOrDefault(c => c.Id.Entry == "ALL_MATTERS_BRILLIANCE");
        NativeEventTests.Check(canonical != null, "Brilliance must be registered as an event card.");
        var card = combat.CreateCard(canonical!, p);
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<StrengthPower>(context, p.Creature, 3, p.Creature, null);
        await PowerCmd.Apply<StrengthPower>(context, p.Creature, -3, p.Creature, null);
        await PowerCmd.Apply<StrengthPower>(context, p.Creature, 2, p.Creature, null);
        await PowerCmd.Apply<DexterityPower>(context, p.Creature, 4, p.Creature, null);
        await CreatureCmd.GainBlock(p.Creature, 20, ValueProp.Unpowered, null);
        var target = combat.CreateCreature(ModelDb.Monster<AllMatters.Monsters.Lagavulin>().ToMutable(), CombatSide.Enemy, "target");
        combat.AddCreature(target); target.SetMaxHpInternal(100); target.SetCurrentHpInternal(100);
        await card.OnPlayWrapper(context, target, true, default, skipCardPileVisuals: true);
        NativeEventTests.Check(target.CurrentHp == 89, "Cumulative gains 3+2+4 produce9 base damage plus2 current Strength; Block does not contribute.");
        NativeEventTests.Check(card.Keywords.Contains(CardKeyword.Exhaust) && card.EnergyCost.Canonical == 2, "Brilliance costs2 and exhausts.");
        card.UpgradeInternal(); card.FinalizeUpgradeInternal();
        NativeEventTests.Check(card.EnergyCost.GetResolved() == 1, "Upgrade reduces energy cost to 1.");
    }

    private static async Task Restoration()
    {
        var (p, combat) = NativeEventTests.Combat();
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<StrengthPower>(context, p.Creature, 3, p.Creature, null);
        var temporary = await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.Mocks.MockTemporaryStrengthLossPower>(context, p.Creature, 4, p.Creature, null);
        await temporary!.AfterSideTurnEnd(context, CombatSide.Player, [p.Creature]);
        await PowerCmd.Apply<BarricadePower>(context, p.Creature, 1, p.Creature, null);
        await PowerCmd.Apply<BarricadePower>(context, p.Creature, 1, p.Creature, null);
        await PowerCmd.Remove<BarricadePower>(p.Creature);
        await PowerCmd.Apply<BarricadePower>(context, p.Creature, 1, p.Creature, null);
        var target = combat.CreateCreature(ModelDb.Monster<AllMatters.Monsters.Lagavulin>().ToMutable(), CombatSide.Enemy, "target");
        combat.AddCreature(target); target.SetMaxHpInternal(100); target.SetCurrentHpInternal(100);
        var card = combat.CreateCard<AllMatters.Cards.Brilliance>(p);
        await card.OnPlayWrapper(context, target, true, default, skipCardPileVisuals: true);
        NativeEventTests.Check(target.CurrentHp == 92, "Count initial Strength3 plus two Barricade acquisitions, not natural restoration; add current Strength3 to attack.");
    }

    private static async Task Lighthouse()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player();
        await PotionCmd.TryToProcure<MegaCrit.Sts2.Core.Models.Potions.BlockPotion>(p);
        var e = await NativeEventTests.Begin<MegaCrit.Sts2.Core.Models.Events.DrowningBeacon>(p);
        var option = e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_LIGHT"));
        NativeEventTests.Check(option.Description.GetFormattedText().Contains(p.Potions.Single().Title.GetFormattedText()), "Offer must display the concrete potion.");
        await option.Chosen();
        NativeEventTests.Check(e.IsFinished && !p.Potions.Any() && p.Deck.Cards.Last() is AllMatters.Cards.Brilliance, "One potion buys an actual Brilliance card.");
        NativeEventTests.Check(Godot.ResourceLoader.Load<Godot.Texture2D>("res://AllMatters/images/card_portraits/brilliance.png").GetWidth() > 0, "Original Brilliance portrait must load from the pack.");
    }

    private static async Task Ownership()
    {
        var p = MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(MegaCrit.Sts2.Core.Unlocks.UnlockState.all, 1);
        var other = MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun<MegaCrit.Sts2.Core.Models.Characters.Silent>(MegaCrit.Sts2.Core.Unlocks.UnlockState.all, 2);
        var run = MegaCrit.Sts2.Core.Runs.RunState.CreateForTest([p, other], acts: [], seed: "RADIANCE_OWNERS");
        var combat = new CombatState(runState: run);
        foreach (var member in run.Players) { combat.AddPlayer(member); member.ResetCombatState(); }
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<StrengthPower>(context, p.Creature, 3, other.Creature, null);
        await PowerCmd.Apply<DexterityPower>(context, other.Creature, 7, p.Creature, null);
        var target = combat.CreateCreature(ModelDb.Monster<AllMatters.Monsters.Lagavulin>().ToMutable(), CombatSide.Enemy, "target");
        combat.AddCreature(target); target.SetMaxHpInternal(100); target.SetCurrentHpInternal(100);
        await combat.CreateCard<AllMatters.Cards.Brilliance>(p).OnPlayWrapper(context, target, true, default, skipCardPileVisuals: true);
        NativeEventTests.Check(target.CurrentHp == 94, "Only recipient's3 gain plus3 Strength affects first attack.");
        await combat.CreateCard<AllMatters.Cards.Brilliance>(other).OnPlayWrapper(context, target, true, default, skipCardPileVisuals: true);
        NativeEventTests.Check(target.CurrentHp == 87, "Other player independently accumulated7.");
        p.Creature.Reset();
        var next = new CombatState(runState: run); next.AddPlayer(p); p.ResetCombatState();
        target = next.CreateCreature(ModelDb.Monster<AllMatters.Monsters.Lagavulin>().ToMutable(), CombatSide.Enemy, "target");
        next.AddCreature(target); target.SetMaxHpInternal(100); target.SetCurrentHpInternal(100);
        await next.CreateCard<AllMatters.Cards.Brilliance>(p).OnPlayWrapper(context, target, true, default, skipCardPileVisuals: true);
        NativeEventTests.Check(target.CurrentHp == 100, "The next combat starts at zero.");
    }
}
