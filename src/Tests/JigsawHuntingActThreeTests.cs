using AllMatters.Jigsaw;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawHuntingActThreeTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw hunting act three Mecha Knight initialization fixes Artifact at five for one to four players", MechaArtifact),
      ("Jigsaw hunting act three Mecha Knight reentrant Artifact source keeps native scaling", ReentrantArtifact),
      ("Jigsaw hunting act three Soul Nexus opening grants Territorial one and native enemy turn grants Strength once", SoulNexusOpening),
      ("Jigsaw hunting act three Mecha Knight first native action is Flamethrower with original version effects and followups", MechaFirstFlame),
      ("Jigsaw hunting act three Flail grants native Tangled to every player and restores attack costs at player turn end", FlailTangled) ];

    private static async Task FlailTangled()
    {
        for (int count = 1; count <= 4; count++)
        foreach (bool hunted in new[] { false, true })
        foreach (bool artifact in new[] { false, true })
        {
            var players = Enumerable.Range(1, count)
                .Select(id => Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            var run = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING_86_FLAIL");
            run.CurrentActIndex = 2;
            var encounter = ModelDb.AllEncounters.Single(model => model.RoomType == RoomType.Elite &&
                model.AllPossibleMonsters.Any(monster => monster is FlailKnight)).ToMutable();
            var combat = new CombatState(encounter, run) { RoundNumber = 1 };
            ulong? previousNetId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
            try
            {
                RunManager.Instance.SetUpTest(run, new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
                MegaCrit.Sts2.Core.Context.LocalContext.NetId = players[0].NetId;
                CombatManager.Instance.Reset(true);
                combat.AddPlayer(players[0]); players[0].ResetCombatState();
                CombatManager.Instance.SetUpCombat(combat);
                foreach (var player in players.Skip(1))
                { player.RunState = run; combat.AddPlayer(player); player.ResetCombatState(); }
                run.PushRoom(new CombatRoom(encounter, run));
                JigsawCheckpoint.Capture(run, EliteRoot([players[0].NetId], hunted));
                JigsawNativeSession.Restore(run);
                // Real Knight encounter has several enemies; do not use the
                // single-monster host or discard its original companions.
                encounter.GenerateMonstersWithSlots(run);
                foreach (var (monster, slot) in encounter.MonstersWithSlots)
                {
                    combat.AddCreature(combat.CreateCreature(monster, CombatSide.Enemy, slot));
                    monster.SetUpForCombat();
                }
                var flail = combat.Enemies.Select(creature => creature.Monster).OfType<FlailKnight>().Single();
                var existing = new Dictionary<Player, CardModel>();
                var skills = new Dictionary<Player, CardModel>();
                var attacks = new List<CardModel>();
                foreach (var player in players)
                {
                    existing[player] = combat.CreateCard<StrikeIronclad>(player);
                    attacks.Add(existing[player]);
                    skills[player] = combat.CreateCard<DefendIronclad>(player);
                    await CardPileCmd.AddGeneratedCardToCombat(existing[player], PileType.Hand, player);
                    await CardPileCmd.AddGeneratedCardToCombat(skills[player], PileType.Hand, player);
                    await PlayerCmd.SetEnergy(1, player);
                }
                if (artifact) await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), players[0].Creature, 1, players[0].Creature, null);
                int hp = players.Sum(player => player.Creature.CurrentHp);
                // The native move's target list is deliberately a subset;
                // Tangled's specification concerns all players, not this list.
                await flail.FlailMove([players[0].Creature]);
                NativeEventTests.Check(hp - players.Sum(player => player.Creature.CurrentHp) == 18 * count,
                    "Original A0 Flail resolves two nine-damage hits against every native opponent without changing its targeting.");
                foreach (var player in players)
                {
                    bool tangled = hunted && !(artifact && player == players[0]);
                    NativeEventTests.Check(player.Creature.GetPowerAmount<TangledPower>() == (tangled ? 1 : 0),
                        "Hunted Flail reaches each actual player once, while native Artifact prevents its owner's debuff.");
                    CheckAttack(existing[player], tangled);
                    NativeEventTests.Check(skills[player].Affliction == null && skills[player].EnergyCost.GetAmountToSpend() == 1 && skills[player].CanPlay(),
                        "Native Tangled leaves non-Attack cards playable at their original cost.");
                    var later = combat.CreateCard<StrikeIronclad>(player);
                    attacks.Add(later);
                    await CardPileCmd.AddGeneratedCardToCombat(later, PileType.Hand, player);
                    CheckAttack(later, tangled);
                }
                NativeEventTests.Check(players[0].Creature.GetPowerAmount<ArtifactPower>() == (artifact && !hunted ? 1 : 0),
                    "Artifact is consumed only by the hunted debuff and otherwise keeps its native layer.");
#if STS2_PUBLIC
                await Hook.AfterTurnEnd(combat, CombatSide.Player, combat.PlayerCreatures);
#else
                await Hook.AfterSideTurnEnd(combat, CombatSide.Player, combat.PlayerCreatures);
#endif
                foreach (var player in players)
                {
                    NativeEventTests.Check(player.Creature.GetPowerAmount<TangledPower>() == 0 &&
                        attacks.Where(card => card.Owner == player)
                            .All(card => card.Affliction == null && card.EnergyCost.GetAmountToSpend() == 1 && card.CanPlay()),
                        "The native player-side end removes Tangled and returns all existing and later Attack costs/playability.");
                }

                void CheckAttack(CardModel card, bool tangled)
                {
                    NativeEventTests.Check((card.Affliction is Entangled) == tangled && card.EnergyCost.GetAmountToSpend() == (tangled ? 2 : 1) && card.CanPlay() == !tangled,
                        "With one energy, native Tangled raises a one-cost Attack to two and prevents only the unaffordable play.");
                    card.Owner.PlayerCombatState!.Energy = 2;
                    NativeEventTests.Check(card.CanPlay(), "Two energy still permits the native Tangled Attack; this is not an unconditional Attack ban.");
                    card.Owner.PlayerCombatState.Energy = 1;
                }
            }
            finally
            {
                JigsawNativeSession.Current?.Close();
                CombatManager.Instance.Reset(true);
                RunManager.Instance.CleanUp(false);
                MegaCrit.Sts2.Core.Context.LocalContext.NetId = previousNetId;
            }
        }
    }

    private static async Task MechaFirstFlame()
    {
        for (int count = 1; count <= 4; count++)
        foreach (int ascension in new[] { 0, 9 })
        foreach (bool hunted in new[] { false, true })
        {
            var players = Enumerable.Range(1, count)
                .Select(id => Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            var run = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], ascensionLevel: ascension, seed: "HUNTING_86_FIRST_FLAME");
            run.CurrentActIndex = 2;
            var encounter = ModelDb.AllEncounters.Single(model => model.RoomType == RoomType.Elite &&
                model.AllPossibleMonsters.Any(monster => monster is MechaKnight)).ToMutable();
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, encounter);
            foreach (var player in players.Skip(1))
            { player.RunState = run; battle.Combat.AddPlayer(player); player.ResetCombatState(); }
            run.PushRoom(new CombatRoom(encounter, run));
            JigsawCheckpoint.Capture(run, EliteRoot([players[0].NetId], hunted));
            JigsawNativeSession.Restore(run);
            try
            {
                await battle.Monster.AfterAddedToRoom();
                battle.Monster.RollMove(battle.Combat.PlayerCreatures);
                NativeEventTests.Check(battle.Monster.NextMove.Id == (hunted ? "FLAMETHROWER_MOVE" : "CHARGE_MOVE"),
                    $"players={count}, hunted={hunted}: the first native RollMove selects the intended opening.");
                if (!hunted) continue;
                int[] hp = players.Select(player => player.Creature.CurrentHp).ToArray();
                await battle.Monster.PerformMove();
                for (int index = 0; index < players.Length; index++)
                {
                    NativeEventTests.Check(PileType.Hand.GetPile(players[index]).Cards.OfType<Burn>().Count() == 4,
                        "The original first Flamethrower places four Burns into every target's actual hand.");
#if STS2_PUBLIC
                    const int flameDamage = 0;
#else
                    int flameDamage = ascension == 9 ? 12 : 8;
#endif
                    NativeEventTests.Check(hp[index] - players[index].Creature.CurrentHp == flameDamage,
                        "First Flamethrower preserves public status-only versus beta eight/twelve direct damage at A0/A9.");
                }
                battle.Monster.RollMove(battle.Combat.PlayerCreatures);
                NativeEventTests.Check(battle.Monster.NextMove.Id == "WINDUP_MOVE", "Native follow-up after the first Flamethrower remains Windup.");
                await battle.Monster.PerformMove();
                NativeEventTests.Check(battle.Monster.Creature.Block == 15 && battle.Monster.Creature.GetPowerAmount<StrengthPower>() == 5,
                    "Original Windup still grants fifteen Block and five Strength.");
                battle.Monster.RollMove(battle.Combat.PlayerCreatures);
                NativeEventTests.Check(battle.Monster.NextMove.Id == "HEAVY_CLEAVE_MOVE", "Original Heavy Cleave follows Windup.");
                await battle.Monster.PerformMove();
                battle.Monster.RollMove(battle.Combat.PlayerCreatures);
                NativeEventTests.Check(battle.Monster.NextMove.Id == "FLAMETHROWER_MOVE",
                    "Original Heavy Cleave returns to Flamethrower without reintroducing Charge.");
            }
            finally { JigsawNativeSession.Current?.Close(); }
        }
    }

    private static async Task SoulNexusOpening()
    {
        using var eventScope = HeadlessCombatInProgress.EnterEvent();
        for (int count = 1; count <= 4; count++)
        foreach (bool hunted in new[] { false, true })
        {
            var players = Enumerable.Range(1, count)
                .Select(id => Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            var run = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING_86_TERRITORIAL");
            run.CurrentActIndex = 2;
            var encounter = ModelDb.AllEncounters.Single(model => model.RoomType == RoomType.Elite &&
                model.AllPossibleMonsters.Any(monster => monster is SoulNexus)).ToMutable();
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, encounter);
            // Only the session owner belongs to the run roster. Additional
            // combat roles test native hooks/scaling, not connected clients.
            foreach (var player in players.Skip(1))
            { player.RunState = run; battle.Combat.AddPlayer(player); player.ResetCombatState(); }
            run.PushRoom(new CombatRoom(encounter, run));
            JigsawCheckpoint.Capture(run, EliteRoot([players[0].NetId], hunted));
            JigsawNativeSession.Restore(run);
            try
            {
                int historyStart = CombatManager.Instance.History.Entries.Count();
                await battle.Monster.AfterAddedToRoom();
                NativeEventTests.Check(battle.Monster.Creature.GetPowerAmount<TerritorialPower>() == (hunted ? 1 : 0),
                    $"players={count}, hunted={hunted}: native Soul Nexus opening has Territorial {(hunted ? 1 : 0)}.");
                var initial = CombatManager.Instance.History.Entries.Skip(historyStart).OfType<PowerReceivedEntry>()
                    .Where(entry => entry.Power is TerritorialPower).ToArray();
                NativeEventTests.Check(hunted ? initial.Length == 1 && initial[0].Amount == 1 : initial.Length == 0,
                    "Only the hunted opening records one native Territorial grant, without stacking for each player.");

                await EndSide(CombatSide.Player, battle.Combat.PlayerCreatures);
                NativeEventTests.Check(battle.Monster.Creature.GetPowerAmount<StrengthPower>() == 0,
                    "Ending the player side does not grant the monster Territorial Strength.");
                await EndSide(CombatSide.Enemy, battle.Combat.Enemies);
                NativeEventTests.Check(battle.Monster.Creature.GetPowerAmount<StrengthPower>() == (hunted ? 1 : 0),
                    "Native enemy-side hooks grant exactly one Strength, independently of player count.");
                await EndSide(CombatSide.Enemy, battle.Combat.Enemies);
                NativeEventTests.Check(battle.Monster.Creature.GetPowerAmount<StrengthPower>() == (hunted ? 2 : 0),
                    "A second enemy-side hook grants the next single Strength through the original Territorial power.");

                Task EndSide(CombatSide side, IEnumerable<Creature> participants)
                {
#if STS2_PUBLIC
                    return Hook.AfterTurnEnd(battle.Combat, side, participants);
#else
                    return Hook.AfterSideTurnEnd(battle.Combat, side, participants);
#endif
                }
            }
            finally { JigsawNativeSession.Current?.Close(); }
        }
    }

    private sealed class ReentrantGrant(Creature target)
    {
        internal readonly Creature Target = target;
        internal bool Started;
        internal int Received;
    }
    private static ReentrantGrant? reentrantGrant;

    private static async Task ReentrantArtifact()
    {
        using var eventScope = HeadlessCombatInProgress.EnterEvent();
        var harmony = new Harmony("AllMatters.Tests.Hunting86.ReentrantArtifact");
        harmony.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforePowerAmountChanged)),
            postfix: new HarmonyMethod(typeof(JigsawHuntingActThreeTests), nameof(ReenterArtifact)));
        try
        {
            for (int count = 1; count <= 4; count++)
            {
                var players = Enumerable.Range(1, count)
                    .Select(id => Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
                var run = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING_86_REENTRANT");
                run.CurrentActIndex = 2;
                var encounter = ModelDb.AllEncounters.Single(model => model.RoomType == RoomType.Elite &&
                    model.AllPossibleMonsters.Any(monster => monster is MechaKnight)).ToMutable();
                using var battle = new HeadlessTestSetup.EncounterCombatScope(run, encounter);
                // Additional native combat roles exercise scaling, not networking.
                foreach (var player in players.Skip(1))
                { battle.Combat.AddPlayer(player); player.ResetCombatState(); }
                run.PushRoom(new CombatRoom(encounter, run));
                JigsawCheckpoint.Capture(run, EliteRoot([players[0].NetId], true));
                JigsawNativeSession.Restore(run);
                var probe = new ReentrantGrant(battle.Monster.Creature);
                reentrantGrant = probe;
                try
                {
                    int historyStart = CombatManager.Instance.History.Entries.Count();
                    await battle.Monster.AfterAddedToRoom();
                    var received = CombatManager.Instance.History.Entries.Skip(historyStart)
                        .OfType<PowerReceivedEntry>().Where(entry => entry.Power is ArtifactPower).ToArray();
                    NativeEventTests.Check(probe.Started && probe.Received == 2 + count - 1,
                        $"players={count}: another Artifact source in the awaited initialization hook must retain native scaling.");
                    NativeEventTests.Check(received.Length == 2 && received[0].Amount == 2 + count - 1 && received[1].Amount == 5,
                        "Native history distinguishes the temporary hook grant from the single fixed opening grant.");
                    NativeEventTests.Check(battle.Monster.Creature.GetPowerAmount<ArtifactPower>() == 5,
                        "After native removal of the temporary hook power, the opening grants exactly five.");
                }
                finally { reentrantGrant = null; JigsawNativeSession.Current?.Close(); }
            }
        }
        finally { reentrantGrant = null; harmony.UnpatchAll(harmony.Id); }
    }

    private static void ReenterArtifact(PowerModel power, Creature target, ref Task __result)
    {
        if (power is ArtifactPower && reentrantGrant is { Started: false } probe && ReferenceEquals(probe.Target, target))
        {
            probe.Started = true;
            __result = GrantTemporaryArtifact(__result, probe);
        }
    }

    private static async Task GrantTemporaryArtifact(Task originalHook, ReentrantGrant probe)
    {
        await originalHook;
        await Task.Yield();
        var power = await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), probe.Target, 2, probe.Target, null);
        NativeEventTests.Check(power != null, "The native hook grants an actual mutable Artifact instance.");
        probe.Received = power!.Amount;
        // Native removal before the suspended original Apply resumes avoids
        // creating two non-instanced powers; history still records this source.
        await PowerCmd.Remove(power);
    }

    // N seam agreed in #73: real mutable monster initialization and native power commands.
    // This does not claim full room entry, live turn scheduling, layout or save recovery.
    private static async Task MechaArtifact()
    {
        using var eventScope = HeadlessCombatInProgress.EnterEvent();
        for (int count = 1; count <= 4; count++)
        foreach (bool hunted in new[] { false, true })
        {
            var players = Enumerable.Range(1, count)
                .Select(id => Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            // One actual session owner, with 1–4 native combat roles for the
            // power scaling input. This is not a connected multiplayer session.
            var run = RunState.CreateForTest(players.Take(1).ToArray(), acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING_86_ARTIFACT");
            run.CurrentActIndex = 2;
            var encounter = ModelDb.AllEncounters.Single(model => model.RoomType == RoomType.Elite &&
                model.AllPossibleMonsters.Any(monster => monster is MechaKnight)).ToMutable();
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, encounter);
            foreach (var player in players.Skip(1))
            { battle.Combat.AddPlayer(player); player.ResetCombatState(); }
            run.PushRoom(new CombatRoom(encounter, run));
            var map = EliteRoot([players[0].NetId], hunted);
            JigsawCheckpoint.Capture(run, map);
            JigsawNativeSession.Restore(run);
            try
            {
                NativeEventTests.Check(JigsawNativeSession.Current!.Map!.CurrentRoomHunted == hunted,
                    "The real session reads the accepted root snapshot, independently of personal relics.");
                await battle.Monster.AfterAddedToRoom();
                int expected = hunted ? 5 : 3 + count - 1;
                NativeEventTests.Check(battle.Monster.Creature.GetPowerAmount<ArtifactPower>() == expected,
                    $"players={count}, hunted={hunted}: native initialization must grant Artifact {expected}.");
                var other = battle.Combat.CreateCreature(ModelDb.Monster<MechaKnight>().ToMutable(), CombatSide.Enemy, null);
                battle.Combat.AddCreature(other);
                await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), other, 2, other, null);
                NativeEventTests.Check(other.GetPowerAmount<ArtifactPower>() == 2 + count - 1,
                    "A separate Artifact source still uses native multiplayer scaling after initialization.");
            }
            finally { JigsawNativeSession.Current?.Close(); }
        }
    }

    private static JigsawRun EliteRoot(ulong[] players, bool hunted)
    {
        var map = JigsawRun.Create(75, budget: 13, players: players);
        foreach (ulong player in players) Ok(map.ChooseEnd(player, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        foreach (ulong player in players) Ok(map.CompleteAncient(player));
        foreach (ulong player in players) Ok(map.Continue(player));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var pending = new Queue<int>(); pending.Enqueue(0);
        while (pending.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (map.Nodes.Single(node => node.Id == next).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss) && previous.TryAdd(next, current))
                    pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == JigsawRoom.Elite && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        Ok(map.SetRemainingSteps(hunted ? 0 : 100));
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!; Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        return map;
    }

    private static void Ok(JigsawResult result) => NativeEventTests.Check(result.Success, result.Reason);
}
