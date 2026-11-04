// Native choose-screen Ready/layout/focus/completion. Input managers are unattached
// host nodes: their game startup would initialize/save input mappings. No settings IO.
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.TestSupport;
#if !STS2_PUBLIC
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
#endif

internal static class PrototypeHuntingChoiceScreenTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingChoiceScreen native three pair screens and completion",Screen)];
    private static NControllerManager? Controller;
    private static NInputManager? InputHost;
    private static bool ControllerGetter(ref NControllerManager __result){__result=Controller!;return false;}
    private static bool InputGetter(ref NInputManager __result){__result=InputHost!;return false;}
    private static bool NoGlyph()=>false;
#if !STS2_PUBLIC
    private static NOverlayStack? StackHost;
    private static bool StackGetter(ref NOverlayStack __result){__result=StackHost!;return false;}
    private static bool NotCovered(ref bool __result){__result=false;return false;}
#endif
    private static async Task Screen()
    {
        PrototypeHuntingKnowledgeTests.Inject();PrototypeHuntingUiTests.CardPool();
        MegaCrit.Sts2.Core.Nodes.Pooling.NodePool.Init<NGridCardHolder>("res://scenes/cards/holders/grid_card_holder.tscn",0);
        using var atlases=HeadlessTestSetup.LoadAtlases("card_atlas","power_atlas");
        using var outside=HeadlessCombatInProgress.EnterEvent();
        using var controller=new NControllerManager();using var input=new NInputManager();Controller=controller;InputHost=input;
        var patch=new Harmony("allmatters.prototype70.choose-screen");
        patch.Patch(AccessTools.PropertyGetter(typeof(NControllerManager),nameof(NControllerManager.Instance)),prefix:new HarmonyMethod(typeof(PrototypeHuntingChoiceScreenTests),nameof(ControllerGetter)));
        patch.Patch(AccessTools.PropertyGetter(typeof(NInputManager),nameof(NInputManager.Instance)),prefix:new HarmonyMethod(typeof(PrototypeHuntingChoiceScreenTests),nameof(InputGetter)));
        patch.Patch(AccessTools.Method(typeof(NChooseACardSelectionScreen),"UpdateControllerIcons"),prefix:new HarmonyMethod(typeof(PrototypeHuntingChoiceScreenTests),nameof(NoGlyph)));
#if !STS2_PUBLIC
        patch.Patch(AccessTools.Method(typeof(NHotkeyIcon),nameof(NHotkeyIcon.UpdateInput)),prefix:new HarmonyMethod(typeof(PrototypeHuntingChoiceScreenTests),nameof(NoGlyph)));
#endif
        patch.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.GodotExtensions.NButton),"RegisterHotkeys"),prefix:new HarmonyMethod(typeof(PrototypeHuntingChoiceScreenTests),nameof(NoGlyph)));
        patch.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.GodotExtensions.NButton),"UnregisterHotkeys"),prefix:new HarmonyMethod(typeof(PrototypeHuntingChoiceScreenTests),nameof(NoGlyph)));
        patch.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Combat.NExhaustPileButton),"SetAnimInOutPositions"),prefix:new HarmonyMethod(typeof(PrototypeHuntingChoiceScreenTests),nameof(NoGlyph)));
        patch.Patch(AccessTools.PropertyGetter(typeof(CardModel),nameof(CardModel.Description)),prefix:new HarmonyMethod(typeof(PrototypeHuntingKnowledgeTests),nameof(PrototypeHuntingKnowledgeTests.DisplayDescription)));
        var tree=(SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
#if !STS2_PUBLIC
        using var stack=new NOverlayStack();StackHost=stack;
        foreach(string method in new[]{"_Ready","_EnterTree","_ExitTree"})patch.Patch(AccessTools.Method(typeof(NOverlayStack),method),prefix:new HarmonyMethod(typeof(PrototypeHuntingChoiceScreenTests),nameof(NoGlyph)));
        patch.Patch(AccessTools.PropertyGetter(typeof(NOverlayStack),nameof(NOverlayStack.Instance)),prefix:new HarmonyMethod(typeof(PrototypeHuntingChoiceScreenTests),nameof(StackGetter)));
        patch.Patch(AccessTools.PropertyGetter(typeof(NOverlayStack),"StackIsCovered"),prefix:new HarmonyMethod(typeof(PrototypeHuntingChoiceScreenTests),nameof(NotCovered)));
        patch.Patch(AccessTools.Method(typeof(ActiveScreenContext),"Update"),prefix:new HarmonyMethod(typeof(PrototypeHuntingChoiceScreenTests),nameof(NoGlyph)));
        var backstop=new Control{Name="OverlayBackstop"};stack.AddChild(backstop);AccessTools.Field(typeof(NOverlayStack),"_backstop").SetValue(stack,backstop);tree.Root.AddChild(stack);
#endif
        await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        try
        {
            var player=HeadlessTestSetup.PlayerWithRun("HUNTING70_CHOOSE_SCREEN");var combat=HeadlessTestSetup.Combat(player.RunState);
            CardModel[][] pairs=[[ModelDb.Card<MindRot>(),ModelDb.Card<PrototypeHuntingCalamityChoice>()],[ModelDb.Card<Sloth>(),ModelDb.Card<PrototypeHuntingTenderChoice>()],[ModelDb.Card<WasteAway>(),ModelDb.Card<Disintegration>()]];
            foreach(string language in new[]{"eng","zhs"})for(int round=0;round<3;round++)for(int choice=0;choice<2;choice++)
            {
#if STS2_PUBLIC
                // One bounded negative resource measurement is sufficient: the
                // shared beta scene references NHotkeyIcon, absent from public.
                if(language!="eng"||round!=0||choice!=0)continue;
#endif
                MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage(language);
                var cards=pairs[round].Select(c=>combat.CreateCard(c,player)).ToArray();
                using var scene=ResourceLoader.Load<PackedScene>("res://scenes/screens/card_selection/choose_a_card_selection_screen.tscn");
                using var screen=scene.Instantiate<NChooseACardSelectionScreen>();
                AccessTools.Field(typeof(NChooseACardSelectionScreen),"_cards").SetValue(screen,cards);
                AccessTools.Field(typeof(NChooseACardSelectionScreen),"_canSkip").SetValue(screen,false);
                bool mode=TestMode.IsOn;
                try
                {
                    try{TestMode.IsOn=false;
#if STS2_PUBLIC
                        tree.Root.AddChild(screen);
#else
                        stack.Push(screen);
                        NativeEventTests.Check(stack.ScreenCount==1&&stack.Peek()==screen,"Native overlay Push owns the exact choice screen");
#endif
                    }
                    finally{TestMode.IsOn=mode;}
                    await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                    var holders=screen.GetNode<Control>("CardRow").GetChildren().OfType<NGridCardHolder>().ToArray();
                    Console.WriteLine($"UI_CHOICE_HOST nativeReady={screen.IsNodeReady()} holders={holders.Length} models={string.Join(',',holders.Select(h=>h.CardModel?.Id.Entry))} expected={string.Join(',',cards.Select(c=>c.Id.Entry))}");
                    NativeEventTests.Check(screen.IsNodeReady()&&holders.Length==2&&holders.Select(h=>h.CardModel).SequenceEqual(cards),"Native complete screen Ready creates exact original pair");
                    NativeEventTests.Check(screen.DefaultFocusedControl==holders[1]&&holders.All(h=>h.FocusNeighborLeft==holders[1-Array.IndexOf(holders,h)].GetPath()),"Native pair default focus and circular neighbors are wired");
                    var source=(TaskCompletionSource<IEnumerable<CardModel>>)AccessTools.Field(typeof(NChooseACardSelectionScreen),"_completionSource").GetValue(screen);
                    NativeEventTests.Check(!source.Task.IsCompleted&&!screen.GetNode<Control>("SkipButton").Visible,"Choice remains pending and no skip is offered");
                    AccessTools.Field(typeof(NChooseACardSelectionScreen),"_openedTicks").SetValue(screen,0UL);
                    AccessTools.Method(typeof(NChooseACardSelectionScreen),"SelectHolder").Invoke(screen,[holders[choice]]);
                    NativeEventTests.Check(source.Task.IsCompletedSuccessfully&&(await source.Task).Single()==cards[choice]&&cards.All(c=>c.Pile==null&&!player.Deck.Cards.Contains(c)),"Native selection completes with chosen model and no pile/deck insertion");
#if STS2_PUBLIC
                    Console.WriteLine("UI_CHOICE_SCREEN_NEGATIVE language=eng round=1 selected=0 nativePairAndCompletion=true sharedBetaPck=true missingPublicType=NHotkeyIcon skipButtonReadyNull=true requirementSatisfied=false; public original scene not available, child script/Ready errors are real resource incompatibility evidence, NOT full public screen pass");
#else
                    var selected=await screen.CardsSelected();
                    NativeEventTests.Check(selected.Single()==cards[choice]&&stack.ScreenCount==0&&stack.Peek()==null,"Native CardsSelected removes its actual overlay and leaves empty stack");
                    await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                    await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                    NativeEventTests.Check(!GodotObject.IsInstanceValid(screen),"Native AfterOverlayClosed frees choice screen via queued deletion");
                    Console.WriteLine($"UI_CHOICE_OVERLAY language={language} round={round+1} selected={choice} nativePush/Opened/Shown=true nativeCardsSelected/Remove/Hidden/Closed=true screenCount=0 screenFreed=true; raw overlay backstop, map-covered query and global ActiveScreenContext.Update isolated, no complete game focus restoration/public scene claim");
                    Console.WriteLine($"UI_CHOICE_SCREEN language={language} round={round+1} selected={choice} nativeReady=true holders=2 defaultFocus=right neighbors=circular skip=false nativeCompletion=true pile=null deck=false; unattached input managers, glyph/hotkey registration and hidden exhaust positioning isolated, native overlay removal/free verified separately; no physical input/game appearance/global focus restoration proof");
#endif
                }
                finally{if(GodotObject.IsInstanceValid(screen)){screen.GetParent()?.RemoveChild(screen);screen.Free();}}
                await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
            }
        }
        finally{
#if !STS2_PUBLIC
            stack.GetParent()?.RemoveChild(stack);stack.Free();StackHost=null;
#endif
            patch.UnpatchAll(patch.Id);Controller=null;InputHost=null;MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage("eng");}
    }
}
