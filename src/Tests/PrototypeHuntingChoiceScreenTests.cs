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

internal static class PrototypeHuntingChoiceScreenTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingChoiceScreen native three pair screens and completion",Screen)];
    private static NControllerManager? Controller;
    private static NInputManager? InputHost;
    private static bool ControllerGetter(ref NControllerManager __result){__result=Controller!;return false;}
    private static bool InputGetter(ref NInputManager __result){__result=InputHost!;return false;}
    private static bool NoGlyph()=>false;
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
                    try{TestMode.IsOn=false;tree.Root.AddChild(screen);}
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
                    Console.WriteLine($"UI_CHOICE_SCREEN language={language} round={round+1} selected={choice} nativeReady=true holders=2 defaultFocus=right neighbors=circular skip=false nativeCompletion=true pile=null deck=false; unattached input managers, glyph/hotkey registration and hidden exhaust positioning isolated, no overlay-stack removal/physical input/game appearance proof");
#endif
                }
                finally{if(screen.GetParent()!=null)tree.Root.RemoveChild(screen);screen.Free();}
                await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
            }
        }
        finally{patch.UnpatchAll(patch.Id);Controller=null;InputHost=null;MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage("eng");}
    }
}
