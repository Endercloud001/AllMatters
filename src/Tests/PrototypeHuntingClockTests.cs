// THROWAWAY Aeonglass instance counter adapter; native card play, overflow and upgrade hooks.
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;

internal static class PrototypeHuntingClockTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingClock per player twelve actual plays overflow and Wither chain",Clock)];
    private static bool Twelve(WitheringPresencePower __instance,CardPlay cardPlay,ref Task __result)
    {
        __result=Count();return false;
        async Task Count()
        {
            if(cardPlay.Card.Owner!=__instance.Target.Player)return;
            var left=__instance.DynamicVars["CardsLeft"];left.BaseValue--;
            AccessTools.Method(typeof(PowerModel),"InvokeDisplayAmountChanged").Invoke(__instance,null);
            if(left.IntValue>0)return;
            var target=cardPlay.Card.Owner.Creature;
            await CardPileCmd.AddToCombatAndPreview<Wound>(target,PileType.Hand,1,null);
            await CardPileCmd.AddToCombatAndPreview<Burn>(target,PileType.Hand,1,null);
            await CardPileCmd.AddToCombatAndPreview<MegaCrit.Sts2.Core.Models.Cards.Void>(target,PileType.Hand,1,null);
            await CardPileCmd.AddToCombatAndPreview<Dazed>(target,PileType.Hand,1,null);
            await CardPileCmd.AddToCombatAndPreview<Wither>(target,PileType.Hand,1,null);
            left.BaseValue=12;
            AccessTools.Method(typeof(PowerModel),"InvokeDisplayAmountChanged").Invoke(__instance,null);
        }
    }
    private static async Task Clock()
    {
        var patch=new Harmony("allmatters.prototype70.clock");
        patch.Patch(AccessTools.Method(typeof(WitheringPresencePower),nameof(WitheringPresencePower.AfterCardPlayed)),prefix:new HarmonyMethod(typeof(PrototypeHuntingClockTests),nameof(Twelve)));
        try
        {
            for(int n=1;n<=4;n++)foreach(bool overflow in new[]{false,true})
            {
                var run=HeadlessTestSetup.Run("HUNTING70_CLOCK",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
                using var battle=new HeadlessTestSetup.EncounterCombatScope(run,ModelDb.Encounter<AeonglassBoss>().ToMutable());
                var boss=(Aeonglass)battle.Monster;var combat=battle.Combat;var ctx=new ThrowingPlayerChoiceContext();
                await boss.AfterAddedToRoom();await PowerCmd.Remove(boss.Creature.GetPower<ArtifactPower>());
                var powers=boss.Creature.Powers.OfType<WitheringPresencePower>().ToArray();
                NativeEventTests.Check(powers.Length==n,"One native instance per player");
                foreach(var power in powers)power.DynamicVars["CardsLeft"].BaseValue=12;
                int hp=run.Players.Sum(p=>p.Creature.CurrentHp);
                await (Task)AccessTools.Method(typeof(Aeonglass),"EbbMove").Invoke(boss,[combat.PlayerCreatures])!;
                int nativeDamage=hp-run.Players.Sum(p=>p.Creature.CurrentHp);
#if STS2_PUBLIC
                NativeEventTests.Check(nativeDamage==26*n,"Public Ebb26 preserved");
#else
                NativeEventTests.Check(nativeDamage==22*n,"Beta Ebb22 preserved");
#endif
                boss.WitherUpgradeCount=2;
                foreach(var player in run.Players)
                {
                    var own=powers.Single(p=>p.Target.Player==player);
                    if(overflow)for(int i=0;i<9;i++)await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<DefendIronclad>(player),PileType.Hand,player);
                    var otherBefore=powers.Where(p=>p!=own).Select(p=>p.DisplayAmount).ToArray();
                    for(int played=1;played<=12;played++)
                    {
                        var card=combat.CreateCard<DefendIronclad>(player);
                        await CardPileCmd.AddGeneratedCardToCombat(card,PileType.Discard,player);
                        await CardCmd.AutoPlay(ctx,card,null,skipCardPileVisuals:true);
                        NativeEventTests.Check(own.DisplayAmount==(played==12?12:12-played),"Native display follows per-player12 reset");
                    }
                    NativeEventTests.Check(otherBefore.SequenceEqual(powers.Where(p=>p!=own).Select(p=>p.DisplayAmount)),"Other players do not consume this counter");
                    var hand=PileType.Hand.GetPile(player).Cards;
                    var disc=PileType.Discard.GetPile(player).Cards;
                    var wither=hand.Concat(disc).OfType<Wither>().Single();
                    NativeEventTests.Check(wither.DynamicVars.Damage.IntValue==9,"Native Wither generated hook retains two-upgrade chain");
                    if(overflow)NativeEventTests.Check(hand.Count==10&&hand.Last() is Wound&&disc.Any(c=>c is Burn)&&disc.Any(c=>c is Dazed)&&disc.Contains(wither),"Five status insertion order preserves native full-hand overflow");
                    else NativeEventTests.Check(hand.Count==5&&hand[0] is Wound&&hand[1] is Burn&&hand[2] is MegaCrit.Sts2.Core.Models.Cards.Void&&hand[3] is Dazed&&hand[4] is Wither,"Exactly ordered five statuses at count12");
                    Console.WriteLine($"CLOCK players={n} player={player.NetId} actualPlays=12 initial/display/reset=12 isolated=true overflow={overflow} WitherDamage=9 nativeEbbTotal={nativeDamage}; serialization/UI not tested");
                }
            }
        }
        finally{patch.UnpatchAll(patch.Id);}
    }
}
