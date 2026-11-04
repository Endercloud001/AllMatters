// Exhaustive native encounter factory/add endpoint coverage, NOT full battle coverage.
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class PrototypeHuntingCoverageTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=
    [("PrototypeHuntingCoverage all native encounter overgrowth addition",()=>AddEverywhere(0)),("PrototypeHuntingCoverage all native encounter underdocks addition",()=>AddEverywhere(0,true)),("PrototypeHuntingCoverage all native encounter hive debuffs",()=>AddEverywhere(1)),("PrototypeHuntingCoverage all native encounter glory addition",()=>AddEverywhere(2))];
    private static async Task AddEverywhere(int act,bool harbor=false)
    {
        PrototypeHuntingModelTests.Inject();
        var factory=typeof(ModelDb).GetMethods().Single(m=>m.Name=="Encounter"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==0);
        var types=typeof(MonsterModel).Assembly.GetTypes().Where(t=>!t.IsAbstract&&typeof(EncounterModel).IsAssignableFrom(t)&&!t.Name.StartsWith("Mock")).OrderBy(t=>t.FullName).ToArray();
        var failures=new List<string>();int count=0;
        foreach(int n in new[]{1,2,3,4})foreach(var type in types)foreach(int variant in type.Name=="BattlewornDummyEventEncounter"?new[]{1,2,3}:new[]{0})
        {
            var previous=MegaCrit.Sts2.Core.Context.LocalContext.NetId;
            try
            {
                var players=Enumerable.Range(1,n).Select(i=>MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(ModelDb.Character<Ironclad>(),UnlockState.all,(ulong)i)).ToArray();
                var run=RunState.CreateForTest(players,acts:[harbor?ModelDb.Act<Underdocks>():ModelDb.Act<Overgrowth>(),ModelDb.Act<Hive>(),ModelDb.Act<Glory>()],seed:"HUNTING70_ALL_SCENES");run.CurrentActIndex=act;
                var encounter=((EncounterModel)factory.MakeGenericMethod(type).Invoke(null,null)!).ToMutable();
                if(variant>0){var setting=type.GetProperty("Setting")!;setting.SetValue(encounter,Enum.ToObject(setting.PropertyType,variant));}
                encounter.GenerateMonstersWithSlots(run);
                var combat=new CombatState(encounter,run){RoundNumber=1};
                foreach(var p in players){combat.AddPlayer(p);p.ResetCombatState();}
                foreach(var (m,slot) in encounter.MonstersWithSlots)combat.AddCreature(combat.CreateCreature(m,CombatSide.Enemy,slot));
                var originals=combat.Enemies.ToArray();
                RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());MegaCrit.Sts2.Core.Context.LocalContext.NetId=1;
                CombatManager.Instance.Reset(true);CombatManager.Instance.SetUpCombat(combat);
                MonsterModel? model=act switch{0=>harbor?ModelDb.Monster<Seapunk>().ToMutable():ModelDb.Monster<PrototypeHuntingFungus>().ToMutable(),1=>null,_=>ModelDb.Monster<PrototypeHuntingMystic>().ToMutable()};
                string? slotName=encounter.HasScene?"prototype_hunting_append":null;
                Creature? added=null;
                if(model!=null)
                {
                    added=await CreatureCmd.Add(model,combat,CombatSide.Enemy,slotName);
                    if(model is PrototypeHuntingFungus or PrototypeHuntingMystic)
                        NativeEventTests.Check(added.MaxHp==(int)Creature.ScaleHpForMultiplayer(model.MinInitialHp,encounter,n,act),"Fixed model base HP follows exactly one native encounter/act/player scaling");
                }
                else
                {
                    var ctx=new ThrowingPlayerChoiceContext();
                    await PowerCmd.Apply<WeakPower>(ctx,combat.PlayerCreatures,1,null,null);
                    await PowerCmd.Apply<FrailPower>(ctx,combat.PlayerCreatures,1,null,null);
                    await PowerCmd.Apply<VulnerablePower>(ctx,combat.PlayerCreatures,1,null,null);
                    NativeEventTests.Check(players.All(p=>p.Creature.GetPowerAmount<WeakPower>()==1&&p.Creature.GetPowerAmount<FrailPower>()==1&&p.Creature.GetPowerAmount<VulnerablePower>()==1),"Hive ordinary/event context gets only1/1/1 opening debuffs");
                }
                NativeEventTests.Check(combat.Enemies.Count==originals.Length+(added==null?0:1)&&originals.All(combat.Enemies.Contains)&&(added==null||added.IsPrimaryEnemy),"Native endpoint preserves originals and only the specified addition");
                int rank=added==null?-1:combat.Enemies.ToList().IndexOf(added);
                Console.WriteLine($"COVERAGE act={act+1} harbor={harbor} players={n} encounter={type.Name} variant={variant} roomType={encounter.RoomType} original={originals.Length} after={combat.Enemies.Count} added={model?.GetType().Name??"none-Hive-1/1/1"} hp={added?.MaxHp} nativeOrderIndex={rank} dedicatedScene={encounter.HasScene} missingAppendMarker={added!=null&&encounter.HasScene}; ordinary/event context supplied, originals not fully initialized, no resource/UI/full-combat proof");
                count++;
            }
            catch(Exception ex)
            {
                failures.Add($"act={act+1} players={n} encounter={type.Name}: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine("COVERAGE_FAILURE "+failures[^1]);
            }
            finally{CombatManager.Instance.Reset(true);RunManager.Instance.CleanUp(false);MegaCrit.Sts2.Core.Context.LocalContext.NetId=previous;}
        }
        Console.WriteLine($"COVERAGE_SUMMARY act={act+1} harbor={harbor} passed={count} failed={failures.Count} encounterTypes={types.Length} playerCounts=1,2,3,4; endpoint-level only");
        NativeEventTests.Check(failures.Count==0,string.Join(Environment.NewLine,failures));
    }
}
