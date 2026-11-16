using System.Reflection;
using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.MapDrawing;

internal static class JigsawDrawingToolsTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Jigsaw drawing tools native strokes follow camera and panorama capture restores it", NativeCanvas)
    ];

    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
    private static bool Skip() => false;
    private static void Set(object owner, string field, object value) => AccessTools.Field(owner.GetType(), field).SetValue(owner, value);

    private static Task NativeCanvas() => JigsawHuntingTreasureTests.WithTreasure(true, async (session, _) =>
    {
        foreach (var action in new[] { MegaCrit.Sts2.Core.ControllerInput.MegaInput.viewMap, MegaCrit.Sts2.Core.ControllerInput.MegaInput.pauseAndBack })
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        using var ui = new JigsawNativeMapUiHost();
        var screen = await ui.Open(session);
        screen.Size = new(1280, 720);
        using var peripheral = new HarmonyScope();
        var map = new NMapScreen();
        var drawings = new NMapDrawings { Size = new(960, 1200) };
        var tools = new Control { Name = "DrawingTools", Size = new(180, 60) };
        var draw = new NMapDrawButton { Name = "DrawButton", Position = new(-96, 0), Size = new(48, 48) };
        var erase = new NMapEraseButton { Name = "EraseButton", Position = new(-40, 0), Size = new(48, 48) };
        var clear = new NMapClearButton { Name = "ClearButton", Position = new(16, 0), Size = new(48, 48) };
        var owner = ResourceLoader.Load<PackedScene>("res://scenes/screens/map/map_drawing.tscn").Instantiate<Control>();
        var viewport = owner.GetNode<SubViewport>("DrawViewport");
        var texture = owner.GetNode<TextureRect>("DrawViewportTextureRect");
        JigsawDrawingTools? adapter = null;
        try
        {
            map.AddChild(drawings); map.AddChild(tools);
            tools.Owner = map; tools.UniqueNameInOwner = true;
            tools.AddChild(draw); draw.Owner = map; draw.UniqueNameInOwner = true;
            tools.AddChild(erase); erase.Owner = map; erase.UniqueNameInOwner = true;
            peripheral.SkipReady(typeof(NMapClearButton));
            tools.AddChild(clear); clear.Owner = map; clear.UniqueNameInOwner = true;
            var drawIcon = new TextureRect(); draw.AddChild(drawIcon); Set(draw, "_icon", drawIcon);
            var eraseIcon = new TextureRect(); erase.AddChild(eraseIcon); Set(erase, "_icon", eraseIcon);
#if !STS2_PUBLIC
            var share = new NMapShareButton { Name = "ShareButton" };
            map.AddChild(share); share.Owner = map; share.UniqueNameInOwner = true;
            var toastType = AccessTools.Field(typeof(NMapShareButton), "_toast").FieldType;
            var toast = (Node)Activator.CreateInstance(toastType)!;
            peripheral.SkipReady(toastType);
            toast.Name = "ShareToast"; map.AddChild(toast); toast.Owner = map; toast.UniqueNameInOwner = true;
#endif
            drawings.AddChild(owner);
            AccessTools.Property(typeof(NMapScreen), nameof(NMapScreen.Drawings)).SetValue(map, drawings);
            AccessTools.Property(NRun.Instance.GlobalUi.GetType(), "MapScreen").SetValue(NRun.Instance.GlobalUi, map);
            drawings.Initialize(RunManager.Instance.NetService, session.Native, RunManager.Instance.InputSynchronizer);
            var lineScene = ResourceLoader.Load<PackedScene>("res://scenes/screens/map/map_line_draw.tscn");
            var eraseScene = ResourceLoader.Load<PackedScene>("res://scenes/screens/map/map_line_erase.tscn");
            Check(lineScene != null && eraseScene != null, "The test uses packaged native stroke scenes.");
            Set(drawings, "_lineDrawScene", lineScene!); Set(drawings, "_lineEraseScene", eraseScene!);
            using var erased = eraseScene!.Instantiate<Line2D>();
            Set(drawings, "_eraserMaterial", erased.Material);
            var stateType = typeof(NMapDrawings).GetNestedType("DrawingState", BindingFlags.NonPublic)!;
            var state = Activator.CreateInstance(stateType, true)!;
            Set(state, "playerId", session.Native.Players[0].NetId);
            Set(state, "drawViewport", viewport); Set(state, "drawingTexture", texture);
            ((System.Collections.IList)AccessTools.Field(typeof(NMapDrawings), "_drawingStates").GetValue(drawings)!).Add(state);
            var imported = new SerializableMapDrawings();
            var playerDrawing = new SerializablePlayerMapDrawings { playerId = session.Native.Players[0].NetId };
            playerDrawing.lines.Add(new SerializableMapDrawingLine { mapPoints = [new(0, .15f), new(.05f, .2f)] });
            imported.drawings.Add(playerDrawing);
            drawings.LoadDrawings(imported);
            await HeadlessUiHost.Mount(map);
            screen.AttachDrawingTools(map);
            adapter = JigsawDrawingTools.For(map)!;
            Check(adapter != null, "Native tools attach to the existing puzzle canvas.");
            adapter!.Update(true);
            Check(drawings.GetSerializableMapDrawings().drawings.Single().lines.Single().mapPoints.SequenceEqual(playerDrawing.lines.Single().mapPoints),
                "A fresh native layer loaded before puzzle attachment retains its saved normalized coordinates after expansion.");
            drawings.ClearAllLines();
            Check(drawings.GetParent() == screen.MapControl && tools.GetParent() == screen,
                "The native drawing layer and toolbar move to the puzzle map.");
            var screenRect = screen.GetGlobalRect();
            foreach (var button in new Control[] { draw, erase, clear })
                Check(screenRect.Grow(-16).Encloses(button.GetGlobalRect()),
                    $"Native button {button.Name} at {button.GetGlobalRect()} must fit inside the map {screenRect}.");
            Check(draw.GetGlobalRect().Position.X - screenRect.Position.X == 32 &&
                clear.GetGlobalRect().End.Y >= screenRect.End.Y - 24,
                "The drawing tools stay at the screen's lower left.");
            Check(viewport.Size == (Vector2I)(drawings.Size / 2) && texture.Size == drawings.Size,
                "Native half-resolution strokes cover the entire expanded puzzle canvas.");
            var point = new Vector2(80, 120);
            drawings.BeginLineLocal(point, DrawingMode.Drawing);
            drawings.UpdateCurrentLinePositionLocal(point + new Vector2(30, 40)); drawings.StopLineLocal();
            drawings.BeginLineLocal(point + new Vector2(10, 10), DrawingMode.Erasing); drawings.StopLineLocal();
            var strokes = viewport.GetChildren().OfType<Line2D>().ToArray();
            Check(strokes.Length == 2 && strokes[0].Points[0] == point / 2 && strokes[1].Material == erased.Material,
                "Native drawing creates half-resolution points and native erasing creates a subtractive stroke.");
            var saved = JigsawSaveSource.Copy(RunManager.Instance.ToSave(null));
            Check(saved.MapDrawings!.drawings.Single().lines.Count == 2, "The real save contains both native strokes.");
            session.HideMap();
            session.ShowMap();
            Check(viewport.GetChildren().OfType<Line2D>().Count() == 2,
                "Returning from the native room reuses the same drawing data.");
            var controllerProperty = AccessTools.Property(NGame.Instance.InputManager.GetType(), "ControllerManager");
            var previousController = controllerProperty.GetValue(NGame.Instance.InputManager);
            var controller = (Node)Activator.CreateInstance(controllerProperty.PropertyType)!;
            controllerProperty.SetValue(NGame.Instance.InputManager, controller);
            try
            {
                adapter.Toggle(DrawingMode.Drawing);
                int votes = 0;
                screen.VoteRequested = _ => { votes++; return JigsawResult.Ok; };
                var at = screen.MapControl.GetGlobalTransformWithCanvas() * screen.GridToCanvas(new(3, 0));
                var inputTree = (SceneTree)Engine.GetMainLoop();
                inputTree.Root.PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                await inputTree.ToSignal(inputTree, SceneTree.SignalName.ProcessFrame);
                inputTree.Root.PushInput(new InputEventMouseMotion { Position = at + new Vector2(20, 0), GlobalPosition = at + new Vector2(20, 0) }, true);
                inputTree.Root.PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = false }, true);
                await inputTree.ToSignal(inputTree, SceneTree.SignalName.ProcessFrame);
                Check(viewport.GetChildren().OfType<Line2D>().Count() == 3 && votes == 0,
                    "Engine painting creates a native line while leaving map votes untouched.");
                adapter.Toggle(DrawingMode.Drawing);
            }
            finally
            {
                controllerProperty.SetValue(NGame.Instance.InputManager, previousController);
                controller.Free();
            }
            Set(screen, "pan", new Vector2(75, -31)); Set(screen, "zoom", 1.7f); adapter.Update(true);
            var before = drawings.Position; var scale = drawings.Scale;
            Check(drawings.Scale.IsEqualApprox(Vector2.One * screen.DrawingCell / 64) &&
                drawings.Position.IsEqualApprox(screen.DrawingOrigin + screen.DrawingBounds.Grow(2).Position * screen.DrawingCell),
                "Pan and zoom transform the same native drawing coordinates as puzzle cells.");
            adapter.BeginShare();
            Check(adapter.IsSharing && screen.DrawingCell == 48 && drawings.Scale.IsEqualApprox(Vector2.One * .75f),
                "Panorama capture replaces camera zoom with the full-map 48 pixel cell transform.");
            adapter.EndShare();
            Check(!adapter.IsSharing && drawings.Position.IsEqualApprox(before) && drawings.Scale.IsEqualApprox(scale),
                "Finishing native panorama capture restores the player's camera and annotations.");
#if STS2_PUBLIC
            if (System.Environment.GetEnvironmentVariable("ALLMATTERS_DRAWING_CAPTURE") is { } capture)
            {
                using var image = await adapter.CapturePanorama();
                Check(image.GetWidth() >= 1280 && image.GetHeight() >= 720 &&
                    image.SavePng(capture) == Error.Ok, "The rendered panorama exports a full-size PNG.");
                Check(!adapter.IsSharing && drawings.Position.IsEqualApprox(before), "Image export restores the live map camera.");
            }
#endif
            drawings.ClearDrawnLinesLocal();
            Check(RunManager.Instance.ToSave(null).MapDrawings!.drawings.Single().lines.Count == 0,
                "Native clear is immediately reflected in a same-frame save.");
            drawings.LoadDrawings(saved.MapDrawings);
            var restored = drawings.GetSerializableMapDrawings();
            Check(restored.drawings.Single().lines.Count == 2 &&
                restored.drawings.Single().lines[0].mapPoints.SequenceEqual(saved.MapDrawings.drawings.Single().lines[0].mapPoints),
                "Native loading restores the saved lines and normalized coordinates without repainting them.");
            drawings.ClearDrawnLinesLocal();
            erased.Free();
            var tree = (SceneTree)Engine.GetMainLoop();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        finally { adapter?.Dispose(); map.Free(); }
    });

    // Only absent scene startup and peripheral cursor refresh are suppressed; stroke creation stays native.
    private sealed class HarmonyScope : IDisposable
    {
        private readonly Harmony harmony = new("AllMatters.Tests.DrawingToolsHost");
        private readonly Node cursor;
        private readonly object? previousCursor;
        internal void SkipReady(Type type) => harmony.Patch(AccessTools.Method(type, "_Ready"),
            prefix: new HarmonyMethod(typeof(JigsawDrawingToolsTests), nameof(Skip)));
        internal HarmonyScope()
        {
            var skip = new HarmonyMethod(typeof(JigsawDrawingToolsTests), nameof(Skip));
            foreach (var type in new[] { typeof(NMapScreen), typeof(NMapDrawings), typeof(NMapDrawButton), typeof(NMapEraseButton)
#if !STS2_PUBLIC
                , typeof(NMapShareButton)
#endif
            }) harmony.Patch(AccessTools.Method(type, "_Ready"), prefix: skip);
            harmony.Patch(AccessTools.Method(typeof(NMapScreen), "_Process"), prefix: skip);
            harmony.Patch(AccessTools.Method(typeof(NMapDrawings), "UpdateLocalCursor"), prefix: skip);
            var cursorProperty = AccessTools.Property(typeof(NGame), "RemoteCursorContainer");
            previousCursor = cursorProperty.GetValue(NGame.Instance);
            cursor = (Node)Activator.CreateInstance(cursorProperty.PropertyType)!;
            cursorProperty.SetValue(NGame.Instance, cursor);
            harmony.Patch(AccessTools.Method(cursorProperty.PropertyType, "DrawingCursorStateChanged"), prefix: skip);
        }
        public void Dispose()
        {
            AccessTools.Property(typeof(NGame), "RemoteCursorContainer").SetValue(NGame.Instance, previousCursor);
            cursor.Free();
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
