using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawCompatibilityTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw compatibility Ancient excludes Compass before three native draws and preserves ordinary offers", Ancient) ];

    private sealed record Offer(string[] Choices, string[] Candidates, ulong Random, bool Boots, bool Compass);

    private static async Task<Offer> Generate(string seed, bool enabled)
    {
        bool previous = JigsawPreferences.Enabled;
        JigsawPreferences.Enabled = enabled;
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
        var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>()], seed: seed);
        try
        {
            RunManager.Instance.SetUpNewSingleplayer(native, false);
            using var outsideCombat = HeadlessCombatInProgress.EnterEvent();
            var ancient = (Tezcatara)await NativeEventTests.Begin<Tezcatara>(player);
            NativeEventTests.Check(ancient.CurrentOptions.Count == 3,
                "The real Ancient entry generates exactly three visible reward options.");
            return new(ancient.CurrentOptions.Select(option => option.Relic!.Id.Entry).ToArray(),
                ancient.AllPossibleOptions.Select(option => option.Relic!.Id.Entry).ToArray(),
                HeadlessTestSetup.RngPosition(ancient.Rng),
                ModelDb.Relic<WingedBoots>().IsAllowedAtNeow(player),
                ModelDb.Relic<GoldenCompass>().IsAllowedAtNeow(player));
        }
        finally
        {
            RunManager.Instance.CleanUp(false);
            JigsawPreferences.Enabled = previous;
        }
    }

    private static async Task Ancient()
    {
        using var atlases = HeadlessTestSetup.LoadAtlases("relic_atlas");
        bool ordinaryCompass = false;
        for (int seed = 0; seed < 24; seed++)
        {
            string input = $"COMPATIBILITY_88_{seed}";
            var ordinary = await Generate(input, false);
            var jigsaw = await Generate(input, true);
            var ordinaryAgain = await Generate(input, false);
            ordinaryCompass |= ordinary.Choices.Contains("GOLDEN_COMPASS");
            NativeEventTests.Check(ordinary.Choices.SequenceEqual(ordinaryAgain.Choices) &&
                ordinary.Candidates.SequenceEqual(ordinaryAgain.Candidates) && ordinary.Random == ordinaryAgain.Random,
                "Entering and leaving jigsaw cannot change ordinary Ancient offers or their random state.");
            NativeEventTests.Check(jigsaw.Candidates.SequenceEqual(ordinary.Candidates.Where(id => id != "GOLDEN_COMPASS")),
                "Only Golden Compass is removed from the real Ancient candidates, preserving every other candidate's order.");
            NativeEventTests.Check(!jigsaw.Choices.Contains("GOLDEN_COMPASS") &&
                jigsaw.Choices.Take(2).SequenceEqual(ordinary.Choices.Take(2)) && jigsaw.Random == ordinary.Random,
                "The other two pools and the native three draws stay unchanged, without a Compass reroll.");
            NativeEventTests.Check(ordinary.Boots && !jigsaw.Boots && !jigsaw.Compass,
                "Both prohibited relics are excluded before Neow selection only in jigsaw.");
        }
        NativeEventTests.Check(ordinaryCompass, "The fixed ordinary inputs really include a Golden Compass offer.");
    }
}
