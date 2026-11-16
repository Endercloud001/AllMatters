using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;

internal static class JigsawQuestMarkerTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw FurCoat pickup persists shared markers", Finalized),
     ("Jigsaw FurCoat sets opening and summoned enemies to one current HP", FurCombat),
     ("Jigsaw marked hunted chest pays each native owner's cards once", Owners),
     ("Jigsaw marked safe chest pays Spoils and ordinary gold", SafeChest),
     ("Jigsaw unmarked chest keeps late Spoils cards unpaid", LateCards),
     ("Jigsaw automatic acceptance marks one logical Spoils chest", AutomaticChest),
     ("Jigsaw Spoils without a chest shows native deck removal at manual finalization", NoChest)];

    private static bool removalPreview;
    private static void ObserveRemoval(CardModel card, bool showPreview)
    {
        if (card is SpoilsMap) removalPreview |= showPreview;
    }

    private static bool SkipUninitializedProfile() => false;

    private static Task AutomaticChest()
    {
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
        var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>()], seed: "QUEST_AUTOMATIC");
        native.CurrentActIndex = 1;
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        try
        {
            var map = Enumerable.Range(0, 100).Select(seed => JigsawRun.Create(seed)).First(candidate => candidate.AutomaticWitness is { } witness && candidate.Pool
                .Where(piece => witness.Placements.Any(placement => placement.PieceId == piece.Id))
                .SelectMany(piece => piece.Nodes).Count(node => node.Room == JigsawRoom.Treasure) > 1);
            NativeEventTests.Check(map.ChooseEnd(1, EndChoice.Automatic).Success, "Automatic finalization is authorized.");
            player.Deck.AddInternal(native.CreateCard<SpoilsMap>(player));
            JigsawCheckpoint.Capture(native, map);
            JigsawNativeSession.Restore(native);
            var session = JigsawNativeSession.Current!;
            NativeEventTests.Check(session.ApplyAutomatic(map.AutomaticWitness, null).Success && session.Map!.Phase == JigsawPhase.Ancient,
                "The session accepts the real automatic result.");
            var finalized = session.Map!;
            NativeEventTests.Check(finalized.CreateSave().QuestMarkers?.SpoilsNode is { } node &&
                finalized.Nodes.Any(candidate => candidate.Id == node && candidate.Room == JigsawRoom.Treasure),
                "Automatic acceptance chooses one shared logical treasure from a layout with multiple possible chests.");
            return Task.CompletedTask;
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static async Task NoChest()
    {
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
        var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>()], seed: "QUEST_NO_CHEST");
        native.CurrentActIndex = 1;
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        var observer = new Harmony("AllMatters.Tests.SpoilsRemovalPreview");
        observer.Patch(AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.RemoveFromDeck), [typeof(CardModel), typeof(bool)]),
            prefix: new HarmonyMethod(typeof(JigsawQuestMarkerTests), nameof(ObserveRemoval)));
        observer.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)),
            prefix: new HarmonyMethod(typeof(JigsawQuestMarkerTests), nameof(SkipUninitializedProfile)));
        removalPreview = false;
        try
        {
            RunManager.Instance.GenerateRooms(); await RunManager.Instance.GenerateMap();
            var map = Enumerable.Range(0, 100).Select(seed => JigsawRun.Create(seed)).First(run =>
                run.Pool[0].Nodes.All(n => n.Room != JigsawRoom.Treasure));
            NativeEventTests.Check(map.Take(1, 0).Success && map.Drop(1, new(3, 0)).Success && map.AddEdge(1, 0, 1).Success, "Legal chestless manual layout");
            NativeEventTests.Check(map.ChooseEnd(1, EndChoice.Manual).Success, "Authorize manual finalization");
            player.Deck.AddInternal(native.CreateCard<SpoilsMap>(player));
            JigsawCheckpoint.Capture(native, map);
            JigsawNativeSession.Restore(native);
            var session = JigsawNativeSession.Current!;
            await session.Inspect();
            NativeEventTests.Check(session.Map!.Phase == JigsawPhase.Ancient && removalPreview &&
                !player.Deck.Cards.OfType<SpoilsMap>().Any(), "The actual manual commit removes the quest through the visible Guilty command.");
        }
        finally
        {
            observer.UnpatchAll(observer.Id);
            RunManager.Instance.CleanUp(false);
        }
    }

    private static async Task FurCombat()
    {
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
        var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>()], seed: "FUR_COMBAT");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        try
        {
            var map = Enumerable.Range(0, 100).Select(seed => JigsawRun.Create(seed)).First(run =>
                run.Pool[0].Nodes[0].Room == JigsawRoom.Combat);
            NativeEventTests.Check(map.Take(1, 0).Success && map.Drop(1, new(3, 0)).Success && map.AddEdge(1, 0, 1).Success, "Legal combat layout");
            map.ChooseEnd(1, EndChoice.Manual); map.Inspect(); map.CompleteAncient(1); map.Continue(1);
            JigsawCheckpoint.Capture(native, map);
            JigsawNativeSession.Restore(native);
            await RelicCmd.Obtain<FurCoat>(player);
            var actual = JigsawNativeSession.Current!.Map!;
            var entry = actual.BeginEnter(1)!;
            NativeEventTests.Check(actual.ConfirmEnter(entry.Id).Success, "Confirm logical combat destination");
            var combat = HeadlessTestSetup.Combat(native);
            CombatManager.Instance.SetUpCombat(combat);
            native.PushRoom(new CombatRoom(combat));
            var prototype = ModelDb.Monster<AllMatters.Monsters.Lagavulin>();
            var opening = combat.CreateCreature(prototype.ToMutable(), CombatSide.Enemy, null);
            combat.AddCreature(opening);
            await Hook.BeforeCombatStart(native, combat);
            var summoned = await CreatureCmd.Add(prototype.ToMutable(), combat);
            NativeEventTests.Check(opening.CurrentHp == 1 && opening.MaxHp > 1 &&
                summoned.CurrentHp == 1 && summoned.MaxHp > 1, "Both actual native combat hooks change only current HP.");
            await CreatureCmd.SetCurrentHp(summoned, 3);
            NativeEventTests.Check(summoned.CurrentHp == 3, "The marker does not impose a continuing HP cap.");
        }
        finally
        {
            CombatManager.Instance.Reset(true);
            RunManager.Instance.CleanUp(false);
        }
    }

    private static Task Owners() => JigsawHuntingTreasureTests.WithTreasure(true, async (session, room) =>
    {
        var native = session.Native;
        int[] before = native.Players.Select(p => p.Gold).ToArray();
        var method = AccessTools.Method(typeof(OneOffSynchronizer), "DoTreasureRoomRewards", [typeof(Player)]);
        var sync = RunManager.Instance.OneOffSynchronizer;
        int local = await (Task<int>)method.Invoke(sync, [native.Players[0]])!;
        int remote = await (Task<int>)method.Invoke(sync, [native.Players[1]])!;
        NativeEventTests.Check(local == 1200 && remote == 1800, "The same marked hunted chest pays 600 for every actual owner's card.");
        NativeEventTests.Check(native.Players[0].Gold == before[0] + 1200 && native.Players[1].Gold == before[1] + 1800 && native.Players.All(p => !p.Deck.Cards.OfType<SpoilsMap>().Any()), "Both owners receive their own gold and all quest cards are removed.");
        int repeated = await (Task<int>)method.Invoke(sync, [native.Players[1]])!;
        NativeEventTests.Check(repeated == 0 && native.Players[1].Gold == before[1] + 1800, "Repeated native chest source cannot pay the remote owner twice.");
    }, questAtFinalization: true, owners: 2, beforeEntry: native =>
    {
        for (int i = 0; i < native.Players.Count; i++)
            for (int copy = 0; copy < i + 2; copy++) native.Players[i].Deck.AddInternal(native.CreateCard<SpoilsMap>(native.Players[i]));
    });

    private static Task SafeChest() => JigsawHuntingTreasureTests.WithTreasure(false, async (session, room) =>
    {
        var player = session.Native.Players[0];
        int before = player.Gold;
        int paid = await room.DoNormalRewards();
        NativeEventTests.Check(paid is >= 642 and <= 652 && player.Gold == before + paid &&
            !player.Deck.Cards.OfType<SpoilsMap>().Any(), "A safe marked chest pays 600 plus the native chest roll and removes the card.");
    }, face: false, questAtFinalization: true);

    private static Task LateCards() => JigsawHuntingTreasureTests.WithTreasure(true, async (session, room) =>
    {
        var player = session.Native.Players[0];
        var card = session.Native.CreateCard<SpoilsMap>(player); player.Deck.AddInternal(card);
        int before = player.Gold;
        int paid = await room.DoNormalRewards();
        NativeEventTests.Check(paid == 0 && player.Gold == before && player.Deck.Cards.Contains(card), "A real unmarked chest cannot pay or remove a card added after finalization.");
    }, secondAct: true);

    private static async Task Finalized()
    {
        NativeEventTests.Check(AllMatters.Jigsaw.Presentation.JigsawNativeArt.QuestMarker() is { } texture &&
            texture.GetWidth() > 0 && texture.GetHeight() > 0, "The map overlay loads the native quest icon texture.");
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, 1);
        var native = RunState.CreateForTest([player], acts: [ModelDb.Act<Overgrowth>()], seed: "QUEST_MARKERS");
        RunManager.Instance.SetUpTest(native, new NetSingleplayerGameService());
        try
        {
            var map = JigsawRun.Create(12);
            var witness = map.AutomaticWitness!;
            foreach (var placement in witness.Placements)
            {
                NativeEventTests.Check(map.Take(1, placement.PieceId).Success, "Take witness piece");
                for (int i = 0; i < placement.Rotation; i++) map.Rotate(1);
                NativeEventTests.Check(map.Drop(1, placement.Anchor).Success, "Drop witness piece");
            }
            foreach (var edge in witness.Edges.Where(e => !e.Internal)) map.AddEdge(1, edge.First, edge.Second);
            map.ChooseEnd(1, EndChoice.Manual);
            NativeEventTests.Check(map.Inspect().Success, "Actual legal manual finalization");
            JigsawCheckpoint.Capture(native, map);
            JigsawNativeSession.Restore(native);
            await RelicCmd.Obtain<FurCoat>(player);
            JigsawNativeSession.Current!.OnEdited();
            var json = JsonSerializer.SerializeToElement(JigsawNativeSession.Current.Map!.CreateSave());
            NativeEventTests.Check(json.TryGetProperty("QuestMarkers", out var marks) && marks.ValueKind == JsonValueKind.Object,
                "Finalizing a map with FurCoat must save shared logical marker IDs.");
            int candidates = map.Nodes.Count(n => n.Room is JigsawRoom.Combat or JigsawRoom.Elite);
            Console.WriteLine($"FUR_MARKER candidates={candidates} limit={relicCount()} actual={marks.GetProperty("FurNodes").GetArrayLength()}");
            NativeEventTests.Check(marks.GetProperty("FurNodes").GetArrayLength() == Math.Min(candidates, relicCount()), "Pickup marks up to the version-specific number of combats.");
            int relicCount() => player.Relics.OfType<FurCoat>().Single().DynamicVars["Combats"].IntValue;
        }
        finally { RunManager.Instance.CleanUp(false); }
    }
}
