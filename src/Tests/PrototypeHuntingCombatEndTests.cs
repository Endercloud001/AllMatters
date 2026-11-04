// Actual native death -> CheckWinCondition -> EndCombat -> hooks -> cleanup ->
// CombatWon reward generation. Persistence/progress/replay/achievement calls are
// replaced AT THIS CALLER, so native or Ritsu save postfixes cannot touch data.
// This is not a start/turn-loop, save/recovery, network or game UI test.
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

public sealed class PrototypeHuntingFinalFungusEncounter:EncounterModel
{
    public bool BossInput{get;set;}
    public override RoomType RoomType=>BossInput?RoomType.Boss:RoomType.Monster;
    public override bool IsWeak=>false;
    public override IReadOnlyList<string> Slots=>["fungus"];
    public override IEnumerable<MonsterModel> AllPossibleMonsters=>[ModelDb.Monster<PrototypeHuntingFungus>()];
    protected override IReadOnlyList<(MonsterModel,string?)> GenerateMonsters()=>[(ModelDb.Monster<PrototypeHuntingFungus>().ToMutable(),"fungus")];
}
public sealed class PrototypeHuntingEndObserver:RelicModel
{
    public override RelicRarity Rarity=>RelicRarity.Event;
    public int EndVuln{get;private set;}=-1;
    public int VictoryVuln{get;private set;}=-1;
    public int RewardVuln{get;private set;}=-1;
    public override Task AfterCombatEnd(CombatRoom room){EndVuln=Owner.Creature.GetPowerAmount<VulnerablePower>();return Task.CompletedTask;}
    public override Task AfterCombatVictory(CombatRoom room){VictoryVuln=Owner.Creature.GetPowerAmount<VulnerablePower>();return Task.CompletedTask;}
    public override bool TryModifyRewards(Player player,List<Reward> rewards,AbstractRoom? room){if(player==Owner)RewardVuln=player.Creature.GetPowerAmount<VulnerablePower>();return false;}
}
internal static class PrototypeHuntingCombatEndTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingCombatEnd native victory cleanup and reward ordering with IO isolated",End)];
    private static readonly List<string> Io=[];
    private static void NoReplay(RunManager manager,bool stopRecording)=>Io.Add("WriteReplay");
    private static Task NoSave(SaveManager manager,AbstractRoom room,bool saveProgress){Io.Add("SaveRun");return Task.CompletedTask;}
    private static void NoProgress(SaveManager manager,Player player,CombatRoom room)=>Io.Add("UpdateProgressAfterCombatWon");
    private static void NoProgressFile(SaveManager manager)=>Io.Add("SaveProgressFile");
    private static void NoAchievement(ActModel act,Player player)=>Io.Add("CheckForDefeatedAllEnemiesAchievement");
    private static void NoBossAchievement(Player player)=>Io.Add("AfterBossDefeated");
    private static IEnumerable<CodeInstruction> IsolateIo(IEnumerable<CodeInstruction> instructions)
    {
        var code=instructions.ToList();int replaced=0;
        foreach(var c in code)
        {
            if(c.operand is not MethodInfo m)continue;
            string? replacement=(m.DeclaringType?.Name,m.Name) switch
            {
                ("RunManager","WriteReplay")=>nameof(NoReplay),
                ("SaveManager","SaveRun")=>nameof(NoSave),
                ("SaveManager","UpdateProgressAfterCombatWon")=>nameof(NoProgress),
                ("SaveManager","SaveProgressFile")=>nameof(NoProgressFile),
                ("AchievementsHelper","CheckForDefeatedAllEnemiesAchievement")=>nameof(NoAchievement),
                ("AchievementsHelper","AfterBossDefeated")=>nameof(NoBossAchievement),_=>null
            };
            if(replacement==null)
            {
                if((m.DeclaringType==typeof(SaveManager)&&m.Name!="get_Instance")||m.DeclaringType?.Name=="AchievementsHelper")throw new InvalidOperationException("Unisolated data/achievement call: "+m);
                continue;
            }
            var stub=AccessTools.Method(typeof(PrototypeHuntingCombatEndTests),replacement);
            var parameters=(m.IsStatic?Array.Empty<Type>():new[]{m.DeclaringType!}).Concat(m.GetParameters().Select(p=>p.ParameterType));
            if(m.ReturnType!=stub.ReturnType||!parameters.SequenceEqual(stub.GetParameters().Select(p=>p.ParameterType)))throw new InvalidOperationException("Unsafe IO isolation signature: "+m);
            c.opcode=OpCodes.Call;c.operand=stub;replaced++;
        }
        // Data safety: do not invoke native EndCombat unless all six audited
        // persistence/progress/achievement roots were replaced before execution.
        // A normal assertion would fail only AFTER an accidental save. Git,
        // version labels and types do not isolate loaded native/Ritsu IO bodies.
        // This check protects the user's no-real-save boundary, not a release gate.
        if(replaced!=6)throw new InvalidOperationException("Incomplete EndCombat IO isolation: "+replaced);
        return code;
    }
    private static async Task End()
    {
        PrototypeHuntingModelTests.Inject();PrototypeHuntingMaskTests.Inject();ModelDb.Inject(typeof(PrototypeHuntingFinalFungusEncounter));ModelDb.Inject(typeof(PrototypeHuntingEndObserver));
        var patch=new Harmony("allmatters.prototype70.native-end-io-only");
        var end=typeof(CombatManager).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>m.Name=="EndCombatInternal").OrderByDescending(m=>m.GetParameters().Length).First();
        var machine=end.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
        patch.Patch(AccessTools.Method(machine,"MoveNext"),transpiler:new HarmonyMethod(typeof(PrototypeHuntingCombatEndTests),nameof(IsolateIo)));
        try
        {
            foreach(string scenario in new[]{"monster","single-final","dual-first","dual-final","event-at-final-coordinate"})for(int n=1;n<=4;n++)
            {
                bool boss=scenario!="monster",dual=scenario.StartsWith("dual"),eventBoss=scenario.StartsWith("event");
                bool keepMask=!boss||scenario=="dual-first"||eventBoss;
                Io.Clear();var players=Enumerable.Range(1,n).Select(i=>Player.CreateForNewRun(ModelDb.Character<Ironclad>(),UnlockState.all,(ulong)i)).ToArray();
                var run=RunState.CreateForTest(players,acts:[ModelDb.Act<Overgrowth>(),ModelDb.Act<Hive>(),ModelDb.Act<Glory>()],seed:"HUNTING70_END_REAL");
                run.CurrentActIndex=boss?2:0;
                var previous=MegaCrit.Sts2.Core.Context.LocalContext.NetId;
                RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());MegaCrit.Sts2.Core.Context.LocalContext.NetId=1;
                var encounter=(PrototypeHuntingFinalFungusEncounter)ModelDb.Encounter<PrototypeHuntingFinalFungusEncounter>().ToMutable();encounter.BossInput=boss;
                var room=new CombatRoom(encounter,run){ParentEventId=eventBoss?ModelDb.AllEvents.First().Id:null};run.PushRoom(room);
                run.Map=new PrototypeHuntingBossMap(dual);run.AddVisitedMapCoord(scenario=="dual-final"?run.Map.SecondBossMapPoint!.coord:boss?run.Map.BossMapPoint.coord:run.Map.StartingMapPoint.coord);run.AppendToMapPointHistory(boss?MapPointType.Boss:MapPointType.Monster,room.RoomType,encounter.Id);
                var combat=room.CombatState;foreach(var p in players){combat.AddPlayer(p);p.AddRelicInternal((PrototypeHuntingEndObserver)ModelDb.Relic<PrototypeHuntingEndObserver>().ToMutable());p.AddRelicInternal((PrototypeHuntingMask)ModelDb.Relic<PrototypeHuntingMask>().ToMutable());p.AddRelicInternal((PrototypeHuntingRewardObserver)ModelDb.Relic<PrototypeHuntingRewardObserver>().ToMutable());}
                Task? rewardTask=null;int won=0,ended=0;bool maskAtWon=false;
                void OnWon(CombatRoom value){won++;maskAtWon=players.All(p=>p.Relics.OfType<PrototypeHuntingMask>().Any()==keepMask);rewardTask=Generate();async Task Generate(){foreach(var p in players)await RewardsCmd.GenerateForRoomEnd(p,value);}}
                void OnEnded(CombatRoom value)=>ended++;
                CombatManager.Instance.CombatWon+=OnWon;CombatManager.Instance.CombatEnded+=OnEnded;
                try
                {
                    CombatManager.Instance.Reset(true);CombatManager.Instance.SetUpCombat(combat);encounter.GenerateMonstersWithSlots(run);
                    foreach(var (m,slot) in encounter.MonstersWithSlots){var c=combat.CreateCreature(m,CombatSide.Enemy,slot);combat.AddCreature(c);CombatManager.Instance.AddCreature(c);await c.AfterAddedToRoom();}
                    using(HeadlessCombatInProgress.EnterNativeLifecycle())
                    {
                        PrototypeHuntingNativeEndingTests.SupplyActiveInput();
                        NativeEventTests.Check(!await CombatManager.Instance.CheckWinCondition(),"Real native win check refuses victory while the ordinary primary remains alive");
                        await CreatureCmd.Kill(combat.Enemies.Single());
                        NativeEventTests.Check(players.All(p=>p.Creature.GetPowerAmount<VulnerablePower>()==2)&&CombatManager.Instance.IsEnding,"Real last-death settlement precedes native win check");
                        bool finished=await CombatManager.Instance.CheckWinCondition();if(rewardTask!=null)await rewardTask;
                        var observers=players.Select(p=>p.Relics.OfType<PrototypeHuntingEndObserver>().Single()).ToArray();
                        Console.WriteLine($"COMBAT_END players={n} scenario={scenario} inputEncounter=prototype-fungus nativeWinCheck={finished} nativeProgress={CombatManager.Instance.IsInProgress} roomPreFinished={room.IsPreFinished} combatWon={won} combatEnded={ended} afterEndVuln={string.Join(',',observers.Select(o=>o.EndVuln))} afterVictoryVuln={string.Join(',',observers.Select(o=>o.VictoryVuln))} rewardVuln={string.Join(',',observers.Select(o=>o.RewardVuln))} expectedMaskRetained={keepMask} actualMaskAtRewards={string.Join(',',players.Select(p=>p.Relics.OfType<PrototypeHuntingRewardObserver>().Single().MaskAtRewards))} isolatedIo={string.Join(',',Io)}; actual native ending and mask/reward hooks, supplied active/Boss/event inputs over prototype encounter, no original Boss full fight/startup/turn-loop/game/network/save/progress/achievement/replay proof");
                        NativeEventTests.Check(finished&&!CombatManager.Instance.IsInProgress&&room.IsPreFinished&&won==1&&ended==1&&observers.All(o=>o.EndVuln==2&&o.VictoryVuln==0&&o.RewardVuln==0),"Actual native end settles spores then cleans player powers before victory/reward events");
                        NativeEventTests.Check(Io.Contains("SaveRun")&&Io.Contains("SaveProgressFile")&&Io.Contains("WriteReplay"),"Native ending reached IO boundaries but never entered actual IO APIs or postfixes");
                        NativeEventTests.Check(maskAtWon&&players.All(p=>p.Relics.OfType<PrototypeHuntingRewardObserver>().Single().MaskAtRewards==keepMask),"Native actual victory callbacks remove final mask before CombatWon/reward generation, retain first Boss/event/ordinary contexts");
                    }
                }
                finally{CombatManager.Instance.CombatWon-=OnWon;CombatManager.Instance.CombatEnded-=OnEnded;CombatManager.Instance.Reset(true);RunManager.Instance.CleanUp(false);MegaCrit.Sts2.Core.Context.LocalContext.NetId=previous;}
            }
        }
        finally{patch.UnpatchAll(patch.Id);Io.Clear();}
    }
}
