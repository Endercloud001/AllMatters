using AllMatters.Events;
using AllMatters.Encounters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

internal static class TrashHeapRelicTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Trash Heap relics character candidates, native rarity and bilingual assets", Candidates),
        ("Trash Heap relics Omamori removes two added curses and saves exhausted uses", Omamori),
        ("Trash Heap relics Inserter retains own turn remainder across restored combats", Inserter),
        ("Trash Heap relics insect strips Artifact then shrinks actual elite units only", Insect),
        ("Trash Heap relics sword counts defeated units and restores native progress", Sword),
        ("Trash Heap relics native Bandages and Ashes retain their effects", NativeEffects),
    ];
    private static Task Candidates()
    {
        foreach (var character in new CharacterModel[] { ModelDb.Character<Ironclad>(), ModelDb.Character<Silent>(), ModelDb.Character<Defect>() })
        {
            var p = HeadlessTestSetup.PlayerWithRun(character: character);
            var relics = TrashHeapCandidates.Relics(p);
            NativeEventTests.Check(relics.Any(r => r is AllMatters.Relics.Omamori) && relics.Any(r => r is AllMatters.Relics.PreservedInsect), "Unrestricted event relics.");
            NativeEventTests.Check(relics.Any(r => r is AllMatters.Relics.Inserter) == (character is Defect)
                && relics.Any(r => r is ToughBandages) == (character is Silent)
                && relics.Any(r => r is CharonsAshes) == (character is Ironclad), "Character-specific sources.");
            foreach (string lang in new[] { "eng", "zhs" })
            {
                MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage(lang);
                foreach (var relic in relics.Where(r => r is AllMatters.Relics.Omamori or AllMatters.Relics.PreservedInsect or AllMatters.Relics.Inserter or ToughBandages or CharonsAshes))
                {
                    NativeEventTests.Check(relic.Icon != null && relic.IconOutline != null, "Relic icon and outline load.");
                    NativeEventTests.Check(relic.Rarity == (relic is ToughBandages or CharonsAshes ? RelicRarity.Rare : RelicRarity.Event), "Native Rare remains Rare.");
                    NativeEventTests.Check(!relic.DynamicDescription.GetFormattedText().Contains("{"), "Bilingual description resolves.");
                }
            }
        }
        MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage("eng");
        return Task.CompletedTask;
    }
    private static Player Restore(Player player)
    {
        var restored = Player.FromSerializable(player.ToSerializable());
        RunState.CreateForTest([restored], acts: [], seed: "RELIC_RESTORE");
        return restored;
    }
    private static async Task Omamori()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player();
        var relic = await RelicCmd.Obtain<AllMatters.Relics.Omamori>(p);
        await CardPileCmd.Add(p.RunState.CreateCard<StrikeIronclad>(p), PileType.Deck);
        NativeEventTests.Check(relic.DisplayAmount == 2, "Noncurse does not consume a use.");
        var curse = p.RunState.CreateCard<Greed>(p);
        await CardPileCmd.Add(curse, PileType.Deck);
        NativeEventTests.Check(!p.Deck.Cards.Contains(curse) && relic.DisplayAmount == 1, "First actual deck addition removed.");
        p = Restore(p);
        relic = p.Relics.OfType<AllMatters.Relics.Omamori>().Single();
        for (int i = 0; i < 2; i++)
        {
            curse = p.RunState.CreateCard<Greed>(p); await CardPileCmd.Add(curse, PileType.Deck);
            NativeEventTests.Check(p.Deck.Cards.Contains(curse) == (i == 1), "Only remaining saved use removes a curse.");
        }
        NativeEventTests.Check(relic.IsUsedUp && relic.DisplayAmount == 0 && p.Relics.Contains(relic), "Empty relic remains visibly used up.");
        p = Restore(p);
        NativeEventTests.Check(p.Relics.OfType<AllMatters.Relics.Omamori>().Single().IsUsedUp, "Empty state restores.");
    }
    private static async Task Turn(AllMatters.Relics.Inserter relic, Player p, CombatState combat)
    {
#if STS2_PUBLIC
        await relic.AfterSideTurnStart(CombatSide.Player, [p.Creature], combat);
#else
        await relic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), p);
#endif
    }
    private static async Task Inserter()
    {
        var p = HeadlessTestSetup.PlayerWithRun(character: ModelDb.Character<Defect>());
        var combat = HeadlessTestSetup.Combat(p.RunState);
        var relic = await RelicCmd.Obtain<AllMatters.Relics.Inserter>(p);
        int slots = p.PlayerCombatState!.OrbQueue.Capacity;
        var other = NativeEventTests.Player();
#if STS2_PUBLIC
        await relic.AfterSideTurnStart(CombatSide.Enemy, [p.Creature], combat);
        await relic.AfterSideTurnStart(CombatSide.Player, [other.Creature], combat);
#else
        await relic.AfterPlayerTurnStart(new ThrowingPlayerChoiceContext(), other);
#endif
        NativeEventTests.Check(relic.DisplayAmount == 0, "Other turns do not count.");
        await Turn(relic, p, combat);
        NativeEventTests.Check(relic.DisplayAmount == 1 && p.PlayerCombatState.OrbQueue.Capacity == slots, "First own turn waits.");
        p = Restore(p); p.Creature.Reset();
        combat = new CombatState(runState: p.RunState); combat.AddPlayer(p); p.ResetCombatState();
        relic = p.Relics.OfType<AllMatters.Relics.Inserter>().Single();
        slots = p.PlayerCombatState!.OrbQueue.Capacity;
        await Turn(relic, p, combat);
        NativeEventTests.Check(relic.DisplayAmount == 0 && p.PlayerCombatState.OrbQueue.Capacity == slots + 1, "Restored next combat second turn adds one slot.");
    }
    private static Creature Add(CombatState combat, MonsterModel model, string slot)
    {
        var c = combat.CreateCreature(model.ToMutable(), CombatSide.Enemy, slot); combat.AddCreature(c);
        c.SetMaxHpInternal(100); c.SetCurrentHpInternal(100); return c;
    }
    private static async Task Insect()
    {
        var (p, combat) = NativeEventTests.Combat(); var ctx = new ThrowingPlayerChoiceContext();
        var a = Add(combat, ModelDb.Monster<AllMatters.Monsters.Sentry>(), "a");
        var b = Add(combat, ModelDb.Monster<AllMatters.Monsters.Sentry>(), "b");
        var ordinary = Add(combat, ModelDb.Monster<LivingFog>(), "ordinary");
        var boss = Add(combat, ModelDb.Monster<KinPriest>(), "boss");
        var judge = Add(combat, ModelDb.Monster<OwlMagistrate>(), "judge");
        foreach (var c in new[] { a, b, ordinary, boss, judge }) await PowerCmd.Apply<ArtifactPower>(ctx, c, 2, c, null);
        await RelicCmd.Obtain<AllMatters.Relics.PreservedInsect>(p);
        await Hook.BeforeCombatStart(p.RunState, combat);
        NativeEventTests.Check(new[] { a, b }.All(c => !c.HasPower<ArtifactPower>() && c.GetPower<ShrinkPower>()?.Amount == 99), "Both actual event elites lose all Artifact before Shrink.");
        NativeEventTests.Check(!ordinary.HasPower<ShrinkPower>() && !boss.HasPower<ShrinkPower>() && !judge.HasPower<ShrinkPower>() && ordinary.HasPower<ArtifactPower>() && boss.HasPower<ArtifactPower>(), "Ordinary and Boss preserve Artifact and damage.");
        int hp = p.Creature.CurrentHp;
        await CreatureCmd.Damage(ctx, p.Creature, 10, ValueProp.Move, a);
        NativeEventTests.Check(p.Creature.CurrentHp == hp - 7, "Native Shrink reduces powered attack damage by 30 percent.");
    }
    private static async Task Sword()
    {
        var previousNetId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
        MegaCrit.Sts2.Core.Context.LocalContext.NetId = 1;
        var (p, combat) = NativeEventTests.Combat();
        RunManager.Instance.SetUpTest((RunState)p.RunState, new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
        CombatManager.Instance.Reset(true); CombatManager.Instance.SetUpCombat(combat);
        try
        {
            var sword = await RelicCmd.Obtain<SwordOfStone>(p);
            var a = Add(combat, ModelDb.Monster<AllMatters.Monsters.Sentry>(), "a");
            var b = Add(combat, ModelDb.Monster<AllMatters.Monsters.Sentry>(), "b");
            var ordinary = Add(combat, ModelDb.Monster<LivingFog>(), "ordinary");
            var escaped = Add(combat, ModelDb.Monster<AllMatters.Monsters.Sentry>(), "escaped");
            // Keep one living creature so the native host does not end combat midassertion.
            var boss = Add(combat, ModelDb.Monster<KinPriest>(), "boss");
            var summon = Add(combat, ModelDb.Monster<AllMatters.Monsters.Sentry>(), "summon");
            await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(), summon, 1, summon, null);
            await CreatureCmd.Kill(summon);
            await CreatureCmd.Escape(escaped);
            await CreatureCmd.Kill(ordinary); await CreatureCmd.Kill(a); await CreatureCmd.Kill(b);
            NativeEventTests.Check(sword.ElitesDefeated == 2, "Two defeated Sentries count two; ordinary and escape count zero.");
            await sword.AfterCombatVictory(new CombatRoom(ModelDb.Encounter<JungleMazeEncounter>().ToMutable(), p.RunState));
            NativeEventTests.Check(sword.ElitesDefeated == 2, "Room victory adds no extra unit.");
            var restored = Player.FromSerializable(p.ToSerializable());
            NativeEventTests.Check(restored.Relics.OfType<SwordOfStone>().Single().ElitesDefeated == 2, "Native saved property retains unit progress.");
            sword.ElitesDefeated = 4;
            await CreatureCmd.Kill(boss);
            NativeEventTests.Check(!p.Relics.Contains(sword) && p.Relics.Count(r => r is SwordOfJade) == 1, "Fifth unit transforms once into unchanged native Sword of Jade.");
        }
        finally { CombatManager.Instance.Reset(true); RunManager.Instance.CleanUp(false); MegaCrit.Sts2.Core.Context.LocalContext.NetId = previousNetId; }
    }
    private static async Task NativeEffects()
    {
        var (p, combat) = NativeEventTests.Combat(); var ctx = new ThrowingPlayerChoiceContext();
        var target = Add(combat, ModelDb.Monster<LivingFog>(), "target");
        var card = combat.CreateCard<DefendIronclad>(p);
        var bandages = await RelicCmd.Obtain<ToughBandages>(p);
        await bandages.AfterCardDiscarded(ctx, card);
        NativeEventTests.Check(p.Creature.Block == 3, "Native Bandages block unchanged.");
        var ashes = await RelicCmd.Obtain<CharonsAshes>(p);
        await ashes.AfterCardExhausted(ctx, card, false);
        NativeEventTests.Check(target.CurrentHp == 97, "Native Ashes damage unchanged.");
    }
}
