// THROWAWAY native geometry sweep. Actual visuals/Bounds and native positioning;
// raw creature widget fixtures do not prove physical targeting or intent rendering.
using Godot;
using HarmonyLib;
using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

internal static class PrototypeHuntingLayoutTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingLayout real species bounds and native placement across all encounter variants",Sweep),("PrototypeHuntingLayout five real Wriggler bounds at widened native Phrog slots",FiveWormSpacing)];
    private static bool NoCreatureLifecycle()=>false;
    private static async Task FiveWormSpacing()
    {
        PrototypeHuntingModelTests.Inject();
        STS2RitsuLib.RitsuLibFramework.EnsureGodotScriptsRegistered(typeof(CardModel).Assembly,STS2RitsuLib.RitsuLibFramework.CreateLogger("Prototype70FiveWormSpacing"));
        var pack=Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="ModResourcePack").Value!;
        NativeEventTests.Check(ProjectSettings.LoadResourcePack(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(pack)!,"../.local/resource-search/ActsFromThePast.pck")),false),"Exact monster resource input mounted for five worm layout");
        var tree=(SceneTree)Engine.GetMainLoop();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        var patch=new Harmony("allmatters.prototype70.five-worm-spacing");
        foreach(var m in new[]{"_Ready","_EnterTree","_ExitTree"})patch.Patch(AccessTools.Method(typeof(NCreature),m),prefix:new HarmonyMethod(typeof(PrototypeHuntingLayoutTests),nameof(NoCreatureLifecycle)));
        patch.Patch(AccessTools.Method(AccessTools.Field(typeof(NCreature),"_stateDisplay").FieldType,"SetCreatureBounds"),prefix:new HarmonyMethod(typeof(PrototypeHuntingLayoutTests),nameof(NoCreatureLifecycle)));
        PrototypeHuntingFiveWormProbe.Install(patch);
        var run=RunState.CreateForTest([MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(ModelDb.Character<Ironclad>(),MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1)],acts:[ModelDb.Act<Overgrowth>(),ModelDb.Act<Hive>(),ModelDb.Act<Glory>()],seed:"HUNTING70_WORM_SPACING");
        var combat=new CombatState(ModelDb.Encounter<PhrogParasiteElite>().ToMutable(),run);
        using var room=new NCombatRoom();var nodes=new List<NCreature>();var widgets=new List<Node>();Control? slots=null;
        try
        {
            for(int i=1;i<=5;i++)
            {
                var entity=combat.CreateCreature(ModelDb.Monster<Wriggler>().ToMutable(),CombatSide.Enemy,"wriggler"+i);
                var visual=entity.Monster!.CreateVisuals();var node=new NCreature();nodes.Add(node);
                AccessTools.Property(typeof(NCreature),nameof(NCreature.Entity)).SetValue(node,entity);AccessTools.Property(typeof(NCreature),nameof(NCreature.Visuals)).SetValue(node,visual);
                var hitbox=new Control();var intent=new Control();node.AddChild(hitbox);node.AddChild(intent);
                AccessTools.Property(typeof(NCreature),nameof(NCreature.Hitbox)).SetValue(node,hitbox);AccessTools.Property(typeof(NCreature),nameof(NCreature.IntentContainer)).SetValue(node,intent);
                foreach(string field in new[]{"_selectionReticle","_stateDisplay"}){var member=AccessTools.Field(typeof(NCreature),field);var widget=(Node)Activator.CreateInstance(member.FieldType)!;widgets.Add(widget);member.SetValue(node,widget);}
                node.AddChild(visual);tree.Root.AddChild(node);
                NativeEventTests.Check(visual.IsNodeReady()&&visual.Bounds.Size.X>0&&!visual.SceneFilePath.Contains("fallback",StringComparison.OrdinalIgnoreCase),$"Wriggler {i} uses actual native model visual/Bounds; path={visual.SceneFilePath}");
            }
            AccessTools.Field(typeof(NCombatRoom),"_visuals").SetValue(room,new CombatRoom(combat));
            ((List<NCreature>)AccessTools.Field(typeof(NCombatRoom),"_creatureNodes").GetValue(room)).AddRange(nodes);
            slots=combat.Encounter!.CreateScene();tree.Root.AddChild(slots);slots.Position-=new Vector2(960,540);
            AccessTools.Property(typeof(NCombatRoom),"EncounterSlots").SetValue(room,slots);
            AccessTools.Method(typeof(NCombatRoom),"PositionCreaturesWithSlots").Invoke(room,[nodes]);
            foreach(var node in nodes)AccessTools.Method(typeof(NCreature),"UpdateBounds",[typeof(Node)]).Invoke(node,[node.Visuals]);
            var expected=Enumerable.Range(0,5).Select(i=>new Vector2(840+210*i,700)).ToArray();
            NativeEventTests.Check(Enumerable.Range(0,5).All(i=>slots.GetNode<Marker2D>("wriggler"+(i+1)).Position==expected[i]),"All five real scene markers use centered 210px spacing");
            NativeEventTests.Check(Enumerable.Range(0,5).All(i=>nodes[i].GlobalPosition==slots.GetNode<Marker2D>("wriggler"+(i+1)).GlobalPosition),"Native PositionCreaturesWithSlots places all five creatures at their exact intended markers");
            var boxes=nodes.Select(n=>n.Hitbox.GetGlobalRect()).ToArray();int intersections=0;
            for(int a=0;a<boxes.Length;a++)for(int b=a+1;b<boxes.Length;b++)if(boxes[a].Intersects(boxes[b]))intersections++;
            NativeEventTests.Check(intersections==0,$"Widened five-creature native hitboxes should not intersect; intersections={intersections}");
            Console.WriteLine($"LAYOUT_FIVE_WORM players=1 target=both nativeWrigglerVisuals=5 markerX=840/1050/1260/1470/1680 gap=210 actualNativeSlotPositioning=true updateBounds=true hitboxIntersections={intersections}; real game rendered health bars/intent/physical selection not covered");
        }
        finally
        {
            patch.UnpatchAll(patch.Id);
            foreach(var node in nodes){if(node.GetParent()!=null)tree.Root.RemoveChild(node);node.Free();}
            foreach(var widget in widgets)widget.Free();
            if(slots!=null){if(slots.GetParent()!=null)tree.Root.RemoveChild(slots);slots.Free();}
        }
    }
    private static async Task Sweep()
    {
        PrototypeHuntingModelTests.Inject();
        STS2RitsuLib.RitsuLibFramework.EnsureGodotScriptsRegistered(typeof(CardModel).Assembly,STS2RitsuLib.RitsuLibFramework.CreateLogger("Prototype70Layout"));
        var pack=Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="ModResourcePack").Value!;
        NativeEventTests.Check(ProjectSettings.LoadResourcePack(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(pack)!,"../.local/resource-search/ActsFromThePast.pck")),false),"Exact species resource input mounted read-only");
        var tree=(SceneTree)Engine.GetMainLoop();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        var patch=new Harmony("allmatters.prototype70.native-layout");
        foreach(var m in new[]{"_Ready","_EnterTree","_ExitTree"})
            patch.Patch(AccessTools.Method(typeof(NCreature),m),prefix:new HarmonyMethod(typeof(PrototypeHuntingLayoutTests),nameof(NoCreatureLifecycle)));
        patch.Patch(AccessTools.Method(AccessTools.Field(typeof(NCreature),"_stateDisplay").FieldType,"SetCreatureBounds"),prefix:new HarmonyMethod(typeof(PrototypeHuntingLayoutTests),nameof(NoCreatureLifecycle)));
        var factory=typeof(ModelDb).GetMethods().Single(m=>m.Name=="Encounter"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==0);
        var types=typeof(MonsterModel).Assembly.GetTypes().Where(t=>!t.IsAbstract&&typeof(EncounterModel).IsAssignableFrom(t)&&!t.Name.StartsWith("Mock")).OrderBy(t=>t.FullName).ToArray();
        int completed=0,failed=0,missingSlots=0,overWidth=0,overlaps=0;
        try
        {
            foreach(int playerCount in new[]{1,2,3,4})foreach(var type in types)foreach(int variant in type.Name=="BattlewornDummyEventEncounter"?new[]{1,2,3}:new[]{0})foreach(int ecology in new[]{0,1,2})
            {
                var nodes=new List<NCreature>();var rawWidgets=new List<Node>();Control? slots=null;
                using var nativeRoom=new NCombatRoom();
                var run=RunState.CreateForTest(Enumerable.Range(1,playerCount).Select(id=>MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun(ModelDb.Character<Ironclad>(),MegaCrit.Sts2.Core.Unlocks.UnlockState.all,(ulong)id)).ToList(),acts:[ModelDb.Act<Overgrowth>(),ModelDb.Act<Hive>(),ModelDb.Act<Glory>()],seed:"HUNTING70_LAYOUT");
                run.CurrentActIndex=ecology==2?2:0;
                try
                {
                    var encounter=((EncounterModel)factory.MakeGenericMethod(type).Invoke(null,null)!).ToMutable();
                    if(variant>0){var setting=type.GetProperty("Setting")!;setting.SetValue(encounter,Enum.ToObject(setting.PropertyType,variant));}
                    encounter.GenerateMonstersWithSlots(run);
                    var combat=new CombatState(encounter,run);
                    var creatures=new List<Creature>();
                    foreach(var (model,slot) in encounter.MonstersWithSlots)creatures.Add(combat.CreateCreature(model,CombatSide.Enemy,slot));
                    MonsterModel append=ecology switch{0=>ModelDb.Monster<PrototypeHuntingFungus>().ToMutable(),1=>ModelDb.Monster<Seapunk>().ToMutable(),_=>ModelDb.Monster<PrototypeHuntingMystic>().ToMutable()};
                    creatures.Add(combat.CreateCreature(append,CombatSide.Enemy,"prototype_layout_append"));
                    foreach(var entity in creatures)
                    {
                        var visual=entity.Monster!.CreateVisuals();
                        var node=new NCreature();nodes.Add(node);
                        AccessTools.Property(typeof(NCreature),nameof(NCreature.Entity)).SetValue(node,entity);
                        AccessTools.Property(typeof(NCreature),nameof(NCreature.Visuals)).SetValue(node,visual);
                        var hitbox=new Control();var intent=new Control();node.AddChild(hitbox);node.AddChild(intent);
                        AccessTools.Property(typeof(NCreature),nameof(NCreature.Hitbox)).SetValue(node,hitbox);
                        AccessTools.Property(typeof(NCreature),nameof(NCreature.IntentContainer)).SetValue(node,intent);
                        foreach(string field in new[]{"_selectionReticle","_stateDisplay"})
                        {
                            var member=AccessTools.Field(typeof(NCreature),field);var widget=(Node)Activator.CreateInstance(member.FieldType)!;
                            rawWidgets.Add(widget);member.SetValue(node,widget);
                        }
                        node.AddChild(visual);tree.Root.AddChild(node);
                        NativeEventTests.Check(!string.IsNullOrWhiteSpace(visual.SceneFilePath)&&!visual.SceneFilePath.Contains("fallback",StringComparison.OrdinalIgnoreCase),$"Exact species visual required, actual source={visual.SceneFilePath}");
                        NativeEventTests.Check(visual.IsNodeReady()&&visual.Bounds.Size.X>0,"Actual original/appended model visual Ready provides positive native Bounds width");
                    }
                    AccessTools.Field(typeof(NCombatRoom),"_visuals").SetValue(nativeRoom,new CombatRoom(combat));
                    ((List<NCreature>)AccessTools.Field(typeof(NCombatRoom),"_creatureNodes").GetValue(nativeRoom)).AddRange(nodes);
                    float scaling=encounter.GetCameraScaling();
                    AccessTools.Method(typeof(NCombatRoom),"PositionEnemies").Invoke(nativeRoom,[nodes,scaling]);
                    foreach(var node in nodes)AccessTools.Method(typeof(NCreature),"UpdateBounds",[typeof(Node)]).Invoke(node,[node.Visuals]);
                    var boxes=nodes.Select(c=>c.Hitbox.GetGlobalRect()).ToArray();
                    float left=boxes.Min(b=>b.Position.X),right=boxes.Max(b=>b.End.X),limit=960f/scaling;
                    int pairOverlap=0;
                    for(int a=0;a<boxes.Length;a++)for(int b=a+1;b<boxes.Length;b++)if(boxes[a].Intersects(boxes[b]))pairOverlap++;
                    if(left<0||right>limit)overWidth++;
                    if(pairOverlap>0)overlaps++;
                    string positions=string.Join(';',nodes.Select(c=>$"{c.Entity.Monster!.GetType().Name}@{c.Position.X:F1},{c.Position.Y:F1}/w{c.Visuals.Bounds.Size.X:F1}"));
                    string markers="none";bool missing=false;
                    if(encounter.HasScene)
                    {
                        slots=encounter.CreateScene();tree.Root.AddChild(slots);
                        slots.Position-=new Vector2(1920,1080)*.5f;
                        AccessTools.Property(typeof(NCombatRoom),"EncounterSlots").SetValue(nativeRoom,slots);
                        AccessTools.Method(typeof(NCombatRoom),"PositionCreaturesWithSlots").Invoke(nativeRoom,[nodes.Take(nodes.Count-1).ToList()]);
                        markers=string.Join(';',slots.GetChildren().OfType<Marker2D>().Select(m=>$"{m.Name}@{m.Position.X:F1},{m.Position.Y:F1}"));
                        missing=!slots.HasNode("prototype_layout_append");if(missing)missingSlots++;
                        // Temporary measured append marker only; deliberately not a
                        // production layout proposal and no enemy is dropped.
                        var originals=nodes.Take(nodes.Count-1).ToArray();
                        float x=originals.Max(c=>c.GlobalPosition.X+c.Visuals.Bounds.Size.X*.5f)+40+nodes[^1].Visuals.Bounds.Size.X*.5f;
                        var marker=new Marker2D{Name="prototype_layout_append"};slots.AddChild(marker);marker.GlobalPosition=new Vector2(x,originals.Average(c=>c.GlobalPosition.Y));
                        AccessTools.Method(typeof(NCombatRoom),"PositionCreaturesWithSlots").Invoke(nativeRoom,[nodes]);
                        foreach(var node in nodes)AccessTools.Method(typeof(NCreature),"UpdateBounds",[typeof(Node)]).Invoke(node,[node.Visuals]);
                        NativeEventTests.Check(nodes[^1].GlobalPosition==marker.GlobalPosition,"Native slot positioning uses temporary actual Marker; addition remains present");
                    }
                    // Original CreateEnemyNodes does this after choosing its placement.
                    // It consumes native cosmetic RNG; actual scales are recorded below.
                    AccessTools.Method(typeof(NCombatRoom),"RandomizeEnemyScalesAndHues").Invoke(nativeRoom,null);
                    var scaledBoxes=nodes.Select(c=>c.Hitbox.GetGlobalRect()).ToArray();
                    int scaledOverlaps=0;
                    for(int a=0;a<scaledBoxes.Length;a++)for(int b=a+1;b<scaledBoxes.Length;b++)if(scaledBoxes[a].Intersects(scaledBoxes[b]))scaledOverlaps++;
                    string scales=string.Join(';',nodes.Select(c=>$"{c.Entity.Monster!.GetType().Name}={c.Visuals.Scale.X:F3}"));
                    string resources=string.Join(';',nodes.Select(c=>$"{c.Entity.Monster!.GetType().Name}={c.Visuals.SceneFilePath}"));
                    string slotPositions=encounter.HasScene?string.Join(';',nodes.Select(c=>$"{c.Entity.SlotName}@{c.GlobalPosition.X:F1},{c.GlobalPosition.Y:F1}/hitbox{c.Hitbox.Size.X:F1}x{c.Hitbox.Size.Y:F1}/intent{c.IntentContainer.Position.X:F1},{c.IntentContainer.Position.Y:F1}")):"none";
                    Console.WriteLine($"LAYOUT players={playerCount} encounter={type.Name} variant={variant} ecology={ecology} append={append.GetType().Name} count={nodes.Count} scaling={scaling} nativeWidthLimit={limit:F1} positionedSpan={left:F1}..{right:F1} boundsPairOverlaps={pairOverlap} dedicated={encounter.HasScene} missingNativeAppendMarker={missing} markers={markers} fallbackPositions={positions} nativeUpdateBounds=true nativeCosmeticScaling=true scaledHitboxOverlaps={scaledOverlaps} actualScales={scales} exactResources={resources} slotPositions={slotPositions}; actual model visuals/Bounds/native positioning and hitbox/intent-marker geometry into raw widgets, state-display layout omitted, original monster hooks not fully initialized, fallback not the native chosen method for dedicated scenes, temporary marker is measurement only, no rendered intent/physical-input/public-original-PCK approval");
                    completed++;
                }
                catch(Exception error)
                {
                    failed++;var cause=error is TargetInvocationException t&&t.InnerException!=null?t.InnerException:error;
                    Console.WriteLine($"LAYOUT_GAP players={playerCount} encounter={type.Name} variant={variant} ecology={ecology} error={cause.GetType().Name} detail={cause.Message.Replace('\n',' ')} requirementSatisfied=false; no fake visual width or omitted original enemy");
                }
                finally
                {
                    foreach(var node in nodes){if(node.GetParent()!=null)tree.Root.RemoveChild(node);node.Free();}
                    foreach(var widget in rawWidgets)widget.Free();
                    if(slots!=null){if(slots.GetParent()!=null)tree.Root.RemoveChild(slots);slots.Free();}
                    nativeRoom.Free();
                }
            }
            Console.WriteLine($"LAYOUT_SUMMARY players=1-4 encounterTypes={types.Length} completed={completed} failed={failed} missingNativeAppendMarkers={missingSlots} fallbackOverWidth={overWidth} fallbackOverlappingCases={overlaps}; sweep completion is not a layout rule pass, actual failures and geometric capacity remain visible");
            NativeEventTests.Check(completed+failed==types.Sum(t=>t.Name=="BattlewornDummyEventEncounter"?3:1)*3*4,"Native sweep records every encounter/ecology input, including actual gaps");
            NativeEventTests.Check(completed>0,"At least one native geometry path must actually execute");
        }
        finally{patch.UnpatchAll(patch.Id);}
    }
}
