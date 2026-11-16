using AllMatters.Jigsaw;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.ValueProps;

internal static class JigsawHuntingWaterfallTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [("Jigsaw hunting Waterfall About to Blow execution cleanses and grants three Artifact after first lethal damage", ExecutionTiming),
     ("Jigsaw hunting Waterfall About to Blow action removes all debuffs and preserves buffs and native transition", TriggerFiltering),
     ("Jigsaw hunting Waterfall grants three Artifact for one to four combat roles while independent sources and blocking stay native", ArtifactSources),
     ("Jigsaw hunting Waterfall About to Blow action leaves safe unrelated event and stale combat scopes unchanged", TriggerBoundaries),
     ("Jigsaw hunting Waterfall native moves preserve Steam Eruption capture explosion damage and final death", NativeExplosion),
     ("Jigsaw hunting Waterfall native trigger rejects stale awaited work and propagates its dependency failure", AwaitedTriggerBoundaries),
     ("Jigsaw hunting Waterfall nested Artifact source retains native scaling before the fixed grant", ReentrantArtifact),
     ("Jigsaw hunting Waterfall first lethal damage defers Artifact until its action only for the hunted root", FirstLethalDamage)];

    private static async Task ExecuteAboutToBlow(WaterfallGiant giant)
    {
        await giant.TriggerAboutToBlowState();
        await giant.PerformMove();
    }

    private static async Task ExecutionTiming()
    {
        ModelDb.Inject(typeof(HiddenWaterfallState));
        foreach (int count in new[] { 1, 4 })
        {
            using var fixture = new WaterfallBattle(count);
            var giant = fixture.Giant;
            var target = giant.Creature;
            var context = new ThrowingPlayerChoiceContext();
            await giant.AfterAddedToRoom();
            giant.RollMove(fixture.Battle.Combat.PlayerCreatures);
            await giant.PerformMove();
            await CreatureCmd.Damage(context, target, target.CurrentHp,
                ValueProp.Unblockable | ValueProp.Unpowered, fixture.Players[0].Creature);
            Check(target.IsAlive && giant.NextMove.Id == "ABOUT_TO_BLOW_MOVE" && !target.HasPower<ArtifactPower>(),
                "First lethal damage enters About to Blow without granting Artifact before the action executes.");
            await PowerCmd.Apply<WeakPower>(context, target, 2, fixture.Players[0].Creature, null);
            await PowerCmd.Apply<StrengthPower>(context, target, -4, fixture.Players[0].Creature, null);
            await PowerCmd.Apply<HiddenWaterfallState>(context, target, 6, target, null);
            Check(target.HasPower<WeakPower>(), "The waiting phase still accepts debuffs before its action.");
            await giant.PerformMove();
            Check(!target.HasPower<SteamEruptionPower>() && !target.HasPower<WeakPower>() &&
                !target.HasPower<StrengthPower>() && !target.HasPower<HiddenWaterfallState>() && target.GetPowerAmount<ArtifactPower>() == 3,
                $"roles={count}: completing the real About to Blow action consumes Steam, clears negative powers and grants exactly three Artifact.");
            await PowerCmd.Apply<WeakPower>(context, target, 1, fixture.Players[0].Creature, null);
            Check(!target.HasPower<WeakPower>() && target.GetPowerAmount<ArtifactPower>() == 2,
                "The post-action Artifact survives death cleanup and blocks a real later debuff.");
        }
    }

    private static async Task FirstLethalDamage()
    {
        ModelDb.Inject(typeof(HiddenWaterfallState));
        foreach (int count in new[] { 1, 4 })
        foreach (var scope in new[] { BattleScope.Safe, BattleScope.Hunted })
        {
            using var fixture = new WaterfallBattle(count, scope);
            var giant = fixture.Giant;
            var target = giant.Creature;
            var context = new ThrowingPlayerChoiceContext();
            foreach (var player in fixture.Players)
            { player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(1000); }
            await giant.AfterAddedToRoom();
            giant.RollMove(fixture.Battle.Combat.PlayerCreatures);
            Check(giant.NextMove.Id == "PRESSURIZE_MOVE", "The first lethal hit starts from the native opening move.");
            await giant.PerformMove();
            Check(target.IsAlive && target.GetPowerAmount<SteamEruptionPower>() == 15 && !target.HasPower<ArtifactPower>(),
                "Native Pressurize establishes Steam Eruption without preloading the hunting Artifact.");
            await PowerCmd.Apply<WeakPower>(context, target, 2, fixture.Players[0].Creature, null);
            await PowerCmd.Apply<RegenPower>(context, target, 2, target, null);
            await PowerCmd.Apply<HiddenWaterfallState>(context, target, 6, target, null);
            var regen = target.GetPower<RegenPower>()!;
            int regenBefore = regen.Amount;
            var hidden = target.GetPower<HiddenWaterfallState>()!;
            Check(regenBefore > 0 && hidden.Amount == 6 && !hidden.IsVisible,
                $"A real native-scaled positive power and exact hidden state are present before the lethal hit; Regen={regenBefore}, hidden={hidden.Amount}.");
            int historyStart = CombatManager.Instance.History.Entries.Count();

            // Await the entire damage/death cleanup, not a direct Trigger call or
            // its intermediate grant before the native death command returns.
            await CreatureCmd.Damage(context, target, target.CurrentHp,
                ValueProp.Unblockable | ValueProp.Unpowered, fixture.Players[0].Creature);

            Check(target.IsAlive && fixture.Battle.Combat.ContainsCreature(target) &&
                target.MaxHp == 999999999 && target.CurrentHp == 999999999 &&
                target.HpDisplay == HpDisplay.InfiniteWithoutNumbers &&
                giant.NextMove.Id == "ABOUT_TO_BLOW_MOVE" && giant.NextMove.Intents.Single() is StunIntent &&
                target.GetPowerAmount<SteamEruptionPower>() == 15,
                $"Scope={scope}, roles={count}: first lethal damage automatically enters the native living about-to-blow state and preserves Steam.");
            bool hunted = scope == BattleScope.Hunted;
            Check(hunted || !target.HasPower<RegenPower>() && !target.HasPower<HiddenWaterfallState>(),
                $"Scope={scope}, roles={count}: the safe root retains ordinary native death cleanup of Regen and hidden state.");
            Check(!hunted || ReferenceEquals(target.GetPower<RegenPower>(), regen) && regen.Amount == regenBefore &&
                ReferenceEquals(target.GetPower<HiddenWaterfallState>(), hidden) && hidden.Amount == 6,
                $"Scope={scope}, roles={count}: hunted revival preserves the positive Regen and exact hidden state after complete death cleanup; Regen={target.GetPowerAmount<RegenPower>()}, hidden={target.GetPowerAmount<HiddenWaterfallState>()}, Artifact={target.GetPowerAmount<ArtifactPower>()}.");
            var grants = CombatManager.Instance.History.Entries.Skip(historyStart).OfType<PowerReceivedEntry>()
                .Where(entry => ReferenceEquals(entry.Actor, target) && entry.Power is ArtifactPower).ToArray();
            Check(target.GetPowerAmount<ArtifactPower>() == 0 && grants.Length == 0,
                $"Scope={scope}, roles={count}: the complete lethal damage task grants no early Artifact; actual={target.GetPowerAmount<ArtifactPower>()}, grant amounts=[{string.Join(",", grants.Select(entry => entry.Amount))}].");
            // Safe powers may also disappear during ordinary death cleanup;
            // compare a fresh debuff after that cleanup instead of preserving Weak.
            await giant.PerformMove();
            await PowerCmd.Apply<WeakPower>(context, target, 2, fixture.Players[0].Creature, null);
            Check(target.GetPowerAmount<WeakPower>() == (hunted ? 0 : 2) && target.GetPowerAmount<ArtifactPower>() == (hunted ? 2 : 0),
                $"Scope={scope}, roles={count}: the retained hunting Artifact really blocks one new Weak and is consumed, while the safe target receives Weak.");
            await PowerCmd.Apply<ArtifactPower>(context, target, 2, target, null);
            await PowerCmd.Apply<StrengthPower>(context, target, 3, target, null);
            Check(target.GetPowerAmount<ArtifactPower>() == (hunted ? 4 : count + 1) && target.GetPowerAmount<StrengthPower>() > 0,
                "Independent post-transition powers are present before the final real death.");
            giant.RollMove(fixture.Battle.Combat.PlayerCreatures);
            Check(!target.HasPower<SteamEruptionPower>() && giant.NextMove.Id == "EXPLODE_MOVE",
                "The original about-to-blow action consumes Steam and advances to the final explosion.");
            await giant.PerformMove();
            Check(target.IsDead && !fixture.Battle.Combat.ContainsCreature(target) &&
                !target.HasPower<ArtifactPower>() && !target.HasPower<StrengthPower>() &&
                !target.HasPower<RegenPower>() && !target.HasPower<HiddenWaterfallState>(),
                $"Scope={scope}, roles={count}: the genuine final death still removes the creature and its independent powers.");
        }
    }

    private static async Task NativeExplosion()
    {
        for (int count = 1; count <= 4; count++)
        {
            using var fixture = new WaterfallBattle(count);
            var giant = fixture.Giant;
            var target = giant.Creature;
            foreach (var player in fixture.Players)
            { player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(1000); }
            await giant.AfterAddedToRoom();
            giant.RollMove(fixture.Battle.Combat.PlayerCreatures);
            Check(giant.NextMove.Id == "PRESSURIZE_MOVE", "The untouched native state machine starts at Pressurize.");
            await giant.PerformMove();
            Check(target.GetPowerAmount<SteamEruptionPower>() == 15,
                "The original A0 Pressurize action supplies fifteen real Steam Eruption.");
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), target, 2, fixture.Players[0].Creature, null);

            // The trigger entry has its own real red/green evidence. This case
            // follows its native moves, without claiming a lethal entry test.
            await giant.TriggerAboutToBlowState();
            Check(target.HasPower<WeakPower>() && !target.HasPower<ArtifactPower>() && target.GetPowerAmount<SteamEruptionPower>() == 15 &&
                giant.NextMove.Id == "ABOUT_TO_BLOW_MOVE" && giant.NextMove.Intents.Single() is StunIntent,
                "Cleansing preserves Steam Eruption and the native about-to-blow intent.");
            int[] hp = fixture.Players.Select(player => player.Creature.CurrentHp).ToArray();
            await giant.PerformMove();
            Check(!target.HasPower<SteamEruptionPower>() &&
                fixture.Players.Select(player => player.Creature.CurrentHp).SequenceEqual(hp),
                "The original about-to-blow action records and removes Steam Eruption without attacking.");
            giant.RollMove(fixture.Battle.Combat.PlayerCreatures);
            Check(giant.NextMove.Id == "EXPLODE_MOVE" && giant.NextMove.Intents.Single() is DeathBlowIntent intent &&
                intent.GetTotalDamage(fixture.Battle.Combat.PlayerCreatures, target) == 15,
                "Native RollMove advances to the original death-blow preview using the captured fifteen.");
            int historyStart = CombatManager.Instance.History.Entries.Count();
            await giant.PerformMove();
            var hits = CombatManager.Instance.History.Entries.Skip(historyStart).OfType<DamageReceivedEntry>()
                .Where(entry => ReferenceEquals(entry.Dealer, target)).ToArray();
            Check(hits.Length == count && fixture.Players.All(player => hits.Count(entry =>
                    ReferenceEquals(entry.Receiver, player.Creature) && entry.Result.UnblockedDamage == 15) == 1) &&
                fixture.Players.Select((player, index) => hp[index] - player.Creature.CurrentHp).All(loss => loss == 15),
                "The real explosion deals the captured damage exactly once to each native combat target.");
            Check(target.IsDead && !fixture.Battle.Combat.ContainsCreature(target),
                "After Steam Eruption is gone, the native explosion Kill and monster cleanup remove the dead final enemy.");
        }
    }

    private sealed class TriggerPause(Creature target)
    {
        internal readonly Creature Target = target;
        internal readonly TaskCompletionSource Reached = new();
        internal readonly TaskCompletionSource Release = new();
        internal Exception? Failure;
    }
    private static TriggerPause? triggerPause;

    private static void PauseNativeMaxHp(Creature creature, decimal amount, ref Task<decimal> __result)
    {
        if (triggerPause is { } probe && ReferenceEquals(probe.Target, creature) && amount == 999999999)
            __result = PauseAfterNativeMaxHp(__result, probe);
    }

    private static async Task<decimal> PauseAfterNativeMaxHp(Task<decimal> original, TriggerPause probe)
    {
        decimal changed = await original;
        probe.Reached.TrySetResult();
        await probe.Release.Task;
        if (probe.Failure != null) throw probe.Failure;
        return changed;
    }

    private static async Task AwaitedTriggerBoundaries()
    {
        // The real Trigger remains suspended inside its own native HP command.
        // Never substitute a made-up task for the Trigger under test.
        var patch = new Harmony("AllMatters.Tests.Waterfall.NativeAwait");
        patch.Patch(AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp)),
            postfix: new HarmonyMethod(typeof(JigsawHuntingWaterfallTests), nameof(PauseNativeMaxHp)));
        try
        {
            foreach (string boundary in new[] { "closed-session", "replacement-run", "detached-combat", "dependency-failure" })
            {
                using var fixture = new WaterfallBattle(1);
                var target = fixture.Giant.Creature;
                await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), target, 2, fixture.Players[0].Creature, null);
                string moveBefore = fixture.Giant.NextMove.Id;
                var probe = new TriggerPause(target);
                triggerPause = probe;
                Task? operation = null;
                try
                {
                    operation = fixture.Giant.TriggerAboutToBlowState();
                    await probe.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Check(!operation.IsCompleted && target.GetPowerAmount<WeakPower>() == 2 && target.GetPowerAmount<ArtifactPower>() == 0,
                        "The real native transition has reached its awaited HP command before any hunting addition.");
                    if (boundary == "closed-session") JigsawNativeSession.TryGet(fixture.Run)!.Close();
                    if (boundary == "replacement-run")
                    {
                        RunManager.Instance.CleanUp(false);
                        var replacement = RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                            acts: [ModelDb.Act<Underdocks>()], seed: "HUNTING84_WATERFALL_ASYNC_REPLACEMENT");
                        RunManager.Instance.SetUpTest(replacement, new NetSingleplayerGameService());
                    }
                    if (boundary == "detached-combat") fixture.Battle.Combat.RemoveCreature(target, unattach: false);
                    if (boundary == "dependency-failure") probe.Failure = new InvalidOperationException("Injected native HP dependency failure");
                    probe.Release.TrySetResult();
                    Exception? failure = null;
                    try { await operation; } catch (Exception error) { failure = error; }
                    Check(boundary == "dependency-failure" ? ReferenceEquals(failure, probe.Failure) : failure == null,
                        $"Boundary={boundary}: the original native Task completes or propagates the exact injected dependency error.");
                    Check(target.GetPowerAmount<WeakPower>() == 2 && target.GetPowerAmount<ArtifactPower>() == 0,
                        $"Boundary={boundary}: stale or faulted native work never cleanses or grants Artifact.");
                    Check(boundary == "dependency-failure" ? fixture.Giant.NextMove.Id == moveBefore :
                        target.CurrentHp == 999999999 && fixture.Giant.NextMove.Id == "ABOUT_TO_BLOW_MOVE",
                        "The native body, including its failure boundary, remains authoritative.");
                }
                finally
                {
                    probe.Release.TrySetResult();
                    if (operation != null) try { await operation; } catch (Exception error) when (ReferenceEquals(error, probe.Failure)) { }
                    triggerPause = null;
                }
            }
            await SessionClosedDuringRemoval();
        }
        finally { triggerPause = null; patch.UnpatchAll(patch.Id); }
    }

    private static TriggerPause? removalPause;
    public sealed class PausedRemovalDebuff : PowerModel
    {
        public override PowerType Type => PowerType.Debuff;
        public override PowerStackType StackType => PowerStackType.Counter;
        public override async Task AfterRemoved(Creature oldOwner)
        {
            if (removalPause is not { } probe || !ReferenceEquals(probe.Target, oldOwner)) return;
            probe.Reached.TrySetResult();
            await probe.Release.Task;
        }
    }

    private static async Task SessionClosedDuringRemoval()
    {
        ModelDb.Inject(typeof(PausedRemovalDebuff));
        using var fixture = new WaterfallBattle(1);
        var target = fixture.Giant.Creature;
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<PausedRemovalDebuff>(context, target, 1, fixture.Players[0].Creature, null);
        await PowerCmd.Apply<WeakPower>(context, target, 2, fixture.Players[0].Creature, null);
        var probe = new TriggerPause(target);
        removalPause = probe;
        Task? operation = null;
        try
        {
            operation = ExecuteAboutToBlow(fixture.Giant);
            await probe.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(!operation.IsCompleted && !target.HasPower<PausedRemovalDebuff>() && target.GetPowerAmount<WeakPower>() == 2,
                "The native Remove command has removed its first debuff and is awaiting that instance's real AfterRemoved hook.");
            JigsawNativeSession.TryGet(fixture.Run)!.Close();
            probe.Release.TrySetResult();
            await operation;
            Check(target.GetPowerAmount<WeakPower>() == 2 && target.GetPowerAmount<ArtifactPower>() == 0,
                "Closing the session during awaited removal prevents subsequent cleansing and the Artifact grant.");
        }
        finally
        {
            probe.Release.TrySetResult();
            if (operation != null) await operation;
            removalPause = null;
        }
    }

    private sealed class ArtifactReentry(Creature target)
    {
        internal readonly Creature Target = target;
        internal bool Started;
        internal int Received;
    }
    private static ArtifactReentry? artifactReentry;

    private static void ReenterArtifact(PowerModel power, Creature target, ref Task __result)
    {
        if (power is ArtifactPower && artifactReentry is { Started: false } probe && ReferenceEquals(probe.Target, target))
        {
            probe.Started = true;
            __result = GrantIndependentArtifact(__result, probe);
        }
    }

    private static async Task GrantIndependentArtifact(Task original, ArtifactReentry probe)
    {
        await original;
        await Task.Yield();
        var power = await PowerCmd.Apply<ArtifactPower>(new ThrowingPlayerChoiceContext(), probe.Target, 2, probe.Target, null);
        Check(power != null, "The nested native hook creates its own actual Artifact instance.");
        probe.Received = power!.Amount;
        // Remove this independent power before the suspended native Apply
        // resumes, preserving native non-instanced stack ownership.
        await PowerCmd.Remove(power);
    }

    private static async Task ReentrantArtifact()
    {
        var patch = new Harmony("AllMatters.Tests.Waterfall.ArtifactReentry");
        patch.Patch(AccessTools.Method(typeof(Hook), nameof(Hook.BeforePowerAmountChanged)),
            postfix: new HarmonyMethod(typeof(JigsawHuntingWaterfallTests), nameof(ReenterArtifact)));
        try
        {
            for (int count = 1; count <= 4; count++)
            {
                using var fixture = new WaterfallBattle(count);
                var target = fixture.Giant.Creature;
                var probe = new ArtifactReentry(target);
                artifactReentry = probe;
                int historyStart = CombatManager.Instance.History.Entries.Count();
                try
                {
                    await ExecuteAboutToBlow(fixture.Giant);
                    var grants = CombatManager.Instance.History.Entries.Skip(historyStart).OfType<PowerReceivedEntry>()
                        .Where(entry => ReferenceEquals(entry.Actor, target) && entry.Power is ArtifactPower).ToArray();
                    Check(probe.Started && probe.Received == count + 1 && grants.Length == 2 &&
                        grants[0].Amount == count + 1 && grants[1].Amount == 3 &&
                        !ReferenceEquals(grants[0].Power, grants[1].Power),
                        "A nested independent source retains its native multiplayer scaling while only the hunting instance grants three.");
                    Check(target.GetPowerAmount<ArtifactPower>() == 3,
                        "After native removal of the nested source, only the fixed hunting Artifact remains.");
                }
                finally { artifactReentry = null; }
            }
        }
        finally { artifactReentry = null; patch.UnpatchAll(patch.Id); }
    }

    private static async Task ArtifactSources()
    {
        for (int count = 1; count <= 4; count++)
        foreach (int previousGrant in new[] { 0, 3 })
        {
            using var fixture = new WaterfallBattle(count);
            var target = fixture.Giant.Creature;
            var context = new ThrowingPlayerChoiceContext();
            Check(fixture.Battle.Combat.Players.Count == count && fixture.Run.Players.Count == 1,
                "One legal run owner supplies actual native combat roles, not remote clients.");
            await PowerCmd.Apply<StrengthPower>(context, target, 3, target, null);
            await PowerCmd.Apply<DexterityPower>(context, target, 4, target, null);
            int strength = target.GetPowerAmount<StrengthPower>(), dexterity = target.GetPowerAmount<DexterityPower>();
            Check(strength > 0 && dexterity > 0, "Positive native counters are present before the transition.");
            if (previousGrant > 0)
                await PowerCmd.Apply<ArtifactPower>(context, target, previousGrant, target, null);
            int before = previousGrant == 0 ? 0 : count + 2;
            Check(target.GetPowerAmount<ArtifactPower>() == before,
                "An independent existing Artifact grant uses its original multiplayer scaling.");
            int historyStart = CombatManager.Instance.History.Entries.Count();

            await ExecuteAboutToBlow(fixture.Giant);

            var grants = CombatManager.Instance.History.Entries.Skip(historyStart).OfType<PowerReceivedEntry>()
                .Where(entry => ReferenceEquals(entry.Actor, target) && entry.Power is ArtifactPower).ToArray();
            Check(target.GetPowerAmount<ArtifactPower>() == before + 3 && grants.Length == 1 && grants[0].Amount == 3,
                $"Combat roles={count}, previous grant={previousGrant}: the transition adds three native Artifact without replacing existing stacks.");
            Check(target.GetPowerAmount<StrengthPower>() == strength && target.GetPowerAmount<DexterityPower>() == dexterity,
                "Positive Strength and Dexterity survive the cleansing unchanged.");
            await PowerCmd.Apply<WeakPower>(context, target, 2, fixture.Players[0].Creature, null);
            Check(!target.HasPower<WeakPower>() && target.GetPowerAmount<ArtifactPower>() == before + 2,
                "Native Artifact blocks the next visible debuff and consumes exactly one layer.");
            await PowerCmd.Remove<ArtifactPower>(target);
            await PowerCmd.Apply<ArtifactPower>(context, target, 2, target, null);
            Check(target.GetPowerAmount<ArtifactPower>() == count + 1,
                "A later independent new Artifact source on the same monster retains native multiplayer scaling.");
            await PowerCmd.Apply<ArtifactPower>(context, target, 2, target, null);
            Check(target.GetPowerAmount<ArtifactPower>() == count + 3,
                "A later independent addition to an existing stack retains native unscaled ModifyAmount behavior.");
        }
    }

    private static async Task TriggerBoundaries()
    {
        foreach (var scope in new[] { BattleScope.Safe, BattleScope.NonJigsaw, BattleScope.NoActs,
            BattleScope.OtherAct, BattleScope.Event, BattleScope.NonCurrentRun, BattleScope.DetachedCombat })
        {
            using var fixture = new WaterfallBattle(1, scope);
            var target = fixture.Giant.Creature;
            await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(), target, 2, fixture.Players[0].Creature, null);
            Check(target.GetPowerAmount<WeakPower>() == 2 && target.GetPowerAmount<ArtifactPower>() == 0,
                "The negative-scope fixture starts with the same real visible debuff and no Artifact.");
            if (scope == BattleScope.NonCurrentRun)
            {
                RunManager.Instance.CleanUp(false);
                var replacement = RunState.CreateForTest([Player.CreateForNewRun<Ironclad>(UnlockState.all, 1)],
                    acts: [ModelDb.Act<Underdocks>()], seed: "HUNTING84_WATERFALL_REPLACEMENT");
                RunManager.Instance.SetUpTest(replacement, new NetSingleplayerGameService());
                Check(!ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), fixture.Run),
                    "The old monster's native run has actually been replaced.");
            }
            if (scope == BattleScope.DetachedCombat)
            {
                fixture.Battle.Combat.RemoveCreature(target, unattach: false);
                Check(!fixture.Battle.Combat.ContainsCreature(target) && ReferenceEquals(target.CombatState, fixture.Battle.Combat),
                    "A removed creature retains its old combat reference but is no longer a combat participant.");
            }

            await ExecuteAboutToBlow(fixture.Giant);

            Check(target.MaxHp == 999999999 && fixture.Giant.NextMove.Id == "ABOUT_TO_BLOW_MOVE",
                $"Scope={scope}: the original native transition still completes.");
            Check(target.GetPowerAmount<WeakPower>() == 2 && target.GetPowerAmount<ArtifactPower>() == 0,
                $"Scope={scope}: no Waterfall-specific cleanse or Artifact is applied.");
        }
    }

    private enum BattleScope { Hunted, Safe, NonJigsaw, NoActs, OtherAct, Event, NonCurrentRun, DetachedCombat }

    private sealed class WaterfallBattle : IDisposable
    {
        internal Player[] Players { get; }
        internal RunState Run { get; }
        internal HeadlessTestSetup.EncounterCombatScope Battle { get; }
        internal WaterfallGiant Giant => (WaterfallGiant)Battle.Monster;

        internal WaterfallBattle(int count, BattleScope scope = BattleScope.Hunted)
        {
            Players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
            ActModel[] acts = scope == BattleScope.NoActs ? [] :
                scope == BattleScope.OtherAct ? [ModelDb.Act<Overgrowth>()] : [ModelDb.Act<Underdocks>()];
            Run = RunState.CreateForTest([Players[0]], acts: acts, seed: "HUNTING84_WATERFALL_BOUNDARIES");
            Battle = new HeadlessTestSetup.EncounterCombatScope(Run, ModelDb.Encounter<WaterfallGiantBoss>().ToMutable());
            try
            {
                foreach (var player in Players.Skip(1))
                { player.RunState = Run; Battle.Combat.AddPlayer(player); player.ResetCombatState(); }
                var parent = scope == BattleScope.Event ? ModelDb.Event<InfestedAutomaton>() : null;
                if (parent != null) Run.PushRoom(new EventRoom(parent));
                Run.PushRoom(new CombatRoom(Battle.Combat) { ParentEventId = parent?.Id });
                if (scope is not (BattleScope.NonJigsaw or BattleScope.NoActs))
                {
                    JigsawCheckpoint.Capture(Run, AcceptedRoot(scope != BattleScope.Safe,
                        scope == BattleScope.Event ? JigsawRoom.Unknown : JigsawRoom.Boss));
                    JigsawNativeSession.Restore(Run);
                }
                Check(scope != BattleScope.Safe || !JigsawHuntingNative.RootIsHunted(Players[0]), "The safe control has an accepted unpunished root.");
                Check(scope is not (BattleScope.NonJigsaw or BattleScope.NoActs) || JigsawNativeSession.TryGet(Run) == null,
                    "The non-Jigsaw controls have no bound session.");
                Check(scope != BattleScope.Event || Run.CurrentRoomCount == 2 && Run.BaseRoom is EventRoom &&
                    Run.CurrentRoom is CombatRoom { ParentEventId: not null } && JigsawHuntingNative.RootIsHunted(Players[0]),
                    "The hunted same-name Boss is a real child combat under an accepted event root.");
                Check(scope != BattleScope.OtherAct || Run.Act is Overgrowth, "The other-act control uses the native Overgrowth act.");
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            JigsawNativeSession.TryGet(Run)?.Close();
            Battle.Dispose();
        }
    }

    private static async Task TriggerFiltering()
    {
        ModelDb.Inject(typeof(HiddenWaterfallState));
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var run = RunState.CreateForTest([player], acts: [ModelDb.Act<Underdocks>()], seed: "HUNTING84_WATERFALL_TRIGGER");
        var encounter = ModelDb.Encounter<WaterfallGiantBoss>().ToMutable();
        Check(encounter.RoomType == RoomType.Boss, "Use the actual native Waterfall Boss encounter.");
        using var battle = new HeadlessTestSetup.EncounterCombatScope(run, encounter);
        run.PushRoom(new CombatRoom(battle.Combat));
        JigsawCheckpoint.Capture(run, AcceptedRoot(true));
        JigsawNativeSession.Restore(run);
        try
        {
            Check(run.CurrentRoomCount == 1 && run.CurrentRoom is CombatRoom { ParentEventId: null, RoomType: RoomType.Boss } &&
                ReferenceEquals(((CombatRoom)run.CurrentRoom).CombatState, battle.Combat) && JigsawHuntingNative.RootIsHunted(player),
                "The actual Waterfall combat belongs to an accepted hunted Underdocks Boss root.");
            var giant = (WaterfallGiant)battle.Monster;
            var target = giant.Creature;
            var context = new ThrowingPlayerChoiceContext();
            await giant.AfterAddedToRoom();
            await PowerCmd.Apply<WeakPower>(context, target, 2, player.Creature, null);
            await PowerCmd.Apply<VulnerablePower>(context, target, 3, player.Creature, null);
            await PowerCmd.Apply<StrengthPower>(context, target, -4, player.Creature, null);
            await PowerCmd.Apply<DexterityPower>(context, target, -5, player.Creature, null);
            await PowerCmd.Apply<RegenPower>(context, target, 2, target, null);
            await PowerCmd.Apply<SteamEruptionPower>(context, target, 7, target, null);
            await PowerCmd.Apply<HiddenWaterfallState>(context, target, 6, target, null);
            var hidden = target.GetPower<HiddenWaterfallState>()!;
            Check(target.GetPowerAmount<WeakPower>() == 2 && target.GetPowerAmount<VulnerablePower>() == 3 &&
                target.GetPowerAmount<StrengthPower>() == -4 && target.GetPowerAmount<DexterityPower>() == -5 &&
                target.GetPower<StrengthPower>()!.TypeForCurrentAmount == PowerType.Debuff &&
                target.GetPower<DexterityPower>()!.TypeForCurrentAmount == PowerType.Debuff &&
                !hidden.IsVisible && hidden.TypeForCurrentAmount == PowerType.Debuff &&
                target.GetPowerAmount<RegenPower>() == 2 && target.GetPowerAmount<SteamEruptionPower>() == 7 &&
                target.GetPowerAmount<ArtifactPower>() == 0,
                "Real native commands establish visible debuffs, negative counters, positive powers and a hidden debuff without Artifact blocking setup.");

            await ExecuteAboutToBlow(giant);

            Check(target.MaxHp == 999999999 && target.CurrentHp == 999999999 &&
                target.HpDisplay == HpDisplay.InfiniteWithoutNumbers && giant.NextMove.Id == "ABOUT_TO_BLOW_MOVE",
                "The original awaited trigger retains infinite full health, hidden numbers and the immediate about-to-blow move.");
            Check(!target.HasPower<WeakPower>() && !target.HasPower<VulnerablePower>() &&
                !target.HasPower<StrengthPower>() && !target.HasPower<DexterityPower>(),
                "The hunted trigger removes visible native debuffs including negative Strength and Dexterity.");
            Check(target.GetPowerAmount<RegenPower>() == 2 && !target.HasPower<SteamEruptionPower>() &&
                !target.HasPower<HiddenWaterfallState>(),
                "Positive Regen survives while the action consumes Steam and cleansing removes hidden negative state.");
            Check(target.GetPowerAmount<ArtifactPower>() == 3,
                "After the action and cleansing, the hunted Waterfall receives exactly three additional Artifact.");
        }
        finally { JigsawNativeSession.TryGet(run)?.Close(); }
    }

    // A hidden negative power must also be removed by the post-action cleanse.
    public sealed class HiddenWaterfallState : PowerModel
    {
        public override PowerType Type => PowerType.Debuff;
        public override PowerStackType StackType => PowerStackType.Counter;
        protected override bool IsVisibleInternal => false;
    }

    private static JigsawRun AcceptedRoot(bool hunted, JigsawRoom kind = JigsawRoom.Boss)
    {
        var map = JigsawRun.Create(75, players: [1UL]);
        Ok(map.ChooseEnd(1, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        Ok(map.CompleteAncient(1)); Ok(map.Continue(1));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var pending = new Queue<int>(); pending.Enqueue(0);
        while (pending.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if ((kind == JigsawRoom.Boss || map.Nodes.Single(node => node.Id == next).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss)) &&
                    previous.TryAdd(next, current)) pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == kind && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse(); Ok(map.SetRemainingSteps(hunted ? 0 : 100));
        foreach (int node in path)
        {
            var entry = map.BeginEnter(node)!; Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        Check(map.CurrentRoomHunted == hunted, "Legal root entry establishes the requested punishment before the root.");
        return map;
    }

    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
}
