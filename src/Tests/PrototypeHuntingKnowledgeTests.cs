// THROWAWAY choice adapters. No art paths and no deck/pile insertion.
using HarmonyLib;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

public sealed class PrototypeHuntingTenderChoice : CardModel,KnowledgeDemon.IChoosable
{
    public PrototypeHuntingTenderChoice():base(-1,CardType.Status,CardRarity.Quest,TargetType.Self){}
    public override string Title=>ModelDb.Power<TenderPower>().Title.GetFormattedText();
    public override string PortraitPath=>ModelDb.Power<TenderPower>().IconPath;
    public override CardPoolModel Pool=>ModelDb.Card<MindRot>().Pool;
    protected override void AddExtraArgsToDescription(MegaCrit.Sts2.Core.Localization.LocString description)=>description.Add("Amount",1);
    public Task OnChosen()=>PowerCmd.Apply<TenderPower>(new ThrowingPlayerChoiceContext(),Owner.Creature,1,Owner.Creature,null);
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>Task.CompletedTask;
}
public sealed class PrototypeHuntingCalamityChoice : CardModel,KnowledgeDemon.IChoosable
{
    public PrototypeHuntingCalamityChoice():base(-1,CardType.Status,CardRarity.Quest,TargetType.Self){}
    public override string Title=>ModelDb.Card<Calamity>().Title;
    public override string PortraitPath=>ModelDb.Card<Calamity>().PortraitPath;
    public override CardPoolModel Pool=>ModelDb.Card<MindRot>().Pool;
    public Task OnChosen()=>PowerCmd.Apply<CalamityPower>(new ThrowingPlayerChoiceContext(),Owner.Creature,1,Owner.Creature,null);
    protected override Task OnPlay(PlayerChoiceContext context,CardPlay play)=>Task.CompletedTask;
}
internal static class PrototypeHuntingKnowledgeTests
{
    private static bool Injected;
    internal static void Inject(){if(Injected)return;ModelDb.Inject(typeof(PrototypeHuntingTenderChoice));ModelDb.Inject(typeof(PrototypeHuntingCalamityChoice));Injected=true;}
    internal static bool DisplayDescription(CardModel __instance,ref MegaCrit.Sts2.Core.Localization.LocString __result)
    {
        if(__instance is PrototypeHuntingCalamityChoice){__result=ModelDb.Card<Calamity>().Description;return false;}
        if(__instance is PrototypeHuntingTenderChoice){__result=ModelDb.Power<TenderPower>().Description;return false;}
        return true;
    }
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingKnowledge native per living player three choices adapters and damage",Knowledge)];
    private static int Ascension;
    private static void Slap(ref int __result)=>__result=Ascension>=9?16:15;
    private static void Multi(ref int __result)=>__result=Ascension>=9?7:6;
    private static void Heal(Creature creature,ref decimal amount){if(creature.Monster is KnowledgeDemon)amount=20;}
    private static bool Choose(KnowledgeDemon __instance,Creature target,ref Task __result)
    {
        __result=Run();return false;
        async Task Run()
        {
            if(target.IsDead)return;
            int round=(int)AccessTools.Property(typeof(KnowledgeDemon),"CurseOfKnowledgeCounter").GetValue(__instance)!;
            CardModel[][] pairs=[[ModelDb.Card<MindRot>(),ModelDb.Card<PrototypeHuntingCalamityChoice>()],[ModelDb.Card<Sloth>(),ModelDb.Card<PrototypeHuntingTenderChoice>()],[ModelDb.Card<WasteAway>(),ModelDb.Card<Disintegration>()]];
            var cards=pairs[round].Select(c=>__instance.CombatState.CreateCard(c,target.Player!)).ToArray();
            foreach(var c in cards.OfType<Disintegration>())c.DynamicVars["DisintegrationPower"].BaseValue=21;
            var selected=await CardSelectCmd.FromChooseACardScreen(new BlockingPlayerChoiceContext(),cards,target.Player!);
            if(selected!=null)await ((KnowledgeDemon.IChoosable)selected).OnChosen();
        }
    }
    private sealed class Pick(int index):ICardSelector
    {
        internal int Calls;
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options,int min,int max)
        {Calls++;var cards=options.ToArray();NativeEventTests.Check(cards.Length==2,"Exactly two display options");return Task.FromResult<IEnumerable<CardModel>>([cards[index]]);}
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,IReadOnlyList<CardRewardAlternative> alternatives)=>throw new InvalidOperationException();
    }
    private static async Task Knowledge()
    {
        Inject();
        var patch=new Harmony("allmatters.prototype70.knowledge");
        patch.Patch(AccessTools.Method(typeof(KnowledgeDemon),"ChooseCurse"),prefix:new HarmonyMethod(typeof(PrototypeHuntingKnowledgeTests),nameof(Choose)));
        patch.Patch(AccessTools.PropertyGetter(typeof(KnowledgeDemon),"SlapDamage"),postfix:new HarmonyMethod(typeof(PrototypeHuntingKnowledgeTests),nameof(Slap)));
        patch.Patch(AccessTools.PropertyGetter(typeof(KnowledgeDemon),"KnowledgeOverwhelmingDamage"),postfix:new HarmonyMethod(typeof(PrototypeHuntingKnowledgeTests),nameof(Multi)));
        patch.Patch(AccessTools.Method(typeof(CreatureCmd),nameof(CreatureCmd.Heal)),prefix:new HarmonyMethod(typeof(PrototypeHuntingKnowledgeTests),nameof(Heal)));
        try
        {
            foreach(int asc in new[]{0,9})for(int n=1;n<=4;n++)foreach(int pick in new[]{0,1}) foreach(bool withDead in n==1?new[]{false}:new[]{false,true})
            {
                Ascension=asc;
                var players=Enumerable.Range(1,n).Select(i=>MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(ModelDb.Character<Ironclad>(),UnlockState.all,(ulong)i)).ToArray();
                var run=RunState.CreateForTest(players,acts:[],ascensionLevel:asc,seed:"HUNTING70_KNOWLEDGE");
                using var battle=new HeadlessTestSetup.EncounterCombatScope(run,ModelDb.Encounter<VantomBoss>().ToMutable());
                foreach(var p in players){p.Creature.SetMaxHpInternal(10000);p.Creature.SetCurrentHpInternal(10000);}
                if(withDead)players[^1].Creature.SetCurrentHpInternal(0);
                int living=withDead?n-1:n;
                var c=await CreatureCmd.Add<KnowledgeDemon>(battle.Combat,"prototype_knowledge");var demon=(KnowledgeDemon)c.Monster!;
                int[] decks=players.Select(p=>p.Deck.Cards.Count).ToArray();
                var selector=new Pick(pick);
                using(CardSelectCmd.UseSelector(selector))for(int round=0;round<3;round++)
                {
                    string method=AccessTools.Method(typeof(KnowledgeDemon),"CurseOfKnowledgeMove")!=null?"CurseOfKnowledgeMove":"CurseOfKnowledge";
                    await (Task)AccessTools.Method(typeof(KnowledgeDemon),method).Invoke(demon,[battle.Combat.PlayerCreatures])!;
                }
                NativeEventTests.Check(selector.Calls==3*living,"Three independent selections per living player");
                foreach(var p in players)
                {
                    NativeEventTests.Check(p.Deck.Cards.Count==decks[(int)p.NetId-1]&&p.PlayerCombatState!.AllCards.Where(card=>card.Pile!=null).All(card=>card is not PrototypeHuntingTenderChoice&&card is not PrototypeHuntingCalamityChoice&&card is not Disintegration&&card is not MindRot&&card is not Sloth&&card is not WasteAway),"Display cards do not enter persistent deck or combat piles");
                    if(pick==1&&p.Creature.IsAlive)NativeEventTests.Check(p.Creature.HasPower<CalamityPower>()&&p.Creature.HasPower<TenderPower>()&&p.Creature.GetPowerAmount<DisintegrationPower>()==21,"Adapter powers and21 native Disintegration");
                    foreach(var power in p.Creature.Powers.ToArray())await PowerCmd.Remove(power);
                }
                foreach(string move in new[]{"SLAP_MOVE","KNOWLEDGE_OVERWHELMING_MOVE"})
                {
                    var state=(MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine.MoveState)demon.MoveStateMachine.States[move];
                    int hp=players.Sum(p=>p.Creature.CurrentHp);await state.PerformMove(battle.Combat.PlayerCreatures);
                    int expected=(move=="SLAP_MOVE"?(asc==0?15:16):(asc==0?18:21))*living;
                    NativeEventTests.Check(hp-players.Sum(p=>p.Creature.CurrentHp)==expected,"Configured native attack damage matches rule");
                }
                c.SetCurrentHpInternal(c.MaxHp-50);
                await (Task)AccessTools.Method(typeof(KnowledgeDemon),"PonderMove").Invoke(demon,[battle.Combat.PlayerCreatures])!;
                NativeEventTests.Check(c.CurrentHp==c.MaxHp-30,"Ponder heals fixed20 independent of player count");
                Console.WriteLine($"KNOWLEDGE players={n} ascension={asc} option={pick} living={living} choices={selector.Calls} displayNotInPiles=true disintegration21={(pick==1)} attackRules=true heal20=true; dead-player skipped; UI art remains untested");
            }
        }
        finally{patch.UnpatchAll(patch.Id);}
    }
}
