using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using AllMatters.Encounters;
using AllMatters.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

internal static class TeaTrialTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("TeaTrial new battle options preserve native choices and use shared confirmation", Pages),
        ("TeaTrial Champion strict half-health switches intent without interrupting damage and executes every three moves", Champion),
        ("TeaTrial Champion regular moves respect native eligibility and specified all-player effects", RegularMoves),
        ("TeaTrial belt triggers only for owner successful Vulnerable including reapplication and Artifact", Belt),
        ("TeaTrial magistrate grip covers third player phases and charges only each card player", Grip),
        ("TeaTrial native room entry and victory return preserve both narratives", RoomReturn),
        ("TeaTrial native elite loot and extra belt respect personal forfeiture", Rewards),
        ("TeaTrial full Champion and native Owl animations, bilingual spans and hover art load", Presentation),
        ("TeaTrial native shared synchronizer rejects mixed votes for tea and trial", Unanimity),
    ];

    private static async Task Pages()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var player = NativeEventTests.Player("TEA_TRIAL");
        var tea = await NativeEventTests.Begin<RoundTeaParty>(player);
        NativeEventTests.Check(tea.IsShared && tea.CurrentOptions.Count == 3
            && tea.CurrentOptions.Any(o => o.TextKey.EndsWith(".AM_FIGHT")), "Tea retains both native choices and adds a shared Champion battle.");
        var trial = await NativeEventTests.Begin<Trial>(player);
        NativeEventTests.Check(trial.IsShared && trial.CurrentOptions.Count == 3, "Trial retains both native choices and adds a shared challenge.");
        await trial.CurrentOptions.Single(o => o.TextKey.EndsWith(".AM_CHALLENGE")).Chosen();
        NativeEventTests.Check(!trial.IsFinished && trial.CurrentOptions.Count == 1
            && trial.CurrentOptions[0].TextKey.EndsWith(".AM_CONTINUE"), "Challenge opens its confirmation page before combat.");
    }

    private static Task Unanimity()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (var canonical in new EventModel[] { ModelDb.Event<RoundTeaParty>(), ModelDb.Event<Trial>() })
        {
            var run = HeadlessTestSetup.Run("TEA_TRIAL_VOTES", ModelDb.Character<Ironclad>(), ModelDb.Character<Silent>());
            var service = System.Reflection.DispatchProxy.Create<MegaCrit.Sts2.Core.Multiplayer.Game.INetGameService, WhisperingHollowTests.EventNetwork>();
            var buffer = new MegaCrit.Sts2.Core.Multiplayer.Game.RunLocationTargetedMessageBuffer(service);
#if STS2_PUBLIC
            using var sync = new MegaCrit.Sts2.Core.Multiplayer.Game.EventSynchronizer(buffer, service, run, 1, run.Rng.Seed);
#else
            using var sync = new MegaCrit.Sts2.Core.Multiplayer.Game.EventSynchronizer(buffer, service, run, run, 1, run.Rng.Seed);
#endif
            sync.BeginEvent(canonical);
            sync.ChooseLocalOption(2);
            ((WhisperingHollowTests.EventNetwork)service).Receive(new MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync.VotedForSharedEventOptionMessage
                { optionIndex = 0, pageIndex = 0, location = buffer.CurrentLocation }, 2);
            NativeEventTests.Check(sync.IsShared && sync.Events.All(e => e.CurrentOptions.Count == 3 && !e.IsFinished), "Mixed votes cannot select either the battle or a native choice.");
        }
        return Task.CompletedTask;
    }

    private static HeadlessTestSetup.EncounterCombatScope Battle<T>(int players = 1) where T : EncounterModel
    {
        var run = HeadlessTestSetup.Run("TEA_TRIAL_BATTLE", players == 1
            ? [ModelDb.Character<Ironclad>()] : [ModelDb.Character<Ironclad>(), ModelDb.Character<Silent>()]);
        var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<T>().ToMutable());
        try
        {
            foreach (var player in run.Players)
            {
                player.Creature.SetMaxHpInternal(2000); player.Creature.SetCurrentHpInternal(2000);
            }
            // Keep native commands active after killing the subject.
            battle.Combat.AddCreature(battle.Combat.CreateCreature(ModelDb.Monster<LivingFog>().ToMutable(), CombatSide.Enemy, "sentinel"));
            return battle;
        }
        catch { battle.Dispose(); throw; }
    }

    private static async Task Champion()
    {
        using var battle = Battle<ChampionEncounter>();
        var (run, combat, monster) = (battle.Run, battle.Combat, battle.Monster);
        var c = monster.Creature; var p = run.Players[0]; var ctx = new ThrowingPlayerChoiceContext();
        NativeEventTests.Check(c.MaxHp == 320 && combat.Encounter!.RoomType == RoomType.Boss, "Champion has 320 singleplayer HP and Boss identity.");
        monster.RollMove(combat.PlayerCreatures);
        NativeEventTests.Check(monster.NextMove.Id == "DEFEND", "First move is always Defense.");
        await monster.PerformMove();
        NativeEventTests.Check(c.Block == 15, "Defense grants exactly 15 Block.");
        await CreatureCmd.Damage(ctx, c, 175, ValueProp.Unpowered, p.Creature);
        NativeEventTests.Check(c.CurrentHp == 160 && monster.NextMove.Id == "DEFEND", "Exactly half HP does not trigger Anger.");
        await CreatureCmd.Damage(ctx, c, 1, ValueProp.Unpowered, p.Creature);
        NativeEventTests.Check(monster.NextMove.Id == "ANGER" && c.GetPowerAmount<StrengthPower>() == 0, "Strictly below half changes intent immediately without executing Anger.");
        await CreatureCmd.Damage(ctx, c, 3, ValueProp.Unpowered, p.Creature);
        NativeEventTests.Check(c.CurrentHp == 156, "Remaining player damage resolves before Anger.");
        monster.RollMove(combat.PlayerCreatures);
        NativeEventTests.Check(monster.NextMove.Id == "ANGER", "A round-end roll cannot skip pending Anger, including a threshold reached during the enemy turn.");
        await PowerCmd.Apply<WeakPower>(ctx, c, 2, p.Creature, null);
        await monster.PerformMove();
        NativeEventTests.Check(!c.HasPower<WeakPower>() && c.GetPowerAmount<StrengthPower>() == 9, "Anger cleanses and grants exactly 9 Strength.");
        for (int i = 0; i < 7; i++)
        {
            monster.RollMove(combat.PlayerCreatures);
            bool execute = i % 3 == 0;
            NativeEventTests.Check((monster.NextMove.Id == "EXECUTE") == execute, $"Execute immediately follows Anger, then exactly two regular moves separate executions (step {i}, actual {monster.NextMove.Id}).");
            await monster.PerformMove();
        }
        await CreatureCmd.Damage(ctx, c, 1, ValueProp.Unpowered, p.Creature);
        NativeEventTests.Check(monster.NextMove.Id != "ANGER" && c.GetPowerAmount<StrengthPower>() == 9, "Threshold triggers only once.");
    }

    private static async Task RegularMoves()
    {
        using var battle = Battle<ChampionEncounter>(2);
        var (run, combat, monster) = (battle.Run, battle.Combat, battle.Monster);
        NativeEventTests.Check(monster.Creature.MaxHp == Creature.ScaleHpForMultiplayer(320, combat.Encounter!, 2, run.CurrentActIndex), "Native multiplayer HP scaling is preserved.");
        monster.RollMove(combat.PlayerCreatures);
        string? previous = null; int defenses = 0;
        var seen = new HashSet<string>();
        for (int i = 0; i < 45; i++)
        {
            string move = monster.NextMove.Id;
            NativeEventTests.Check(move != previous, "Regular actions never repeat consecutively.");
            if (move == "DEFEND") defenses++;
            seen.Add(move);
            // Restore player HP/status so independent action effects remain observable.
            foreach (var p in run.Players)
            {
                foreach (var power in p.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
                p.Creature.SetCurrentHpInternal(2000);
            }
            await monster.PerformMove();
            if (move == "TAUNT" || move == "SLAP")
                foreach (var p in run.Players)
                    NativeEventTests.Check(p.Creature.GetPowerAmount<VulnerablePower>() == 2
                        && (move == "TAUNT" ? p.Creature.GetPowerAmount<WeakPower>() : p.Creature.GetPowerAmount<FrailPower>()) == 2,
                        "Both players get the specified two-layer debuffs.");
            if (move == "SLASH") NativeEventTests.Check(run.Players.All(p => p.Creature.CurrentHp == 1982), "Heavy Slash deals 18 using the native monster target selection.");
            previous = move;
            monster.RollMove(combat.PlayerCreatures);
        }
        NativeEventTests.Check(defenses == 2 && seen.SetEquals(["DEFEND", "TAUNT", "SLASH", "SLAP"]), "At most two defenses and only the four permitted regular actions.");
    }

    private static async Task Belt()
    {
        using var battle = Battle<ChampionEncounter>(2);
        var (run, combat, monster) = (battle.Run, battle.Combat, battle.Monster);
        var first = run.Players[0]; var second = run.Players[1]; var c = monster.Creature;
        await RelicCmd.Obtain<AllMatters.Relics.ChampionBelt>(first);
        var ctx = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<ArtifactPower>(ctx, c, 1, c, null);
        await PowerCmd.Apply<VulnerablePower>(ctx, c, 2, first.Creature, null);
        NativeEventTests.Check(!c.HasPower<VulnerablePower>() && !c.HasPower<WeakPower>(), "Artifact blocking Vulnerable does not give Weak.");
        await PowerCmd.Apply<VulnerablePower>(ctx, c, 2, second.Creature, null);
        NativeEventTests.Check(!c.HasPower<WeakPower>(), "Ally applications do not trigger the owner's belt.");
        await PowerCmd.Apply<VulnerablePower>(ctx, c, 2, first.Creature, null);
        await PowerCmd.Apply<VulnerablePower>(ctx, c, 1, first.Creature, null);
        NativeEventTests.Check(c.GetPowerAmount<WeakPower>() == 2, "Each successful owner application gives exactly one Weak, including stacked Vulnerable.");
    }

    private static async Task Grip()
    {
        using var battle = Battle<TrialEncounter>(2);
        var (run, combat, monster) = (battle.Run, battle.Combat, battle.Monster);
        var ctx = new ThrowingPlayerChoiceContext();
        for (int round = 1; round <= 10; round++)
        {
            combat.RoundNumber = round;
            await monster.BeforeSideTurnStart(ctx, CombatSide.Player, combat.PlayerCreatures, combat);
            bool active = round % 3 == 0;
            NativeEventTests.Check(monster.Creature.HasPower<TightGripPower>() == active, "Grip exists only on player turns 3, 6, 9 and is gone before 4, 7, 10.");
            foreach (var p in run.Players) await PlayerCmd.SetEnergy(8, p);
            var card = combat.CreateCard<DefendIronclad>(run.Players[0]);
            await CardPileCmd.Add(card, PileType.Hand);
            await card.SpendResources();
            await card.OnPlayWrapper(ctx, null, true, default, skipCardPileVisuals: true);
            NativeEventTests.Check(run.Players[0].PlayerCombatState!.Energy == (active ? 6 : 7)
                && run.Players[1].PlayerCombatState!.Energy == 8, "Only the player of the card loses an extra Energy.");
            // Repeated OnPlayWrapper is the native repeat/autoplay boundary, no resource spend.
            await card.OnPlayWrapper(ctx, null, true, default, skipCardPileVisuals: true);
            NativeEventTests.Check(run.Players[0].PlayerCombatState!.Energy == (active ? 5 : 7), "Repeated/free plays still trigger Grip.");
        }
        combat.RoundNumber = 12;
        await monster.BeforeSideTurnStart(ctx, CombatSide.Player, combat.PlayerCreatures, combat);
        await CreatureCmd.Kill(monster.Creature);
        var player = run.Players[0]; await PlayerCmd.SetEnergy(8, player);
        await combat.CreateCard<DefendIronclad>(player).OnPlayWrapper(ctx, null, true, default, skipCardPileVisuals: true);
        NativeEventTests.Check(player.PlayerCombatState!.Energy == 8, "Magistrate death immediately stops the penalty.");

        battle.Dispose();
        using var ordinaryBattle = Battle<MegaCrit.Sts2.Core.Models.Encounters.OwlMagistrateNormal>();
        var (ordinary, native) = (ordinaryBattle.Combat, ordinaryBattle.Monster);
        ordinary.RoundNumber = 3;
        await native.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Player, ordinary.PlayerCreatures, ordinary);
        NativeEventTests.Check(!native.Creature.HasPower<TightGripPower>(), "The native magistrate encounter is unchanged.");
    }

    private static async Task RoomReturn()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var player = NativeEventTests.Player("TEA_TRIAL_RETURN"); var run = (RunState)player.RunState;
        var previousNetId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
        RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
        MegaCrit.Sts2.Core.Context.LocalContext.NetId = player.NetId;
        HeadlessBanditRoomEntrance.Active = true;
        try
        {
            foreach (bool tea in new[] { true, false })
            {
                HeadlessBanditRoomEntrance.Rooms.Clear();
                EventModel e = (tea ? (EventModel)ModelDb.Event<RoundTeaParty>() : ModelDb.Event<Trial>()).ToMutable();
#if STS2_PUBLIC
                await e.BeginEvent(player, false);
#else
                var sync = new MegaCrit.Sts2.Core.Multiplayer.Game.EventCombatSynchronizer(run, run);
                await e.BeginEvent(player, sync, false);
                sync.InitializeForEvent(e);
#endif
                await e.CurrentOptions.Single(o => o.TextKey.EndsWith(tea ? ".AM_FIGHT" : ".AM_CHALLENGE")).Chosen();
                if (!tea) await e.CurrentOptions.Single().Chosen();
                var room = HeadlessBanditRoomEntrance.Rooms.Single();
                NativeEventTests.Check(room.ShouldResumeParentEventAfterCombat
                    && (tea ? room.Encounter is ChampionEncounter : room.Encounter is TrialEncounter), "The native room resumes the correct parent event.");
                await e.Resume(room);
                NativeEventTests.Check(e.IsFinished, "Victory finishes on its dedicated ending page.");
            }
        }
        finally { HeadlessBanditRoomEntrance.Active = false; RunManager.Instance.CleanUp(false); MegaCrit.Sts2.Core.Context.LocalContext.NetId = previousNetId; }
    }

    private static async Task Rewards()
    {
        var run = HeadlessTestSetup.Run("TEA_TRIAL_REWARDS", ModelDb.Character<Ironclad>(), ModelDb.Character<Silent>());
        var previousNetId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
        RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
        try
        {
            foreach (var encounter in new EncounterModel[] { ModelDb.Encounter<ChampionEncounter>().ToMutable(), ModelDb.Encounter<TrialEncounter>().ToMutable() })
            {
                foreach (var p in run.Players) p.ResetCombatState();
                var room = new CombatRoom(encounter, run);
                var set = await RewardsCmd.GenerateForRoomEnd(run.Players[0], room);
                NativeEventTests.Check(set.Room!.RoomType == RoomType.Elite && set.Rewards.OfType<GoldReward>().Count() == 1
                    && set.Rewards.OfType<CardReward>().Count() == 1 && set.Rewards.OfType<RelicReward>().Count() == (encounter is ChampionEncounter ? 2 : 1), "Complete native elite rewards, plus exactly one Champion Belt for the Champion.");
                NativeEventTests.Check(encounter.MinGoldReward == 35 && encounter.MaxGoldReward == 45, "Elite gold range is preserved independently of enemy identity.");
                PersonalCombatRewards.Forfeit(run.Players[1]);
                var blocked = await RewardsCmd.GenerateForRoomEnd(run.Players[1], room);
                NativeEventTests.Check(blocked.Rewards.Count == 0 && set.Rewards.Count >= 3, "One player's forfeiture does not remove an ally's rewards.");
            }
        }
        finally { RunManager.Instance.CleanUp(false); MegaCrit.Sts2.Core.Context.LocalContext.NetId = previousNetId; }
    }

    private static async Task Presentation()
    {
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
        using var atlases = HeadlessTestSetup.LoadAtlases("relic_atlas");
        foreach (var monster in new MonsterModel[] { ModelDb.Monster<AllMatters.Monsters.Champion>(), ModelDb.Monster<OwlMagistrate>() })
        {
            // Native scene scripts reference the game's original assembly location.
            // Rebind the root using the existing Ritsu factory in this standalone host.
            using var visuals = monster is AllMatters.Monsters.Champion ? monster.CreateVisuals()
                : STS2RitsuLib.Scaffolding.Godot.RitsuGodotNodeFactories.CreateFromScene<MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals>(
                    Godot.ResourceLoader.Load<Godot.PackedScene>(MegaCrit.Sts2.Core.Helpers.SceneHelper.GetScenePath("creature_visuals/owl_magistrate")));
            tree.Root.AddChild(visuals);
            try
            {
                var skeleton = visuals.SpineBody!.GetSkeleton()!.GetData()!;
                NativeEventTests.Check(skeleton.HasAnimation(monster is AllMatters.Monsters.Champion ? "Idle" : "idle_loop"), "Real Spine animations load.");
                monster.GenerateAnimator(visuals.SpineBody).SetTrigger("Hit");
            }
            finally { tree.Root.RemoveChild(visuals); visuals.Free(); }
        }
        var belt = ModelDb.Relic<AllMatters.Relics.ChampionBelt>();
        NativeEventTests.Check(belt.Icon != null && belt.IconOutline != null && ModelDb.Power<TightGripPower>().Icon != null, "Relic icon/outline and Tight Grip icon load.");
        foreach (var lang in new[] { "eng", "zhs" })
        {
            MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage(lang);
            var text = new MegaCrit.Sts2.Core.Localization.LocString("events", "TRIAL.pages.AM_CHALLENGE.description").GetRawText();
            NativeEventTests.Check(text.Contains("[jitter]") && text.Contains("[gold]") && text.Count(c => c == '\n') == 4, "Trial confirmation has both required effects in both languages.");
            NativeEventTests.Check(!belt.Title.GetFormattedText().Contains("ALL_MATTERS"), "Belt title is localized.");
        }
        MegaCrit.Sts2.Core.Localization.LocManager.Instance.SetLanguage("eng");
    }
}
