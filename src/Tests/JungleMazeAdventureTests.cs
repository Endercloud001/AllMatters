using AllMatters.Encounters;
using AllMatters.Monsters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JungleMazeAdventureTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Jungle Maze shared choice enters two elite Sentries and grants eligible players native elite loot plus bonus gold", SharedFight),
        ("Jungle Maze Sentries use fixed STS1 values, opposite first intents, all-player damage and Dazed", SentryBehavior),
        ("Jungle Maze encounter, bilingual copy and complete Sentry Spine assets load", Presentation),
        ("Jungle Maze move commands trigger distinct Spine actions and load their actual hit effects", VisualActions),
    ];

    private static EventModel Canonical => ModelDb.Event<JungleMazeAdventure>();

    private static async Task SharedFight()
    {
        var previousNetId = LocalContext.NetId;
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (ulong localId in new ulong[] { 1, 2 })
        {
            var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
            var run = RunState.CreateForTest([first, second], acts: [], seed: "JUNGLE_MAZE_FIGHT");
            run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, Canonical.Id);
            LocalContext.NetId = localId;
            RunManager.Instance.SetUpTest(run, new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
            HeadlessBanditRoomEntrance.Active = true;
            HeadlessBanditRoomEntrance.Rooms.Clear();
            try
            {
                var a = Canonical.ToMutable();
                var b = Canonical.ToMutable();
#if STS2_PUBLIC
                await a.BeginEvent(first, false); await b.BeginEvent(second, false);
#else
                var synchronizer = new MegaCrit.Sts2.Core.Multiplayer.Game.EventCombatSynchronizer(run, run);
                await a.BeginEvent(first, synchronizer, false); await b.BeginEvent(second, synchronizer, false);
                synchronizer.InitializeForEvent(localId == 1 ? a : b);
#endif
                var firstOptions = a.CurrentOptions;
                var secondOptions = b.CurrentOptions;
                NativeEventTests.Check(firstOptions.Count == 2 && secondOptions.Count == 2, "Keep both native Jungle Maze choices.");
                NativeEventTests.Check(firstOptions[1].Description.GetRawText() == "[red]Fight[/red] two Sentries to claim the spoils.",
                    "The shared option describes the sentry fight with the required emphasis.");
                await firstOptions[1].Chosen();
#if !STS2_PUBLIC
                NativeEventTests.Check(HeadlessBanditRoomEntrance.Rooms.Count == 0,
                    "The beta event synchronizer waits for the other player's agreement.");
#endif
                await secondOptions[1].Chosen();

                NativeEventTests.Check(HeadlessBanditRoomEntrance.Rooms.Count == 1, "Shared agreement enters one combat room.");
                var room = HeadlessBanditRoomEntrance.Rooms.Single();
                NativeEventTests.Check(room.RoomType == RoomType.Elite && room.ParentEventId == Canonical.Id && room.ShouldResumeParentEventAfterCombat,
                    "The event fight has elite identity and returns to its parent event.");
                NativeEventTests.Check(room.Encounter is JungleMazeEncounter, "Use the dedicated two-Sentry encounter.");
                room.Encounter!.GenerateMonstersWithSlots(run);
                var sentries = room.Encounter.MonstersWithSlots.Select(pair => pair.Item1).OfType<Sentry>().ToArray();
                NativeEventTests.Check(sentries.Length == 2 && !ReferenceEquals(sentries[0], sentries[1]), "Create two independent sentry models.");
                NativeEventTests.Check(sentries[0].BoltFirst != sentries[1].BoltFirst, "The two sentries start on opposite moves.");
                var activeOwner = localId == 1 ? first : second;
                var forfeitingAlly = localId == 1 ? second : first;
                PersonalCombatRewards.Forfeit(forfeitingAlly);
                foreach (var player in run.Players)
                {
                    var rewards = new RewardsSet(player).WithRewardsFromRoom(room);
                    if (player == forfeitingAlly)
                    {
                        NativeEventTests.Check(rewards.Rewards.Count == 0, "The existing personal combat forfeiture removes all rewards for its owner.");
                        continue;
                    }
                    NativeEventTests.Check(rewards.Rewards.OfType<GoldReward>().Count() == 2, "Keep native elite gold and add exactly one bonus gold reward.");
                    NativeEventTests.Check(rewards.Rewards.OfType<CardReward>().Any() && rewards.Rewards.OfType<RelicReward>().Any(),
                        "Keep the native elite card and relic rewards.");
                    await rewards.GenerateWithoutOffering();
                    NativeEventTests.Check(rewards.Rewards.OfType<GoldReward>().Any(g => g.Amount is >= 35 and <= 64),
                        "The per-player bonus is rolled in the requested range.");
                    NativeEventTests.Check(PersonalCombatRewards.IsEligible(player), "Eligible participant keeps event combat rewards.");
                }
                room.MarkPreFinished();
                var restored = CombatRoom.FromSerializable(room.ToSerializable(), run);
                NativeEventTests.Check(restored.RoomType == RoomType.Elite && restored.ShouldResumeParentEventAfterCombat
                    && restored.ParentEventId == Canonical.Id, "Elite identity and event return policy survive legal room restoration.");
                var resumedRewards = new RewardsSet(activeOwner).WithRewardsFromRoom(restored);
                NativeEventTests.Check(resumedRewards.Rewards.OfType<GoldReward>().Count() == 2,
                    "Restoration retains the native reward plus a single bonus, without rerolling the room policy.");
                var localEvent = localId == 1 ? a : b;
                await localEvent.Resume(room);
                NativeEventTests.Check(localEvent.IsFinished, "Returning from the sentry combat finishes the event on its victory narrative.");
            }
            finally
            {
                HeadlessBanditRoomEntrance.Active = false;
                RunManager.Instance.CleanUp(false);
                LocalContext.NetId = previousNetId;
            }
        }
    }

    private static async Task SentryBehavior()
    {
        var previousNetId = LocalContext.NetId;
        foreach (int playerCount in new[] { 1, 2 })
        {
            var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
            var players = playerCount == 1 ? new[] { first } : new[] { first, second };
            var run = RunState.CreateForTest(players, acts: [], seed: "JUNGLE_MAZE_SENTRY");
            RunManager.Instance.SetUpTest(run, new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
            try
            {
                var encounter = ModelDb.Encounter<JungleMazeEncounter>().ToMutable();
                var combat = new CombatState(encounter, run);
                foreach (var player in players)
                {
                    combat.AddPlayer(player);
                    player.ResetCombatState();
                    player.PopulateCombatState(run.Rng.CombatCardGeneration, combat);
                }
                encounter.GenerateMonstersWithSlots(run);
                var sentries = encounter.MonstersWithSlots.Select(pair => pair.Item1).OfType<Sentry>().ToArray();
                for (int i = 0; i < sentries.Length; i++)
                {
                    var sentry = sentries[i];
                    NativeEventTests.Check(sentry.MinInitialHp == 38 && sentry.MaxInitialHp == 42, "Sentry base HP remains 38-42 at every ascension.");
                    var creature = combat.CreateCreature(sentry, CombatSide.Enemy,
                        encounter.MonstersWithSlots[i].Item2);
                    combat.AddCreature(creature); sentry.SetUpForCombat();
                    var allowedHp = Enumerable.Range(38, 5).Select(hp =>
                        MegaCrit.Sts2.Core.Entities.Creatures.Creature.ScaleHpForMultiplayer(hp, encounter, playerCount, run.CurrentActIndex));
                    NativeEventTests.Check(allowedHp.Any(hp => Math.Abs(hp - creature.MaxHp) < 1),
                        "Sentry HP uses native multiplayer scaling exactly once.");
                    await sentry.AfterAddedToRoom();
                    NativeEventTests.Check(creature.HasPower<ArtifactPower>() && creature.GetPowerAmount<ArtifactPower>() == 1,
                        "Each sentry starts with exactly one Artifact in singleplayer and multiplayer.");
                }

                for (int round = 0; round < 2; round++)
                {
                    foreach (var sentry in sentries)
                    {
                        sentry.RollMove(combat.PlayerCreatures);
                        if (sentry.NextMove.Id == Sentry.BEAM)
                        {
                            var before = players.ToDictionary(player => player, player => player.Creature.CurrentHp);
                            await sentry.NextMove.PerformMove(combat.PlayerCreatures);
                            foreach (var player in players)
                                NativeEventTests.Check(player.Creature.CurrentHp == before[player] - 9, "Each Beam hits every player for exactly 9.");
                        }
                        else
                        {
                            var before = players.ToDictionary(player => player,
                                player => player.PlayerCombatState!.DiscardPile.Cards.Count(card => card.Id == ModelDb.Card<Dazed>().Id));
                            await sentry.NextMove.PerformMove(combat.PlayerCreatures);
                            foreach (var player in players)
                                NativeEventTests.Check(player.PlayerCombatState!.DiscardPile.Cards.Count(card => card.Id == ModelDb.Card<Dazed>().Id) == before[player] + 2,
                                    "Each Bolt puts exactly two Dazed into every player's discard pile.");
                        }
                        sentry.MoveStateMachine!.OnMovePerformed(sentry.NextMove);
                    }
                    foreach (var sentry in sentries) sentry.RollMove(combat.PlayerCreatures);
                    NativeEventTests.Check(sentries[0].NextMove.Id != sentries[1].NextMove.Id,
                        "The sentries continue alternating independently after their first moves.");
                }
            }
            finally { RunManager.Instance.CleanUp(false); LocalContext.NetId = previousNetId; }
        }
    }

    private static async Task Presentation()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var encounter = ModelDb.Encounter<JungleMazeEncounter>();
        NativeEventTests.Check(!encounter.IsValidForAct(ModelDb.Act<MegaCrit.Sts2.Core.Models.Acts.Overgrowth>())
            && !encounter.IsValidForAct(ModelDb.Act<MegaCrit.Sts2.Core.Models.Acts.Underdocks>()),
            "The event-only sentries never enter ordinary act encounter pools.");
        try
        {
            foreach (var (language, expected) in new[] { ("eng", "Two Sentries"), ("zhs", "双哨卫") })
            {
                LocManager.Instance.SetLanguage(language);
                NativeEventTests.Check(encounter.Title.GetFormattedText() == expected, "Encounter history has a localized title.");
                string optionDescription = new LocString("events",
                    "JUNGLE_MAZE_ADVENTURE.pages.INITIAL.options.JOIN_FORCES.description").GetRawText();
                NativeEventTests.Check(optionDescription == (language == "zhs"
                    ? "与两名哨卫[red]战斗[/red]以取得战利品。"
                    : "[red]Fight[/red] two Sentries to claim the spoils."), "Localized event option and red emphasis match the request.");
                string ending = new LocString("events", "JUNGLE_MAZE_ADVENTURE.pages.JOIN_FORCES.description").GetRawText();
                NativeEventTests.Check(ending.Contains(language == "zhs" ? "[blue]乌贼巫师阿奎德[/blue]" : "[blue]squid wizard Aquied[/blue]")
                    && ending.Contains(language == "zhs" ? "[red]格拉索里安战士[/red]" : "[red]Graxorian warrior[/red]")
                    && ending.Contains(language == "zhs" ? "[gold]构装体守卫[/gold]" : "[gold]construct guards[/gold]")
                    && ending.Contains(language == "zhs" ? "[jitter]美好的回忆[/jitter]" : "[jitter]wonderful memories[/jitter]"),
                    "Victory narrative retains the requested colors and jitter in both languages.");
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
        using (var scene = encounter.CreateScene())
        {
            NativeEventTests.Check(scene.HasNode("sentry_left") && scene.HasNode("sentry_right"), "Both sentry slots exist in the encounter scene.");
            scene.Free();
        }
        var sentry = ModelDb.Monster<Sentry>();
        using (var visuals = sentry.CreateVisuals())
        {
            var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
            await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
            tree.Root.AddChild(visuals);
            var skeleton = visuals.SpineBody?.GetSkeleton()?.GetData();
            NativeEventTests.Check(skeleton != null && new[] { "idle", "attack", "spaz1", "spaz2", "spaz3", "hit" }.All(skeleton.HasAnimation),
                "Sentry visuals load all idle, attack, bolt and hit animations.");
            tree.Root.RemoveChild(visuals); visuals.Free();
        }
    }

    private static async Task VisualActions()
    {
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
        using var visuals = ModelDb.Monster<Sentry>().CreateVisuals();
        tree.Root.AddChild(visuals);
        try
        {
            var sprite = visuals.SpineBody!;
            SentryVisualCommandProbe.Animator = ModelDb.Monster<Sentry>().GenerateAnimator(sprite);
            SentryVisualCommandProbe.Sprite = sprite;
            SentryVisualCommandProbe.Errors.Clear();
            SentryVisualCommandProbe.Attacks = 0;
            SentryVisualCommandProbe.Bolts = 0;
            await SentryBehavior();
            NativeEventTests.Check(SentryVisualCommandProbe.Errors.Count == 0,
                string.Join("; ", SentryVisualCommandProbe.Errors));
            NativeEventTests.Check(SentryVisualCommandProbe.Attacks == 4 && SentryVisualCommandProbe.Bolts == 4,
                "Exercise both visual moves in singleplayer and multiplayer.");
        }
        finally
        {
            SentryVisualCommandProbe.Animator = null;
            SentryVisualCommandProbe.Sprite = null;
            tree.Root.RemoveChild(visuals);
            visuals.Free();
        }
    }
}

// The headless host has no NCreature room nodes, so bridge only visual commands to
// the real Spine animator and load the hit effect that the actual move selected.
internal static class SentryVisualCommandProbe
{
    public static MegaCrit.Sts2.Core.Animation.CreatureAnimator? Animator;
    public static MegaCrit.Sts2.Core.Bindings.MegaSpine.MegaSprite? Sprite;
    public static readonly List<string> Errors = [];
    public static int Attacks;
    public static int Bolts;

    [HarmonyLib.HarmonyPatch(typeof(MegaCrit.Sts2.Core.Commands.CreatureCmd), nameof(MegaCrit.Sts2.Core.Commands.CreatureCmd.TriggerAnim))]
    private static class Animation
    {
        private static void Prefix(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature, string triggerName)
        {
            if (Animator == null || creature.Monster is not Sentry) return;
            if (triggerName.StartsWith("spaz")) Bolts++; else Attacks++;
            if (!Animator.HasTrigger(triggerName))
            {
                Errors.Add($"Sentry move requested unregistered animation trigger '{triggerName}'.");
                return;
            }
            Animator.SetTrigger(triggerName);
#if STS2_PUBLIC
            var track = Sprite!.GetAnimationState().GetCurrent(0);
#else
            using var track = Sprite!.GetAnimationState().GetCurrent(0);
#endif
            string expected = triggerName == "Attack" ? "attack" : triggerName;
            if (track?.GetAnimationName() != expected)
                Errors.Add($"Trigger {triggerName} did not start its Spine action {expected}.");
            if (track != null)
            {
                var state = Sprite.GetAnimationState();
                state.Update(track.GetAnimationDuration() + 0.1f);
                state.Apply(Sprite.GetSkeleton()!);
                state.Update(0.01f);
                state.Apply(Sprite.GetSkeleton()!);
#if STS2_PUBLIC
                var next = state.GetCurrent(0);
#else
                using var next = state.GetCurrent(0);
#endif
                if (next?.GetAnimationName() != "idle") Errors.Add($"Sentry {expected} did not return to idle.");
            }
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand), nameof(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand.Execute))]
    private static class HitEffect
    {
        private static void Prefix(MegaCrit.Sts2.Core.Commands.Builders.AttackCommand __instance)
        {
            if (Animator == null || __instance.Attacker?.Monster is not Sentry || __instance.HitVfx == null) return;
            string path = $"res://scenes/{__instance.HitVfx}.tscn";
            if (!Godot.ResourceLoader.Exists(path))
            {
                Errors.Add($"Sentry hit effect is missing: {path}");
                return;
            }
            using var scene = Godot.ResourceLoader.Load<Godot.PackedScene>(path);
            NativeEventTests.Check(scene.CanInstantiate(), $"Sentry hit effect could not load: {path}");
        }
    }
}
