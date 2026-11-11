using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawNativeProgressTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Jigsaw native progress shop revisits preserve history without advancing native consumers", ShopProgress),
        ("Jigsaw native progress Lantern Key retains native unknown and next event hooks", LanternContext),
        ("Jigsaw native progress rejects corrupt history indices and leaves ordinary runs unchanged", IsolationAndCorruption)
    ];

    private static JigsawRun Ready(JigsawRoom firstRoom)
    {
        var map = Enumerable.Range(0, 100).Select(seed => JigsawRun.Create(seed))
            .First(run => run.Pool[0].Nodes[0].Room == firstRoom);
        Check(map.Take(1, 0).Success && map.Drop(1, new(3, 0)).Success && map.AddEdge(1, 0, 1).Success &&
            map.ChooseEnd(1, EndChoice.Manual).Success && map.Inspect().Success && map.CompleteAncient(1).Success && map.Continue(1).Success,
            "A generated validated partial layout exposes the requested first room.");
        return map;
    }

    private static RunState Native() => RunState.CreateForTest(
        [Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)],
        acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "NATIVE_PROGRESS_81");

    private static async Task ShopProgress()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        using var eventScope = HeadlessCombatInProgress.EnterEvent();
        var native = Native();
        var io = new MockGodotFileIo("native-progress-81-memory-only");
        var saves = new RunSaveManager(81, io, new MigrationManager(io), forceSynchronous: true);
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        JigsawNativeSaveHost.Save = room => saves.SaveRun(RunManager.Instance.ToSave(room), false);
        try
        {
            native.ExtraFields.StartedWithNeow = true;
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            // Native Ancient history is fixture input; every shop entry below runs the real room lifecycle.
            native.AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.Ancient, RoomType.Event, null);
            native.AddVisitedMapCoord(native.Map.StartingMapPoint.coord);
            JigsawCheckpoint.Capture(native, Ready(JigsawRoom.Shop));
            JigsawNativeSession.Restore(native);
            var session = JigsawNativeSession.TryGet(native)!;
            var map = session.Map!;
            var beforeConfirmation = new List<(int Steps, int Total, int Act)>();
            void ObserveReady()
            {
                if (native.CurrentRoom is MerchantRoom && map.PendingEntry != null)
                    beforeConfirmation.Add((map.Steps, native.TotalFloor, native.ActFloor));
            }
            RunManager.Instance.RoomEntered += ObserveReady;
            await session.Enter(1);
            Check(native.CurrentRoom is MerchantRoom && map.FirstProgress == 1 && native.TotalFloor == 2 && native.ActFloor == 1,
                "The first actual native shop entry advances content progress once.");
            int firstScore = ScoreUtility.GetScoreForFloor(native.MapPointHistory);
            Check(firstScore == 20 && map.CompleteRoom(map.CreateSave().NextEntry).Success, "The first real entry counts toward native score; leave its logical shop.");
            await session.Enter(1);
            RunManager.Instance.RoomEntered -= ObserveReady;
            Check(beforeConfirmation.SequenceEqual(new[] { (0, 2, 1), (1, 2, 1) }),
                "Native consumers see the first entry and revisit floors before logical RoomEntered confirmation, without an off-by-one.");
            Check(native.CurrentRoom is MerchantRoom && native.MapPointHistory[0].Count == 3 && native.VisitedMapCoords.Count == 3 &&
                map.Steps == 2 && map.FirstProgress == 1, "A real revisit preserves another native history and coordinate entry, but one first visit.");
            Check(native.TotalFloor == 2 && native.ActFloor == 1 && ScoreUtility.GetScoreForFloor(native.MapPointHistory) == firstScore,
                $"A shop revisit must not advance native qualification, top-bar floor or floor score (total={native.TotalFloor}, act={native.ActFloor}).");
            var relic = await RelicCmd.Obtain<Lantern>(native.Players[0]);
            var card = native.CreateCard<Anger>(native.Players[0]);
            await CardPileCmd.Add(card, native.Players[0].Deck);
            Check(relic.FloorAddedToDeck == 2 && card.FloorAddedToDeck == 2 && native.MapPointHistory[0][2].GetEntry(1).RelicChoices.Count > 0,
                "Real obtain commands use first-entry floor while recording acquisition in the actual revisit history.");
            // Cross Juzu's actual native cutoff using genuine repeated shop entries.
            for (int visit = 2; visit < 42; visit++)
            {
                Check(map.CompleteRoom(map.CreateSave().NextEntry).Success, "Leave each real native shop.");
                await session.Enter(1);
            }
            Check(native.MapPointHistory[0].Count == 43 && native.TotalFloor == 2 && ModelDb.Relic<JuzuBracelet>().IsAllowed(native),
                "Preserved revisit history cannot push Juzu beyond its real native floor cutoff.");
            Check(map.CompleteRoom(map.CreateSave().NextEntry).Success, "Leave the final shop before saving.");
            await session.WaitForSave();
            await saves.SaveRun(RunManager.Instance.ToSave(null), false);
            var loaded = saves.LoadRunSave();
            Check(loaded.Success && loaded.SaveData != null, "Native save reload parses the same complete point.");
            Check(ScoreUtility.CalculateScore(loaded.SaveData!, false) == ScoreUtility.CalculateScore(native, false),
                $"Native serialized-run scoring uses the same first-entry projection. saved={ScoreUtility.CalculateScore(loaded.SaveData!, false)}, live={ScoreUtility.CalculateScore(native, false)}, savedFloor={ScoreUtility.GetScoreForFloor(loaded.SaveData!.MapPointHistory)}, liveFloor={ScoreUtility.GetScoreForFloor(native.MapPointHistory)}.");
            RunManager.Instance.CleanUp(false);
            var restored = RunState.FromSerializable(loaded.SaveData!);
            Check(restored.TotalFloor == 2 && restored.MapPointHistory[0].Count == 43 && ScoreUtility.GetScoreForFloor(restored.MapPointHistory) == firstScore,
                "Reload retains both the full history and corrected progress.");
            await RunManager.Instance.SetUpSavedSingleplayer(restored, loaded.SaveData!);
            RunManager.Instance.Launch();
            await RunManager.Instance.GenerateMap();
            await RunManager.Instance.LoadIntoLatestMapCoord(null);
            // Direct next-act test input; the destination entry itself remains native.
            restored.CurrentActIndex = 1;
            JigsawCheckpoint.Capture(restored, Ready(JigsawRoom.Shop));
            JigsawNativeSession.Restore(restored);
            await RunManager.Instance.GenerateMap();
            await JigsawNativeSession.TryGet(restored)!.Enter(1);
            Check(restored.MapPointHistory[0].Count == 43 && restored.MapPointHistory[1].Count == 1 && restored.TotalFloor == 3 &&
                ScoreUtility.GetScoreForFloor(restored.MapPointHistory) == 40,
                "The next act preserves earlier revisit offsets and uses the native per-act score multiplier.");
        }
        finally { JigsawNativeSaveHost.Save = null; RunManager.Instance.CleanUp(false); }
    }

    private static async Task LanternContext()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas");
        using var eventScope = HeadlessCombatInProgress.EnterEvent();
        var native = Native();
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        var io = new MockGodotFileIo("native-progress-lantern-memory-only");
        var saves = new RunSaveManager(81, io, new MigrationManager(io), forceSynchronous: true);
        JigsawNativeSaveHost.Save = room => saves.SaveRun(RunManager.Instance.ToSave(room), false);
        try
        {
            native.CurrentActIndex = 2;
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            await CardPileCmd.Add(native.CreateCard<LanternKey>(native.Players[0]), native.Players[0].Deck);
            JigsawCheckpoint.Capture(native, Ready(JigsawRoom.Unknown));
            JigsawNativeSession.Restore(native);
            await JigsawNativeSession.TryGet(native)!.Enter(1);
            Check(native.CurrentRoom is EventRoom { CanonicalEvent: WarHistorianRepy } &&
                native.CurrentMapPointHistoryEntry!.MapPointType == MegaCrit.Sts2.Core.Map.MapPointType.Unknown &&
                native.CurrentMapPointHistoryEntry.Rooms[0].RoomType == RoomType.Event,
                "A real unknown entry honors Lantern Key's event-only odds and next-event override in the third act.");
        }
        finally { JigsawNativeSaveHost.Save = null; RunManager.Instance.CleanUp(false); }
    }

    private static Task IsolationAndCorruption()
    {
        var native = Native();
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        try
        {
            // Direct native history input exercises the consumer and malformed-save boundary.
            native.AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.Monster, RoomType.Monster, null);
            native.AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.Shop, RoomType.Shop, null);
            Check(native.TotalFloor == 2 && ScoreUtility.GetScoreForFloor(native.MapPointHistory) == 20,
                "An ordinary run retains native floor and score behavior.");
            var save = RunManager.Instance.ToSave(null);
            Check(ScoreUtility.CalculateScore(save, false) == ScoreUtility.CalculateScore(native, false),
                "Ordinary serialized scoring is unchanged.");
            JigsawCheckpoint.Capture(native, Ready(JigsawRoom.Shop));
            JigsawCheckpoint.Slot.TryGet(native, out var first);
            first.RevisitedHistoryIndices = new() { [0] = [1] };
            JigsawCheckpoint.Capture(native, Ready(JigsawRoom.Shop));
            JigsawCheckpoint.Slot.TryGet(native, out var next);
            next.RevisitedHistoryIndices![0].Add(0);
            Check(first.RevisitedHistoryIndices[0].SetEquals(new[] { 1 }), "Capturing progress clones nested sets instead of mutating the previous checkpoint.");
            foreach (var invalid in new Dictionary<int, HashSet<int>>[]
            {
                new() { [0] = [2] }, new() { [-1] = [0] }, new() { [3] = [0] }, new() { [0] = [-1] }, new() { [0] = [0] }
            })
            {
                next.RevisitedHistoryIndices = invalid;
                JigsawSaveSource.CaptureDocument(save, native);
                bool rejected = false;
                try { RunState.FromSerializable(save); }
                catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Actual native deserialization rejects invalid act or entry indices without discarding history or rerolling.");
            }
            return Task.CompletedTask;
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
