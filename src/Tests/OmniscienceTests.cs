using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.TestSupport;
using AllMatters.Events;
internal static class OmniscienceTests
{
    public static readonly (string Name,Func<Task> Test)[] Cases=[("Omniscience is a Trash Heap source",Eligibility), ("Omniscience native automatic resolution covers legality, power and recursive choices", Effects)];
    private static async Task Effects()
    {
        foreach(string scenario in new[]{"attack","exhaust","power","unplayable","conditionFalse","conditionTrue","candle","recursive","lethal"})
        {
            var previousNetId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
            MegaCrit.Sts2.Core.Context.LocalContext.NetId=1;
            var(p,combat)=NativeEventTests.Combat();var ctx=new ThrowingPlayerChoiceContext();
            MegaCrit.Sts2.Core.Runs.RunManager.Instance.SetUpTest((MegaCrit.Sts2.Core.Runs.RunState)p.RunState,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
            CombatManager.Instance.Reset(true);CombatManager.Instance.SetUpCombat(combat);
            try
            {
                var target=TrashHeapDefectTests.Enemy(combat);var second=TrashHeapDefectTests.Enemy(combat,"second");
                var source=combat.CreateCard<AllMatters.Cards.Omniscience>(p);
                CardModel selected=scenario switch
                {
                    "power"=>combat.CreateCard<Inflame>(p),
                    "exhaust"=>combat.CreateCard<AllMatters.Cards.CoreSurge>(p),
                    "unplayable" or "candle"=>combat.CreateCard<Greed>(p),
                    "conditionFalse" or "conditionTrue"=>combat.CreateCard<Clash>(p),
                    "recursive"=>combat.CreateCard<AllMatters.Cards.Omniscience>(p),
                    _=>combat.CreateCard<StrikeIronclad>(p)
                };
                if(scenario=="conditionFalse") await CardPileCmd.Add(combat.CreateCard<DefendIronclad>(p),PileType.Hand);
                if(scenario=="candle") await RelicCmd.Obtain<AllMatters.Relics.BlueCandle>(p);
                if(scenario=="lethal") target.SetCurrentHpInternal(1);
                await CardPileCmd.Add(selected,PileType.Draw);
                CardModel? a=null,b=null;
                if(scenario=="recursive")
                {
                    a=combat.CreateCard<DefendIronclad>(p);b=combat.CreateCard<DefendIronclad>(p);
                    await CardPileCmd.Add(a,PileType.Draw);await CardPileCmd.Add(b,PileType.Draw);
                }
                var selector=new Choose(source,new[]{selected,a,b}.OfType<CardModel>().ToArray());
                int enemyHp=target.CurrentHp+second.CurrentHp;
                int energy=p.PlayerCombatState!.Energy,hp=p.Creature.CurrentHp;
                using(CardSelectCmd.UseSelector(selector)) await CardCmd.AutoPlay(ctx,source,null,skipCardPileVisuals:true);
                NativeEventTests.Check(p.PlayerCombatState.Energy==energy,"No extra energy is spent on automatic repeats: "+scenario);
                NativeEventTests.Check(p.PlayerCombatState.ExhaustPile.Cards.Contains(source) && (scenario=="power" ? selected.Pile==null : p.PlayerCombatState.ExhaustPile.Cards.Contains(selected)),"Source and selected card end in Exhaust: "+scenario);
                int damage=enemyHp-target.CurrentHp-second.CurrentHp;
                if(scenario=="attack") NativeEventTests.Check(damage==12,"Selected attack plays twice.");
                if(scenario=="exhaust") NativeEventTests.Check(damage==22 && p.Creature.GetPower<ArtifactPower>()?.Amount==2,"Exhaust on first play still permits the second native play.");
                if(scenario=="power") NativeEventTests.Check(p.Creature.GetPower<StrengthPower>()?.Amount==4,"Power leaving combat still plays twice.");
                if(scenario is "unplayable" or "conditionFalse") NativeEventTests.Check(damage==0,"Illegal selected card exhausts without effects.");
                if(scenario=="conditionTrue") NativeEventTests.Check(damage==28,"Satisfied conditional card plays twice.");
                if(scenario=="candle") NativeEventTests.Check(p.Creature.CurrentHp==hp-4,"Blue Candle removes restriction and both plays lose2 HP.");
                if(scenario=="recursive") NativeEventTests.Check(selector.Calls==3 && p.Creature.Block==20 && p.PlayerCombatState.ExhaustPile.Cards.Contains(a!) && p.PlayerCombatState.ExhaustPile.Cards.Contains(b!),"Nested Omniscience opens two distinct subsequent choices after source exhausted.");
                if(scenario=="lethal") NativeEventTests.Check(damage>=1 && damage<=12,"Native automatic targeting remains valid when a target dies.");
            }
            finally{CombatManager.Instance.Reset(true);MegaCrit.Sts2.Core.Runs.RunManager.Instance.CleanUp(false); MegaCrit.Sts2.Core.Context.LocalContext.NetId = previousNetId;}
        }
    }
    private sealed class Choose(CardModel source,CardModel[] choices):ICardSelector
    {
        internal int Calls;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options,int minSelect,int maxSelect)
        {
            if(Calls>0) NativeEventTests.Check(source.Pile?.Type==PileType.Exhaust,"Current Omniscience finishes before nested selection.");
            var chosen=choices[Calls++];NativeEventTests.Check(options.Contains(chosen),"Each selector receives its own available draw cards.");
            return Task.FromResult<IEnumerable<CardModel>>(new[]{chosen});
        }
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,IReadOnlyList<CardRewardAlternative> alternatives)=>throw new InvalidOperationException();
    }
    private static Task Eligibility()
    {
        NativeEventTests.Check(TrashHeapCandidates.Cards(NativeEventTests.Player()).Any(c=>c.Id.Entry=="ALL_MATTERS_OMNISCIENCE"),"Omniscience must be obtainable.");
        var model=ModelDb.Card<AllMatters.Cards.Omniscience>();
        NativeEventTests.Check(model.Portrait!=null && model.Rarity==CardRarity.Event,"Event portrait loads.");
        foreach(string lang in new[]{"eng","zhs"})
        {
            MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage(lang);
            var card=model.ToMutable();
            NativeEventTests.Check(card.EnergyCost.GetAmountToSpend()==4 && !card.GetDescriptionForPile(PileType.None).Contains("{"),"Normal text/cost.");
            CardCmd.Upgrade(card);
            NativeEventTests.Check(card.EnergyCost.GetAmountToSpend()==3 && !card.GetDescriptionForUpgradePreview().Contains("{"),"Upgrade text/cost.");
        }
        MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage("eng");
        return Task.CompletedTask;
    }
}
