using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using AllMatters.Relics;
using HarmonyLib;

internal static class NativeRelicEventTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Native relic events choir pays14 HP for the displayed mushroom", Choir),
        ("Native relic events mushroom adjusts only owners powered Vulnerable damage", Mushroom),
        ("Native relic events nest obtains native Meat on the Bone and heals at half HP", Nest),
        ("Native relic events trader keeps pickup benefits and prioritizes Common including wax", Trader),
        ("Native relic events doll grants Strength for permanent curses only", Doll),
    ];
    private static async Task Choir()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player(); int hp = p.Creature.CurrentHp;
        var e = await NativeEventTests.Begin<LuminousChoir>(p);
        var option = e.CurrentOptions.SingleOrDefault(o => o.TextKey.EndsWith(".AM_MUSHROOM"));
        NativeEventTests.Check(option != null, "Choir must offer Strange Mushroom.");
        await option!.Chosen();
        NativeEventTests.Check(e.IsFinished && p.Creature.CurrentHp == hp - 14 && p.Relics.Any(r => r.Id.Entry == "ALL_MATTERS_STRANGE_MUSHROOM"), "Pay14 HP and actually obtain mushroom.");
    }

    private static async Task Mushroom()
    {
        var (p, combat) = NativeEventTests.Combat();
        var context = new ThrowingPlayerChoiceContext();
        var enemy = combat.CreateCreature(ModelDb.Monster<AllMatters.Monsters.Lagavulin>().ToMutable(), CombatSide.Enemy, "dealer");
        combat.AddCreature(enemy);
        await RelicCmd.Obtain<StrangeMushroom>(p);
        await PowerCmd.Apply<VulnerablePower>(context, p.Creature, 2, enemy, null);
        int hp = p.Creature.CurrentHp;
        await CreatureCmd.Damage(context, p.Creature, 20, ValueProp.Move, enemy);
        NativeEventTests.Check(p.Creature.CurrentHp == hp - 25, "Powered attack20 becomes25 for mushroom owner.");
        hp = p.Creature.CurrentHp;
        await CreatureCmd.Damage(context, p.Creature, 8, ValueProp.Unpowered, enemy);
        NativeEventTests.Check(p.Creature.CurrentHp == hp - 8, "Unpowered damage is unchanged.");
        var vulnerable = p.Creature.GetPower<VulnerablePower>()!;
        NativeEventTests.Check(Multiplier(vulnerable, enemy, p.Creature) == 1m, "Other targets are unchanged.");
        await PowerCmd.Apply<DebilitatePower>(context, p.Creature, 1, enemy, null);
        NativeEventTests.Check(Multiplier(vulnerable, p.Creature, enemy) == 1.5m, "Native Debilitate doubles the adjusted25 percent bonus rather than being overwritten.");
        await PowerCmd.Remove<DebilitatePower>(p.Creature);
        await RelicCmd.Remove(p.Relics.OfType<StrangeMushroom>().Single());
        NativeEventTests.Check(Multiplier(vulnerable, p.Creature, enemy) == 1.5m, "No mushroom keeps native50 percent.");
    }
    private static decimal Multiplier(VulnerablePower power, Creature target, Creature dealer)
#if STS2_PUBLIC
        => power.ModifyDamageMultiplicative(target, 20, ValueProp.Move, dealer, null);
#else
        => power.ModifyDamageMultiplicative(target, 20, ValueProp.Move, dealer, null, null);
#endif

    private static async Task Nest()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player(); int hp = p.Creature.CurrentHp;
        var e = await NativeEventTests.Begin<ByrdonisNest>(p);
        await e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_HELP")).Chosen();
        var relic = p.Relics.OfType<MeatOnTheBone>().Single();
        NativeEventTests.Check(e.IsFinished && p.Creature.CurrentHp == hp - 12, "Pay12 and obtain native relic.");
        p.Creature.SetCurrentHpInternal(p.Creature.MaxHp / 2);
        hp = p.Creature.CurrentHp;
        await relic.AfterCombatVictoryEarly(null!);
        NativeEventTests.Check(p.Creature.CurrentHp == hp + 12, "Native relic heals12 at half HP.");
        p.Creature.SetCurrentHpInternal(p.Creature.MaxHp / 2 + 1); hp = p.Creature.CurrentHp;
        await relic.AfterCombatVictoryEarly(null!);
        NativeEventTests.Check(p.Creature.CurrentHp == hp, "Above half HP does not heal.");
    }

    private static async Task Trader()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player();
        int oldMax = p.Creature.MaxHp;
        var berry = await RelicCmd.Obtain<Strawberry>(p);
        var wax = await RelicCmd.Obtain<MeatOnTheBone>(p); wax.IsWax = true;
        await RelicCmd.Obtain<Anchor>(p);
        await RelicCmd.Obtain<Pear>(p);
        var melted = await RelicCmd.Obtain<Mango>(p); melted.IsWax = true; melted.IsMelted = true;
        var starter = p.Relics.First(r => r.Rarity == MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Starter);
        starter.IsWax = true;
        var e = await NativeEventTests.Begin<RelicTrader>(p);
        var offered = (IReadOnlyList<RelicModel>)AccessTools.Field(typeof(RelicTrader), "_ownedRelics").GetValue(e)!;
        NativeEventTests.Check(offered.Contains(berry) && offered.Contains(wax) && offered.All(r => r != melted && r != starter && (r.IsWax || r.Rarity == MegaCrit.Sts2.Core.Entities.Relics.RelicRarity.Common)), "Choose lowest effective rarity; keep melted and starter exclusions.");
        int berryIndex = offered.ToList().IndexOf(berry);
        await e.CurrentOptions[berryIndex].Chosen();
        NativeEventTests.Check(!p.Relics.Contains(berry) && p.Creature.MaxHp == oldMax + 7 + 10 + 14, "Trading pickup relic does not roll back any applied Max HP gains.");
    }

    private static async Task Doll()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player();
        await CardPileCmd.AddCursesToDeck([ModelDb.Card<Regret>(), ModelDb.Card<Regret>()], p);
        var e = await NativeEventTests.Begin<DollRoom>(p);
        await e.CurrentOptions[2].Chosen();
        NativeEventTests.Check(e.CurrentOptions.Count == 4, "Examine offers all four dolls.");
        await e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_DOLL")).Chosen();
        var doll = p.Relics.OfType<DuVuDoll>().Single();
        var combat = new CombatState(runState: p.RunState); combat.AddPlayer(p); p.ResetCombatState();
        await CardPileCmd.Add(combat.CreateCard<Regret>(p), PileType.Draw);
        await doll.BeforeCombatStart();
        NativeEventTests.Check(e.IsFinished && p.Creature.GetPower<StrengthPower>()?.Amount == 2, "Permanent curses count2; temporary draw pile curse does not count.");
    }
}
