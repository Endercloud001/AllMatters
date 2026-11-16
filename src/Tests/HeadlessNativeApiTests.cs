using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

internal static class HeadlessNativeApiTests
{
    internal const string Name = "Test host native selection scope restores the previous run after an exception";

    internal static async Task Run()
    {
        var first = HeadlessTestSetup.PlayerWithRun("SELECTION_SCOPE_FIRST");
        var second = HeadlessTestSetup.PlayerWithRun("SELECTION_SCOPE_SECOND");
        var firstCard = first.Deck.Cards[0];
        var secondCard = second.Deck.Cards[1];
        var prefs = new CardSelectorPrefs(firstCard.TitleLocString, 1);
        var previousLocalId = LocalContext.NetId;
        RunManager.Instance.SetUpTest((RunState)first.RunState, new NetSingleplayerGameService());
        try
        {
#if !STS2_PUBLIC
            using var outer = RunManager.Instance.CardSelectorStack.UseSelector(first, new NativeEventTests.Select(firstCard));
#endif
            bool interrupted = false;
            try
            {
                using var selection = HeadlessNativeApi.UseSelector(second.RunState, new NativeEventTests.Select(secondCard));
                var selected = await CardSelectCmd.FromDeckGeneric(second, prefs);
                NativeEventTests.Check(selected.SequenceEqual([secondCard]), "The second run selects its actual card through the native command.");
                throw new InvalidDataException("selection scope interruption");
            }
            catch (InvalidDataException error) when (error.Message == "selection scope interruption") { interrupted = true; }
#if STS2_PUBLIC
            using var restored = HeadlessNativeApi.UseSelector(first.RunState, new NativeEventTests.Select(firstCard));
#endif
            var after = await CardSelectCmd.FromDeckGeneric(first, prefs);
            NativeEventTests.Check(interrupted && after.SequenceEqual([firstCard]),
                "After the interrupted scope, the previous run's real selector still chooses its own card.");
        }
        finally { RunManager.Instance.CleanUp(false); LocalContext.NetId = previousLocalId; }
    }
}
