using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

internal static class PrototypeHuntingFatalTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingFatal actual Feed HandOfGreed and GremlinHorn normal vs Minion",Fatal)];
    private static async Task Fatal()
    {
        for(int n=1;n<=4;n++)foreach(bool minion in new[]{false,true})foreach(bool greed in new[]{false,true})
        {
            var run=HeadlessTestSetup.Run("HUNTING70_FATAL",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
            using var battle=new HeadlessTestSetup.EncounterCombatScope(run,ModelDb.Encounter<VantomBoss>().ToMutable());
            var combat=battle.Combat;var player=run.Players[0];var ctx=new ThrowingPlayerChoiceContext();
            await RelicCmd.Obtain<GremlinHorn>(player);
            player.PlayerCombatState!.Energy=0;
            for(int i=0;i<3;i++)await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<DefendIronclad>(player),PileType.Draw,player);
            var target=await CreatureCmd.Add<Seapunk>(combat,"prototype_fatal");
            if(minion)await PowerCmd.Apply<MinionPower>(ctx,target,1,null,null);
            target.SetCurrentHpInternal(1);
            int max=player.Creature.MaxHp,gold=player.Gold;
            CardModel card=greed?combat.CreateCard<HandOfGreed>(player):combat.CreateCard<Feed>(player);
            await CardPileCmd.AddGeneratedCardToCombat(card,PileType.Discard,player);
            await CardCmd.AutoPlay(ctx,card,target,skipCardPileVisuals:true);
            NativeEventTests.Check(target.IsDead,"Actual attack kills added species");
            NativeEventTests.Check(player.Creature.MaxHp-max==(!greed&&!minion?3:0),"Feed Fatal excludes Minion");
            NativeEventTests.Check(player.Gold-gold==(greed&&!minion?20:0),"HandOfGreed Fatal excludes Minion");
            NativeEventTests.Check(player.PlayerCombatState.Energy==1&&PileType.Hand.GetPile(player).Cards.Count==1,"Native GremlinHorn still reacts to either actual death");
            Console.WriteLine($"FATAL players={n} minion={minion} card={(greed?"HandOfGreed":"Feed")} hpGain={player.Creature.MaxHp-max} goldGain={player.Gold-gold} GremlinHornEnergy=1 draw=1 actualKill=true");
        }
    }
}
