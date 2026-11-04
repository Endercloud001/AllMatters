// Isolated native UI node experiment; no game/Steam process, no physical-user acceptance.
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Multiplayer.Game;

internal sealed class PrototypeHuntingDisabledDisplay(RestSiteOption original,Player owner):RestSiteOption(owner)
{
    public override string OptionId=>original.OptionId;
    public override MegaCrit.Sts2.Core.Localization.LocString Description=>original.Description;
    public override bool IsEnabled=>false;
    public override Task<bool> OnSelect()=>Task.FromResult(false);
}
internal static class PrototypeHuntingUiTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingUi native rest disabled icon and release path",RestButtons),("PrototypeHuntingUi native shop hidden used visuals and input handlers",ShopSlots),("PrototypeHuntingUi original Calamity and Tender power presentation adapters",ChoiceCards)];
    private static bool PoolReady;
    private static void CardPool(){if(PoolReady)return;MegaCrit.Sts2.Core.Nodes.Pooling.NodePool.Init<MegaCrit.Sts2.Core.Nodes.Cards.NCard>("res://scenes/cards/card.tscn",0);PoolReady=true;}
    private static async Task ChoiceCards()
    {
        STS2RitsuLib.RitsuLibFramework.EnsureGodotScriptsRegistered(typeof(CardModel).Assembly,STS2RitsuLib.RitsuLibFramework.CreateLogger("Prototype70NativeUI"));
        PrototypeHuntingKnowledgeTests.Inject();CardPool();
        using var atlases=HeadlessTestSetup.LoadAtlases("card_atlas","power_atlas");
        var tree=(SceneTree)Engine.GetMainLoop();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        var player=HeadlessTestSetup.PlayerWithRun("HUNTING70_CHOICE_UI");var combat=HeadlessTestSetup.Combat(player.RunState);
        var patch=new Harmony("allmatters.prototype70.choice-ui");
        patch.Patch(AccessTools.PropertyGetter(typeof(CardModel),nameof(CardModel.Description)),prefix:new HarmonyMethod(typeof(PrototypeHuntingKnowledgeTests),nameof(PrototypeHuntingKnowledgeTests.DisplayDescription)));
        try
        {
            foreach(string language in new[]{"eng","zhs"})foreach(var canonical in new CardModel[]{ModelDb.Card<PrototypeHuntingCalamityChoice>(),ModelDb.Card<PrototypeHuntingTenderChoice>()})
            {
                MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage(language);
                var card=combat.CreateCard(canonical,player);bool mode=TestMode.IsOn;
                MegaCrit.Sts2.Core.Nodes.Cards.NCard node;
                try{TestMode.IsOn=false;node=MegaCrit.Sts2.Core.Nodes.Cards.NCard.Create(card)!;}
                finally{TestMode.IsOn=mode;}
                try
                {
                    tree.Root.AddChild(node);node.UpdateVisuals(MegaCrit.Sts2.Core.Entities.Cards.PileType.None,MegaCrit.Sts2.Core.Entities.Cards.CardPreviewMode.Normal);
                    await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                    var portrait=node.GetNode<TextureRect>("%Portrait");
                    var title=node.GetNode<MegaCrit.Sts2.addons.mega_text.MegaLabel>("%TitleLabel");
                    var description=node.GetNode<MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel>("%DescriptionLabel");
                    NativeEventTests.Check(portrait.Texture!=null&&portrait.Texture.GetWidth()>0&&title.Text==card.Title&&!string.IsNullOrWhiteSpace(description.Text)&&!description.Text.Contains(".description"),"Actual native card renders original title, actual source texture and formatted description");
                    NativeEventTests.Check(card.Pile==null&&!player.Deck.Cards.Contains(card),"Rendered selection-only card stays outside every pile and permanent deck");
                    Console.WriteLine($"UI_CHOICE language={language} card={card.GetType().Name} nativeReady={node.IsNodeReady()} title={title.Text} texture={card.PortraitPath} textureSize={portrait.Texture!.GetSize()} description={description.Text.Replace('\n',' ')} pile=null deck=false; actual NCard, full selection-screen navigation and user appearance untested");
                }
                finally{tree.Root.RemoveChild(node);node.Free();}
            }
        }
        finally{patch.UnpatchAll(patch.Id);MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage("eng");}
    }
    private static bool NoTooltip()=>false;
    private static void RestrictVisual(NMerchantSlot __instance)
    {
        __instance.FocusMode=Control.FocusModeEnum.None;
        __instance.MouseFilter=Control.MouseFilterEnum.Ignore;
        __instance.Hitbox.MouseFilter=Control.MouseFilterEnum.Ignore;
        if(__instance is NMerchantCardRemoval)
        {
            var animator=__instance.GetNode<AnimationPlayer>("%Animation");animator.Play("Used");animator.Advance(0);
            __instance.GetNode<Control>("Cost").Visible=false;
        }
        else __instance.Visible=false;
    }
    private static async Task ShopSlots()
    {
        STS2RitsuLib.RitsuLibFramework.EnsureGodotScriptsRegistered(typeof(CardModel).Assembly,STS2RitsuLib.RitsuLibFramework.CreateLogger("Prototype70NativeUI"));
        using var atlases=HeadlessTestSetup.LoadAtlases("card_atlas","relic_atlas");
        CardPool();
        var tree=(SceneTree)Engine.GetMainLoop();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        using var outside=HeadlessCombatInProgress.EnterEvent();
        var run=HeadlessTestSetup.Run("HUNTING70_SHOP_UI",MegaCrit.Sts2.Core.Models.ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>());
        var p=run.Players[0];p.Gold=1000;
        RunManager.Instance.SetUpTest(run,new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());run.PushRoom(new MerchantRoom());
        var inv=MerchantInventory.CreateForNormalMerchant(p);
        using var rug=new NMerchantInventory();AccessTools.Property(typeof(NMerchantInventory),nameof(NMerchantInventory.Inventory)).SetValue(rug,inv);
        var patch=new Harmony("allmatters.prototype70.shop-ui");
        Type[] types=[typeof(NMerchantCard),typeof(NMerchantRelic),typeof(NMerchantPotion),typeof(NMerchantCardRemoval)];
        foreach(var type in types)
        {
            patch.Patch(AccessTools.Method(type,"UpdateVisual"),postfix:new HarmonyMethod(typeof(PrototypeHuntingUiTests),nameof(RestrictVisual)));
            patch.Patch(AccessTools.Method(type,"CreateHoverTip"),prefix:new HarmonyMethod(typeof(PrototypeHuntingUiTests),nameof(NoTooltip)));
        }
        patch.Patch(AccessTools.Method(typeof(MerchantEntry),nameof(MerchantEntry.OnTryPurchaseWrapper)),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),"BlockShop"));
        patch.Patch(AccessTools.Method(typeof(MerchantCardRemovalEntry),nameof(MerchantCardRemovalEntry.OnTryPurchaseWrapper),[typeof(MerchantInventory),typeof(bool),typeof(bool)]),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),"BlockShop"));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),nameof(OneOffSynchronizer.DoLocalMerchantCardRemoval)),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),"BlockRemovalSender"));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),"DoMerchantCardRemoval"),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),"BlockRemovalReceiver"));
        bool hadAction=InputMap.HasAction(MegaInput.select);
        var oldInputs=hadAction?InputMap.ActionGetEvents(MegaInput.select).ToArray():[];
        if(!hadAction)InputMap.AddAction(MegaInput.select);
        using var key=new InputEventKey{Keycode=Key.Enter,Pressed=true};using var pad=new InputEventJoypadButton{ButtonIndex=JoyButton.A,Pressed=true};
        InputMap.ActionAddEvent(MegaInput.select,key);InputMap.ActionAddEvent(MegaInput.select,pad);
        try
        {
            foreach(var (type,file,entry) in new (Type,string,MerchantEntry)[]{(typeof(NMerchantCard),"merchant_card",inv.CardEntries.First()),(typeof(NMerchantRelic),"merchant_relic",inv.RelicEntries.First()),(typeof(NMerchantPotion),"merchant_potion",inv.PotionEntries.First()),(typeof(NMerchantCardRemoval),"merchant_card_removal",inv.CardRemovalEntry!)})
            {
                using var scene=ResourceLoader.Load<PackedScene>("res://scenes/merchant/"+file+".tscn");
                using var slot=scene.Instantiate<NMerchantSlot>();
                try
                {
                    tree.Root.AddChild(slot);slot.Initialize(rug);
                    // Native factories deliberately return null in TestMode. Enable only
                    // the synchronous native visual call; all transactions remain in TestMode.
                    bool testMode=TestMode.IsOn;
                    try{TestMode.IsOn=false;AccessTools.Method(type,"FillSlot").Invoke(slot,[entry]);}
                    finally{TestMode.IsOn=testMode;}
                    int gold=p.Gold,uses=p.ExtraFields.CardShopRemovalsUsed,cards=p.Deck.Cards.Count;ulong rng=HeadlessTestSetup.RngPosition(p.PlayerRng.Shops);
                    for(int reopen=0;reopen<2;reopen++)
                    {
                        try{TestMode.IsOn=false;AccessTools.Method(type,"UpdateVisual").Invoke(slot,null);}
                        finally{TestMode.IsOn=testMode;}
                        if(slot is NMerchantCardRemoval)NativeEventTests.Check(!inv.CardRemovalEntry!.Used&&!slot.GetNode<Control>("Cost").Visible,"Used animation does not counterfeit model Used");
                        else NativeEventTests.Check(!slot.Visible,"Product remains hidden after native visual refresh");
                        slot._GuiInput(key);slot._GuiInput(pad);
                        AccessTools.Field(typeof(NMerchantSlot),"_isHovered").SetValue(slot,true);
                        using var down=new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=true};using var up=new InputEventMouseButton{ButtonIndex=MouseButton.Left,Pressed=false};
                        AccessTools.Method(typeof(NMerchantSlot),"OnMousePressed").Invoke(slot,[down]);AccessTools.Method(typeof(NMerchantSlot),"OnMouseReleased").Invoke(slot,[up]);
                        AccessTools.Field(typeof(NMerchantSlot),"_isHovered").SetValue(slot,false);
                        await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                    }
                    NativeEventTests.Check(p.Gold==gold&&p.ExtraFields.CardShopRemovalsUsed==uses&&p.Deck.Cards.Count==cards&&entry.IsStocked&&rng==HeadlessTestSetup.RngPosition(p.PlayerRng.Shops),"Native input handlers cannot purchase hidden/free/used-looking stock or mutate state");
                    Console.WriteLine($"UI_SHOP type={type.Name} nativeReady={slot.IsNodeReady()} hideOrUsed=true modelStocked={entry.IsStocked} modelUsed={inv.CardRemovalEntry!.Used} key/gamepad/mouseHandlers=denied refreshes=2 money/deck/rng/uses=unchanged; input handlers injected, tooltip host isolated, physical focus still untested");
                }
                finally{tree.Root.RemoveChild(slot);slot.Free();}
            }
        }
        finally
        {
            InputMap.ActionEraseEvents(MegaInput.select);if(hadAction){foreach(var e in oldInputs)InputMap.ActionAddEvent(MegaInput.select,e);}else InputMap.EraseAction(MegaInput.select);
            patch.UnpatchAll(patch.Id);rug.Free();RunManager.Instance.CleanUp(false);
        }
    }
    private static async Task RestButtons()
    {
        STS2RitsuLib.RitsuLibFramework.EnsureGodotScriptsRegistered(typeof(CardModel).Assembly,STS2RitsuLib.RitsuLibFramework.CreateLogger("Prototype70NativeUI"));
        var player=HeadlessTestSetup.PlayerWithRun("HUNTING70_UI");
        var tree=(SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        foreach(RestSiteOption original in new RestSiteOption[]{new HealRestSiteOption(player),new SmithRestSiteOption(player)})
        {
            bool allowed=original is HealRestSiteOption;
            RestSiteOption shown=allowed?original:new PrototypeHuntingDisabledDisplay(original,player);
            using var button=NRestSiteButton.Create(shown);
            try
            {
                tree.Root.AddChild(button);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                var icon=button.GetNode<TextureRect>("%Icon");
                var hsv=(ShaderMaterial)icon.Material;
                bool unclickable=(bool)AccessTools.Field(typeof(NRestSiteButton),"_isUnclickable").GetValue(button);
                NativeEventTests.Check(icon.Texture!=null&&icon.Texture.GetWidth()>0,"Original icon retained");
                NativeEventTests.Check(unclickable==!allowed,"Native cached execution disable follows display state");
                NativeEventTests.Check(Math.Abs(hsv.GetShaderParameter("s").AsDouble()-(allowed?1:0))<0.001&&Math.Abs(hsv.GetShaderParameter("v").AsDouble()-(allowed?1:0.6))<0.001,"Native grey shader matches enabled/disabled");
                if(!allowed)AccessTools.Method(typeof(NRestSiteButton),"OnRelease").Invoke(button,null);
                Console.WriteLine($"UI_REST option={shown.OptionId} iconWidth={icon.Texture.GetWidth()} nativeIsReady={button.IsNodeReady()} unclickable={unclickable} s={hsv.GetShaderParameter("s")} v={hsv.GetShaderParameter("v")} disabledReleaseInvoked={!allowed}; standalone native node, no physical focus/resolution acceptance");
            }
            finally{tree.Root.RemoveChild(button);button.Free();}
        }
        foreach(var card in player.Deck.Cards.ToArray())player.Deck.RemoveInternal(card);
        var nativeDisabled=new SmithRestSiteOption(player);
        NativeEventTests.Check(!nativeDisabled.IsEnabled,"Native Smith with no upgradable cards remains disabled before any hunting display adapter");
        using var disabledButton=NRestSiteButton.Create(nativeDisabled);
        try
        {
            tree.Root.AddChild(disabledButton);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
            NativeEventTests.Check((bool)AccessTools.Field(typeof(NRestSiteButton),"_isUnclickable").GetValue(disabledButton),"Native disabled condition preserved by native UI");
            AccessTools.Method(typeof(NRestSiteButton),"OnRelease").Invoke(disabledButton,null);
            Console.WriteLine("UI_REST_ORIGINAL emptyUpgradableDeck=true originalSmithDisabled=true nativeButtonRelease=denied; no option enable override");
        }
        finally{tree.Root.RemoveChild(disabledButton);disabledButton.Free();}
    }
}
