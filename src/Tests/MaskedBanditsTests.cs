using AllMatters.Encounters;
using AllMatters.Monsters;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Localization;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;

internal static class MaskedBanditsTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Masked Bandits admission checks act, floor 22/23 and every player's mask", Admission),
        ("Masked Bandits shared payment charges each owner once and retains three dialogue pages", Payment),
        ("Masked Bandits combat entrance and rewards work for either local owner without double gold", FightRewards),
        ("Masked Bandits Bear hug applies minus two dexterity to each player with native intent", BearHug),
        ("Masked Bandits bilingual monster scenes and encounter slots load from the pack", Presentation),
    ];
    private static EventModel Canonical => ModelDb.AllEvents.Single(m => m.Id.Entry == "ALL_MATTERS_EVENT_MASKED_BANDITS");
    private static Task Admission()
    {
        var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([first, second], acts: [], seed: "BANDITS");
        run.CurrentActIndex = 1;
        for (int i = 0; i < 22; i++) run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, Canonical.Id);
        Check(!Canonical.IsAllowed(run), "Floor 22 must be excluded.");
        run.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, Canonical.Id);
        Check(Canonical.IsAllowed(run), "Floor 23 in Act 2 must be admitted.");
        foreach (int act in new[] { 0, 2 }) { run.CurrentActIndex = act; Check(!Canonical.IsAllowed(run), "Wrong act admitted."); }
        run.CurrentActIndex = 1;
        second.AddRelicInternal(ModelDb.Relic<RedMask>().ToMutable());
        Check(!Canonical.IsAllowed(run), "One teammate's mask must exclude everyone.");
        return Task.CompletedTask;
    }
    private static async Task Payment()
    {
        var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        RunState.CreateForTest([first, second], acts: [], seed: "BANDITS");
        first.Gold = 71; second.Gold = 204;
        var a = await Begin(first); var b = await Begin(second);
        Check(a.IsShared && b.IsShared, "Votes must be shared.");
        var pay = a.CurrentOptions[0];
        await pay.Chosen();
        Check(first.Gold == 0 && second.Gold == 204, "A choice handler must charge only its owner.");
        await b.CurrentOptions[0].Chosen(); await pay.Chosen();
        Check(first.Gold == 0 && second.Gold == 0, "Each owner's payment must occur once.");
        foreach (var model in new[] { a, b })
        {
            Check(!model.IsFinished && model.CurrentOptions.Count == 1, "First dialogue must allow continuing.");
            await model.CurrentOptions[0].Chosen();
            Check(!model.IsFinished && model.CurrentOptions.Count == 1, "Second dialogue must allow continuing.");
            await model.CurrentOptions[0].Chosen();
            Check(model.IsFinished && model.CurrentOptions.Count == 0, "Third dialogue must finish.");
        }
    }
    private static async Task FightRewards()
    {
        foreach (ulong localId in new ulong[] { 1, 2 })
        {
            var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
            var run = RunState.CreateForTest([first, second], acts: [], seed: "BANDIT_FIGHT");
            LocalContext.NetId = localId;
            RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
            HeadlessBanditRoomEntrance.Active = true;
            HeadlessBanditRoomEntrance.Rooms.Clear();
            try
            {
                var a = Canonical.ToMutable(); var b = Canonical.ToMutable();
#if STS2_PUBLIC
                await a.BeginEvent(first, false); await b.BeginEvent(second, false);
                (localId == 1 ? a : b).GenerateInternalCombatState(run);
#else
                var synchronizer = new EventCombatSynchronizer(run, run);
                await a.BeginEvent(first, synchronizer, false); await b.BeginEvent(second, synchronizer, false);
                synchronizer.InitializeForEvent(localId == 1 ? a : b);
#endif
                await a.CurrentOptions[1].Chosen(); await b.CurrentOptions[1].Chosen();
                Check(HeadlessBanditRoomEntrance.Rooms.Count == 1, "The shared vote must enter just one room.");
                var room = HeadlessBanditRoomEntrance.Rooms.Single();
                Check(room.Encounter is RedMaskBandits && room.ParentEventId == Canonical.Id
                    && !room.ShouldResumeParentEventAfterCombat && !room.ShouldCreateCombat,
                    "Combat layout must reuse its scene and leave after combat.");
                Check(room.CombatState.Enemies.Select(c => c.Monster!.GetType()).SequenceEqual(new[] { typeof(Pointy), typeof(Romeo), typeof(Bear) }), "All three bandits must be present in order.");
                foreach (var player in run.Players)
                {
                    var rewards = new RewardsSet(player).WithRewardsFromRoom(room);
                    Check(rewards.Rewards.OfType<GoldReward>().Count() == 1, "Default and explicit gold must not stack.");
                    Check(rewards.Rewards.OfType<RelicReward>().Count() == 1, "Each participant must receive one mask.");
                    Check(rewards.Rewards.OfType<CardReward>().Count() == 1, "Normal inherited card reward must remain.");
                    await rewards.GenerateWithoutOffering();
                    var gold = rewards.Rewards.OfType<GoldReward>().Single();
                    Check(gold.Amount >= 25 && gold.Amount <= 35, "Inherited explicit gold must be 25-35.");
                    int before = player.Gold;
                    await gold.SelectUnsynchronized();
                    await rewards.Rewards.OfType<RelicReward>().Single().SelectUnsynchronized();
                    Check(player.Gold == before + gold.Amount && player.Relics.OfType<RedMask>().Count() == 1,
                        "Native reward selection must pay this participant once.");
                }
                room.MarkPreFinished();
                var saved = room.ToSerializable();
                var restored = CombatRoom.FromSerializable(saved, run);
                Check(restored.ParentEventId == Canonical.Id && restored.ExtraRewards.Count == 2,
                    "Native room serialization must retain parent and both players' explicit rewards.");
            }
            finally
            {
                HeadlessBanditRoomEntrance.Active = false;
                RunManager.Instance.CleanUp(false);
                LocalContext.NetId = 1;
            }
        }
    }

    private static async Task BearHug()
    {
        var first = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var second = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([first, second], acts: [], seed: "BEAR_HUG");
        var combat = new CombatState(runState: run);
        foreach (var player in run.Players) { combat.AddPlayer(player); player.ResetCombatState(); }
        var bear = ModelDb.Monster<Bear>().ToMutable();
        var creature = combat.CreateCreature(bear, CombatSide.Enemy, "bear");
        combat.AddCreature(creature);
        bear.SetUpForCombat();
        bear.RollMove(combat.PlayerCreatures);
        Check(bear.NextMove.Id == "BEAR_HUG" && bear.NextMove.Intents.Single() is MegaCrit.Sts2.Core.MonsterMoves.Intents.DebuffIntent,
            "First turn must show the Bear Hug debuff intent.");
        await bear.NextMove.PerformMove(combat.PlayerCreatures);
        Check(run.Players.All(p => p.Creature.GetPowerAmount<DexterityPower>() == -2), "Bear Hug must apply -2 dexterity to every player.");
        bear.MoveStateMachine.OnMovePerformed(bear.NextMove);
        bear.RollMove(combat.PlayerCreatures);
        Check(bear.NextMove.Id == "LUNGE", "Bear Hug must lead to Lunge, not repeat.");
    }

    private static async Task Presentation()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
#if !STS2_PUBLIC
        Check(ModelDb.EventEncounters.Count(e => e is RedMaskBandits) == 1, "Bandits must appear once in event bestiary.");
#endif
        foreach (var monster in new MonsterModel[] { ModelDb.Monster<Pointy>(), ModelDb.Monster<Romeo>(), ModelDb.Monster<Bear>() })
        {
            using var nativeVisuals = monster.CreateVisuals();
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(nativeVisuals);
            Check(nativeVisuals.SpineBody?.GetSkeleton()?.GetData()?.HasAnimation("Idle") == true,
                "The native creature visuals must bind the animated Spine body.");
            ((SceneTree)Engine.GetMainLoop()).Root.RemoveChild(nativeVisuals);
            nativeVisuals.Free();
            string name = monster.GetType().Name.ToLowerInvariant();
            using var scene = ResourceLoader.Load<PackedScene>($"res://AllMatters/monsters/{name}/{name}.tscn");
            using var root = scene.Instantiate();
            var sprite = root.GetNode("Visuals");
            Check(sprite.IsClass("SpineSprite") && sprite.Get("skeleton_data_res").AsGodotObject() != null, "Complete Spine data must load for " + name);
            root.Free();
        }
        using var encounter = ModelDb.Encounter<RedMaskBandits>().CreateScene();
        Check(new[] { "pointy", "romeo", "bear" }.All(slot => encounter.HasNode(slot)), "All encounter slots must be present.");
        encounter.Free();
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                Check(Canonical.Title.GetFormattedText().Length > 0, "Event title missing.");
                foreach (var monster in ModelDb.Encounter<RedMaskBandits>().AllPossibleMonsters)
                    Check(monster.Title.Exists() && monster.Title.GetFormattedText().Length > 0, "Monster name missing.");
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
    }

    private static async Task<EventModel> Begin(Player player)
    {
        var model = Canonical.ToMutable();
#if STS2_PUBLIC
        await model.BeginEvent(player, false);
#else
        await model.BeginEvent(player, new MegaCrit.Sts2.Core.Multiplayer.Game.EventCombatSynchronizer(player.RunState, player.RunState), false);
#endif
        return model;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}

// Replaces room scheduling/UI only. Event handlers, branch-specific synchronizer,
// combat state creation, native reward generation and selection remain real.
[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterRoomWithoutExitingCurrentRoom))]
internal static class HeadlessBanditRoomEntrance
{
    public static bool Active;
    public static readonly List<CombatRoom> Rooms = [];
    private static bool Prefix(AbstractRoom room, ref Task __result)
    {
        if (!Active) return true;
        Rooms.Add((CombatRoom)room);
        __result = Task.CompletedTask;
        return false;
    }
}
