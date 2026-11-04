// THROWAWAY: exercise native per-enemy entrypoints with explicit local rule adjustments.
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

internal static class PrototypeHuntingTableTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingTable native first second and third act entrypoints",Table)];
    private static readonly ThrowingPlayerChoiceContext Ctx=new();
    private static async Task Fixed<T>(Creature target,int amount) where T:PowerModel
    {
        var old=target.GetPower<T>();
        var power=old??await PowerCmd.Apply<T>(Ctx,target,amount,null,null);
        if(power!.Amount!=amount)await PowerCmd.ModifyAmount(Ctx,power,amount-power.Amount,null,null);
        NativeEventTests.Check(power.Amount==amount,"Fixed rule amount survives multiplayer");
    }
    private static async Task Scenario<T>(int n,Func<Creature,CombatState,Task> action) where T:MonsterModel
    {
        var run=HeadlessTestSetup.Run("HUNTING70_TABLE",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
        using var battle=new HeadlessTestSetup.EncounterCombatScope(run,ModelDb.Encounter<VantomBoss>().ToMutable());
        foreach(var p in run.Players){p.Creature.SetMaxHpInternal(10000);p.Creature.SetCurrentHpInternal(10000);}
        var model=ModelDb.Monster<T>().ToMutable();
        using var background=new MegaCrit.Sts2.Core.Nodes.Vfx.Backgrounds.NKaiserCrabBossBackground();
        if(typeof(T)==typeof(Crusher)||typeof(T)==typeof(Rocket))
        {
            AccessTools.Field(typeof(T),"_background")?.SetValue(model,background);
            Console.WriteLine($"TABLE_HOST {typeof(T).Name} receives non-rendered native background root; full background scene NOT exercised");
        }
        var target=await CreatureCmd.Add(model,battle.Combat,CombatSide.Enemy,typeof(T)==typeof(PhantasmalGardener)?"first":"prototype_table");
        await action(target,battle.Combat);
        background.Free();
        Console.WriteLine($"TABLE players={n} enemy={typeof(T).Name} native-entry-plus-local-adjustment=passed; not complete hunting integration/UI/checkpoint");
    }
    private static Task Invoke(MonsterModel m,string method,IReadOnlyList<Creature> targets)=>(Task)AccessTools.Method(m.GetType(),method).Invoke(m,[targets])!;
    private static async Task Table()
    {
        for(int n=1;n<=4;n++)
        {
            await Scenario<Byrdonis>(n,async(c,b)=>{await Fixed<RegenPower>(c,6);NativeEventTests.Check(c.HasPower<TerritorialPower>(),"Native Territorial retained");});
            await Scenario<BygoneEffigy>(n,async(c,b)=>
            {
                var m=c.Monster!;m.SetMoveImmediate((MoveState)m.MoveStateMachine.States["WAKE_MOVE"],true);
                await m.NextMove.PerformMove(b.PlayerCreatures);m.MoveStateMachine.OnMovePerformed(m.NextMove);m.RollMove(b.PlayerCreatures);
                NativeEventTests.Check(c.GetPowerAmount<StrengthPower>()==10&&m.NextMove.Id=="SLASHES_MOVE","Immediate native Wake then unchanged Slash");
            });
            await Scenario<CeremonialBeast>(n,async(c,b)=>
            {
                await Invoke(c.Monster!,"BeastCryMove",b.PlayerCreatures);
                await PowerCmd.Apply<WeakPower>(Ctx,b.PlayerCreatures,2,c,null);await PowerCmd.Apply<FrailPower>(Ctx,b.PlayerCreatures,2,c,null);
                NativeEventTests.Check(b.Players.All(p=>p.Creature.GetPowerAmount<RingingPower>()>0&&p.Creature.GetPowerAmount<WeakPower>()==2&&p.Creature.GetPowerAmount<FrailPower>()==2),"Cry retains ringing and adds all-player2/2");
            });
            await Scenario<KinPriest>(n,async(c,b)=>
            {
                var targets=b.PlayerCreatures.Take(1).ToArray();
                await Invoke(c.Monster!,"OrbOfFrailtyMove",targets);await PowerCmd.Apply<FrailPower>(Ctx,targets,1,c,null);
                await Invoke(c.Monster!,"OrbOfWeaknessMove",targets);await PowerCmd.Apply<WeakPower>(Ctx,targets,1,c,null);
                NativeEventTests.Check(targets[0].GetPowerAmount<FrailPower>()==2&&targets[0].GetPowerAmount<WeakPower>()==2&&b.PlayerCreatures.Skip(1).All(p=>!p.HasPower<FrailPower>()&&!p.HasPower<WeakPower>()),"Two layers preserve supplied original target subset");
            });
            await Scenario<PhantasmalGardener>(n,async(c,b)=>await Fixed<SkittishPower>(c,9));
            await Scenario<SkulkingColony>(n,async(c,b)=>{int raw=c.GetPowerAmount<HardenedShellPower>();await Fixed<HardenedShellPower>(c,20);await Fixed<PlatingPower>(c,6);NativeEventTests.Check(c.GetPowerAmount<HardenedShellPower>()==20,"Fixed shell20 retained alongside Plating6");Console.WriteLine($"TABLE_FIXED players={n} shellNative={raw} adjusted=20 plating=6");});
            await Scenario<TerrorEel>(n,async(c,b)=>
            {
                var targets=b.PlayerCreatures.Take(1).ToArray();await Invoke(c.Monster!,"TerrorMove",targets);await PowerCmd.Apply<WeakPower>(Ctx,targets,99,c,null);
                NativeEventTests.Check(targets[0].GetPowerAmount<VulnerablePower>()==99&&targets[0].GetPowerAmount<WeakPower>()==99&&b.PlayerCreatures.Skip(1).All(p=>!p.HasPower<WeakPower>()),"Same native targets retain99 vulnerable and add99 weak");
            });
            await Scenario<WaterfallGiant>(n,async(c,b)=>
            {
                await PowerCmd.Apply<StrengthPower>(Ctx,c,-3,null,null);await PowerCmd.Apply<DexterityPower>(Ctx,c,-2,null,null);await PowerCmd.Apply<WeakPower>(Ctx,c,2,null,null);await PowerCmd.Apply<PlatingPower>(Ctx,c,6,null,null);
                int positive=c.GetPowerAmount<PlatingPower>();
                var hidden=c.Powers.Where(p=>!p.IsVisible).ToArray();
                await ((WaterfallGiant)c.Monster!).TriggerAboutToBlowState();
                foreach(var p in c.Powers.Where(p=>p.IsVisible&&p.TypeForCurrentAmount==PowerType.Debuff).ToArray())await PowerCmd.Remove(p);
                await Fixed<ArtifactPower>(c,1);
                NativeEventTests.Check(!c.HasPower<StrengthPower>()&&!c.HasPower<DexterityPower>()&&!c.HasPower<WeakPower>()&&c.GetPowerAmount<PlatingPower>()==positive&&hidden.All(c.Powers.Contains),"Clear visible negatives and retain positive/hidden powers");
                NativeEventTests.Check(c.Monster!.NextMove.Id=="ABOUT_TO_BLOW_MOVE"&&c.MaxHp==999999999,"Native explosion phase state retained");
                await c.Monster.NextMove.PerformMove(b.PlayerCreatures);
            });
            await Scenario<Crusher>(n,async(c,b)=>await Fixed<ArtifactPower>(c,3));
            await Scenario<Rocket>(n,async(c,b)=>await Fixed<ArtifactPower>(c,3));
            await Scenario<MechaKnight>(n,async(c,b)=>
            {
                await Fixed<ArtifactPower>(c,5);var m=c.Monster!;
                m.SetMoveImmediate((MoveState)m.MoveStateMachine.States["FLAMETHROWER_MOVE"],true);
                int hp=b.Players.Sum(p=>p.Creature.CurrentHp);
                await m.NextMove.PerformMove(b.PlayerCreatures);
                int damage=hp-b.Players.Sum(p=>p.Creature.CurrentHp);
#if STS2_PUBLIC
                NativeEventTests.Check(damage==0,"Public native flamethrower has no direct damage");
#else
                NativeEventTests.Check(damage==8*n,"Beta native flamethrower retains8 base damage at A0");
#endif
                NativeEventTests.Check(b.Players.All(p=>PileType.Hand.GetPile(p).Cards.Count(x=>x is Burn)==4),"Native four Burns retained");
                Console.WriteLine($"TABLE_VERSION players={n} mechaNativeFlamethrowerDamage={damage} burnsPerPlayer=4 noCharge=true");
            });
            await Scenario<SoulNexus>(n,async(c,b)=>await Fixed<TerritorialPower>(c,1));
            await Scenario<FlailKnight>(n,async(c,b)=>
            {
                await Invoke(c.Monster!,"FlailMove",b.PlayerCreatures);await PowerCmd.Apply<TangledPower>(Ctx,b.PlayerCreatures,1,c,null);
                foreach(var p in b.Players)
                {
                    var card=b.CreateCard<StrikeIronclad>(p);await CardPileCmd.AddGeneratedCardToCombat(card,PileType.Hand,p);
                    NativeEventTests.Check(card.Affliction is MegaCrit.Sts2.Core.Models.Afflictions.Entangled,"Newly generated Attack inherits Entangled");
                    await p.Creature.GetPower<TangledPower>()!.AfterSideTurnEnd(Ctx,CombatSide.Player,b.PlayerCreatures);
                    NativeEventTests.Check(card.Affliction==null,"Native end clears attack affliction");
                }
            });
        }
    }
}
