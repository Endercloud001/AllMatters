using AllMatters.Events;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

internal static class TrashHeapRedGreenTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Trash red green cards are actually obtainable without character restrictions", Eligibility),
        ("Trash red green native draw clones once, keeps upgrade and overflows", Draw),
        ("Trash red green attacks, healing, debuffs and Ethereal use native commands", Effects)
    ];
    private static async Task Draw()
    {
        foreach (bool upgraded in new[] { false, true })
        foreach (bool full in new[] { false, true })
        {
            var (p, combat) = NativeEventTests.Combat();
            var card = combat.CreateCard<AllMatters.Cards.EndlessAgony>(p);
            if (upgraded) CardCmd.Upgrade(card);
            if (full) for (int i=0; i<9; i++) await CardPileCmd.Add(combat.CreateCard<MegaCrit.Sts2.Core.Models.Cards.DefendIronclad>(p), PileType.Hand);
            await CardPileCmd.Add(card, PileType.Draw);
            await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, p);
            var copies = new[] { p.PlayerCombatState!.Hand, p.PlayerCombatState.DiscardPile, p.PlayerCombatState.DrawPile }.SelectMany(pile => pile.Cards).OfType<AllMatters.Cards.EndlessAgony>().ToArray();
            NativeEventTests.Check(copies.Length == 2 && copies.All(c => c.IsUpgraded == upgraded && c.Owner == p), "One draw adds exactly one upgraded-state-preserving copy.");
            NativeEventTests.Check((full ? p.PlayerCombatState.DiscardPile : p.PlayerCombatState.Hand).Cards.OfType<AllMatters.Cards.EndlessAgony>().Count() == (full ? 1 : 2), "Native full-hand overflow goes to discard.");
        }
    }
    private static async Task Effects()
    {
        foreach (bool upgraded in new[] { false, true })
        {
            MegaCrit.Sts2.Core.Context.LocalContext.NetId = 1;
            var (p, combat) = NativeEventTests.Combat(); var ctx = new ThrowingPlayerChoiceContext();
            MegaCrit.Sts2.Core.Runs.RunManager.Instance.SetUpTest((MegaCrit.Sts2.Core.Runs.RunState)p.RunState, new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
            MegaCrit.Sts2.Core.Combat.CombatManager.Instance.Reset(true);
            MegaCrit.Sts2.Core.Combat.CombatManager.Instance.SetUpCombat(combat);
            try
            {
            var target = TrashHeapDefectTests.Enemy(combat);
            var terror = combat.CreateCard<AllMatters.Cards.Terror>(p);
            var drop = combat.CreateCard<AllMatters.Cards.Dropkick>(p);
            var disarm = combat.CreateCard<AllMatters.Cards.Disarm>(p);
            var armor = combat.CreateCard<AllMatters.Cards.GhostlyArmor>(p);
            var reaper = combat.CreateCard<AllMatters.Cards.Reaper>(p);
            if(upgraded) foreach(var c in new CardModel[] {terror,drop,disarm,armor,reaper}) CardCmd.Upgrade(c);
            int beforeEnergy = p.PlayerCombatState!.Energy;
            await drop.OnPlayWrapper(ctx,target,true,default,skipCardPileVisuals:true);
            NativeEventTests.Check(p.PlayerCombatState.Energy == beforeEnergy && p.PlayerCombatState.Hand.Cards.Count == 0, "No Vulnerable means no draw or energy.");
            var agony = combat.CreateCard<AllMatters.Cards.EndlessAgony>(p); if(upgraded) CardCmd.Upgrade(agony);
            int beforeHp = target.CurrentHp;
            await agony.OnPlayWrapper(ctx,target,true,default,skipCardPileVisuals:true);
            NativeEventTests.Check(target.CurrentHp == beforeHp - (upgraded?6:4) && p.PlayerCombatState.ExhaustPile.Cards.Contains(agony), "Endless Agony deals4/6 and exhausts.");
            await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.ArtifactPower>(ctx,target,1,target,null);
            await terror.OnPlayWrapper(ctx,target,true,default,skipCardPileVisuals:true);
            NativeEventTests.Check(!target.HasPower<MegaCrit.Sts2.Core.Models.Powers.VulnerablePower>() && !target.HasPower<MegaCrit.Sts2.Core.Models.Powers.ArtifactPower>(), "Artifact prevents Terror through the native debuff path.");
            await terror.OnPlayWrapper(ctx,target,true,default,skipCardPileVisuals:true);
            NativeEventTests.Check(target.GetPower<MegaCrit.Sts2.Core.Models.Powers.VulnerablePower>()?.Amount==99 && terror.EnergyCost.GetAmountToSpend()==(upgraded?0:1), "Terror applies 99 Vulnerable at cost1/0.");
            await CardPileCmd.Add(combat.CreateCard<MegaCrit.Sts2.Core.Models.Cards.DefendIronclad>(p),PileType.Draw);
            int energy=p.PlayerCombatState!.Energy, hp=target.CurrentHp;
            await drop.OnPlayWrapper(ctx,target,true,default,skipCardPileVisuals:true);
            NativeEventTests.Check(p.PlayerCombatState.Energy==energy+1 && p.PlayerCombatState.Hand.Cards.Count==1 && target.CurrentHp==hp-(upgraded?12:7), "Vulnerable target grants energy and one real draw after damage.");
            await disarm.OnPlayWrapper(ctx,target,true,default,skipCardPileVisuals:true);
            NativeEventTests.Check(target.GetPower<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>()?.Amount==(upgraded?-3:-2), "Disarm reduces Strength2/3.");
            await armor.OnPlayWrapper(ctx,null,true,default,skipCardPileVisuals:true);
            NativeEventTests.Check(p.Creature.Block==(upgraded?13:10) && armor.Keywords.Contains(CardKeyword.Ethereal) && !armor.Keywords.Contains(CardKeyword.Exhaust) && !p.PlayerCombatState.ExhaustPile.Cards.Contains(armor), "Ghostly Armor is Ethereal, not Exhaust.");
            await PowerCmd.Remove(target.GetPower<MegaCrit.Sts2.Core.Models.Powers.VulnerablePower>()!);
            target.SetCurrentHpInternal(1); var second=TrashHeapDefectTests.Enemy(combat,"second");
            await CreatureCmd.GainBlock(second,2,MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered,null);
            p.Creature.SetCurrentHpInternal(10);
            await reaper.OnPlayWrapper(ctx,null,true,default,skipCardPileVisuals:true);
            NativeEventTests.Check(p.Creature.CurrentHp==(upgraded?15:13) && second.CurrentHp==(upgraded?96:98), $"Reaper heals actual HP loss: owner={p.Creature.CurrentHp}, second={second.CurrentHp}, upgraded={upgraded}.");
            NativeEventTests.Check(new CardModel[]{terror,disarm,reaper}.All(c=>p.PlayerCombatState.ExhaustPile.Cards.Contains(c)), "Native Exhaust applies to all three cards.");
            }
            finally { MegaCrit.Sts2.Core.Combat.CombatManager.Instance.Reset(true); MegaCrit.Sts2.Core.Runs.RunManager.Instance.CleanUp(false); }
        }
    }
    private static Task Eligibility()
    {
        var p = NativeEventTests.Player();
        foreach (string entry in new[] { "ENDLESS_AGONY", "TERROR", "DROPKICK", "REAPER", "DISARM", "GHOSTLY_ARMOR" })
        {
            var model = TrashHeapCandidates.Cards(p).SingleOrDefault(c => c.Id.Entry == "ALL_MATTERS_" + entry);
            NativeEventTests.Check(model != null && model.Rarity == CardRarity.Event && model.Portrait != null, "Returning event card and portrait: " + entry);
            foreach (string language in new[] {"eng", "zhs"})
            {
                MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage(language);
                var card = model!.ToMutable();
                NativeEventTests.Check(!card.GetDescriptionForPile(PileType.None).Contains("{"), "Normal localized description resolves.");
                card.UpgradeInternal();
                NativeEventTests.Check(!card.GetDescriptionForUpgradePreview().Contains("{"), "Localized upgrade preview resolves.");
                card.FinalizeUpgradeInternal();
            }
        }
        MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage("eng");
        return Task.CompletedTask;
    }
}
