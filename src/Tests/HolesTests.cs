using AllMatters.Encounters;
using AllMatters.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models.Events;

internal static class HolesTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Holes real multiplayer synchronizer shares the same roll and rejects mixed votes", Unanimity),
        ("Holes full converted animations and bilingual text load", Presentation),
        ("Holes shared save survives reconstruction and fight returns to its narrative", ReturnAndSave),
        ("Holes combat restores unblocked Strength loss and fades only after attack five", Combat),
        ("Holes rewards double native gold and cards without relics or forfeiture leakage", Rewards),
        ("Holes page offers a stable shared battle on some seeds and preserves native choices", Page),
    ];

    private static Task Unanimity()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        for (int seed = 0; seed < 30; seed++)
        {
            var run = HeadlessTestSetup.Run("HOLES_SYNC" + seed, ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Ironclad>(), ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Silent>());
            var service = System.Reflection.DispatchProxy.Create<MegaCrit.Sts2.Core.Multiplayer.Game.INetGameService, WhisperingHollowTests.EventNetwork>();
            var buffer = new MegaCrit.Sts2.Core.Multiplayer.Game.RunLocationTargetedMessageBuffer(service);
#if STS2_PUBLIC
            using var sync = new MegaCrit.Sts2.Core.Multiplayer.Game.EventSynchronizer(buffer, service, run, 1, run.Rng.Seed);
#else
            using var sync = new MegaCrit.Sts2.Core.Multiplayer.Game.EventSynchronizer(buffer, service, run, run, 1, run.Rng.Seed);
#endif
            sync.BeginEvent(ModelDb.Event<FieldOfManSizedHoles>());
            NativeEventTests.Check(sync.Events.All(e => e.IsShared == sync.IsShared), "Every owner and the canonical synchronizer agree on sharing.");
            if (!sync.IsShared) continue;
            sync.ChooseLocalOption(2);
            ((WhisperingHollowTests.EventNetwork)service).Receive(new MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync.VotedForSharedEventOptionMessage { optionIndex = 0, pageIndex = 0, location = buffer.CurrentLocation }, 2);
            NativeEventTests.Check(sync.Events.All(e => !e.IsFinished && e.CurrentOptions.Count == 3), "Mixed votes leave the whole page awaiting consensus.");
            return Task.CompletedTask;
        }
        throw new Exception("No shared seed exercised.");
    }

    private static async Task Presentation()
    {
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
        var monster = ModelDb.Monster<AllMatters.Monsters.Transient>();
        using var visuals = monster.CreateVisuals(); tree.Root.AddChild(visuals);
        try
        {
            var skeleton = visuals.SpineBody!.GetSkeleton()!.GetData()!;
            NativeEventTests.Check(new[] { "Idle", "Attack", "Hurt" }.All(skeleton.HasAnimation), "Full converted animation set loads.");
            var animator = monster.GenerateAnimator(visuals.SpineBody);
            animator.SetTrigger("Attack"); animator.SetTrigger("Hit");
            NativeEventTests.Check(ModelDb.Power<FadingPower>().Icon != null && ModelDb.Power<ShiftingPower>().Icon != null, "Both power icons load.");
            foreach (string lang in new[] { "eng", "zhs" })
            {
                MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage(lang);
                NativeEventTests.Check(!monster.Title.GetFormattedText().Contains("ALL_MATTERS"), "Monster name is localized.");
                var text = new MegaCrit.Sts2.Core.Localization.LocString("events", "FIELD_OF_MAN_SIZED_HOLES.pages.AM_FIGHT.description").GetRawText();
                NativeEventTests.Check(text.Contains("[red]") && text.Contains("[jitter]"), "Ending preserves red and jitter spans in both languages.");
            }
        }
        finally { MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage("eng"); tree.Root.RemoveChild(visuals); visuals.Free(); }
    }

    private static async Task ReturnAndSave()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        MegaCrit.Sts2.Core.Entities.Players.Player p;
        EventModel e;
        int seed = 0;
        do { p = NativeEventTests.Player("HOLES_RETURN" + seed++); e = await NativeEventTests.Begin<FieldOfManSizedHoles>(p); } while (!e.IsShared && seed < 100);
        NativeEventTests.Check(e.IsShared, "Find a deterministic battle seed.");
        var run = (RunState)p.RunState;
        var previousNetId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
        RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
        MegaCrit.Sts2.Core.Context.LocalContext.NetId = p.NetId;
        HeadlessBanditRoomEntrance.Active = true; HeadlessBanditRoomEntrance.Rooms.Clear();
        try
        {
            var restored = RunState.FromSerializable(RunManager.Instance.ToSave(null));
            var rebuilt = await NativeEventTests.Begin<FieldOfManSizedHoles>(restored.Players[0]);
            NativeEventTests.Check(rebuilt.IsShared && rebuilt.CurrentOptions.Count == 3, "Saved page keeps the battle and shared policy.");
            e = ModelDb.Event<FieldOfManSizedHoles>().ToMutable();
#if STS2_PUBLIC
            await e.BeginEvent(p, false);
#else
            var sync = new MegaCrit.Sts2.Core.Multiplayer.Game.EventCombatSynchronizer(run, run);
            await e.BeginEvent(p, sync, false); sync.InitializeForEvent(e);
#endif
            await e.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_FIGHT")).Chosen();
            var room = HeadlessBanditRoomEntrance.Rooms.Single();
            NativeEventTests.Check(room.Encounter is HolesEncounter && room.ShouldResumeParentEventAfterCombat, "One native combat returns to its parent.");
            await e.Resume(room);
            NativeEventTests.Check(e.IsFinished, "Victory ends on the dedicated narrative.");
        }
        finally { HeadlessBanditRoomEntrance.Active = false; RunManager.Instance.CleanUp(false); MegaCrit.Sts2.Core.Context.LocalContext.NetId = previousNetId; }
    }

    private static async Task Combat()
    {
        var p = NativeEventTests.Player("TRANSIENT");
        var run = (RunState)p.RunState;
        var encounter = ModelDb.Encounter<HolesEncounter>().ToMutable();
        using var battle = new HeadlessTestSetup.EncounterCombatScope(run, encounter);
        var combat = battle.Combat;
        var monster = battle.Monster;
        var creature = monster.Creature;
        p.Creature.SetMaxHpInternal(1000); p.Creature.SetCurrentHpInternal(1000);
        await monster.AfterAddedToRoom();
        NativeEventTests.Check(creature.MaxHp == 999 && creature.GetPowerAmount<FadingPower>() == 5 && creature.GetPowerAmount<ShiftingPower>() == 1, "Native singleplayer 999 HP, five Fading and one Shifting.");
        var ctx = new ThrowingPlayerChoiceContext();
        await CreatureCmd.GainBlock(creature, 7, ValueProp.Unpowered, null);
        await CreatureCmd.Damage(ctx, creature, 7, ValueProp.Move, p.Creature);
        NativeEventTests.Check(creature.GetPowerAmount<StrengthPower>() == 0, "Fully blocked damage does not shift Strength.");
        await CreatureCmd.Damage(ctx, creature, 3, ValueProp.Unpowered, p.Creature);
        await CreatureCmd.Damage(ctx, creature, 4, ValueProp.Move, p.Creature);
        NativeEventTests.Check(creature.GetPowerAmount<StrengthPower>() == -7, "Both non-attack and attack unblocked damage shift their full amounts.");
        var shifting = creature.GetPower<ShiftingPower>()!;
        await shifting.AfterSideTurnEnd(ctx, CombatSide.Player, combat.PlayerCreatures);
        NativeEventTests.Check(creature.GetPowerAmount<StrengthPower>() == -7, "Player turn end does not restore Strength.");
        await shifting.AfterSideTurnEnd(ctx, CombatSide.Enemy, [creature]);
        NativeEventTests.Check(creature.GetPowerAmount<StrengthPower>() == 0, "Enemy turn end restores all accumulated loss.");
        var sentinel = combat.CreateCreature(ModelDb.Monster<MegaCrit.Sts2.Core.Models.Monsters.LivingFog>().ToMutable(), CombatSide.Enemy, "sentinel");
        combat.AddCreature(sentinel);
        monster.RollMove(combat.PlayerCreatures);
        for (int turn = 1; turn <= 5; turn++)
        {
            combat.RoundNumber = turn;
            int hp = p.Creature.CurrentHp;
            await monster.NextMove.PerformMove(combat.PlayerCreatures);
            NativeEventTests.Check(p.Creature.CurrentHp == hp - 10 * turn, "Attack damage is ten times this battle's turn number.");
            NativeEventTests.Check(creature.IsDead == (turn == 5), "Death occurs after the fifth attack, never before it.");
        }
    }

    private static async Task Rewards()
    {
        var p = NativeEventTests.Player("HOLES_REWARDS");
        var run = (RunState)p.RunState;
        var previousNetId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
        RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
        MegaCrit.Sts2.Core.Context.LocalContext.NetId = p.NetId;
        try
        {
            var room = new CombatRoom(ModelDb.Encounter<HolesEncounter>().ToMutable(), run);
            var rewards = await RewardsCmd.GenerateForRoomEnd(p, room);
            NativeEventTests.Check(rewards.Rewards.OfType<GoldReward>().Count() == 2 && rewards.Rewards.OfType<CardReward>().Count() == 2
                && !rewards.Rewards.OfType<RelicReward>().Any() && rewards.Rewards.OfType<PotionReward>().Count() <= 1, "Exactly two gold/card bundles and at most the one ordinary potion roll.");
            PersonalCombatRewards.Forfeit(p);
            var blocked = await RewardsCmd.GenerateForRoomEnd(p, room);
            NativeEventTests.Check(blocked.Rewards.Count == 0, "Personal forfeiture removes all battle rewards.");
        }
        finally { RunManager.Instance.CleanUp(false); MegaCrit.Sts2.Core.Context.LocalContext.NetId = previousNetId; }
    }

    private static async Task Page()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        int offered = 0;
        for (int seed = 0; seed < 24; seed++)
        {
            var p = NativeEventTests.Player("HOLES" + seed);
            var e = await NativeEventTests.Begin<FieldOfManSizedHoles>(p);
            bool battle = e.CurrentOptions.Any(o => o.TextKey.EndsWith(".AM_FIGHT"));
            NativeEventTests.Check(e.IsShared == battle && e.CurrentOptions.Count == (battle ? 3 : 2), "Only battle pages make all choices shared.");
            var rebuilt = await NativeEventTests.Begin<FieldOfManSizedHoles>(p);
            NativeEventTests.Check(rebuilt.IsShared == battle, "Reopening cannot reroll the page.");
            if (battle) offered++;
        }
        NativeEventTests.Check(offered > 0 && offered < 24, "The 33% battle branch must coexist with original pages.");
    }
}
