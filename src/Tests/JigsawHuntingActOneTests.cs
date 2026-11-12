using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

internal static class JigsawHuntingActOneTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("JigsawHuntingActOne native Byrdonis gains fixed regeneration and retains Territorial", ByrdonisOpening),
      ("JigsawHuntingActOne native Effigy opens Awake and retains Slow and Slash", EffigyOpening),
      ("JigsawHuntingActOne native Kin orbs apply two once before Artifact and keep target subset", KinOrbs),
      ("JigsawHuntingActOne native second-phase Beast Cry retains Ringing and debuffs all living players", BeastCry),
      ("JigsawHuntingActOne native Phrog death summons five worms with original spawn turn and ordered first actions", PhrogWorms),
      ("JigsawHuntingActOne native Vantom opens between two default nonminion Inklets", VantomOpening),
      ("JigsawHuntingActOne unpunished native Vantom remains the original lone boss", VantomUnpunished),
      ("JigsawHuntingActOne native Vantom scope preserves nonjigsaw and other hunted bosses", VantomScope),
      ("JigsawHuntingActOne native nonjigsaw Vantom generation needs no act data", VantomWithoutActs),
      ("JigsawHuntingActOne Byrdonis later regeneration sources retain native multiplayer scaling", ByrdonisOtherRegeneration),
      ("JigsawHuntingActOne native Kin orb does not alter a reentrant same giver and power hook", KinReentrant),
      ("JigsawHuntingActOne Phrog nonzero native death preserves five node visibility", () =>
          NativeOpening<PhrogParasite>(JigsawHuntingPhrogNodeTests.NonzeroDeath, maxPlayers: 1)),
      ("JigsawHuntingActOne old Phrog death cannot change a replacement native room", () =>
          NativeOpening<PhrogParasite>(JigsawHuntingPhrogNodeTests.OldReveal, maxPlayers: 1)),
      ("JigsawHuntingActOne Phrog five native hitboxes intents and health bars fit compact and wide rooms", () =>
          NativeOpening<PhrogParasite>(JigsawHuntingPhrogNodeTests.Geometry)),
      ("JigsawHuntingActOne Phrog node cleanup restores globals before later native UI use", () =>
          NativeOpening<PhrogParasite>(JigsawHuntingPhrogNodeTests.Cleanup, maxPlayers: 1)) ];

    private static Task VantomOpening() => NativeOpening<Vantom>(vantom =>
    {
        var combat = vantom.Monster!.CombatState;
        var enemies = combat.Enemies.ToArray();
        Check(enemies.Length == 3 && enemies[0].Monster is Inklet &&
            ReferenceEquals(enemies[1], vantom) && enemies[2].Monster is Inklet,
            $"The actual hunted native Vantom opening is ordered Inklet, Vantom, Inklet. players={combat.Players.Count}, enemies={string.Join(",", enemies.Select(creature => creature.Monster!.Id.Entry))}");
        var inklets = new[] { enemies[0], enemies[2] };
        int SlipperyAmount(Creature creature, int amount) => combat.Players.Count == 1 ? amount :
            (int)ModelDb.Power<SlipperyPower>().GetScaledAmountForMultiplayer(combat, creature, amount, creature, null);
        Check(!ReferenceEquals(inklets[0].Monster, inklets[1].Monster) && inklets.All(creature =>
            creature.Monster is Inklet { MiddleInklet: false } inklet && inklet.NextMove.Id == "JAB_MOVE" &&
            creature.GetPowerAmount<SlipperyPower>() == SlipperyAmount(creature, 1) && !creature.HasPower<MinionPower>()),
            "Both distinct native Inklets retain default Jab and native scaled Slippery without becoming Minions. " +
            string.Join(";", inklets.Select(creature => $"players={combat.Players.Count},middle={((Inklet)creature.Monster!).MiddleInklet},move={creature.Monster.NextMove.Id},slippery={creature.GetPowerAmount<SlipperyPower>()},minion={creature.HasPower<MinionPower>()}")));
        Check(vantom.GetPowerAmount<SlipperyPower>() == SlipperyAmount(vantom, ((Vantom)vantom.Monster).SlipperyAmt),
            "The boss retains its original native Slippery opening.");
        return Task.CompletedTask;
    }, RoomType.Boss);

    private static Task VantomUnpunished() => NativeOpening<Vantom>(vantom =>
    {
        Check(vantom.Monster!.CombatState.Enemies.Single() == vantom &&
            !JigsawHuntingActOne.HasSpecialRules(vantom.Monster),
            "The same actual unpunished native Boss root retains only Vantom and has no hunting special rules.");
        return Task.CompletedTask;
    }, RoomType.Boss, hunted: false);

    private static async Task VantomScope()
    {
        await NativeOpening<Vantom>(vantom =>
        {
            Check(vantom.Monster!.CombatState.Enemies.Single() == vantom &&
                JigsawNativeSession.TryGet((RunState)vantom.CombatState!.RunState) == null,
                "A real standalone native Vantom Boss without a Jigsaw session keeps its sole original monster.");
            return Task.CompletedTask;
        }, RoomType.Boss, jigsaw: false);
        await NativeOpening<KinPriest>(priest =>
        {
            Check(JigsawHuntingActOne.HasSpecialRules(priest.Monster!) &&
                priest.CombatState!.Enemies.All(creature => creature.Monster is not Inklet),
                "A different actual hunted native Boss retains its own enemy roster without Vantom additions.");
            return Task.CompletedTask;
        }, RoomType.Boss);
    }

    private static Task VantomWithoutActs()
    {
        var run = HeadlessTestSetup.Run("HUNTING84_NONJIGSAW_NO_ACTS", ModelDb.Character<Ironclad>());
        var encounter = ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.VantomBoss>().ToMutable();
        Check(JigsawNativeSession.TryGet(run) == null,
            "The native standalone encounter has no Jigsaw session or hunting root.");
        encounter.GenerateMonstersWithSlots(run);
        Check(encounter.MonstersWithSlots.Count == 1 && encounter.MonstersWithSlots[0].Item1 is Vantom,
            "Native non-Jigsaw Vantom generation keeps its original lone boss without reading absent act data.");
        return Task.CompletedTask;
    }

    private static Task ByrdonisOpening() => NativeOpening<Byrdonis>(bird =>
    {
        Check(bird.GetPowerAmount<RegenPower>() == 6 && bird.GetPowerAmount<TerritorialPower>() == 1,
            "The actual native opening applies fixed six Regen and preserves native Territorial for every player count.");
        return Task.CompletedTask;
    });

    private static Task EffigyOpening() => NativeOpening<BygoneEffigy>(async effigy =>
    {
        var monster = effigy.Monster!;
        Check(monster.NextMove.Id == "WAKE_MOVE" && effigy.GetPowerAmount<SlowPower>() == 1,
            "The hunted native Effigy starts Awake and preserves its original Slow.");
        await monster.NextMove.PerformMove(monster.CombatState.PlayerCreatures);
        monster.MoveStateMachine.OnMovePerformed(monster.NextMove);
        monster.RollMove(monster.CombatState.PlayerCreatures);
        Check(monster.NextMove.Id == "SLASHES_MOVE" && effigy.GetPowerAmount<StrengthPower>() == 10,
            "The actual native Wake adds unchanged ten Strength and follows the original Slash state.");
    });

    private static Task ByrdonisOtherRegeneration() => NativeOpening<Byrdonis>(async bird =>
    {
        var combat = bird.Monster!.CombatState;
        await PowerCmd.Remove(bird.GetPower<RegenPower>());
        await PowerCmd.Apply<RegenPower>(new ThrowingPlayerChoiceContext(), bird, 3, combat.PlayerCreatures[0], null);
        decimal expected = combat.Players.Count == 1 ? 3 :
            3 * combat.Players.Count * MultiplayerScalingModel.GetMultiplayerScaling(combat.Encounter, combat.RunState.CurrentActIndex);
        Check(bird.GetPowerAmount<RegenPower>() == (int)expected,
            "A later independent native Regen application to the same hunted bird retains the original multiplayer scaling.");
    });

    private static async Task KinReentrant()
    {
        ModelDb.Inject(typeof(ReentrantKinPower));
        await NativeOpening<KinPriest>(async priest =>
        {
            var monster = priest.Monster!;
            if (monster.CombatState.Players.Count < 2) return;
            var first = monster.CombatState.PlayerCreatures[0];
            var other = monster.CombatState.PlayerCreatures.Last();
            await PowerCmd.Apply<ReentrantKinPower>(new ThrowingPlayerChoiceContext(), first, 1, null, null);
            monster.SetMoveImmediate((MoveState)monster.MoveStateMachine.States["ORB_OF_FRAILTY_MOVE"], true);
            await monster.NextMove.PerformMove([first]);
            Check(first.GetPowerAmount<FrailPower>() == 2 && other.GetPowerAmount<FrailPower>() == 1,
                "Only the native orb's own application is two; a nested hook with the same giver/power/current move remains native one.");
        }, RoomType.Boss);
    }

    public sealed class ReentrantKinPower : PowerModel
    {
        private bool triggered;
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Single;
        public override async Task BeforePowerAmountChanged(PowerModel power, decimal amount, Creature target, Creature? applier, CardModel? cardSource)
        {
            if (triggered || target != Owner || power is not FrailPower || applier?.Monster is not KinPriest)
                return;
            triggered = true;
            await PowerCmd.Apply<FrailPower>(new ThrowingPlayerChoiceContext(), CombatState.PlayerCreatures.Last(), 1, applier, null);
        }
    }

    private static Task KinOrbs() => NativeOpening<KinPriest>(async priest =>
    {
        var monster = priest.Monster!;
        var targets = monster.CombatState.PlayerCreatures.Take(1).ToArray();
        foreach (var player in monster.CombatState.Players)
        { player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(1000); }
        var context = new ThrowingPlayerChoiceContext();
        foreach (string move in new[] { "ORB_OF_FRAILTY_MOVE", "ORB_OF_WEAKNESS_MOVE" })
        {
            foreach (bool artifact in new[] { true, false })
            {
                if (artifact) await PowerCmd.Apply<ArtifactPower>(context, targets[0], 1, null, null);
                int historyBefore = CombatManager.Instance.History.Entries.Count();
                monster.SetMoveImmediate((MoveState)monster.MoveStateMachine.States[move], true);
                await monster.NextMove.PerformMove(targets);
                int amount = move == "ORB_OF_FRAILTY_MOVE" ? targets[0].GetPowerAmount<FrailPower>() : targets[0].GetPowerAmount<WeakPower>();
                Check(amount == (artifact ? 0 : 2) && targets[0].GetPowerAmount<ArtifactPower>() == 0,
                    $"Each native orb is one application of two: one Artifact blocks it completely. players={monster.CombatState.Players.Count}, move={move}, artifact={artifact}, amount={amount}, remainingArtifact={targets[0].GetPowerAmount<ArtifactPower>()}");
                var received = CombatManager.Instance.History.Entries.Skip(historyBefore).OfType<PowerReceivedEntry>()
                    .Where(entry => entry.Applier == priest && entry.Power is FrailPower or WeakPower).ToArray();
                Check(artifact ? received.Length == 0 : received.Length == 1 && received[0].Amount == 2,
                    "A blocked orb has no received debuff; an unblocked orb records exactly one native application of two.");
            }
            int stackHistoryBefore = CombatManager.Instance.History.Entries.Count();
            monster.SetMoveImmediate((MoveState)monster.MoveStateMachine.States[move], true);
            await monster.NextMove.PerformMove(targets);
            int stacked = move == "ORB_OF_FRAILTY_MOVE" ? targets[0].GetPowerAmount<FrailPower>() : targets[0].GetPowerAmount<WeakPower>();
            Check(stacked == 4 && monster.CombatState.PlayerCreatures.Skip(1).All(player =>
                !player.HasPower<FrailPower>() && !player.HasPower<WeakPower>()),
                "Existing powers gain two through the same native orb and non-targeted players receive no debuff.");
            var stackedEntries = CombatManager.Instance.History.Entries.Skip(stackHistoryBefore).OfType<PowerReceivedEntry>()
                .Where(entry => entry.Applier == priest && entry.Power is FrailPower or WeakPower).ToArray();
            Check(stackedEntries.Length == 1 && stackedEntries[0].Amount == 2,
                "Native stacking records one increment of two, never a native one followed by an extra one.");
        }
    }, RoomType.Boss);

    private static Task PhrogWorms() => NativeOpening<PhrogParasite>(async phrog =>
    {
        var combat = phrog.Monster!.CombatState;
        Check(phrog.HasPower<InfestedPower>(), "The native Phrog opening owns its original Infested death listener.");
        using var lifecycle = new JigsawHuntingCombatTests.NativeEndingScope();
        await CreatureCmd.Kill(phrog);
        var worms = combat.Enemies.Where(creature => creature.Monster is Wriggler)
            .OrderBy(creature => creature.SlotName, StringComparer.Ordinal).ToArray();
        Check(worms.Length == 5 && worms.All(creature => creature.IsAlive) && !CombatManager.Instance.IsEnding,
            $"Actual native death produces five live worms and keeps the same combat active. count={worms.Length}, living={worms.Count(creature => creature.IsAlive)}, ending={CombatManager.Instance.IsEnding}");
        for (int index = 0; index < worms.Length; index++)
        {
            var worm = worms[index].Monster!;
            Check(worms[index].SlotName == "wriggler" + (index + 1) && worm.NextMove.Id == "SPAWNED_MOVE",
                "Every native worm retains its numbered formal slot and the original empty spawn action.");
            int[] hp = combat.PlayerCreatures.Select(player => player.CurrentHp).ToArray();
            await worm.NextMove.PerformMove(combat.PlayerCreatures);
            worm.MoveStateMachine.OnMovePerformed(worm.NextMove); worm.RollMove(combat.PlayerCreatures);
            Check(combat.PlayerCreatures.Select(player => player.CurrentHp).SequenceEqual(hp) &&
                worm.NextMove.Id == (index % 2 == 0 ? "NASTY_BITE_MOVE" : "WRIGGLE_MOVE"),
                "Spawn is a true empty turn; first actual moves are native Bite on 1/3/5 and Wriggle on 2/4.");
        }
    });

    private static Task BeastCry() => NativeOpening<CeremonialBeast>(async beast =>
    {
        var monster = (CeremonialBeast)beast.Monster!;
        var allPlayers = monster.CombatState.PlayerCreatures.ToArray();
        if (allPlayers.Length > 1) allPlayers[^1].SetCurrentHpInternal(0);
        await monster.SetStunned();
        Check(monster.IsInSecondPhase, "The native Plow-removal transition enters the second phase.");
        await monster.StunnedMove(monster.CombatState.PlayerCreatures);
        monster.SetMoveImmediate(monster.BeastCryState, true);
        // Native Ringing follows the passed targets; the hunting additions are
        // explicitly all living players, so exercise a strict subset here.
        var originalTargets = monster.CombatState.PlayerCreatures.Take(1).ToArray();
        await monster.NextMove.PerformMove(originalTargets);
        Check(originalTargets[0].GetPowerAmount<RingingPower>() == 1 &&
            monster.CombatState.PlayerCreatures.Skip(1).All(player => !player.HasPower<RingingPower>()),
            "The original native Ringing application and its target subset remain unchanged.");
        Check(allPlayers.Where(player => player.IsAlive).All(player =>
            player.GetPowerAmount<WeakPower>() == 2 && player.GetPowerAmount<FrailPower>() == 2),
            "Second-phase Beast Cry adds fixed two Weak and Frail to every living player.");
        Check(allPlayers.Where(player => !player.IsAlive).All(player =>
            !player.HasPower<WeakPower>() && !player.HasPower<FrailPower>()),
            "The added cry debuffs do not apply to dead players.");
        var received = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
            .Where(entry => entry.Applier == beast && entry.Power is RingingPower or WeakPower or FrailPower).ToArray();
        int ringingIndex = Array.FindIndex(received, entry => entry.Power is RingingPower);
        Check(ringingIndex == 0 && received.Length == 1 + 2 * allPlayers.Count(player => player.IsAlive) &&
            received.Skip(1).All(entry => entry.Amount == 2),
            "Native Ringing resolves first, followed by one application of each added debuff per living player.");
    }, RoomType.Boss);

    private static async Task NativeOpening<T>(Func<Creature, Task> assertion, RoomType tier = RoomType.Elite, int maxPlayers = 4, bool hunted = true, bool jigsaw = true) where T : MonsterModel
    {
        for (int count = 1; count <= maxPlayers; count++)
        {
            using var eventScope = HeadlessCombatInProgress.EnterEvent();
            var players = Enumerable.Range(1, count).Select(id =>
                Player.CreateForNewRun(ModelDb.Character<Ironclad>(), UnlockState.all, (ulong)id)).ToArray();
            var native = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Overgrowth>()], seed: "HUNTING84_" + typeof(T).Name);
            var manager = RunManager.Instance;
            manager.SetUpTest(native, new NetSingleplayerGameService());
            ulong? previousNetId = LocalContext.NetId;
            LocalContext.NetId = 1;
            try
            {
                var encounter = ModelDb.AllEncounters.First(model => model.RoomType == tier &&
                    model.AllPossibleMonsters.Any(monster => monster is T)).ToMutable();
                JigsawRoom logicalRoom = tier == RoomType.Boss ? JigsawRoom.Boss : JigsawRoom.Elite;
                var map = JigsawRun.Create(75, players: [1UL]);
                Ok(map.ChooseEnd(1, EndChoice.Automatic));
                Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
                Ok(map.CompleteAncient(1));
                Ok(map.Continue(1));
                // Follow a legal domain route; native room construction below is
                // real, while this case isolates its own monster-opening rule.
                var previous = new Dictionary<int, int> { [0] = -1 };
                var pending = new Queue<int>(); pending.Enqueue(0);
                while (pending.TryDequeue(out int current))
                    foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
                    {
                        int next = edge.First == current ? edge.Second : edge.First;
                        if ((map.Nodes.Single(node => node.Id == next).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss) ||
                            map.Nodes.Single(node => node.Id == next).Room == logicalRoom) &&
                            previous.TryAdd(next, current)) pending.Enqueue(next);
                    }
                int target = map.Nodes.First(node => node.Room == logicalRoom && previous.ContainsKey(node.Id)).Id;
                var path = new List<int>();
                for (int node = target; node != 0; node = previous[node]) path.Add(node);
                path.Reverse();
                Ok(map.SetRemainingSteps(hunted ? 0 : 1000));
                foreach (int node in path)
                {
                    var entry = map.BeginEnter(node)!; Ok(map.ConfirmEnter(entry.Id));
                    if (node != target) Ok(map.CompleteRoom(entry.Id));
                }
                if (jigsaw)
                {
                    JigsawCheckpoint.Capture(native, map);
                    JigsawNativeSession.Restore(native);
                }
                // Native debug enrollment supplies the requested combat-player
                // count after legal single-player root setup. This is command
                // scaling evidence, not a connected multiplayer session claim.
                native.AppendToMapPointHistory(tier == RoomType.Boss ? MapPointType.Boss : MapPointType.Elite, tier, encounter.Id);
                for (int index = 1; index < players.Length; index++) native.AddPlayerDebug(players[index], index);
                await manager.EnterRoom(new CombatRoom(encounter, native));
                var room = (CombatRoom)native.CurrentRoom!;
                eventScope.Dispose();
                await assertion(room.CombatState.Enemies.Single(creature => creature.Monster is T));
            }
            finally { CombatManager.Instance.Reset(true); manager.CleanUp(false); LocalContext.NetId = previousNetId; }
        }
    }

    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
}
