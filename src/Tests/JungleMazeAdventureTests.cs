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
        ("Jungle Maze move commands trigger distinct Spine actions and laser volleys", VisualActions),
        ("Jungle Maze laser resources, multiplayer geometry, fade and cleanup", LaserPresentation),
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
            SentryVisualCommandProbe.Container = visuals;
            SentryVisualCommandProbe.Errors.Clear();
            SentryVisualCommandProbe.Attacks = 0;
            SentryVisualCommandProbe.Bolts = 0;
            SentryVisualCommandProbe.Lasers = 0;
            SentryVisualCommandProbe.Endpoints = 0;
            SentryVisualCommandProbe.Sounds = 0;
            await SentryBehavior();
            NativeEventTests.Check(SentryVisualCommandProbe.Errors.Count == 0,
                string.Join("; ", SentryVisualCommandProbe.Errors));
            NativeEventTests.Check(SentryVisualCommandProbe.Attacks == 4 && SentryVisualCommandProbe.Bolts == 4,
                "Exercise both visual moves in singleplayer and multiplayer.");
            NativeEventTests.Check(SentryVisualCommandProbe.Lasers == 4 && SentryVisualCommandProbe.Endpoints == 6
                && SentryVisualCommandProbe.Sounds == 4, "Each Beam callback emits one volley and sound, reaching every living target.");
        }
        finally
        {
            SentryVisualCommandProbe.Animator = null;
            SentryVisualCommandProbe.Sprite = null;
            SentryVisualCommandProbe.Container = null;
            tree.Root.RemoveChild(visuals);
            visuals.Free();
        }
    }

    private static async Task LaserPresentation()
    {
        using var texture = Godot.ResourceLoader.Load<Godot.Texture2D>(AllMatters.Vfx.SmallLaserEffect.TexturePath);
        using var sound = Godot.ResourceLoader.Load<Godot.AudioStreamOggVorbis>(AllMatters.Vfx.SmallLaserEffect.SoundPath);
        NativeEventTests.Check(texture.GetWidth() == 981 && texture.GetHeight() == 101
            && sound.GetLength() > 0 && !sound.Loop, "Packed original laser crop and nonlooping audio decode.");
        NativeEventTests.Check(AllMatters.Vfx.SmallLaserEffect.Create(Godot.Vector2.Zero, []) == null,
            "No living targets produces no visual node.");
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
        using var parent = new Godot.Node2D { Position = new Godot.Vector2(70, 80), Scale = new Godot.Vector2(2, 2), Rotation = 0.3f };
        tree.Root.AddChild(parent);
        try
        {
            var source = new Godot.Vector2(900, 350);
            Godot.Vector2[] targets = [new(250, 300), new(400, 500)];
            float previousMaster = MegaCrit.Sts2.Core.Saves.SaveManager.Instance.SettingsSave.VolumeMaster;
            try
            {
                SentryVisualCommandProbe.CaptureSound = true;
                foreach (float volume in new[] { 0f, 0.5f, 1f })
                {
                    MegaCrit.Sts2.Core.Saves.SaveManager.Instance.SettingsSave.VolumeMaster = volume;
                    AllMatters.Vfx.SmallLaserEffect.PlayAt(parent, source, targets);
                    NativeEventTests.Check(SentryVisualCommandProbe.SoundVolume == volume * volume
                        && SentryVisualCommandProbe.SoundPitch is >= 0.5f and <= 1.5f
                        && SentryVisualCommandProbe.SoundBus == STS2RitsuLib.Audio.FmodStudioRouting.SfxBus,
                        "Virtual sound preserves master mute/volume and samples the native SFX bus with original pitch variance.");
                }
                foreach (var child in parent.GetChildren()) child.Free();
            }
            finally
            {
                SentryVisualCommandProbe.CaptureSound = false;
                MegaCrit.Sts2.Core.Saves.SaveManager.Instance.SettingsSave.VolumeMaster = previousMaster;
            }
            // Let renderer initialization / first-frame shader work settle before
            // measuring the half-second pulse in the optional graphical host.
            if (Godot.DisplayServer.GetName() != "headless")
                await tree.ToSignal(tree.CreateTimer(0.5), Godot.SceneTreeTimer.SignalName.Timeout);
            var livePulse = AllMatters.Vfx.SmallLaserEffect.Create(source, targets)!;
            parent.AddChild(livePulse);
            await tree.ToSignal(tree.CreateTimer(0.15), Godot.SceneTreeTimer.SignalName.Timeout);
            NativeEventTests.Check(Godot.GodotObject.IsInstanceValid(livePulse) && livePulse.Modulate.A > 0.1f,
                $"SceneTree processing makes the pulse visible: valid={Godot.GodotObject.IsInstanceValid(livePulse)}, alpha={(Godot.GodotObject.IsInstanceValid(livePulse) ? livePulse.Modulate.A : -1)}.");
            await tree.ToSignal(tree.CreateTimer(0.5), Godot.SceneTreeTimer.SignalName.Timeout);
            NativeEventTests.Check(!Godot.GodotObject.IsInstanceValid(livePulse),
                "SceneTree processing automatically frees the pulse after its lifetime.");
            var effect = AllMatters.Vfx.SmallLaserEffect.Create(source, targets)!;
            parent.AddChild(effect);
            NativeEventTests.Check(effect.GetChildCount() == 2 && effect.GlobalTransform.IsEqualApprox(Godot.Transform2D.Identity),
                "One beam per player uses global combat coordinates even inside a transformed container.");
            for (int i = 0; i < targets.Length; i++)
            {
                var pivot = effect.GetChild<Godot.Node2D>(i);
                var primary = pivot.GetNode<Godot.Sprite2D>("Primary");
                NativeEventTests.Check(pivot.GlobalPosition.IsEqualApprox(source)
                    && pivot.ToGlobal(new Godot.Vector2(primary.Scale.X * texture.GetWidth(), 0)).DistanceTo(targets[i]) < 0.01f,
                    "Beam starts at the attacker and reaches its own player's center.");
                NativeEventTests.Check(primary.UseParentMaterial && pivot.UseParentMaterial
                    && ((Godot.CanvasItemMaterial)effect.Material).BlendMode == Godot.CanvasItemMaterial.BlendModeEnum.Add,
                    "Beam layers inherit additive blending.");
            }
            await tree.ToSignal(tree.CreateTimer(0.25), Godot.SceneTreeTimer.SignalName.Timeout);
            NativeEventTests.Check(effect.Modulate.A > 0.9f, "SceneTree tween brings the pulse near its peak halfway through its lifetime.");
            await tree.ToSignal(tree.CreateTimer(0.35), Godot.SceneTreeTimer.SignalName.Timeout);
            NativeEventTests.Check(!Godot.GodotObject.IsInstanceValid(effect), "Tween frees the pulse without direct lifecycle calls.");
            var interrupted = AllMatters.Vfx.SmallLaserEffect.Create(source, targets)!;
            parent.AddChild(interrupted);
            parent.QueueFree();
            await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
            NativeEventTests.Check(!Godot.GodotObject.IsInstanceValid(interrupted), "Leaving the combat container cleans up an unfinished pulse.");
        }
        finally { if (Godot.GodotObject.IsInstanceValid(parent)) parent.Free(); }
        if (Godot.DisplayServer.GetName() != "headless") await RenderLaser(tree);
    }

    private static async Task RenderLaser(Godot.SceneTree tree)
    {
        using var viewport = new Godot.SubViewport
        {
            Size = new Godot.Vector2I(1024, 600),
            RenderTargetUpdateMode = Godot.SubViewport.UpdateMode.Always,
            TransparentBg = true,
        };
        tree.Root.AddChild(viewport);
        try
        {
            viewport.AddChild(AllMatters.Vfx.SmallLaserEffect.Create(new Godot.Vector2(900, 350), [new(250, 300)])!);
            await tree.ToSignal(tree.CreateTimer(0.15), Godot.SceneTreeTimer.SignalName.Timeout);
            await tree.ToSignal(Godot.RenderingServer.Singleton, Godot.RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            int litPixels = 0;
            // Interior of the beam, away from the border flash and both endpoints.
            for (int y = 280; y < 380; y++)
                for (int x = 400; x < 650; x++)
                {
                    var pixel = image.GetPixel(x, y);
                    if (pixel.G > 0.2f && pixel.B > 0.2f && pixel.A > 0.1f) litPixels++;
                }
            NativeEventTests.Check(litPixels > 100, "Renderer produces visible cyan/blue pixels between the two endpoints.");
            string capture = Godot.OS.GetEnvironment("ALLMATTERS_LASER_CAPTURE");
            if (!string.IsNullOrEmpty(capture))
                NativeEventTests.Check(image.SavePng(capture) == Godot.Error.Ok, "Save the independent rendering evidence.");
            Console.WriteLine($"Laser rendering probe: {litPixels} visible interior pixels.");
        }
        finally { viewport.Free(); }
    }
}

// The headless host has no NCreature room nodes, so bridge only visual commands to
// the real Spine animator and load the hit effect that the actual move selected.
internal static class SentryVisualCommandProbe
{
    public static MegaCrit.Sts2.Core.Animation.CreatureAnimator? Animator;
    public static MegaCrit.Sts2.Core.Bindings.MegaSpine.MegaSprite? Sprite;
    public static Godot.Node? Container;
    public static readonly List<string> Errors = [];
    public static int Attacks;
    public static int Bolts;
    public static int Lasers;
    public static int Endpoints;
    public static int Sounds;
    public static bool CaptureSound;
    public static float SoundVolume;
    public static float SoundPitch;
    public static string? SoundBus;

    [HarmonyLib.HarmonyPatch(typeof(STS2RitsuLib.Audio.VirtualFmodEventRegistry), nameof(STS2RitsuLib.Audio.VirtualFmodEventRegistry.RegisterOneShot))]
    private static class SoundDefinition
    {
        private static void Prefix(string eventPath, string busPath, float volume, float pitch)
        {
            if (!CaptureSound || eventPath != AllMatters.Vfx.SmallLaserEffect.SoundEvent) return;
            SoundVolume = volume; SoundPitch = pitch; SoundBus = busPath;
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(AllMatters.Vfx.SmallLaserEffect), nameof(AllMatters.Vfx.SmallLaserEffect.Play))]
    private static class Laser
    {
        private static bool Prefix(IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> targets)
        {
            if (Animator == null) return true;
            // Only replace absent room-to-creature coordinate lookup; exercise the
            // production creation/audio path from the actual move's BeforeDamage.
            var endpoints = targets.Where(target => target.IsAlive)
                .Select((_, i) => new Godot.Vector2(200 + i * 200, 300)).ToArray();
            Lasers++; Endpoints += endpoints.Length;
            AllMatters.Vfx.SmallLaserEffect.PlayAt(Container!, new Godot.Vector2(900, 350), endpoints);
            return false;
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(MegaCrit.Sts2.Core.Commands.SfxCmd), nameof(MegaCrit.Sts2.Core.Commands.SfxCmd.Play), [typeof(string), typeof(float)])]
    private static class Sound
    {
        private static bool Prefix(string __0)
        {
            if ((Animator == null && !CaptureSound) || __0 != AllMatters.Vfx.SmallLaserEffect.SoundEvent) return true;
            Sounds++;
            return false; // No FMOD device in the headless host; decode audio separately.
        }
    }

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
