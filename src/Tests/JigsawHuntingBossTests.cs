using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

internal static class JigsawHuntingBossTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("Jigsaw hunting Boss Aeonglass initializes independent twelve card counters without Artifact", AeonglassOpening),
      ("Jigsaw hunting Boss Aeonglass mixed owner twelfth plays generate five ordered statuses and reset displays", AeonglassPlays),
      ("Jigsaw hunting Boss Test Subject first real revival replaces only its phase two grants", TestSubjectFirstRevival),
      ("Jigsaw hunting Boss Test Subject native second revival and nine complete player turns", TestSubjectTurns) ];

    private static Creature? heldSubject;
    private static TaskCompletionSource? revivalReached;
    private static TaskCompletionSource? releaseRevival;
    private static bool HoldSecondRevival(Creature creature, decimal amount, ref Task<decimal> __result)
    {
        if (!ReferenceEquals(creature, heldSubject)) return true;
        heldSubject = null;
        revivalReached!.TrySetResult();
        __result = Continue();
        return false;
        async Task<decimal> Continue()
        {
            await releaseRevival!.Task;
            return await CreatureCmd.SetMaxHp(creature, amount);
        }
    }

    private static bool SkipCombatFtue(ref bool __result) { __result = true; return false; }

    private static async Task Until(Func<bool> ready, string reason)
    {
        for (int frame = 0; frame < 300 && !ready(); frame++)
            await Godot.Engine.GetMainLoop().ToSignal(Godot.Engine.GetMainLoop(), Godot.SceneTree.SignalName.ProcessFrame);
        NativeEventTests.Check(ready(), reason);
    }

    private static async Task TestSubjectTurns()
    {
        var patch = new HarmonyLib.Harmony("AllMatters.Test.SubjectTurns");
        patch.Patch(HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Saves.SaveManager), "SeenFtue"),
            prefix: new HarmonyLib.HarmonyMethod(typeof(JigsawHuntingBossTests), nameof(SkipCombatFtue)));
        patch.Patch(HarmonyLib.AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp)),
            prefix: new HarmonyLib.HarmonyMethod(typeof(JigsawHuntingBossTests), nameof(HoldSecondRevival)));
        try
        {
          for (int count = 1; count <= 4; count++)
          {
            var players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            var run = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING_87_TURNS");
            run.CurrentActIndex = 2;
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.TestSubjectBoss>().ToMutable());
            foreach (var player in players.Skip(1))
            { player.RunState = run; battle.Combat.AddPlayer(player); player.ResetCombatState(); }
            run.PushRoom(new CombatRoom(battle.Combat.Encounter!, run));
            JigsawCheckpoint.Capture(run, BossRoot([players[0].NetId], true));
            JigsawNativeSession.Restore(run);
            using var lifecycle = new NativeRevivalScope();
            foreach (var player in players)
            {
                for (int card = 0; card < 100; card++) PileType.Draw.GetPile(player).AddInternal(battle.Combat.CreateCard<DefendIronclad>(player));
                await CreatureCmd.SetMaxHp(player.Creature, 5000);
                player.Creature.SetCurrentHpInternal(5000);
            }
            var manager = CombatManager.Instance;
            Task? start = null;
            int readyRound = 0;
            void PlayerTurnReady(CombatState state) { if (state.CurrentSide == CombatSide.Player) readyRound = state.RoundNumber; }
            manager.TurnStarted += PlayerTurnReady;
            try
            {
#if STS2_PUBLIC
            start = manager.StartCombatInternal();
#else
            var turnState = HarmonyLib.AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(manager);
            start = (Task)HarmonyLib.AccessTools.Method(typeof(CombatManager), "StartCombatInternal").Invoke(manager, [turnState])!;
#endif
            await Until(() => readyRound == battle.Combat.RoundNumber && players.All(player => PileType.Hand.GetPile(player).Cards.Count == 5), "The one real native coordinator completes its first five-card player setup.");
            for (int phase = 1; phase <= 2; phase++)
            {
                await CreatureCmd.Kill(battle.Monster.Creature);
                int oldRound = battle.Combat.RoundNumber;
                if (phase == 2)
                {
                    heldSubject = battle.Monster.Creature;
                    revivalReached = new(); releaseRevival = new();
                }
                manager.SetReadyToEndTurn(players[0], false);
                if (phase == 2)
                {
                    await Until(() => revivalReached!.Task.IsCompleted, "The real enemy phase reaches the second revival SetMaxHp.");
                    NativeEventTests.Check(battle.Combat.CurrentSide == CombatSide.Enemy && !battle.Monster.Creature.HasPower<NemesisPower>(),
                        "Second revival remains genuinely pending before native third-form grants and the next complete player turn.");
                    releaseRevival!.TrySetResult();
                }
                await Until(() => readyRound > oldRound && battle.Combat.CurrentSide == CombatSide.Player && players.All(player => PileType.Hand.GetPile(player).Cards.Count == 5),
                    "The native coordinator completes exactly one revival and starts the next player turn once.");
            }
            NativeEventTests.Check(battle.Monster.Creature.HasPower<NemesisPower>() && !battle.Monster.Creature.HasPower<AdaptablePower>() &&
                !battle.Monster.Creature.HasPower<SkittishPower>() && !battle.Monster.Creature.HasPower<SuckPower>(), "Native completed third form keeps Nemesis and clears phase-two powers.");
            for (int turn = 1; turn <= 9; turn++)
            {
              NativeEventTests.Check(battle.Monster.Creature.HasPower<AllMatters.Powers.ScrutinyPower>() == (turn % 3 == 0), "Only the monster holds Scrutiny at shared full turns3/6/9.");
              foreach (var player in players)
              {
                await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, player, fromHandDraw: false);
                NativeEventTests.Check(PileType.Hand.GetPile(player).Cards.Count == (turn % 3 == 0 ? 5 : 6),
                    $"Actual complete phase-three player turn {turn} allows opening draw but limits extra draw exactly at 3/6/9.");
                int hand = PileType.Hand.GetPile(player).Cards.Count;
                await CardPileCmd.AddGeneratedCardToCombat(battle.Combat.CreateCard<DefendIronclad>(player), PileType.Hand, player);
                NativeEventTests.Check(PileType.Hand.GetPile(player).Cards.Count == hand + 1, "Direct generated entry remains available on limited turns.");
              }
                int oldRound = battle.Combat.RoundNumber;
                manager.SetReadyToEndTurn(players[0], false);
                await Until(() => readyRound > oldRound && battle.Combat.CurrentSide == CombatSide.Player && players.All(player => PileType.Hand.GetPile(player).Cards.Count == 5),
                    "Native readiness drives one enemy phase and one next player start without duplicate StartTurn calls.");
            }
            }
            finally
            {
                heldSubject = null;
                releaseRevival?.TrySetResult();
                manager.Reset(true);
                try
                {
                    if (start != null)
                    {
                        try { await start; } catch (OperationCanceledException) { }
                    }
                }
                finally { manager.TurnStarted -= PlayerTurnReady; }
            }
            JigsawNativeSession.Current?.Close();
          }
        }
        finally
        {
            heldSubject = null; releaseRevival?.TrySetResult();
            CombatManager.Instance.Reset(true);
            patch.UnpatchAll(patch.Id);
            JigsawNativeSession.Current?.Close();
        }
    }

    private static async Task TestSubjectFirstRevival()
    {
        for (int count = 1; count <= 4; count++)
        foreach (bool hunted in new[] { false, true })
        {
            var players = Enumerable.Range(1, count)
                .Select(id => Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            var run = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING_87_REVIVE");
            run.CurrentActIndex = 2;
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run,
                ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.TestSubjectBoss>().ToMutable());
            foreach (var player in players.Skip(1))
            { player.RunState = run; battle.Combat.AddPlayer(player); player.ResetCombatState(); }
            run.PushRoom(new CombatRoom(battle.Combat.Encounter!, run));
            JigsawCheckpoint.Capture(run, BossRoot([players[0].NetId], hunted));
            JigsawNativeSession.Restore(run);
            try
            {
                await battle.Monster.AfterAddedToRoom();
                using var lifecycle = new NativeRevivalScope();
                await CreatureCmd.Kill(battle.Monster.Creature);
                NativeEventTests.Check(battle.Monster.NextMove.Id == "RESPAWN_MOVE" &&
                    battle.Combat.Enemies.Contains(battle.Monster.Creature) && !CombatManager.Instance.IsEnding,
                    "The real native death retains the adaptable subject and selects its actual revival instead of ending combat.");
                int historyStart = CombatManager.Instance.History.Entries.Count();
                await battle.Monster.PerformMove();
                var grants = CombatManager.Instance.History.Entries.Skip(historyStart).OfType<PowerReceivedEntry>().ToArray();
                var creature = battle.Monster.Creature;
                NativeEventTests.Check(creature.IsAlive && creature.CurrentHp == creature.MaxHp && creature.HasPower<AdaptablePower>(),
                    "The completed native first revival restores full scaled second-form HP and retains its later revival.");
                NativeEventTests.Check(creature.GetPowerAmount<SkittishPower>() == (hunted ? 10 : 0) &&
                    creature.GetPowerAmount<SuckPower>() == (hunted ? 3 : 0) &&
                    creature.GetPowerAmount<PainfulStabsPower>() == (hunted ? 0 : 1),
                    $"The real first revival grants fixed Skittish10/Suck3 without Painful Stabs only in the hunted boss; players={count}, hunted={hunted}.");
                NativeEventTests.Check(grants.Count(entry => entry.Power is PainfulStabsPower) == (hunted ? 0 : 1) &&
                    grants.Count(entry => entry.Power is SkittishPower && entry.Amount == 10) == (hunted ? 1 : 0) &&
                    grants.Count(entry => entry.Power is SuckPower && entry.Amount == 3) == (hunted ? 1 : 0),
                    "Actual native history records only the intended phase-two grants, with no scaled grant followed by correction.");
                if (hunted)
                {
                    await PowerCmd.Remove<SkittishPower>(creature);
                    await PowerCmd.Apply<SkittishPower>(new ThrowingPlayerChoiceContext(), creature, 3, creature, null);
                    NativeEventTests.Check(creature.GetPowerAmount<SkittishPower>() == new[] { 3, 4, 6, 7 }[count - 1],
                        "A later independent Skittish grant to the same subject retains its original multiplayer scaling.");
                }
                battle.Monster.RollMove(battle.Combat.PlayerCreatures);
                NativeEventTests.Check(battle.Monster.NextMove.Id == "MULTI_CLAW_MOVE", "The original phase-two progression remains intact.");
            }
            finally { JigsawNativeSession.Current?.Close(); }
        }
    }

    private sealed class NativeRevivalScope : IDisposable
    {
        private readonly HarmonyLib.Harmony host = new("AllMatters.TestHost");
        internal NativeRevivalScope()
        {
            host.Unpatch(HarmonyLib.AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                HarmonyLib.AccessTools.Method(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Unpatch(HarmonyLib.AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                HarmonyLib.AccessTools.Method(typeof(HeadlessCombatNotEnding), "Prefix"));
#if STS2_PUBLIC
            HarmonyLib.AccessTools.Property(typeof(CombatManager), nameof(CombatManager.IsInProgress)).SetValue(CombatManager.Instance, true);
#else
            var state = HarmonyLib.AccessTools.Field(typeof(CombatManager), "_turnState").GetValue(CombatManager.Instance)!;
            HarmonyLib.AccessTools.Property(state.GetType(), "IsInProgress").SetValue(state, true);
#endif
        }
        public void Dispose()
        {
            host.Patch(HarmonyLib.AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsInProgress)),
                prefix: new HarmonyLib.HarmonyMethod(typeof(HeadlessCombatInProgress), "Prefix"));
            host.Patch(HarmonyLib.AccessTools.PropertyGetter(typeof(CombatManager), nameof(CombatManager.IsEnding)),
                prefix: new HarmonyLib.HarmonyMethod(typeof(HeadlessCombatNotEnding), "Prefix"));
        }
    }

    private static async Task AeonglassPlays()
    {
        for (int count = 1; count <= 4; count++)
        foreach (bool overflow in new[] { false, true })
        {
            var players = Enumerable.Range(1, count)
                .Select(id => Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            var run = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING_87_PLAYS");
            run.CurrentActIndex = 2;
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run,
                ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.AeonglassBoss>().ToMutable());
            // Extra combat roles have the same real run context for native card
            // history/hooks; they are not a connected multiplayer session.
            foreach (var player in players.Skip(1))
            { player.RunState = run; battle.Combat.AddPlayer(player); player.ResetCombatState(); }
            run.PushRoom(new CombatRoom(battle.Combat.Encounter!, run));
            JigsawCheckpoint.Capture(run, BossRoot([players[0].NetId], true));
            JigsawNativeSession.Restore(run);
            var observers = new List<(WitheringPresencePower Power, Action Handler)>();
            try
            {
                await battle.Monster.AfterAddedToRoom();
                var counters = battle.Monster.Creature.Powers.OfType<WitheringPresencePower>().ToArray();
                NativeEventTests.Check(counters.Length == count, "Each native combat role has a separate targeted presence.");
                var displays = players.ToDictionary(player => player, _ => new List<int>());
                foreach (var counter in counters)
                {
                    Action observer = () => displays[counter.Target.Player].Add(counter.DisplayAmount);
                    counter.DisplayAmountChanged += observer;
                    observers.Add((counter, observer));
                }
                if (overflow)
                    foreach (var player in players)
                        for (int index = 0; index < 9; index++)
                            await CardPileCmd.AddGeneratedCardToCombat(battle.Combat.CreateCard<DefendIronclad>(player), PileType.Hand, player);

                for (int play = 0; play < 5; play++)
                    foreach (var player in players) await Play(player);
                for (int play = 5; play < 11; play++) await Play(players[0]);
                NativeEventTests.Check(Counter(players[0]).DisplayAmount == 1 && players.Skip(1).All(player => Counter(player).DisplayAmount == 7),
                    "Mixed native plays leave the first owner at eleven and other owners at five, independently.");
                NativeEventTests.Check(players.All(player => Statuses(player).Length == 0), "No owner receives a status before its own twelfth play.");

                await Play(players[0]);
                CheckTrigger(players[0]);
                NativeEventTests.Check(players.Skip(1).All(player => Counter(player).DisplayAmount == 7 && Statuses(player).Length == 0),
                    "The first owner's trigger neither generates cards nor consumes the other owners' counters.");
                await Play(players[0]);
                NativeEventTests.Check(Counter(players[0]).DisplayAmount == 11 && Statuses(players[0]).Length == 5,
                    "The thirteenth native play starts a new twelve-card period without a second status batch.");
                foreach (var player in players.Skip(1))
                {
                    for (int play = 5; play < 12; play++) await Play(player);
                    CheckTrigger(player);
                    NativeEventTests.Check(Counter(players[0]).DisplayAmount == 11,
                        "A later owner's trigger does not reset the first owner's thirteenth-play value.");
                }
                foreach (var player in players)
                {
                    int[] expected = Enumerable.Range(1, 12).Select(play => 12 - play).Concat(new[] { 12 })
                        .Concat(player == players[0] ? new[] { 11 } : Array.Empty<int>()).ToArray();
                    NativeEventTests.Check(displays[player].SequenceEqual(expected),
                        "Actual display events expose each decrement, zero and reset twelve, with no updates from another owner's cards.");
                }

                WitheringPresencePower Counter(Player player) => counters.Single(power => power.Target.Player == player);
                CardModel[] Statuses(Player player) => PileType.Hand.GetPile(player).Cards.Concat(PileType.Discard.GetPile(player).Cards)
                    .Where(card => card is Wound or Burn or MegaCrit.Sts2.Core.Models.Cards.Void or Dazed or Wither).ToArray();
                async Task Play(Player player)
                {
                    var card = battle.Combat.CreateCard<DefendIronclad>(player);
                    await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, player);
                    await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), card, null, skipCardPileVisuals: true);
                }
                void CheckTrigger(Player player)
                {
                    var statuses = Statuses(player);
                    NativeEventTests.Check(statuses.Length == 5 && statuses[0] is Wound && statuses[1] is Burn &&
                        statuses[2] is MegaCrit.Sts2.Core.Models.Cards.Void && statuses[3] is Dazed && statuses[4] is Wither &&
                        statuses.All(card => card.Owner == player), "The actual owner's twelfth play generates exactly Wound, Burn, Void, Dazed, Wither in order.");
                    NativeEventTests.Check(Counter(player).DisplayAmount == 12 && Counter(player).DynamicVars["CardsLeft"].IntValue == 12,
                        "The actual owner's twelfth-play counter and display reset to twelve.");
                    var hand = PileType.Hand.GetPile(player).Cards;
                    NativeEventTests.Check(overflow ? hand.Count == 10 && statuses[0].Pile == PileType.Hand.GetPile(player) &&
                        statuses.Skip(1).All(card => card.Pile == PileType.Discard.GetPile(player)) :
                        hand.Count == 5 && statuses.All(card => card.Pile == PileType.Hand.GetPile(player)),
                        "Native pile insertion keeps all five in an empty hand and spills only the last four from a nine-card hand to discard.");
                }
            }
            finally
            {
                foreach (var observer in observers) observer.Power.DisplayAmountChanged -= observer.Handler;
                JigsawNativeSession.Current?.Close();
            }
        }
    }

    // N seam agreed in #73: real mutable monster initialization and native power commands.
    // This does not claim full room entry, live turn scheduling, layout or save recovery.
    private static async Task AeonglassOpening()
    {
        using var eventScope = HeadlessCombatInProgress.EnterEvent();
        for (int count = 1; count <= 4; count++)
        foreach (bool hunted in new[] { false, true })
        {
            var players = Enumerable.Range(1, count)
                .Select(id => Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            // One real session owner; extra combat roles exercise per-target
            // counters and power scaling, without claiming connected multiplayer.
            var run = RunState.CreateForTest(players.Take(1).ToArray(), acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING_87_AEONGLASS");
            run.CurrentActIndex = 2;
            var encounter = ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.AeonglassBoss>().ToMutable();
            using var battle = new HeadlessTestSetup.EncounterCombatScope(run, encounter);
            foreach (var player in players.Skip(1))
            { battle.Combat.AddPlayer(player); player.ResetCombatState(); }
            run.PushRoom(new CombatRoom(encounter, run));
            var map = BossRoot([players[0].NetId], hunted);
            JigsawCheckpoint.Capture(run, map);
            JigsawNativeSession.Restore(run);
            try
            {
                NativeEventTests.Check(JigsawNativeSession.Current!.Map!.CurrentRoomHunted == hunted,
                    "The real session reads the accepted root snapshot, independently of personal relics.");
                int previousHistory = CombatManager.Instance.History.Entries.Count();
                await battle.Monster.AfterAddedToRoom();
                var grants = CombatManager.Instance.History.Entries.Skip(previousHistory).OfType<PowerReceivedEntry>().ToArray();
                NativeEventTests.Check(grants.Count(entry => entry.Power is WitheringPresencePower) == count &&
                    grants.Count(entry => entry.Power is ArtifactPower) == (hunted ? 0 : 1),
                    "The opening history contains each presence and no hunted Artifact grant, rather than granting and removing it.");
                var counters = battle.Monster.Creature.Powers.OfType<WitheringPresencePower>().ToArray();
                NativeEventTests.Check(counters.Length == count && counters.Select(power => power.Target.Player.NetId).ToHashSet()
                    .SetEquals(players.Select(player => player.NetId)), "Native opening creates one independent counter targeting each player's actual creature.");
                foreach (var counter in counters)
                    NativeEventTests.Check(counter.DisplayAmount == (hunted ? 12 : 6) && counter.DynamicVars["CardsLeft"].IntValue == (hunted ? 12 : 6),
                        "The real power display and initial remaining-card value agree with this room's rule.");
                int expected = hunted ? 0 : 3 + count - 1;
                NativeEventTests.Check(battle.Monster.Creature.GetPowerAmount<ArtifactPower>() == expected,
                    $"players={count}, hunted={hunted}: native initialization must grant Artifact {expected}.");
            }
            finally { JigsawNativeSession.Current?.Close(); }
        }
    }

    private static JigsawRun BossRoot(ulong[] players, bool hunted)
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
                if (map.Nodes.Single(node => node.Id == next).Room != JigsawRoom.SecondBoss && previous.TryAdd(next, current))
                    pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == JigsawRoom.Boss && previous.ContainsKey(node.Id)).Id;
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
