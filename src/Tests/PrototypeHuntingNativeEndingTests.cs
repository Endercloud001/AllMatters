// Real native IsInProgress/IsEnding getters; no full start/turn loop and no
// CheckWinCondition/EndCombat call yet, hence no persistence or progress I/O.
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;

internal static class PrototypeHuntingNativeEndingTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingNativeEnding final spore with actual ending getter",LastSpore),("PrototypeHuntingNativeEnding actual prevention and Artifact settlement",PreventedAndArtifact)];
    internal static void SupplyActiveInput()
    {
        var state=AccessTools.Field(typeof(CombatManager),"_turnState")?.GetValue(CombatManager.Instance);
        if(state!=null)AccessTools.Property(state.GetType(),"IsInProgress").SetValue(state,true);
        else AccessTools.Property(typeof(CombatManager),nameof(CombatManager.IsInProgress)).GetSetMethod(true)!.Invoke(CombatManager.Instance,[true]);
    }
    private static async Task PreventedAndArtifact()
    {
        PrototypeHuntingModelTests.Inject();
        for(int n=1;n<=4;n++)
        {
            var run=HeadlessTestSetup.Run("HUNTING70_NATIVE_DEATH_SHIELD",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
            using var battle=new HeadlessTestSetup.EncounterCombatScope(run,ModelDb.Encounter<VantomBoss>().ToMutable());
            var fungus=await CreatureCmd.Add<PrototypeHuntingFungus>(battle.Combat,"prototype_shield_last");await CreatureCmd.Kill(battle.Monster.Creature);
            await PowerCmd.Apply<PrototypeHuntingDeathShield>(new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),fungus,1,null,null);
            using(HeadlessCombatInProgress.EnterNativeLifecycle())
            {
                SupplyActiveInput();await CreatureCmd.Kill(fungus);
                NativeEventTests.Check(fungus.IsAlive&&fungus.CurrentHp==1&&!CombatManager.Instance.IsEnding&&run.Players.All(p=>p.Creature.GetPowerAmount<VulnerablePower>()==0),"Actual native prevented death restores one HP, keeps combat active, and gives no spores");
                var first=run.Players[0];await PowerCmd.Apply<ArtifactPower>(new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),first.Creature,1,null,null);
                NativeEventTests.Check(first.Creature.GetPowerAmount<ArtifactPower>()==1,"Exact one player Artifact is the native immunity fixture");
                await CreatureCmd.Kill(fungus);
                NativeEventTests.Check(first.Creature.GetPowerAmount<ArtifactPower>()==0&&first.Creature.GetPowerAmount<VulnerablePower>()==0&&run.Players.Skip(1).All(p=>p.Creature.GetPowerAmount<VulnerablePower>()==2)&&CombatManager.Instance.IsEnding,"Actual final spores consume first player's Artifact and settle2 for other players before ending");
                Console.WriteLine($"NATIVE_DEATH players={n} actualNativeGetters=true preventedDeathRestoredHp=1 preventedSpores=0 finalDeath=true firstPlayerArtifactConsumed=1 firstPlayerVuln=0 otherPlayersVuln=2 endingReleased=true; no full start/turn-loop/save/game claim");
            }
        }
    }
    private static async Task LastSpore()
    {
        PrototypeHuntingModelTests.Inject();
        foreach(bool hold in new[]{false,true})for(int n=1;n<=4;n++)
        {
            var run=HeadlessTestSetup.Run("HUNTING70_NATIVE_ENDING",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
            using var battle=new HeadlessTestSetup.EncounterCombatScope(run,ModelDb.Encounter<VantomBoss>().ToMutable());
            var fungus=await CreatureCmd.Add<PrototypeHuntingFungus>(battle.Combat,"prototype_actual_last");
            await CreatureCmd.Kill(battle.Monster.Creature);var spores=fungus.GetPower<PrototypeHuntingSpores>()!;spores.HoldNativeEnding=hold;
            using(HeadlessCombatInProgress.EnterNativeLifecycle())
            {
                SupplyActiveInput();
                NativeEventTests.Check(CombatManager.Instance.IsInProgress&&!CombatManager.Instance.IsEnding,"Supplied active native state begins with one living primary");
                await CreatureCmd.Kill(fungus);
                Console.WriteLine($"NATIVE_ENDING players={n} holdNativeEnding={hold} negativeControl={!hold} actualNativeGetters=true endingDuringSpore={spores.NativeEndingAtDeath} vulnerability={string.Join(',',run.Players.Select(p=>p.Creature.GetPowerAmount<VulnerablePower>()))} endingAfterKill={CombatManager.Instance.IsEnding}; controlled active state input, native death/power APIs, no EndCombat/save/game claim");
                NativeEventTests.Check(run.Players.All(p=>p.Creature.GetPowerAmount<VulnerablePower>()==(hold?2:0)),"Native ending rejects last-death powers without hold; default hold settles exact2");
                NativeEventTests.Check(CombatManager.Instance.IsEnding,"After native removal the temporary hold no longer blocks victory detection");
            }
        }
    }
}
