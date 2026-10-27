using AllMatters.Events;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

internal static class TrashHeapDefectTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Trash defect event eligibility, assets and bilingual upgrade previews", Presentation),
        ("Trash defect Auto Shields grants block only without existing block in either form", Shields),
        ("Trash defect Electrodynamics hits all enemies and Lock On modifies only the owner's orbs", Lightning),
        ("Trash defect Core Surge deals upgraded damage and grants native Artifact", Core),
    ];

    private static Task Presentation()
    {
        var loc = MegaCrit.Sts2.Core.Localization.LocManager.Instance;
        try
        {
            foreach (var character in new CharacterModel[] { ModelDb.Character<Ironclad>(), ModelDb.Character<Silent>(), ModelDb.Character<Defect>() })
            foreach (string language in new[] { "eng", "zhs" })
            {
                loc.SetLanguage(language);
                var player = HeadlessTestSetup.PlayerWithRun(character: character);
                foreach (var model in TrashHeapCandidates.Cards(player).Where(c => c is AllMatters.Cards.AutoShields or AllMatters.Cards.Bullseye or AllMatters.Cards.CoreSurge or AllMatters.Cards.Electrodynamics))
                {
                    var card = model.ToMutable();
                    NativeEventTests.Check(card.Rarity == CardRarity.Event && card.VisualCardPool is MegaCrit.Sts2.Core.Models.CardPools.DefectCardPool && card.Portrait != null, "Blue event cards and portraits are available to every character.");
                    var ordinary = card.GetDescriptionForPile(PileType.None);
                    card.UpgradeInternal();
                    var preview = card.GetDescriptionForUpgradePreview();
                    NativeEventTests.Check(!ordinary.Contains("missing", StringComparison.OrdinalIgnoreCase) && !ordinary.Contains("{") && !preview.Contains("{"), "Localized normal and upgrade text resolves.");
                    card.FinalizeUpgradeInternal();
                    NativeEventTests.Check(!card.GetDescriptionForPile(PileType.None).Contains("[green]"), "Ordinary upgraded descriptions do not contain preview highlights.");
                    if (card is not AllMatters.Cards.AutoShields) NativeEventTests.Check(card.HoverTips.Any(), "Orb and power explanations are available.");
                }
            }
            NativeEventTests.Check(ModelDb.Power<AllMatters.Powers.LockOnPower>().Icon != null && ModelDb.Power<AllMatters.Powers.ElectrodynamicsPower>().Icon != null, "Both power icons load.");
        }
        finally { loc.SetLanguage("eng"); }
        return Task.CompletedTask;
    }

    internal static MegaCrit.Sts2.Core.Entities.Creatures.Creature Enemy(MegaCrit.Sts2.Core.Combat.CombatState combat, string slot = "target")
    {
        var target = combat.CreateCreature(ModelDb.Monster<AllMatters.Monsters.Lagavulin>().ToMutable(), MegaCrit.Sts2.Core.Combat.CombatSide.Enemy, slot);
        combat.AddCreature(target); target.SetMaxHpInternal(100); target.SetCurrentHpInternal(100);
        return target;
    }

    private static async Task Core()
    {
        foreach (bool upgraded in new[] { false, true })
        {
            var (p, combat) = NativeEventTests.Combat();
            var target = Enemy(combat);
            var card = combat.CreateCard<AllMatters.Cards.CoreSurge>(p);
            if (upgraded) CardCmd.Upgrade(card);
            await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), target, true, default, skipCardPileVisuals: true);
            NativeEventTests.Check(target.CurrentHp == (upgraded ? 85 : 89) && p.Creature.GetPower<MegaCrit.Sts2.Core.Models.Powers.ArtifactPower>()?.Amount == 1
                && p.PlayerCombatState!.ExhaustPile.Cards.Contains(card), "11/15 attack, one native Artifact and Exhaust.");
        }
    }

    private static async Task Lightning()
    {
        foreach (bool upgraded in new[] { false, true })
        {
            var run = HeadlessTestSetup.Run("TRASH_LIGHTNING", ModelDb.Character<Defect>(), ModelDb.Character<Defect>());
            var combat = HeadlessTestSetup.Combat(run); var p = run.Players[0]; var other = run.Players[1];
            var target = Enemy(combat); var second = Enemy(combat, "second");
            var ctx = new ThrowingPlayerChoiceContext();
            var bullseye = combat.CreateCard<AllMatters.Cards.Bullseye>(p);
            var electro = combat.CreateCard<AllMatters.Cards.Electrodynamics>(p);
            if (upgraded) { CardCmd.Upgrade(bullseye); CardCmd.Upgrade(electro); }
            await bullseye.OnPlayWrapper(ctx, target, true, default, skipCardPileVisuals: true);
            NativeEventTests.Check(target.CurrentHp == (upgraded ? 89 : 92) && target.GetPower<AllMatters.Powers.LockOnPower>()?.Amount == (upgraded ? 3 : 2), "Bullseye applies damage and 2/3 turns of Lock-On.");
            await electro.OnPlayWrapper(ctx, null, true, default, skipCardPileVisuals: true);
            NativeEventTests.Check(p.PlayerCombatState!.OrbQueue.Orbs.Count == 3 && electro.EnergyCost.GetAmountToSpend() == (upgraded ? 1 : 2), $"Channel three Lightning at cost2/1: count={p.PlayerCombatState.OrbQueue.Orbs.Count}, capacity={p.PlayerCombatState.OrbQueue.Capacity}, cost={electro.EnergyCost.Canonical}, upgraded={upgraded}.");
            int hp = target.CurrentHp;
            await p.PlayerCombatState.OrbQueue.Orbs[0].Passive(ctx, target);
            NativeEventTests.Check(target.CurrentHp == hp - 4 && second.CurrentHp == 97, "Marked enemy takes 4; the other enemy takes3, including when a target was supplied.");
            await OrbCmd.Channel<MegaCrit.Sts2.Core.Models.Orbs.LightningOrb>(ctx, other);
            hp = target.CurrentHp;
            await other.PlayerCombatState!.OrbQueue.Orbs[0].Passive(ctx, target);
            NativeEventTests.Check(target.CurrentHp == hp - 3 && second.CurrentHp == 97, "Teammate has neither the area effect nor Lock-On multiplier.");
            await PowerCmd.Apply<AllMatters.Powers.LockOnPower>(ctx, target, 2, other.Creature, null);
            NativeEventTests.Check(target.Powers.OfType<AllMatters.Powers.LockOnPower>().Count() == 2, "Marks from two appliers remain independent.");
            hp = target.CurrentHp;
            await other.PlayerCombatState.OrbQueue.Orbs[0].Passive(ctx, target);
            NativeEventTests.Check(target.CurrentHp == hp - 4, "The second applier benefits from their own mark.");
            hp = target.CurrentHp;
            await p.PlayerCombatState.OrbQueue.Orbs[0].Evoke(ctx);
            NativeEventTests.Check(target.CurrentHp == hp - 12 && second.CurrentHp == 89, "Lightning evoke hits all enemies with per-target modifiers.");
            foreach (var mark in target.Powers.OfType<AllMatters.Powers.LockOnPower>().ToArray())
                await mark.AfterSideTurnEnd(ctx, target.Side, [target]);
            NativeEventTests.Check(target.Powers.OfType<AllMatters.Powers.LockOnPower>().Single(m => m.Applier == p.Creature).Amount == (upgraded ? 2 : 1)
                && target.Powers.OfType<AllMatters.Powers.LockOnPower>().Single(m => m.Applier == other.Creature).Amount == 1, "Each applier has its own remaining duration.");
        }
    }

    private static async Task Shields()
    {
        foreach (bool upgraded in new[] { false, true })
        foreach (int initial in new[] { 0, 2 })
        {
            var p = HeadlessTestSetup.PlayerWithRun(character: ModelDb.Character<Defect>());
            var combat = HeadlessTestSetup.Combat(p.RunState);
            var template = TrashHeapCandidates.Cards(p).SingleOrDefault(c => c.Id.Entry == "ALL_MATTERS_AUTO_SHIELDS");
            NativeEventTests.Check(template != null, "Auto Shields must be obtainable from the actual Trash Heap candidates.");
            var card = combat.CreateCard(template!, p);
            if (upgraded) CardCmd.Upgrade(card);
            if (initial > 0) await CreatureCmd.GainBlock(p.Creature, initial, MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered, null);
            await CardPileCmd.Add(card, PileType.Hand);
            await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), null, true, default, skipCardPileVisuals: true);
            NativeEventTests.Check(p.Creature.Block == (initial == 0 ? upgraded ? 15 : 11 : initial), "Only zero initial Block grants 11/15.");
        }
    }
}



