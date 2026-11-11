using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawMerchantExitTests
{
    internal const string Name = "Jigsaw second act merchant native exit permits actual map input";
    private static NMapScreen? mapScreen;
    private static bool MapInstance(ref NMapScreen __result) { __result = mapScreen!; return false; }
    private static bool SeenMerchant(ref bool __result) { __result = false; return false; }
    internal static async Task Run() { await Run(false); await Run(true); }
    private static async Task Run(bool topBar)
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        using var scope = HeadlessCombatInProgress.EnterEvent();
        HeadlessUiHost.EnsureInputActions();
        var tree = (SceneTree)Engine.GetMainLoop();
        var previousSize = tree.Root.Size;
        tree.Root.Size = new(1280, 720);
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
        var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>()], seed: "SHOP_EXIT_ACT2");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        LocalContext.NetId = 1;
        JigsawNativeSaveHost.Save = _ => Task.CompletedTask;
        var harmony = new Harmony("AllMatters.Tests.MerchantExit");
        JigsawScreen? view = null; JigsawNativeMapUiHost? nativeUi = null;
        using var merchantView = new NMerchantRoom();
        mapScreen = new NMapScreen();
        try
        {
            native.CurrentActIndex = 1;
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            native.AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.Ancient, RoomType.Event, null);
            native.AddVisitedMapCoord(native.Map.StartingMapPoint.coord);
            var map = Enumerable.Range(0, 1000).Select(seed => JigsawRun.Create(seed)).First(candidate => candidate.Pool[0].Nodes[0].Room == JigsawRoom.Shop);
            Require(map.Take(1, 0).Success && map.Drop(1, new(3, 0)).Success && map.AddEdge(1, 0, 1).Success && map.ChooseEnd(1, EndChoice.Manual).Success && map.Inspect().Success && map.CompleteAncient(1).Success && map.Continue(1).Success, "Prepare generated second-act exploration.");
            JigsawCheckpoint.Capture(native, map);
            JigsawNativeSession.Restore(native);
            var session = JigsawNativeSession.Current!; map = session.Map!;
            await session.Enter(1);
            Require(native.CurrentRoom is MerchantRoom, "Enter a real merchant room.");
            player.Gold = 20000;
            var merchantRoom = (MerchantRoom)native.CurrentRoom!;
            var inventory = merchantRoom.Inventories.Single();
            int deck = player.Deck.Cards.Count;
            Require(await inventory.CardEntries.First().OnTryPurchaseWrapper(inventory) && player.Deck.Cards.Count == deck + 1, "Perform a real native purchase before leaving.");
            nativeUi = new();
            view = await nativeUi.Open(session);
            view.Hide();
            // Only global scene lookup and account FTUE persistence are supplied by the host;
            // the real merchant callback, map interception and engine input remain production.
            harmony.Patch(AccessTools.PropertyGetter(typeof(NMapScreen), nameof(NMapScreen.Instance)), prefix: new HarmonyMethod(typeof(JigsawMerchantExitTests), nameof(MapInstance)));
            harmony.Patch(AccessTools.Method(typeof(NMerchantRoom), "MerchantFtueCheck"), prefix: new HarmonyMethod(typeof(JigsawMerchantExitTests), nameof(SeenMerchant)));
            AccessTools.Property(typeof(NMapScreen), nameof(NMapScreen.IsTravelEnabled)).SetValue(mapScreen, true);
            if (topBar) mapScreen.Open(true); else AccessTools.Method(typeof(NMerchantRoom), "HideScreen").Invoke(merchantView, [null]);
            if (topBar)
            {
                Require(view.Visible && view.CanReturnToRoom && map.CurrentNode == 1 && map.RoomCompletedPlayers.Count == 0, "Opening the merchant map does not leave or complete the shop.");
                mapScreen.Open(true);
                Require(!view.Visible && ReferenceEquals(native.CurrentRoom, merchantRoom), "The top-bar map closes back to the still-open merchant room.");
                int cardsBefore = player.Deck.Cards.Count;
                Require(await inventory.CardEntries.ElementAt(1).OnTryPurchaseWrapper(inventory) && player.Deck.Cards.Count == cardsBefore + 1, "Returning from a map preview permits another real purchase.");
                mapScreen.Open(true);
            }
            else Require(view.Visible && !view.CanReturnToRoom && map.CurrentNode == null, "The native proceed callback completes the local merchant.");
            int target = map.VotingDestinations.First();
            for (int frame = 0; frame < 3; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var point = view.MapControl.GlobalPosition + view.GridToCanvas(map.Nodes.Single(node => node.Id == target).Center);
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true });
            string mapRect = view.MapControl.GetGlobalRect().ToString();
            nativeUi?.ReleaseSceneOwnership();
            for (int frame = 0; frame < 12; frame++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Require(map.Steps == 2 && (map.CurrentNode == target || map.PendingEntry?.NodeId == target), $"Actual map input after merchant exit enters next destination: steps={map.Steps}, current={map.CurrentNode}, pending={map.PendingEntry?.NodeId}, votes={string.Join('/', map.DestinationVotes)}, target={target}, point={point}, rect={mapRect}");
        }
        finally { harmony.UnpatchAll(harmony.Id); nativeUi?.Dispose(); if (GodotObject.IsInstanceValid(view)) view?.Free(); mapScreen.Free(); mapScreen = null; JigsawNativeSaveHost.Save = null; RunManager.Instance.CleanUp(false); tree.Root.Size = previousSize; }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
