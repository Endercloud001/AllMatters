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

internal static class PrototypeHuntingUiTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingUi native rest disabled icon and release path",()=>RestButtons(false)),("PrototypeHuntingUi standalone rest DLL actual native buttons",()=>RestButtons(true)),("PrototypeHuntingUi native shop hidden used visuals and input handlers",ShopSlots),("PrototypeHuntingUi standalone shop DLL actual native inputs",()=>ShopSlotsCore(true)),("PrototypeHuntingUi original Calamity and Tender power presentation adapters",ChoiceCards)];
    private static bool PoolReady;
    internal static void CardPool(){if(PoolReady)return;MegaCrit.Sts2.Core.Nodes.Pooling.NodePool.Init<MegaCrit.Sts2.Core.Nodes.Cards.NCard>("res://scenes/cards/card.tscn",0);PoolReady=true;}
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
    private static void RestrictVisual(NMerchantSlot __instance)=>PrototypeHuntingShopProbe.RestrictVisual(__instance);
    private static Task ShopSlots()=>ShopSlotsCore(false);
    private static async Task ShopSlotsCore(bool standalone)
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
        const string liveId="AllMattersHuntingShopProbe";
        if(standalone)
        {
#if STS2_PUBLIC
            const string target="public";
#else
            const string target="public-beta";
#endif
            var pack=System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute),false).Cast<System.Reflection.AssemblyMetadataAttribute>().Single(a=>a.Key=="ModResourcePack").Value!;
            var path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(pack)!,target,liveId,liveId+".dll"));
            var assembly=System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(TestRunner).Assembly)!.LoadFromAssemblyPath(path);
            assembly.GetType("PrototypeHuntingShopMod",true)!.GetMethod("Initialize")!.Invoke(null,null);
            Console.WriteLine($"UI_SHOP_ENTRY target={target} actualDllInitialized=true path={path}; following native nodes and inputs use built DLL hooks");
        }
        Type[] types=[typeof(NMerchantCard),typeof(NMerchantRelic),typeof(NMerchantPotion),typeof(NMerchantCardRemoval)];
        foreach(var type in types)
        {
            if(!standalone)patch.Patch(AccessTools.Method(type,"UpdateVisual"),postfix:new HarmonyMethod(typeof(PrototypeHuntingUiTests),nameof(RestrictVisual)));
            patch.Patch(AccessTools.Method(type,"CreateHoverTip"),prefix:new HarmonyMethod(typeof(PrototypeHuntingUiTests),nameof(NoTooltip)));
        }
        if(!standalone)
        {
        patch.Patch(AccessTools.Method(typeof(MerchantEntry),nameof(MerchantEntry.OnTryPurchaseWrapper)),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),"BlockShop"));
        patch.Patch(AccessTools.Method(typeof(MerchantCardRemovalEntry),nameof(MerchantCardRemovalEntry.OnTryPurchaseWrapper),[typeof(MerchantInventory),typeof(bool),typeof(bool)]),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),"BlockShop"));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),nameof(OneOffSynchronizer.DoLocalMerchantCardRemoval)),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),"BlockRemovalSender"));
        patch.Patch(AccessTools.Method(typeof(OneOffSynchronizer),"DoMerchantCardRemoval"),prefix:new HarmonyMethod(typeof(PrototypeHuntingServiceTests),"BlockRemovalReceiver"));
        }
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
            if(standalone)
            {
                int gold=p.Gold,cards=p.Deck.Cards.Count,uses=p.ExtraFields.CardShopRemovalsUsed;
                ulong rng=HeadlessTestSetup.RngPosition(p.PlayerRng.Shops);
                foreach(var entry in inv.AllEntries)
                    NativeEventTests.Check(!await entry.OnTryPurchaseWrapper(inv)&&!await entry.OnTryPurchaseWrapper(inv,true),"Actual standalone DLL denies paid and free programmatic purchases");
                NativeEventTests.Check(!await inv.CardRemovalEntry!.OnTryPurchaseWrapper(inv,true,false),"Standalone DLL denies free removal wrapper");
                NativeEventTests.Check(!await RunManager.Instance.OneOffSynchronizer.DoLocalMerchantCardRemoval(0,false),"Standalone DLL denies direct free sender");
                NativeEventTests.Check(!await (Task<bool>)AccessTools.Method(typeof(OneOffSynchronizer),"DoMerchantCardRemoval").Invoke(RunManager.Instance.OneOffSynchronizer,[p,0,false])!,"Standalone DLL denies direct receiver");
                NativeEventTests.Check(p.Gold==gold&&p.Deck.Cards.Count==cards&&p.ExtraFields.CardShopRemovalsUsed==uses&&rng==HeadlessTestSetup.RngPosition(p.PlayerRng.Shops)&&inv.AllEntries.All(e=>e.IsStocked)&&!inv.CardRemovalEntry.Used,"Standalone guard leaves inventory, RNG, gold, deck, count and real Used unchanged");
                Console.WriteLine("UI_SHOP_TRANSACTIONS players=1 actualDll=true paidAndFreeAllEntries=denied removalWrapperSenderReceiver=denied goldDeckRngUsesStock=unchanged realUsed=false; native single-process APIs, not network receiver transport");
                run.PopCurrentRoom();p.Gold=100;p.Creature.SetCurrentHpInternal(10);
                var cleric=ModelDb.AllEvents.Single(e=>e.Id.Entry=="ALL_MATTERS_EVENT_CLERIC").ToMutable();
#if STS2_PUBLIC
                await cleric.BeginEvent(p,false);
#else
                await cleric.BeginEvent(p,null,false);
#endif
                await cleric.CurrentOptions.Single(o=>o.TextKey.EndsWith(".HEAL")).Chosen();
                NativeEventTests.Check(cleric.IsFinished&&p.Gold==65&&p.Creature.CurrentHp==30,"Ordinary event service remains native under actual standalone shop DLL");
                Console.WriteLine("UI_SHOP_EVENT players=1 actualDll=true clericHeal=true paid35=true hp30=true eventFinished=true currentRoomNotMerchant=true");
            }
        }
        finally
        {
            InputMap.ActionEraseEvents(MegaInput.select);if(hadAction){foreach(var e in oldInputs)InputMap.ActionAddEvent(MegaInput.select,e);}else InputMap.EraseAction(MegaInput.select);
            patch.UnpatchAll(patch.Id);if(standalone)new Harmony(liveId).UnpatchAll(liveId);rug.Free();RunManager.Instance.CleanUp(false);
        }
    }
    private static async Task RestButtons(bool standalone)
    {
        STS2RitsuLib.RitsuLibFramework.EnsureGodotScriptsRegistered(typeof(CardModel).Assembly,STS2RitsuLib.RitsuLibFramework.CreateLogger("Prototype70NativeUI"));
        var player=HeadlessTestSetup.PlayerWithRun("HUNTING70_UI");
        ((MegaCrit.Sts2.Core.Runs.RunState)player.RunState).PushRoom(new RestSiteRoom());
        var patch=new Harmony(standalone?"AllMattersHuntingRestProbe":"allmatters.prototype70.rest-original-ui");
        if(standalone)
        {
#if STS2_PUBLIC
            const string target="public";
#else
            const string target="public-beta";
#endif
            var pack=System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute),false).Cast<System.Reflection.AssemblyMetadataAttribute>().Single(a=>a.Key=="ModResourcePack").Value!;
            var path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(pack)!,target,"AllMattersHuntingRestProbe","AllMattersHuntingRestProbe.dll"));
            var assembly=System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(TestRunner).Assembly)!.LoadFromAssemblyPath(path);
            assembly.GetType("PrototypeHuntingRestMod",true)!.GetMethod("Initialize")!.Invoke(null,null);
            Console.WriteLine($"UI_REST_ENTRY target={target} actualDllInitialized=true path={path}; following native-node behavior uses built DLL hooks");
        }
        else PrototypeHuntingRestProbe.Install(patch);
        try
        {
        var tree=(SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        foreach(RestSiteOption original in new RestSiteOption[]{new HealRestSiteOption(player),new SmithRestSiteOption(player)})
        {
            bool allowed=original is HealRestSiteOption;
            using var button=NRestSiteButton.Create(original);
            try
            {
                tree.Root.AddChild(button);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                var icon=button.GetNode<TextureRect>("%Icon");
                var hsv=(ShaderMaterial)icon.Material;
                bool unclickable=(bool)AccessTools.Field(typeof(NRestSiteButton),"_isUnclickable").GetValue(button);
                NativeEventTests.Check(icon.Texture!=null&&icon.Texture.GetWidth()>0,"Original icon retained");
                NativeEventTests.Check(ReferenceEquals(button.Option,original)&&icon.Texture==original.Icon,"Original option identity and texture retained; no model wrapper");
                NativeEventTests.Check(unclickable==!allowed,"Native cached execution disable follows display state");
                NativeEventTests.Check(Math.Abs(hsv.GetShaderParameter("s").AsDouble()-(allowed?1:0))<0.001&&Math.Abs(hsv.GetShaderParameter("v").AsDouble()-(allowed?1:0.6))<0.001,"Native grey shader matches enabled/disabled");
                if(!allowed)AccessTools.Method(typeof(NRestSiteButton),"OnRelease").Invoke(button,null);
                AccessTools.Method(typeof(NRestSiteButton),"Reload").Invoke(button,null);
                NativeEventTests.Check((bool)AccessTools.Field(typeof(NRestSiteButton),"_isUnclickable").GetValue(button)==!allowed,"Native reload preserves denial and original option");
                Console.WriteLine($"UI_REST option={original.OptionId} originalIdentity=true actualRestRoom=true iconWidth={icon.Texture.GetWidth()} nativeIsReady={button.IsNodeReady()} unclickable={unclickable} s={hsv.GetShaderParameter("s")} v={hsv.GetShaderParameter("v")} disabledReleaseInvoked={!allowed}; standalone native node, no physical focus/resolution acceptance");
            }
            finally{tree.Root.RemoveChild(button);button.Free();}
        }
        ((MegaCrit.Sts2.Core.Runs.RunState)player.RunState).PopCurrentRoom();
        using(var normalButton=NRestSiteButton.Create(new SmithRestSiteOption(player)))
        {
            tree.Root.AddChild(normalButton);
            try
            {
                await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                NativeEventTests.Check(!(bool)AccessTools.Field(typeof(NRestSiteButton),"_isUnclickable").GetValue(normalButton),"Outside native RestSiteRoom, original enabled Smith is not disabled");
                NativeEventTests.Check(Math.Abs(((ShaderMaterial)normalButton.GetNode<TextureRect>("%Icon").Material).GetShaderParameter("s").AsDouble()-1)<0.001,"Outside rest room original color remains");
                Console.WriteLine($"UI_REST_OUTSIDE standalone={standalone} originalSmithEnabled=true blocked=false colorUnchanged=true");
            }
            finally{tree.Root.RemoveChild(normalButton);normalButton.Free();}
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
        finally{patch.UnpatchAll(patch.Id);}
    }
}
