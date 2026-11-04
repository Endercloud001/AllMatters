// THROWAWAY experiment: scoped runtime changes, never production patches.
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

internal static class PrototypeHuntingSpawnTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("PrototypeHuntingSpawn actual phrog death five spawns and first actions", FiveWorms),
     ("PrototypeHuntingSpawn Inklets Lagavulin and SoulFysh companions native addition", Companions),
     ("PrototypeHuntingSpawn actual native visual resources and five marker prototype", Visuals),
     ("PrototypeHuntingSpawn five-loop change only applies to Phrog owner",OtherOwner),
     ("PrototypeHuntingSpawn separately built live DLL native death and scene",LiveEntry)];
    private static async Task LiveEntry()
    {
#if STS2_PUBLIC
        const string target="public";
#else
        const string target="public-beta";
#endif
        string pack=Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="ModResourcePack").Value!;
        string path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(pack)!,target,"AllMattersHuntingFiveWormProbe","AllMattersHuntingFiveWormProbe.dll"));
        var assembly=System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(TestRunner).Assembly)!.LoadFromAssemblyPath(path);
        const string id="AllMattersHuntingFiveWormProbe";
        try
        {
            assembly.GetType("PrototypeHuntingFiveWormMod",true)!.GetMethod("Initialize")!.Invoke(null,null);
            for(int n=1;n<=4;n++)
            {
                var run=HeadlessTestSetup.Run("HUNTING70_BUILT_FIVE",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
                using var battle=new HeadlessTestSetup.EncounterCombatScope(run,ModelDb.Encounter<PhrogParasiteElite>().ToMutable());
                await battle.Monster.AfterAddedToRoom();
                using(var scene=battle.Combat.Encounter!.CreateScene())
                {
                    NativeEventTests.Check(scene.HasNode("wriggler5")&&scene.GetNode<Godot.Marker2D>("wriggler5").Position.X==1620,"Built DLL modifies actual native scene at runtime");scene.Free();
                }
                await CreatureCmd.Kill(battle.Monster.Creature);
                NativeEventTests.Check(battle.Combat.Enemies.Count(c=>c.Monster is Wriggler)==5&&battle.Combat.Enemies.Where(c=>c.Monster is Wriggler).Select(c=>c.SlotName).SequenceEqual(Enumerable.Range(1,5).Select(i=>"wriggler"+i)),"Built DLL native death/slot order equals shared probe");
                foreach(var worm in battle.Combat.Enemies.Where(c=>c.Monster is Wriggler))
                {
                    var model=worm.Monster!;if(model.NextMove.Id!="SPAWNED_MOVE")model.RollMove(battle.Combat.PlayerCreatures);
                    await model.NextMove.PerformMove(battle.Combat.PlayerCreatures);model.MoveStateMachine.OnMovePerformed(model.NextMove);model.RollMove(battle.Combat.PlayerCreatures);
                }
                NativeEventTests.Check(battle.Combat.Enemies.Single(c=>c.SlotName=="wriggler5").Monster!.NextMove.Id=="NASTY_BITE_MOVE","Built DLL fifth branch runs after actual spawn empty action");
                Console.WriteLine($"LIVE_FIVE players={n} target={target} actualBuiltInitializer=true nativeDeathCount=5 nativeSceneMarker5=true nativeOrder=1/2/3/4/5 fifthFirstAction=NASTY_BITE_MOVE; actual game rendering/music/full turn loop/user acceptance untested");
            }
            string encounter=ModelDb.Encounter<PhrogParasiteElite>().Id.Entry;
            var lookup=ModelDb.GetById<EncounterModel>(new ModelId(ModelId.SlugifyCategory<EncounterModel>(),encounter));
            NativeEventTests.Check(lookup is PhrogParasiteElite,"Exact ID resolves through the same native lookup used by fight command");
            Console.WriteLine($"LIVE_FIVE_COMMAND target={target} actualNativeModelLookup=true manualCommand=fight {encounter}; command execution and room jump not executed by Agent");
        }
        finally{new Harmony(id).UnpatchAll(id);}
    }
    private static async Task OtherOwner()
    {
        var patch=new Harmony("allmatters.prototype70.five-owner-scope");PrototypeHuntingFiveWormProbe.Install(patch);
        try
        {
            for(int n=1;n<=4;n++)
            {
                var run=HeadlessTestSetup.Run("HUNTING70_NON_PHROG",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
                using var battle=new HeadlessTestSetup.EncounterCombatScope(run,ModelDb.Encounter<VantomBoss>().ToMutable());
                var owner=await CreatureCmd.Add<Seapunk>(battle.Combat,"prototype_scope_owner");
                await PowerCmd.Apply<InfestedPower>(new ThrowingPlayerChoiceContext(),owner,1,null,null);
                await CreatureCmd.Kill(owner);
                NativeEventTests.Check(battle.Combat.Enemies.Count(c=>c.Monster is Wriggler)==4&&!battle.Combat.Enemies.Any(c=>c.SlotName=="wriggler5"),"Non-Phrog native Infested retains original four-worm bound");
                Console.WriteLine($"SPAWN_SCOPE players={n} suppliedNonPhrogInfestedOwner=Seapunk actualNativeSpawn=4 fifth=false; owner guard of live probe, not a new gameplay rule");
            }
        }
        finally{patch.UnpatchAll(patch.Id);}
    }

    private static async Task Companions()
    {
        for (int n = 1; n <= 4; n++)
        {
            foreach (var encounter in new EncounterModel[] { ModelDb.Encounter<VantomBoss>().ToMutable(), ModelDb.Encounter<LagavulinMatriarchBoss>().ToMutable(), ModelDb.Encounter<SoulFyshBoss>().ToMutable() })
            {
                var run = HeadlessTestSetup.Run("HUNTING70_COMPANIONS", Enumerable.Repeat(ModelDb.Character<Ironclad>(), n).ToArray());
                using var battle = new HeadlessTestSetup.EncounterCombatScope(run, encounter);
                using var native=HeadlessCombatInProgress.EnterNativeLifecycle();PrototypeHuntingNativeEndingTests.SupplyActiveInput();
                var combat = battle.Combat;
                foreach (var p in run.Players) { p.Creature.SetMaxHpInternal(1000); p.Creature.SetCurrentHpInternal(1000); }
                await battle.Monster.AfterAddedToRoom();
                if (battle.Monster is Vantom)
                {
                    foreach (string slot in new[] { "prototype_left", "prototype_right" })
                    {
                        var ink = await CreatureCmd.Add<Inklet>(combat, slot);
                        NativeEventTests.Check(ink.IsPrimaryEnemy && ink.GetPowerAmount<SlipperyPower>() > 0 && ink.Monster!.NextMove.Id == "JAB_MOVE", "Inklet native capability and opening preserved without Minion/stun");
                        int hp = run.Players.Sum(p => p.Creature.CurrentHp);
                        await ink.Monster.NextMove.PerformMove(combat.PlayerCreatures);
                        NativeEventTests.Check(run.Players.Sum(p => p.Creature.CurrentHp) < hp, "Inklet really attacks immediately");
                        Console.WriteLine($"COMPANION players={n} nativeGetters=true type=Inklet slot={slot} hp={ink.MaxHp} slippery={ink.GetPowerAmount<SlipperyPower>()} first={ink.Monster.NextMove.Id} primary={ink.IsPrimaryEnemy}");
                    }
                    await CreatureCmd.Kill(battle.Monster.Creature);
                    NativeEventTests.Check(combat.Enemies.Count(c=>c.Monster is Inklet&&c.IsAlive)==2&&!await CombatManager.Instance.CheckWinCondition(),"Ordinary additional Inklets must survive main enemy death and block victory");
                }
                else if (battle.Monster is LagavulinMatriarch matriarch)
                {
                    int before = matriarch.Creature.MaxHp;
                    await CreatureCmd.SetMaxAndCurrentHp(matriarch.Creature, before / 2);
                    var lag = await CreatureCmd.Add<AllMatters.Monsters.Lagavulin>(combat, "prototype_left");
                    await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(), lag, 1, null, null);
                    NativeEventTests.Check(lag.IsSecondaryEnemy && !((AllMatters.Monsters.Lagavulin)lag.Monster!).IsAwake && lag.Monster.NextMove.Id == "SLEEP", "Mod Lagavulin sleeps as Minion");
                    NativeEventTests.Check(!matriarch.IsAwake && !matriarch.IsShellAwake && matriarch.Creature.CurrentHp == before / 2, "Matriarch remains full/sleeping after companion addition");
                    await CreatureCmd.Kill(matriarch.Creature);
                    NativeEventTests.Check(lag.IsDead, "Actual native last-primary kill also kills designated Minion");
                    Console.WriteLine($"COMPANION players={n} nativeGetters=true type=Lagavulin hp={lag.MaxHp} sleepingOnEntry=true minion=true diesWithPrimary=true");
                }
                else
                {
                    var left = await CreatureCmd.Add<Toadpole>(combat, "prototype_left");
                    var right = await CreatureCmd.Add<SludgeSpinner>(combat, "prototype_right");
                    foreach (var companion in new[] { left, right })
                        await PowerCmd.Apply<MinionPower>(new ThrowingPlayerChoiceContext(), companion, 1, null, null);
                    NativeEventTests.Check(left.Monster!.NextMove.Id == "WHIRL_MOVE" && right.Monster!.NextMove.Id == "OIL_SPRAY_MOVE", "Native companion first intents are preserved");
                    NativeEventTests.Check(left.IsSecondaryEnemy && right.IsSecondaryEnemy, "Both explicit companions are Minions");
                    await CreatureCmd.Kill(battle.Monster.Creature);
                    NativeEventTests.Check(left.IsDead && right.IsDead, "Both die on last-primary death");
                    Console.WriteLine($"COMPANION players={n} nativeGetters=true types=Toadpole/SludgeSpinner hp={left.MaxHp}/{right.MaxHp} first=WHIRL_MOVE/OIL_SPRAY_MOVE minions=true dieWithPrimary=true");
                }
            }
        }
    }

    private static async Task Visuals()
    {
        STS2RitsuLib.RitsuLibFramework.EnsureGodotScriptsRegistered(typeof(MonsterModel).Assembly,STS2RitsuLib.RitsuLibFramework.CreateLogger("Prototype70NativeVisuals"));
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
        foreach (MonsterModel model in new MonsterModel[] { ModelDb.Monster<Wriggler>(), ModelDb.Monster<Inklet>(), ModelDb.Monster<AllMatters.Monsters.Lagavulin>(), ModelDb.Monster<Toadpole>(), ModelDb.Monster<SludgeSpinner>(), ModelDb.Monster<Seapunk>() })
        {
            string path = (string)typeof(MonsterModel).GetProperty("VisualsPath", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(model)!;
            if(model is AllMatters.Monsters.Lagavulin)path="res://AllMatters/monsters/lagavulin/lagavulin.tscn";
            using var visuals = model.CreateVisuals();
            try
            {
                tree.Root.AddChild(visuals);
                NativeEventTests.Check(visuals.SpineBody?.GetSkeleton()?.GetData() != null && visuals.Bounds.Size.X > 0 && !visuals.SceneFilePath.Contains("fallback"), "Actual species visual loads with Spine and bounds");
                Console.WriteLine($"VISUAL type={model.GetType().Name} sourcePath={path} boundsPosition={visuals.Bounds.Position} boundsSize={visuals.Bounds.Size} spine=true nativeCreateVisuals=true nativeScriptsRegistered=true; beta resource pack in both targets, no UI occlusion claim");
            }
            finally { tree.Root.RemoveChild(visuals); visuals.Free(); }
        }
        using var root = ModelDb.Encounter<PhrogParasiteElite>().CreateScene();
        NativeEventTests.Check(root.HasNode("wriggler4")&&!root.HasNode("wriggler5"),"Actual packed native scene has exactly the documented missing fifth slot");
        root.AddChild(new Godot.Marker2D { Name = "wriggler5" });
        for (int i = 1; i <= 5; i++) root.GetNode<Godot.Marker2D>("wriggler"+i).Position=new Godot.Vector2(900+180*(i-1),700);
        for (int i = 1; i <= 5; i++) NativeEventTests.Check(root.GetNode<Godot.Marker2D>("wriggler" + i).Position.X == 900 + 180 * (i - 1), "Fifth node has an explicit position rather than cancelled addition");
        Console.WriteLine("VISUAL actual native Phrog scene instance gains fifth marker and resolves all names; positions=900,1080,1260,1440,1620 y=700; source PCK untouched, NOT final layout");
        root.Free();
    }

    private static async Task FiveWorms()
    {
        var patch = new Harmony("allmatters.prototype70.five-worms");
        PrototypeHuntingFiveWormProbe.Install(patch);
        try
        {
            for (int n = 1; n <= 4; n++)
            {
                var run = HeadlessTestSetup.Run("HUNTING70_FIVE", Enumerable.Repeat(ModelDb.Character<Ironclad>(), n).ToArray());
                using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<PhrogParasiteElite>().ToMutable());
                var combat = battle.Combat;
                foreach (var p in run.Players) { p.Creature.SetMaxHpInternal(1000); p.Creature.SetCurrentHpInternal(1000); }
                await battle.Monster.AfterAddedToRoom();
                using(var scene=battle.Combat.Encounter!.CreateScene())
                {
                    NativeEventTests.Check(scene.HasNode("wriggler5")&&scene.GetNode<Godot.Marker2D>("wriggler5").Position.X==1620,"Shared live probe actually patches native CreateScene and fifth position");
                    scene.Free();
                }
                var infested = battle.Monster.Creature.GetPower<InfestedPower>()!;
                await infested.AfterDeath(new ThrowingPlayerChoiceContext(), battle.Monster.Creature, true, 0);
                NativeEventTests.Check(combat.Enemies.Count == 1, "Prevented death must not spawn");
                await CreatureCmd.Kill(battle.Monster.Creature);
                var worms = combat.Enemies.Where(c => c.Monster is Wriggler).ToArray();
                NativeEventTests.Check(worms.Length == 5 && worms.All(w => w.IsPrimaryEnemy), "Actual native death chain spawns five primary enemies");
                NativeEventTests.Check(worms.Select(c=>c.SlotName).SequenceEqual(Enumerable.Range(1,5).Select(i=>"wriggler"+i)),"Expanded real slot declarations preserve native1/2/3/4/5 enemy queue order");
                int hpBefore = run.Players.Sum(p => p.Creature.CurrentHp);
                foreach (var worm in worms)
                {
                    var model = worm.Monster!;
                    if (model.NextMove.Id != "SPAWNED_MOVE") model.RollMove(combat.PlayerCreatures);
                    NativeEventTests.Check(model.NextMove.Id == "SPAWNED_MOVE", "Spawn empty action retained");
                    await model.NextMove.PerformMove(combat.PlayerCreatures);
                    model.MoveStateMachine.OnMovePerformed(model.NextMove);
                    model.RollMove(combat.PlayerCreatures);
                    int index = int.Parse(worm.SlotName![8..]);
                    string expected = index % 2 == 1 ? "NASTY_BITE_MOVE" : "WRIGGLE_MOVE";
                    NativeEventTests.Check(model.NextMove.Id == expected, "Five-slot parity matches after real spawn empty action");
                    await model.NextMove.PerformMove(combat.PlayerCreatures);
                    Console.WriteLine($"SPAWN players={n} slot={worm.SlotName} hp={worm.MaxHp} action={model.NextMove.Id} strength={worm.GetPowerAmount<StrengthPower>()}");
                }
                int lost = hpBefore - run.Players.Sum(p => p.Creature.CurrentHp);
                NativeEventTests.Check(lost == 18 * n && run.Players.All(p => p.Creature.CurrentHp == 982), $"Three native bites deal 18 per target player, total={lost}");
                foreach (var p in run.Players)
                    NativeEventTests.Check(PileType.Discard.GetPile(p).Cards.Count(c => c is MegaCrit.Sts2.Core.Models.Cards.Infection) == 2, "Even worms insert two Infections per player");
                Console.WriteLine($"SPAWN_RESULT players={n} five=true primary=true totalBiteDamage={lost} infectionsPerPlayer=2; scene/UI not exercised");
            }
        }
        finally { patch.UnpatchAll(patch.Id); }
    }
}
