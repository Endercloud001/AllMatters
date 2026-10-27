using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using AllMatters.Powers;
using AllMatters.Events;
using MegaCrit.Sts2.Core.Models;
internal static class TrashHeapColorlessTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases = [("Trash colorless candidates include only the four source cards", Eligibility), ("Trash colorless binding, recharge and chain use native commands", Effects), ("Trash colorless foreign discovery preserves native eligibility and upgrade", Discovery)];
    private static async Task Effects()
    {
        foreach (bool upgraded in new[] {false,true})
        {
            var run=HeadlessTestSetup.Run("COLORLESS",ModelDb.Character<Ironclad>(),ModelDb.Character<Silent>());
            var p=run.Players[0]; var ally=run.Players[1]; var combat=HeadlessTestSetup.Combat(run);
            var target=TrashHeapDefectTests.Enemy(combat); var other=TrashHeapDefectTests.Enemy(combat,"other");
            var ctx=new ThrowingPlayerChoiceContext();
            var hand=combat.CreateCard<AllMatters.Cards.TalkToTheHand>(p); if(upgraded) CardCmd.Upgrade(hand);
            await hand.OnPlayWrapper(ctx,target,true,default,skipCardPileVisuals:true);
            NativeEventTests.Check(target.CurrentHp==(upgraded?93:95),"Binding attack deals5/7 before applying mark.");
            await PowerCmd.Apply<TalkToTheHandPower>(ctx,target,4,ally.Creature,null);
            await CreatureCmd.GainBlock(target,99,ValueProp.Unpowered,null);
            int block=p.Creature.Block, allyBlock=ally.Creature.Block;
            await CreatureCmd.Damage(ctx,target,0,ValueProp.Move,ally.Creature);
            await CreatureCmd.Damage(ctx,target,5,ValueProp.Move,p.Creature);
            NativeEventTests.Check(p.Creature.Block==block+2*(upgraded?3:2) && ally.Creature.Block==allyBlock+8,"Zero and blocked teammate attacks trigger each independent applier.");
            block=p.Creature.Block;
            await CreatureCmd.Damage(ctx,target,1,ValueProp.Unpowered,ally.Creature);
            await CreatureCmd.Damage(ctx,target,1,ValueProp.Move,(Creature?)null);
            await CreatureCmd.Damage(ctx,target,1,ValueProp.Move,target);
            await CreatureCmd.Damage(ctx,other,1,ValueProp.Move,ally.Creature);
            NativeEventTests.Check(p.Creature.Block==block,"Nonattack, no dealer, self damage and unbound enemies do not trigger.");
            var fasting=combat.CreateCard<AllMatters.Cards.Fasting>(p); if(upgraded) CardCmd.Upgrade(fasting);
            int energy=p.PlayerCombatState!.Energy;
            await fasting.OnPlayWrapper(ctx,null,true,default,skipCardPileVisuals:true);
            NativeEventTests.Check(p.Creature.GetPower<StrengthPower>()?.Amount==(upgraded?4:3) && p.Creature.GetPower<DexterityPower>()?.Amount==(upgraded?4:3) && p.PlayerCombatState.Energy==energy,"Fasting grants stats and does not immediately spend an extra energy.");
            for(int turn=0;turn<2;turn++)
            {
                p.PlayerCombatState.ResetEnergy();
                await p.Creature.GetPower<FastingPower>()!.AfterEnergyReset(p);
                NativeEventTests.Check(p.PlayerCombatState.Energy==p.PlayerCombatState.MaxEnergy-1,"Every owner recharge loses one energy.");
            }
            ally.PlayerCombatState!.Energy=3;
            await p.Creature.GetPower<FastingPower>()!.AfterEnergyReset(ally);
            NativeEventTests.Check(ally.PlayerCombatState.Energy==3,"Teammate energy is unchanged.");
            for(int i=0;i<5;i++) await CardPileCmd.Add(combat.CreateCard<MegaCrit.Sts2.Core.Models.Cards.DefendIronclad>(p),PileType.Draw);
            ulong rng=HeadlessTestSetup.RngPosition(p.RunState.Rng.Shuffle);
            var alpha=combat.CreateCard<AllMatters.Cards.Alpha>(p); if(upgraded) CardCmd.Upgrade(alpha);
            await alpha.OnPlayWrapper(ctx,null,true,default,skipCardPileVisuals:true);
            NativeEventTests.Check(HeadlessTestSetup.RngPosition(p.RunState.Rng.Shuffle)!=rng,"Alpha randomly inserts into nonempty draw pile using native RNG.");
            var beta=p.PlayerCombatState.DrawPile.Cards.OfType<AllMatters.Cards.Beta>().Single();
            NativeEventTests.Check(!beta.IsUpgraded && alpha.Keywords.Contains(CardKeyword.Innate)==upgraded && p.PlayerCombatState.ExhaustPile.Cards.Contains(alpha),"Alpha exhausts, upgrade is Innate, generated Beta starts normal.");
            if(upgraded) CardCmd.Upgrade(beta);
            NativeEventTests.Check(beta.EnergyCost.GetAmountToSpend()==(upgraded?1:2),"Beta costs2/1.");
            await beta.OnPlayWrapper(ctx,null,true,default,skipCardPileVisuals:true);
            var omega=p.PlayerCombatState.DrawPile.Cards.OfType<AllMatters.Cards.Omega>().Single();
            NativeEventTests.Check(!omega.IsUpgraded && p.PlayerCombatState.ExhaustPile.Cards.Contains(beta),"Beta exhausts and generates normal Omega.");
            if(upgraded) CardCmd.Upgrade(omega);
            await omega.OnPlayWrapper(ctx,null,true,default,skipCardPileVisuals:true);
            var power=p.Creature.GetPower<OmegaPower>()!;
            int hp=other.CurrentHp;
            await power.AfterSideTurnEnd(ctx,CombatSide.Player,[ally.Creature]);
            NativeEventTests.Check(other.CurrentHp==hp,"Only owner end triggers Omega.");
            await power.AfterSideTurnEnd(ctx,CombatSide.Player,[p.Creature]);
            NativeEventTests.Check(other.CurrentHp==hp-(upgraded?60:50),"Omega hits50/60, unaffected by owner Strength.");
        }
    }
    private static async Task Discovery()
    {
        foreach(bool upgraded in new[]{false,true})
        {
            var (p,combat)=NativeEventTests.Combat();var card=combat.CreateCard<AllMatters.Cards.ForeignInfluence>(p);
            if(upgraded) CardCmd.Upgrade(card);
            var selector=new Choose(upgraded);
            using(CardSelectCmd.UseSelector(selector)) await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(),null,true,default,skipCardPileVisuals:true);
            var chosen=p.PlayerCombatState!.Hand.Cards.Single();
            NativeEventTests.Check(selector.Calls==1 && chosen.EnergyCost.GetAmountToSpend()==0 && chosen.IsUpgraded==upgraded,"One native discovery creates the chosen card free this turn.");
        }
    }
    private sealed class Choose(bool upgraded) : ICardSelector
    {
        internal int Calls;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options,int minSelect,int maxSelect)
        {
            var cards=options.ToArray();Calls++;
            NativeEventTests.Check(cards.Length==3 && cards.Select(c=>c.Id).Distinct().Count()==3,"Three distinct candidates.");
            NativeEventTests.Check(cards.All(c=>c.Type==CardType.Skill && c.CanBeGeneratedInCombat && c.IsUpgraded==upgraded && c.Pool.Id!=c.Owner.Character.CardPool.Id && c.GetType().Assembly==typeof(CardModel).Assembly && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare),"Only legal other character native skills, upgraded before presentation.");
            return Task.FromResult<IEnumerable<CardModel>>(cards.Take(1));
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,IReadOnlyList<CardRewardAlternative> alternatives)=>throw new InvalidOperationException();
    }
    private static Task Eligibility()
    {
        var p=NativeEventTests.Player();
        foreach(string entry in new[]{"TALK_TO_THE_HAND","FASTING","ALPHA","FOREIGN_INFLUENCE"})
            NativeEventTests.Check(TrashHeapCandidates.Cards(p).Any(c=>c.Id.Entry=="ALL_MATTERS_"+entry),"Missing source card: "+entry);
        foreach (var model in TrashHeapCandidates.Cards(p).Where(c=>new[]{"ALL_MATTERS_TALK_TO_THE_HAND","ALL_MATTERS_FASTING","ALL_MATTERS_ALPHA","ALL_MATTERS_FOREIGN_INFLUENCE"}.Contains(c.Id.Entry)).Concat(new CardModel[]{ModelDb.Card<AllMatters.Cards.Beta>(),ModelDb.Card<AllMatters.Cards.Omega>()}))
        {
            NativeEventTests.Check(model.Portrait!=null && model.Rarity==CardRarity.Event,"Event card portrait loads.");
            foreach(string language in new[]{"eng","zhs"})
            {
                MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage(language);
                var card=model.ToMutable();
                NativeEventTests.Check(!card.GetDescriptionForPile(PileType.None).Contains("{"),"Description resolves.");
                card.UpgradeInternal(); NativeEventTests.Check(!card.GetDescriptionForUpgradePreview().Contains("{"),"Upgrade preview resolves.");card.FinalizeUpgradeInternal();
            }
        }
        MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage("eng");
        NativeEventTests.Check(!TrashHeapCandidates.Cards(p).Any(c=>c.Id.Entry is "ALL_MATTERS_BETA" or "ALL_MATTERS_OMEGA"),"Derived cards stay out of Trash Heap.");
        return Task.CompletedTask;
    }
}
