using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Localization;

internal static class JigsawStepRelicTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw manual3 real pickups share one budget bonus and preserve native map", Pickup),
      ("Jigsaw manual3 new run mode activation matrix", Modes),
      ("Jigsaw manual3 step bonus keeps active hunting and independent remaining balance", Hunting),
      ("Jigsaw manual3 ordinary relic pickups retain free travel and map generation", Ordinary) ];

    private static async Task Pickup()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("relic_atlas");
        bool previous = JigsawPreferences.Enabled;
        try
        {
            JigsawPreferences.Enabled = true;
            var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
            var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Overgrowth>()], seed: "STEP_RELIC_88");
            RunManager.Instance.SetUpNewSingleplayer(native, false);
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            var session = JigsawNativeSession.TryGet(native)!;
            await session.StartAct();
            var map = session.Map!;
            var nativeMap = native.Map;
            var boots = await RelicCmd.Obtain<WingedBoots>(player);
            NativeEventTests.Check(map.Budget == 18 && map.RemainingSteps == 18 && !boots.ShouldAllowFreeTravel(), "Native Boots pickup replaces free travel with +3 shared budget and balance before Ancient completion.");
            var compass = await RelicCmd.Obtain<GoldenCompass>(player);
            NativeEventTests.Check(map.Budget == 18 && map.RemainingSteps == 18 && ReferenceEquals(native.Map, nativeMap) && compass.GoldenPathAct == -1, "A second relic cannot stack or regenerate the map through native AfterObtained.");
            string previousLanguage = LocManager.Instance.Language;
            try
            {
                foreach (string language in new[] { "eng", "zhs" })
                {
                    LocManager.Instance.SetLanguage(language);
                    foreach (RelicModel relic in new RelicModel[] { boots, compass })
                        NativeEventTests.Check(relic.DynamicDescription.GetFormattedText().Contains("[blue]3[/blue]") &&
                            relic.DynamicEventDescription.GetFormattedText().Contains("[blue]3[/blue]"),
                            $"{relic.Id} highlights the puzzle step bonus in {language} in both descriptions.");
                }
            }
            finally { LocManager.Instance.SetLanguage(previousLanguage); }
            var saved = System.Text.Json.JsonSerializer.Deserialize<JigsawSave>(System.Text.Json.JsonSerializer.Serialize(map.CreateSave()))!;
            var restored = JigsawRun.Restore(saved);
            NativeEventTests.Check(restored.Budget == 18 && restored.RemainingSteps == 18, "Restoring preserves both 18 values rather than silently resetting to 15.");
            JigsawNativeSession.Restore(native, marked: true);
            await RelicCmd.Obtain<WingedBoots>(player);
            NativeEventTests.Check(JigsawNativeSession.TryGet(native)!.Map!.Budget == 18 && JigsawNativeSession.TryGet(native)!.Map!.RemainingSteps == 18, "A duplicate pickup after native session restore cannot award the act bonus twice.");
            native.CurrentActIndex = 1;
            session = JigsawNativeSession.TryGet(native)!;
            await session.StartAct();
            NativeEventTests.Check(session.Map!.Budget == 14 && session.Map.RemainingSteps == 14, "Held relics do not grant a new act bonus.");
            await RelicCmd.Obtain<GoldenCompass>(player);
            NativeEventTests.Check(session.Map.Budget == 17 && session.Map.RemainingSteps == 17, "Only a fresh pickup grants the new act bonus.");
        }
        finally { RunManager.Instance.CleanUp(false); JigsawPreferences.Enabled = previous; }
    }

    private static async Task Ordinary()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("relic_atlas");
        bool previous = JigsawPreferences.Enabled;
        try
        {
            JigsawPreferences.Enabled = false;
            var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
            var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>()], seed: "STEP_RELIC_ORDINARY_88");
            RunManager.Instance.SetUpNewSingleplayer(native, false);
            RunManager.Instance.GenerateRooms();
            await RunManager.Instance.GenerateMap();
            var map = native.Map;
            var boots = await RelicCmd.Obtain<WingedBoots>(player);
            var compass = await RelicCmd.Obtain<GoldenCompass>(player);
            NativeEventTests.Check(JigsawNativeSession.TryGet(native) == null && boots.ShouldAllowFreeTravel() && compass.GoldenPathAct == 0 && !ReferenceEquals(map, native.Map), "Ordinary pickups retain native free travel and Compass map regeneration.");
        }
        finally { RunManager.Instance.CleanUp(false); JigsawPreferences.Enabled = previous; }
    }

    private static Task Hunting()
    {
        var map = JigsawRun.Create(75, players: [1UL, 2UL]);
        foreach (ulong player in map.ActivePlayers) NativeEventTests.Check(map.ChooseEnd(player, EndChoice.Automatic).Success, "Automatic vote accepted.");
        NativeEventTests.Check(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)).Success, "Legal automatic layout accepted.");
        foreach (ulong player in map.ActivePlayers) NativeEventTests.Check(map.CompleteAncient(player).Success, "Ancient completes.");
        foreach (ulong player in map.ActivePlayers) NativeEventTests.Check(map.Continue(player).Success, "Ancient continuation completes.");
        NativeEventTests.Check(map.SetRemainingSteps(0).Success, "Zero balance established.");
        var attempt = map.BeginEnter(map.AvailableDestinations.First())!;
        NativeEventTests.Check(map.ConfirmEnter(attempt.Id).Success && map.CompleteRoom(attempt.Id).Success && map.HuntingActive, "Exhausted root activates hunting.");
        var beforePickup = map.CreateSave();
        NativeEventTests.Check(map.ApplyStepRelicBonus() && map.HuntingActive && map.RemainingSteps == 3 && map.Budget == 18, "Fresh pickup adds three without releasing hunting.");
        var maximum = JigsawRun.Restore(beforePickup);
        NativeEventTests.Check(maximum.SetRemainingSteps(int.MaxValue).Success && maximum.ApplyStepRelicBonus() &&
            maximum.Budget == 18 && maximum.RemainingSteps == int.MaxValue && maximum.HuntingActive,
            "A pickup keeps the console maximum representable while granting the shared budget bonus.");
        var restored = JigsawRun.Restore(map.CreateSave());
        NativeEventTests.Check(restored.Budget == 18 && restored.RemainingSteps == 3 && restored.HuntingActive && !restored.ApplyStepRelicBonus(), "Restore keeps 18/3 and the shared consumed allowance.");
        return Task.CompletedTask;
    }

    private static Task Modes()
    {
        bool previous = JigsawPreferences.Enabled;
        try
        {
            JigsawPreferences.Enabled = true;
            foreach (var mode in Enum.GetValues<GameMode>().Where(mode => mode != GameMode.None))
            foreach (bool custom in new[] { false, true })
            {
#if STS2_PUBLIC
                if (custom) continue;
#endif
                var native = RunState.CreateForTest([Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1)], acts: [ModelDb.Act<Overgrowth>()], gameMode: mode,
#if !STS2_PUBLIC
                    isCustomGame: custom,
#endif
                    seed: "MODE_88");
                try
                {
                    RunManager.Instance.SetUpNewSingleplayer(native, false, mode == GameMode.Daily ? DateTimeOffset.UtcNow : null);
                    bool expected = !custom && (mode == GameMode.Standard
#if !STS2_PUBLIC
                        || mode == GameMode.Dread
#endif
                    );
                    NativeEventTests.Check((JigsawNativeSession.TryGet(native) != null) == expected, $"Actual new run activation mismatch: {mode}, custom={custom}.");
                }
                finally { RunManager.Instance.CleanUp(false); }
            }
        }
        finally { JigsawPreferences.Enabled = previous; }
        return Task.CompletedTask;
    }
}
