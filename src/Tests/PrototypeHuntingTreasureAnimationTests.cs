// THROWAWAY: native empty-collection Tween and completion, not a full treasure screen.
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.addons.mega_text;

internal static class PrototypeHuntingTreasureAnimationTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingTreasureAnimation native empty Tween completes both tasks without early manual notification",EmptyAnimation)];
    private static bool ManualReady()=>false;
    private static async Task EmptyAnimation()
    {
        using var outside=HeadlessCombatInProgress.EnterEvent();
        var tree=(SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        var patch=new Harmony("allmatters.prototype70.empty-tween");
        // Only visual fixture setup is replaced. The real Tween, synchronizer,
        // OnRelicsAwarded, AnimateRelicAwards and both completion sources run.
        patch.Patch(AccessTools.Method(typeof(NTreasureRoomRelicCollection),"_Ready"),prefix:new HarmonyMethod(typeof(PrototypeHuntingTreasureAnimationTests),nameof(ManualReady)));
        PrototypeHuntingTreasureProbe.LoadStandalone();
        try
        {
            for(int n=1;n<=4;n++)
            {
                var run=HeadlessTestSetup.Run("HUNTING70_EMPTY_TWEEN",Enumerable.Repeat(ModelDb.Character<Ironclad>(),n).ToArray());
                RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
                run.PushRoom(new TreasureRoom(0));
                var initialRelicCounts=run.Players.Select(p=>p.Relics.Count).ToArray();
                using var collection=new NTreasureRoomRelicCollection();
                using var hands=new NHandImageCollection(); // Native empty list, not in tree.
                using var holder=new NTreasureRoomRelicHolder(); // Empty invisible holder, not in tree.
                using var chest=new Control();
                var label=new MegaLabel{Name="EmptyLabel",UniqueNameInOwner=true};
                using var font=new SystemFont{FontNames=["Arial"]};
                label.AddThemeFontOverride("font",font);
                var smoke=new GpuParticles2D{Name="SmokePuffVfx",UniqueNameInOwner=true};
                collection.AddChild(label);label.Owner=collection;
                collection.AddChild(smoke);smoke.Owner=collection;
                AccessTools.Field(typeof(NTreasureRoomRelicCollection),"_runState").SetValue(collection,run);
                AccessTools.Field(typeof(NTreasureRoomRelicCollection),"_hands").SetValue(collection,hands);
                AccessTools.Property(typeof(NTreasureRoomRelicCollection),nameof(NTreasureRoomRelicCollection.SingleplayerRelicHolder)).SetValue(collection,holder);
                var sync=RunManager.Instance.TreasureRoomRelicSynchronizer;
                int notifications=0;
                Action<List<RelicPickingResult>> award=results=>
                {
                    notifications++;
                    NativeEventTests.Check(results.Count==0,"Native empty Tween notification contains no rewards");
                    AccessTools.Method(typeof(NTreasureRoomRelicCollection),"OnRelicsAwarded").Invoke(collection,[results]);
                };
                sync.RelicsAwarded+=award;
                try
                {
                    tree.Root.AddChild(collection);tree.Root.AddChild(chest);
                    sync.BeginRelicPicking();
                    NativeEventTests.Check(sync.CurrentRelics==null||sync.CurrentRelics.Count==0,"Native BeginRelicPicking produces empty set");
                    collection.InitializeRelics();
                    Task began=collection.RelicPickingBegan(),finished=collection.RelicPickingFinished();
                    NativeEventTests.Check(!began.IsCompleted&&!finished.IsCompleted&&notifications==0,"Empty list alone does not notify either real completion source");
                    NativeEventTests.Check(label.Visible&&!string.IsNullOrWhiteSpace(label.Text)&&smoke.Emitting&&!holder.Visible,"Native empty VFX initializes real label, smoke and hidden relic holder");
                    var watch=System.Diagnostics.Stopwatch.StartNew();
#if STS2_PUBLIC
                    collection.AnimIn(chest);
#else
                    collection.AnimIn();
#endif
                    NativeEventTests.Check(notifications==0&&!began.IsCompleted,"AnimIn schedules native Tween instead of immediate completion");
                    while((!finished.IsCompleted||watch.ElapsedMilliseconds<1200)&&watch.ElapsedMilliseconds<10000)
                        await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                    NativeEventTests.Check(began.IsCompletedSuccessfully&&finished.IsCompletedSuccessfully&&notifications==1,"Exactly one real Tween callback completes native began and finished tasks");
                    NativeEventTests.Check(run.Players.Select(p=>p.Relics.Count).SequenceEqual(initialRelicCounts),"Empty award callback obtains no actual relic");
                    Console.WriteLine($"UI_TREASURE_TWEEN players={n} input=nativeEmptySynchronizer nativeInitializeRelics=true emptyLabel={label.Text} smoke=true beforeTweenBegan=false beforeTweenFinished=false manualCompleteCalls=0 nativeAwardNotifications={notifications} began={began.Status} finished={finished.Status} elapsedMs={watch.ElapsedMilliseconds} actualRelicDelta=0; reconstructed visual fixture/Ready and event subscription, native Tween and award handler run; empty raw hands, TestMode Cmd.Wait, no full OpenChest/physical input/public PCK claim");
                    // Let the native empty-label Tween finish before disposing its targets.
                    while(watch.ElapsedMilliseconds<3200)
                        await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                }
                finally
                {
                    sync.RelicsAwarded-=award;
                    if(collection.GetParent()!=null)tree.Root.RemoveChild(collection);
                    if(chest.GetParent()!=null)tree.Root.RemoveChild(chest);
                    collection.Free();chest.Free();holder.Free();hands.Free();
                    RunManager.Instance.CleanUp(false);
                }
            }
        }
        finally{patch.UnpatchAll(patch.Id);new Harmony(PrototypeHuntingTreasureProbe.Id).UnpatchAll(PrototypeHuntingTreasureProbe.Id);}
    }
}
