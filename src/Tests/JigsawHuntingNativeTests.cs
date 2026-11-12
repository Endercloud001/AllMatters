using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using AllMatters.Jigsaw;
using AllMatters.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawHuntingNativeTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw hunting native free shop refuses stock and removal without mutating inventory", Shop),
      ("Jigsaw hunting native rest rejects extra actions before side effects and preserves healing", Rest),
      ("Jigsaw hunting native rest without allowed choices exits through native skip", RestExit),
      ("Jigsaw hunting native ordinary event healing remains available", EventService),
      ("Jigsaw hunting native zero selection saves and enters first Boss punished", ZeroBossEntry),
      ("Jigsaw hunting production checkpoint reader preserves independent balance", CheckpointReader),
      ("Jigsaw hunting map notice follows safe exhaustion and Boss lifecycle", MapNotice),
      ("Jigsaw hunting native control command uses ordered action and preserves history", Command),
      ("Jigsaw hunting native final map Boss clears before reward save and restore", BossVictory),
      ("Jigsaw hunting native legacy complete points migrate from primary and selected backup", Legacy) ];
    private static void Require(bool value, string reason) => NativeEventTests.Check(value, reason);
    private static bool scriptsRegistered;
    internal static void RegisterScripts()
    {
        if (scriptsRegistered) return;
        AccessTools.Method(typeof(Node).Assembly.GetType("Godot.Bridge.ScriptManagerBridge"), "LookupScriptsInAssembly").Invoke(null, [typeof(NRestSiteButton).Assembly]);
        scriptsRegistered = true;
    }
    private static MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext.IScreenContext? activeUi;
    private static bool UiContext(ref MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext.IScreenContext? __result) { __result = activeUi is NMerchantInventory { IsOpen: false } ? null : activeUi; return false; }
    private static bool SkipProfile() => false;
    private static bool ExtraRest(ref bool __result) { __result = false; return false; }
    private static bool sawRestReward;
    private static void ObserveRestRewards(List<MegaCrit.Sts2.Core.Rewards.Reward> rewards) => sawRestReward |= rewards.Any(reward => reward is MegaCrit.Sts2.Core.Rewards.CardReward);
    private sealed class TakeRestReward : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect) => Task.FromResult(options.Take(minSelect));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives) => new() { card = options[0].Card };
    }
    private static void Ok(JigsawResult result) => Require(result.Success, result.Reason);

    private static JigsawRun Prepare(JigsawRoom room, bool hunted, ulong[] players, bool stopBeforeTarget = false)
    {
        var map = JigsawRun.Create(75, players: players);
        foreach (var player in players) Ok(map.ChooseEnd(player, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        foreach (var player in players) Ok(map.CompleteAncient(player));
        foreach (var player in players) Ok(map.Continue(player));
        var queue = new Queue<int>(); queue.Enqueue(0);
        var previous = new Dictionary<int, int> { [0] = -1 };
        while (queue.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(e => e.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if ((map.Nodes.Single(n => n.Id == next).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss) || map.Nodes.Single(n => n.Id == next).Room == room) && previous.TryAdd(next, current)) queue.Enqueue(next);
            }
        var target = map.Nodes.First(n => n.Room == room && previous.ContainsKey(n.Id) && previous[n.Id] != 0).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        Ok(map.SetRemainingSteps(hunted ? 1 : 100));
        foreach (int node in path)
        {
            if (stopBeforeTarget && node == target) break;
            if (!hunted && node == target) Ok(map.SetRemainingSteps(1));
            var entry = map.BeginEnter(node)!; Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        if (!stopBeforeTarget) Require(map.CurrentRoomHunted == hunted, "Public room entry establishes the punished or safe root snapshot.");
        return map;
    }

    private static async Task WithRoom(JigsawRoom room, bool hunted, Func<JigsawNativeSession, Task> test, int count = 1)
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
        var native = RunState.CreateForTest(players: players, acts: [ModelDb.Act<Overgrowth>()], seed: "HUNTING_82");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        MegaCrit.Sts2.Core.Context.LocalContext.NetId = 1;
        var viewport = ((SceneTree)Engine.GetMainLoop()).Root; var previousSize = viewport.Size; var previousScale = viewport.ContentScaleSize; viewport.Size = viewport.ContentScaleSize = new Vector2I(1920, 1080);
        var host = new Harmony("AllMatters.Tests.HuntingNative");
        host.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.NGame), "SetScreenShakeTarget"), prefix: new HarmonyMethod(typeof(JigsawHuntingNativeTests), nameof(SkipProfile)));
        host.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Saves.SaveManager), nameof(MegaCrit.Sts2.Core.Saves.SaveManager.SaveProgressFile)), prefix: new HarmonyMethod(typeof(JigsawHuntingNativeTests), nameof(SkipProfile)));
        try
        {
            RunManager.Instance.GenerateRooms(); await RunManager.Instance.GenerateMap();
            await RunManager.Instance.EnterMapCoord(native.Map.StartingMapPoint.coord);
            var map = Prepare(room, hunted, players.Select(p => p.NetId).ToArray());
            await RunManager.Instance.EnterRoom(room == JigsawRoom.Shop ? new MerchantRoom() : room == JigsawRoom.Rest ? new RestSiteRoom() : room == JigsawRoom.Unknown ? new EventRoom(ModelDb.AllEvents.Single(model => model.Id.Entry == "ALL_MATTERS_EVENT_CLERIC")) : new CombatRoom(native.Act.BossEncounter.ToMutable(), native));
            JigsawCheckpoint.Capture(native, map);
            JigsawNativeSession.Restore(native);
            var session = JigsawNativeSession.Current!;
            await test(session);
        }
        finally { host.UnpatchAll(host.Id); MegaCrit.Sts2.Core.Combat.CombatManager.Instance.Reset(true); RunManager.Instance.CleanUp(false); viewport.Size = previousSize; viewport.ContentScaleSize = previousScale; }
    }

    private static async Task Shop()
    {
        await WithRoom(JigsawRoom.Shop, true, async session =>
        {
            var player = session.Native.Players[0]; player.Gold = 20000;
            await JigsawHuntingNative.SynchronizeMasks(session);
            await JigsawHuntingNative.SynchronizeMasks(session);
            Require(session.Native.Players.All(p => p.Relics.OfType<ArchitectMask>().Count() == 1), "Each original role, including an absent role, receives exactly one mask.");
            var mask = player.Relics.OfType<ArchitectMask>().Single();
            Require(!mask.IsTradable && !mask.IsAllowed(session.Native) && !mask.IsAllowedInShops, "The mask is neither tradable nor obtainable from normal pools or shops.");
            RegisterScripts();
            HeadlessUiHost.EnsureInputActions();
            if (!InputMap.HasAction(MegaInput.back)) InputMap.AddAction(MegaInput.back);
            using var uiHost = new JigsawNativeMapUiHost();
            _ = await uiHost.Open(session);
            session.HideMap();
            var hotkeys = new NHotkeyManager();
            AccessTools.Property(typeof(MegaCrit.Sts2.Core.Nodes.NGame), "HotkeyManager").SetValue(MegaCrit.Sts2.Core.Nodes.NGame.Instance, hotkeys);
            await HeadlessUiHost.Mount(hotkeys);
            var root = ResourceLoader.Load<PackedScene>("res://scenes/rooms/merchant_room.tscn").Instantiate<NMerchantRoom>();
            var shopUi = root.GetNode<NMerchantInventory>("%Inventory");
            shopUi.GetParent().RemoveChild(shopUi); root.Free();
#if STS2_PUBLIC
            var consoleField = AccessTools.Field(typeof(MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole), "_instance");
            object? previousConsole = consoleField.GetValue(null);
            var console = new MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole { Visible = false };
            consoleField.SetValue(null, console);
#endif
            var contextHost = new Harmony("AllMatters.Tests.HuntingShopContext");
            activeUi = shopUi;
            contextHost.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext.ActiveScreenContext), "GetCurrentScreen"), prefix: new HarmonyMethod(typeof(JigsawHuntingNativeTests), nameof(UiContext)));
            try
            {
                await HeadlessUiHost.Mount(shopUi);
                var realInventory = ((MerchantRoom)session.Native.CurrentRoom!).Inventories[0];
                string realStock = JsonSerializer.Serialize(JigsawNativeShopPersistence.Capture(realInventory));
                shopUi.Initialize(realInventory, MerchantRoom.Dialogue);
                var slots = shopUi.FindChildren("*", "", true, false).OfType<NMerchantSlot>().ToArray();
                Require(slots.Length > 3 && slots.Where(slot => slot is not NMerchantCardRemoval).All(slot => !slot.Visible), "The actual native merchant scene hides all goods.");
                var removal = slots.OfType<NMerchantCardRemoval>().Single();
                Require(removal.Visible && removal.FocusMode == Control.FocusModeEnum.None && !removal.GetNode<Control>("Cost").Visible && !realInventory.CardRemovalEntry.Used, "The real removal scene uses its disabled Used appearance without changing Used.");
                Require(JsonSerializer.Serialize(JigsawNativeShopPersistence.Capture(realInventory)) == realStock, "Mounting and refreshing the real restricted scene never changes native inventory.");
                var back = shopUi.GetNode<NBackButton>("%BackButton");
                shopUi.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                shopUi.GetNode<Control>("%SlotsContainer").Position = new Vector2(0, 80);
                shopUi.UnblockInput();
                back.Enable();
                Require(ReferenceEquals(shopUi.DefaultFocusedControl, back), "Controller default focus remains on the reachable native Back button.");
                AccessTools.Property(typeof(NMerchantInventory), "IsOpen").SetValue(shopUi, true);
                bool closed = false; shopUi.InventoryClosed += () => closed = true;
                ((SceneTree)Engine.GetMainLoop()).Root.PushInput(new InputEventAction { Action = MegaInput.cancel, Pressed = true }, true);
                await ((SceneTree)Engine.GetMainLoop()).ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
                ((SceneTree)Engine.GetMainLoop()).Root.PushInput(new InputEventAction { Action = MegaInput.cancel, Pressed = false }, true);
                await ((SceneTree)Engine.GetMainLoop()).ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
                Require(closed && !shopUi.IsOpen, "A real Godot cancel action closes the restricted native merchant inventory.");

            }
            finally
            {
                shopUi.Free(); hotkeys.Free(); activeUi = null; contextHost.UnpatchAll(contextHost.Id);
#if STS2_PUBLIC
                consoleField.SetValue(null, previousConsole); console.Free();
#endif
            }
            var inventory = ((MerchantRoom)session.Native.CurrentRoom!).Inventories[0];
            int gold = player.Gold, cards = player.Deck.Cards.Count;
            foreach (var entry in inventory.CardEntries) { AccessTools.Field(typeof(MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry), "_cost").SetValue(entry, 0); Require(!await entry.OnTryPurchaseWrapper(inventory), "A free card is still forbidden."); }
            foreach (var entry in inventory.RelicEntries) Require(!await entry.OnTryPurchaseWrapper(inventory), "Relics cannot be purchased.");
            foreach (var entry in inventory.PotionEntries) Require(!await entry.OnTryPurchaseWrapper(inventory), "Potions cannot be purchased.");
            Require(!await inventory.CardRemovalEntry!.OnTryPurchaseWrapper(inventory, true, true), "Free removal is forbidden before its selection flow.");
            Require(!await RunManager.Instance.OneOffSynchronizer.DoLocalMerchantCardRemoval(0), "The direct removal sender is forbidden.");
            Require(player.Gold == gold && player.Deck.Cards.Count == cards && !inventory.CardRemovalEntry.Used, "Rejected actions preserve gold, deck and Used.");
            // Price changes above are test inputs; the blocked purchases themselves retain stock.
            Require(inventory.CardEntries.All(e => e.IsStocked) && inventory.RelicEntries.All(e => e.IsStocked) && inventory.PotionEntries.All(e => e.IsStocked), "The real stock stays present behind the restricted presentation.");
        });
        await WithRoom(JigsawRoom.Shop, false, async session =>
        {
            var inventory = ((MerchantRoom)session.Native.CurrentRoom!).Inventories[0];
            session.Native.Players[0].Gold = 20000;
            Require(await inventory.CardEntries.First().OnTryPurchaseWrapper(inventory), "The exhausted safe root retains normal shop services.");
        });
    }

    private sealed class ExtraOption(Player player) : RestSiteOption(player)
    {
        internal int Executions;
        public override string OptionId => "SMITH";
        public override Task<bool> OnSelect() { Executions++; return Task.FromResult(true); }
    }
    private static async Task Rest()
    {
        await WithRoom(JigsawRoom.Rest, true, async session =>
        {
            var player = session.Native.Players[0];
            var sync = RunManager.Instance.RestSiteSynchronizer;
            var options = (List<RestSiteOption>)sync.GetOptionsForPlayer(player);
            Require(!options.Any(o => o is MendRestSiteOption), "Single-player generation does not invent Mend.");
            var extra = new ExtraOption(player); options.Add(extra);
            int history = session.Native.CurrentMapPointHistoryEntry!.GetEntry(player.NetId).RestSiteChoices.Count;
            var choose = AccessTools.Method(sync.GetType(), "ChooseOption");
            Require(!await (Task<bool>)choose.Invoke(sync, [player, options.Count - 1])! && extra.Executions == 0, "An added option is rejected before OnSelect.");
            int smith = options.FindIndex(o => o is SmithRestSiteOption);
            Require(!await (Task<bool>)choose.Invoke(sync, [player, smith])!, "Smith is blocked at the receiver boundary.");
            Require(session.Native.CurrentMapPointHistoryEntry.GetEntry(player.NetId).RestSiteChoices.Count == history, "Rejected options do not write rest history.");
            RegisterScripts();
            var button = NRestSiteButton.Create(options[smith]);
            try
            {
                HeadlessUiHost.EnsureInputActions();
                await HeadlessUiHost.Mount(button);
                Require((bool)AccessTools.Field(typeof(NRestSiteButton), "_isUnclickable").GetValue(button)!, "The actual native button uses its disabled execution state.");
                var shader = (ShaderMaterial)button.GetNode<TextureRect>("%Icon").Material;
                Require(shader.GetShaderParameter("s").AsSingle() == 0 && shader.GetShaderParameter("v").AsSingle() == 0.6f, "The real rest icon uses native gray disabled styling.");
                var position = button.GetGlobalRect().GetCenter();
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = position, GlobalPosition = position });
                await ((SceneTree)Engine.GetMainLoop()).ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = position, GlobalPosition = position });
                await ((SceneTree)Engine.GetMainLoop()).ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
                Require(session.Native.CurrentMapPointHistoryEntry.GetEntry(player.NetId).RestSiteChoices.Count == history, "A real Godot click cannot select the forbidden option.");
            }
            finally { button.Free(); }
            var extras = new Harmony("AllMatters.Tests.HuntingRestExtras");
            extras.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.ShouldDisableRemainingRestSiteOptions)), prefix: new HarmonyMethod(typeof(JigsawHuntingNativeTests), nameof(ExtraRest)));
            extras.Patch(AccessTools.Method(typeof(RewardsCmd), nameof(RewardsCmd.OfferCustom)), prefix: new HarmonyMethod(typeof(JigsawHuntingNativeTests), nameof(ObserveRestRewards)));
            try
            {
            using var cardSelector = CardSelectCmd.UseSelector(new TakeRestReward());
            await RelicCmd.Obtain<MegaCrit.Sts2.Core.Models.Relics.RegalPillow>(player);
            await RelicCmd.Obtain<MegaCrit.Sts2.Core.Models.Relics.DreamCatcher>(player);
            sawRestReward = false;
            options.Add(new HealRestSiteOption(player));
            player.Creature.SetCurrentHpInternal(10);
            int hp = player.Creature.CurrentHp;
            int expectedHeal = (int)HealRestSiteOption.GetHealAmount(player);
            Require(expectedHeal > (int)HealRestSiteOption.GetBaseHealAmount(player.Creature), "The real Regal Pillow modifies the native heal amount.");
            Require(await sync.ChooseLocalOption(options.FindIndex(o => o is HealRestSiteOption)) && player.Creature.CurrentHp == Math.Min(player.Creature.MaxHp, hp + expectedHeal), "Actual native healing preserves the real relic modifier.");
            Require(sawRestReward, "The real Dream Catcher reward reaches the native custom-reward offer during punished healing.");
            Require(session.Map!.CurrentRoomHunted && options.Any(o => o is HealRestSiteOption), "An extra rest operation remains available under the same root snapshot.");
            Require(await sync.ChooseLocalOption(options.FindIndex(o => o is HealRestSiteOption)), "The second native Heal remains usable when the native hook grants extra operations.");
            Require(!await (Task<bool>)choose.Invoke(sync, [player, options.IndexOf(extra)])! && extra.Executions == 0, "Extra-operation capacity never authorizes the forbidden added action.");
            }
            finally { extras.UnpatchAll(extras.Id); }
        });
    }



    private static async Task MapNotice()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("relic_atlas");
        var tree = (SceneTree)Engine.GetMainLoop();
        async Task CheckNotice(JigsawRun map, string expected, bool release = false)
        {
            var view = AllMatters.Jigsaw.Presentation.JigsawScreen.Create(map, 1, () => { }, () => Task.CompletedTask, () => Task.CompletedTask, _ => Task.CompletedTask, () => { }, new AllMatters.Jigsaw.Presentation.MemoryJigsawTutorialProfile { Sentence = 6 });
            try
            {
                await HeadlessUiHost.Mount(view);
                var notice = (Label)AccessTools.Field(typeof(AllMatters.Jigsaw.Presentation.JigsawScreen), "explorationNotice").GetValue(view)!;
                Require(notice.Visible && notice.Text == expected, "The real exploration notice follows the current hunting lifecycle.");
                if (release)
                {
                    view.ShowNotice(new MegaCrit.Sts2.Core.Localization.LocString("static_hover_tips", "AM_JIGSAW_HUNTING_RELEASED").GetFormattedText());
                    await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                    Require(notice.Text.Length > 0, "The final Boss release message is visible.");
                    view.AdvanceTime(5);
                    await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                    Require(notice.Text.Length == 0, "The five-second release message expires without a stale zero-step instruction.");
                }
            }
            finally { view.Free(); }
        }
        var safe = Prepare(JigsawRoom.Rest, false, [1UL]);
        await CheckNotice(safe, new MegaCrit.Sts2.Core.Localization.LocString("static_hover_tips", "AM_JIGSAW_EXHAUSTED").GetFormattedText());
        Ok(safe.RecordRoomCompletion(1));
        await CheckNotice(safe, "");
        var timely = Prepare(JigsawRoom.Boss, false, [1UL]);
        Require(timely.BossReachedOnTime, "The actual last-step first Boss is timely.");
        await CheckNotice(timely, "");
        var hunted = Prepare(JigsawRoom.Boss, true, [1UL]);
        Require(hunted.CompleteMapBossVictory(), "The last map Boss clears hunting.");
        await CheckNotice(hunted, "", release: true);
    }

    private static Task CheckpointReader()
    {
        AllMatters.Tests.ExplorationRecoveryTests.CreateSnapshotFromSave();
        AllMatters.Tests.ExplorationRecoveryTests.DetectHuntingPhase();
        AllMatters.Tests.ExplorationRecoveryTests.HuntingStateSurvivesRestore();
        return Task.CompletedTask;
    }

    private static Task ZeroBossEntry() => WithRoom(JigsawRoom.Shop, false, async original =>
    {
        var map = Prepare(JigsawRoom.Boss, false, [1UL], stopBeforeTarget: true);
        JigsawCheckpoint.Capture(original.Native, map); JigsawNativeSession.Restore(original.Native);
        var session = JigsawNativeSession.Current!;
        RunManager.Instance.ActionExecutor.Unpause();
        Ok(await JigsawEditorAction.SetRemainingSteps(session, 0));
        Require(!session.Map!.HuntingActive && original.Native.Players.All(player => !player.Relics.OfType<ArchitectMask>().Any()), "The command at a completed map-selection point issues no mask.");
        var io = new MegaCrit.Sts2.Core.Saves.Test.MockGodotFileIo("hunting82-zero-memory-only");
        var saves = new MegaCrit.Sts2.Core.Saves.Managers.RunSaveManager(82, io, new MegaCrit.Sts2.Core.Saves.Migrations.MigrationManager(io), forceSynchronous: true);
        await saves.SaveRun(RunManager.Instance.ToSave(null), false);
        RunManager.Instance.CleanUp(false);
        var save = saves.LoadRunSave().SaveData!;
        var native = RunState.FromSerializable(save);
        await RunManager.Instance.SetUpSavedSingleplayer(native, save);
        AccessTools.Property(typeof(RunManager), nameof(RunManager.ShouldSave)).SetValue(RunManager.Instance, false);
        await RunManager.Instance.GenerateMap();
        JigsawNativeSession.Restore(native); session = JigsawNativeSession.Current!;
        Require(!session.Map!.HuntingActive && session.Map.RemainingSteps == 0 && !native.Players[0].Relics.OfType<ArchitectMask>().Any(), "The complete native selection save restores without premature mask issuance.");
        int boss = session.Map.Nodes.Single(node => node.Room == JigsawRoom.Boss).Id;
        await session.Enter(boss);
        Require(native.CurrentRoom is CombatRoom && session.Map.HuntingActive && session.Map.CurrentRoomHunted && !session.Map.BossReachedOnTime && native.Players[0].Relics.OfType<ArchitectMask>().Count() == 1,
            "The actual next first-Boss native entry is immediately punished, has one mask, and is not timely.");
        await saves.SaveRun(RunManager.Instance.ToSave(null), false);
        var entered = RunState.FromSerializable(saves.LoadRunSave().SaveData!);
        JigsawNativeSession.Restore(entered);
        Require(JigsawNativeSession.Current!.Map is { HuntingActive: true, CurrentRoomHunted: true, BossReachedOnTime: false } && entered.Players[0].Relics.OfType<ArchitectMask>().Count() == 1,
            "A complete native post-entry save restores the same punished Boss and unique mask.");
    });

    private static Task EventService() => WithRoom(JigsawRoom.Unknown, true, async session =>
    {
        var player = session.Native.Players[0]; player.Gold = 35; player.Creature.SetMaxHpInternal(83); player.Creature.SetCurrentHpInternal(10);
        var model = RunManager.Instance.EventSynchronizer.GetEventForPlayer(player)!;
        Require(session.Map!.CurrentRoomHunted && !JigsawHuntingServices.RestBlocked(player) && !JigsawHuntingServices.ShopBlocked(player), "An ordinary event root does not inherit the native shop/rest service restriction.");
        await model.CurrentOptions.Single(option => option.TextKey.EndsWith(".HEAL")).Chosen();
        Require(player.Gold == 0 && player.Creature.CurrentHp == 30 && model.IsFinished, "The hunted ordinary Cleric event still charges and heals through its native event choice.");
    });

    private static MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen? restMapScreen;
    private static bool RestMapInstance(ref MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen __result) { __result = restMapScreen!; return false; }
    private static bool RestFtueSeen(ref bool __result) { __result = true; return false; }
    private static Task RestExit() => WithRoom(JigsawRoom.Rest, true, async session =>
    {
        await JigsawHuntingNative.SynchronizeMasks(session);
        int previousRoot = session.Map!.CurrentNode!.Value;
        var sync = RunManager.Instance.RestSiteSynchronizer;
        var options = (List<RestSiteOption>)sync.GetLocalOptions();
        options.RemoveAll(option => option is HealRestSiteOption or MendRestSiteOption);
        Require(options.Count > 0 && !JigsawHuntingServices.RestChoicesRemain(session.Native.Players[0]), "The original native rest choices contain only forbidden actions.");
#if !STS2_PUBLIC
        Task finished = sync.AfterAllRestSitesCompleted();
        Require(!finished.IsCompleted, "Native rest completion is initially pending.");
#endif
        RegisterScripts(); HeadlessUiHost.EnsureInputActions();
        if (!InputMap.HasAction("debug_hide_rest_site")) InputMap.AddAction("debug_hide_rest_site");
        using var ui = new JigsawNativeMapUiHost();
        var screen = await ui.Open(session); session.HideMap();
        var shake = new MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.NScreenShake();
        AccessTools.Field(typeof(MegaCrit.Sts2.Core.Nodes.NGame), "_screenShake").SetValue(MegaCrit.Sts2.Core.Nodes.NGame.Instance, shake);
        var hotkeys = new NHotkeyManager();
        AccessTools.Property(typeof(MegaCrit.Sts2.Core.Nodes.NGame), "HotkeyManager").SetValue(MegaCrit.Sts2.Core.Nodes.NGame.Instance, hotkeys);
        await HeadlessUiHost.Mount(hotkeys);
        var harmony = new Harmony("AllMatters.Tests.HuntingRestExit");
        harmony.Patch(AccessTools.Method(typeof(MegaCrit.Sts2.Core.Saves.SaveManager), "SeenFtue"), prefix: new HarmonyMethod(typeof(JigsawHuntingNativeTests), nameof(RestFtueSeen)));
        restMapScreen = new();
        restMapScreen.Visible = false;
        AccessTools.Field(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen), "_runState").SetValue(restMapScreen, session.Native);
        harmony.Patch(AccessTools.PropertyGetter(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen), "Instance"), prefix: new HarmonyMethod(typeof(JigsawHuntingNativeTests), nameof(RestMapInstance)));
        var rest = ResourceLoader.Load<PackedScene>("res://scenes/rooms/rest_site_room.tscn").Instantiate<NRestSiteRoom>();
        AccessTools.Field(typeof(NRestSiteRoom), "_room").SetValue(rest, session.Native.CurrentRoom);
        AccessTools.Field(typeof(NRestSiteRoom), "_runState").SetValue(rest, session.Native);
        try
        {
            bool testing = TestMode.IsOn;
            try { TestMode.IsOn = false; await HeadlessUiHost.Mount(rest); }
            finally { TestMode.IsOn = testing; }
            Require(rest.ProceedButton.IsEnabled && restMapScreen.IsTravelEnabled, "The actual native rest scene enables Proceed and map travel with no legal choice.");
            var tree = (SceneTree)Engine.GetMainLoop();
            for (int i = 0; i < 60; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var point = rest.ProceedButton.GetGlobalRect().GetCenter();
            tree.Root.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            tree.Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point, GlobalPosition = point });
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            tree.Root.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point });
            for (int i = 0; i < 4; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Require(screen.Visible && session.Map.CurrentNode == null && session.Map.Completed.Contains(previousRoot), "The real native Proceed callback opens the production puzzle map and completes the departed root.");
            rest.Free();
            ui.ReleaseSceneOwnership();
            await session.Enter(session.Map!.AvailableDestinations.First());
#if !STS2_PUBLIC
            Require(finished.IsCompletedSuccessfully && options.Count == 0, "Actual native beta room exit skips remaining forbidden choices and releases its completion task.");
#endif
            Require(session.Native.CurrentRoom is not RestSiteRoom, "The actual native room exit enters the destination without requiring a forbidden choice.");
        }
        finally { if (GodotObject.IsInstanceValid(rest)) rest.Free(); hotkeys.Free(); shake.Free(); restMapScreen.Free(); restMapScreen = null; harmony.UnpatchAll(harmony.Id); }
    });

    private static Task Legacy() => WithRoom(JigsawRoom.Rest, false, async session =>
    {
        var io = new MegaCrit.Sts2.Core.Saves.Test.MockGodotFileIo("hunting82-legacy-memory-only");
        var saves = new MegaCrit.Sts2.Core.Saves.Managers.RunSaveManager(82, io, new MegaCrit.Sts2.Core.Saves.Migrations.MigrationManager(io), forceSynchronous: true);
        string path = MegaCrit.Sts2.Core.Saves.Managers.RunSaveManager.GetRunSavePath(82, "current_run.save");
        await saves.SaveRun(RunManager.Instance.ToSave(null), false);
        await saves.SaveRun(RunManager.Instance.ToSave(null), false);
        var original = System.Text.Json.Nodes.JsonNode.Parse(io.ReadFile(path))!;
        System.Text.Json.Nodes.JsonObject Find(System.Text.Json.Nodes.JsonNode node)
        {
            if (node is System.Text.Json.Nodes.JsonObject obj)
            {
                if (obj["jigsaw"] is { } entry) return entry["data"]!.AsObject();
                foreach (var field in obj) if (field.Value != null) { var found = FindOrNull(field.Value); if (found != null) return found; }
            }
            throw new InvalidOperationException("Expected actual native jigsaw entry.");
        }
        System.Text.Json.Nodes.JsonObject? FindOrNull(System.Text.Json.Nodes.JsonNode node)
        {
            try { return Find(node); } catch (InvalidOperationException) { return null; }
        }
        foreach (bool backup in new[] { false, true })
        {
            var legacy = original.DeepClone(); var checkpoint = Find(legacy);
            checkpoint["Version"] = 1; checkpoint["Map"]!.AsObject().Remove("Hunting");
            io.SetFileContent(path, legacy.ToJsonString()); io.SetFileContent(path + ".backup", legacy.ToJsonString());
            if (backup)
            {
                var corrupt = original.DeepClone(); corrupt["schema_version"] = new System.Text.Json.Nodes.JsonObject { ["invalid"] = true };
                io.SetFileContent(path, corrupt.ToJsonString());
            }
            var loaded = saves.LoadRunSave(); Require(loaded.Success && loaded.SaveData != null, "Native primary or backup parsing retains the old complete point.");
            var restored = RunState.FromSerializable(loaded.SaveData!);
            Require(JigsawCheckpoint.Slot.TryGet(restored, out var point) && point.Map!.Hunting is { Active: false, RootHunted: false } migrated && migrated.RemainingSteps == point.Map.Budget - point.Map.Steps,
                "A selected old point below exhaustion migrates its provable balance and safe root.");
        }
        await saves.SaveRun(RunManager.Instance.ToSave(null), false);
        await saves.SaveRun(RunManager.Instance.ToSave(null), false);
        var ambiguous = original.DeepClone(); var old = Find(ambiguous); old["Version"] = 1; old["Map"]!.AsObject().Remove("Hunting"); old["Map"]!["Steps"] = session.Map!.Budget;
        io.SetFileContent(path, ambiguous.ToJsonString()); io.SetFileContent(path + ".backup", ambiguous.ToJsonString());
        string main = io.ReadFile(path), previousBackup = io.ReadFile(path + ".backup");
        bool rejected = false;
        try { _ = RunState.FromSerializable(saves.LoadRunSave().SaveData!); }
        catch (InvalidDataException error) { rejected = error.Message.Contains("追猎边界", StringComparison.Ordinal); }
        Require(rejected && io.ReadFile(path) == main && io.ReadFile(path + ".backup") == previousBackup, "An ambiguous old exhausted point explicitly rejects without rewriting the primary or backup.");
        var missing = original.DeepClone(); Find(missing)["Map"]!.AsObject().Remove("Hunting");
        io.SetFileContent(path, missing.ToJsonString());
        rejected = false;
        try { _ = RunState.FromSerializable(saves.LoadRunSave().SaveData!); } catch (InvalidDataException) { rejected = true; }
        Require(rejected, "Version two missing its required hunting fact never enters legacy inference.");
        foreach (int version in new[] { 2, 999 })
        {
            var invalidMap = session.Map!.CreateSave() with { Hunting = version == 2 ? null : session.Map.CreateSave().Hunting };
            JigsawCheckpoint.Slot.Set(session.Native, new() { Version = version, Act = session.Native.CurrentActIndex, Map = invalidMap });
            var recovery = SaveStateRecovery.CreateForTest((out JigsawCheckpointData? point) => { JigsawCheckpoint.Slot.TryGet(session.Native, out var savedPoint); point = savedPoint; return true; });
            var result = recovery.TryInitialize();
            Require(!result.Success && !recovery.IsInitialized, "The auxiliary save-state reader rejects the same incomplete or unknown outer version.");
        }
    });

    private static Task BossVictory() => WithRoom(JigsawRoom.Boss, true, async session =>
    {
        var player = session.Native.Players[0]; var map = session.Map!;
        var room = (CombatRoom)session.Native.CurrentRoom!;
        await JigsawHuntingNative.SynchronizeMasks(session);
        Require(map.HuntingActive && map.CurrentRoomHunted && player.Relics.OfType<ArchitectMask>().Count() == 1, "The actual map Boss begins with the hunted snapshot and mask.");
        // A victory notification for another room (including an event's named Boss) cannot release this root.
        await MegaCrit.Sts2.Core.Hooks.Hook.AfterCombatVictory(session.Native, null, new CombatRoom(session.Native.Act.BossEncounter.ToMutable(), session.Native));
        Require(map.HuntingActive && player.Relics.OfType<ArchitectMask>().Count() == 1, "An unrelated combat victory cannot release the map Boss mask.");
        await MegaCrit.Sts2.Core.Hooks.Hook.AfterCombatVictory(session.Native, room.CombatState, room);
        Require(!map.HuntingActive && !map.CurrentRoomHunted && !player.Relics.OfType<ArchitectMask>().Any(), "The awaited native victory hook removes both the mask and punished root before rewards.");
        Require(AllMatters.Jigsaw.Presentation.JigsawStepCounter.DescriptionKey(map) != "AM_JIGSAW_EXHAUSTED", "Cleared final Boss rewards no longer describe punishment in the zero-step hover.");
        room.MarkPreFinished();
        var io = new MegaCrit.Sts2.Core.Saves.Test.MockGodotFileIo("hunting82-boss-memory-only");
        var saves = new MegaCrit.Sts2.Core.Saves.Managers.RunSaveManager(82, io, new MegaCrit.Sts2.Core.Saves.Migrations.MigrationManager(io), forceSynchronous: true);
        await saves.SaveRun(RunManager.Instance.ToSave(room), false);
        var saved = saves.LoadRunSave();
        Require(saved.Success && saved.SaveData != null, "The actual native victory reward point roundtrips through isolated JSON storage.");
        var restored = RunState.FromSerializable(saved.SaveData!);
        Require(JigsawCheckpoint.Slot.TryGet(restored, out var checkpoint) && checkpoint.Map!.Hunting is { Active: false, RootHunted: false } &&
            !restored.Players[0].Relics.OfType<ArchitectMask>().Any(), "Restoring the native reward point does not return the cleared mask or punishment.");
    });

    private static Task Command() => WithRoom(JigsawRoom.Shop, false, async session =>
    {
        var player = session.Native.Players[0]; var map = session.Map!; var command = new JigsawStepsCommand();
        string before = JsonSerializer.Serialize(map.CreateSave());
        foreach (var args in new string[][] { [], ["1", "2"], ["-1"], ["1.5"], ["x"], ["2147483648"] })
            Require(!command.Process(player, args).success && JsonSerializer.Serialize(map.CreateSave()) == before, "Invalid arguments atomically retain all domain facts.");
        bool ordered = false;
        RunManager.Instance.ActionExecutor.BeforeActionExecuted += _ => ordered = true;
        int steps = map.Steps, progress = map.FirstProgress;
        foreach (string value in new[] { "0", "1", "40", "2147483647" })
        {
            var result = command.Process(player, [value]);
            Require(result.success && result.task != null, "The host submits a real ordered command.");
            await result.task!;
            Require(map.RemainingSteps == int.Parse(value) && map.Steps == steps && map.FirstProgress == progress && !map.HuntingActive, "Control balance changes neither progress nor the current root safety.");
        }
        Require(ordered && command.DebugOnly, "Native queued execution retains the inherited debug boundary.");
    });
}
