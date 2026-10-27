using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using System.Reflection;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

// The host supplies an active combat without starting the game's UI/network action loop.
// Card models, factories, RNG, hooks, power application and pile commands remain real.
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.IsInProgress), MethodType.Getter)]
internal static class HeadlessCombatInProgress
{
    // Event damage must use the real out-of-combat path, without combat history.
    public static IDisposable EnterEvent()
    {
        bool previous = _inProgress;
        _inProgress = false;
        return new EventScope(previous);
    }

    private static bool _inProgress = true;
    private sealed class EventScope(bool previous) : IDisposable
    {
        public void Dispose() => _inProgress = previous;
    }

    private static bool Prefix(ref bool __result)
    {
        __result = _inProgress;
        return false;
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.IsEnding), MethodType.Getter)]
internal static class HeadlessCombatNotEnding
{
    private static bool Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}

// The public branch routes empty-hand notifications through the absent global room.
[HarmonyPatch]
internal static class HeadlessEmptyHandNotification
{
    private static IEnumerable<MethodBase> TargetMethods() => typeof(CombatManager).GetMethods()
        .Where(method => method.Name == nameof(CombatManager.CheckForEmptyHand));

    private static bool Prefix(ref Task __result)
    {
        __result = Task.CompletedTask;
        return false;
    }
}

// Shared preparation only; gameplay still runs through native models and commands.
internal static class HeadlessTestSetup
{
    internal static RunState Run(string seed, params CharacterModel[] characters)
        => RunState.CreateForTest(characters.Select((character, index) =>
            Player.CreateForNewRun(character, UnlockState.all, (ulong)index + 1)).ToArray(), acts: [], seed: seed);

    internal static Player PlayerWithRun(string seed = "NATIVE_EVENTS", CharacterModel? character = null)
        => Run(seed, character ?? ModelDb.Character<Ironclad>()).Players[0];

    internal static CombatState Combat(IRunState run)
    {
        var combat = new CombatState(runState: run) { RoundNumber = 1 };
        foreach (var player in run.Players)
        {
            combat.AddPlayer(player);
            player.ResetCombatState();
        }
        return combat;
    }

    // Event-monster tests need the real encounter identity and must restore all
    // global host state even when setup or the first behavior assertion fails.
    internal sealed class EncounterCombatScope : IDisposable
    {
        private readonly ulong? _previousNetId = MegaCrit.Sts2.Core.Context.LocalContext.NetId;
        private bool _disposed;
        public RunState Run { get; }
        public CombatState Combat { get; }
        public MonsterModel Monster { get; }

        internal EncounterCombatScope(RunState run, EncounterModel encounter)
        {
            Run = run;
            Combat = new CombatState(encounter, run) { RoundNumber = 1 };
            try
            {
                foreach (var player in run.Players)
                {
                    Combat.AddPlayer(player);
                    player.ResetCombatState();
                }
                RunManager.Instance.SetUpTest(run, new MegaCrit.Sts2.Core.Multiplayer.NetSingleplayerGameService());
                MegaCrit.Sts2.Core.Context.LocalContext.NetId = run.Players[0].NetId;
                CombatManager.Instance.Reset(true);
                CombatManager.Instance.SetUpCombat(Combat);
                encounter.GenerateMonstersWithSlots(run);
                var (monster, slot) = encounter.MonstersWithSlots.Single();
                Monster = monster;
                Combat.AddCreature(Combat.CreateCreature(monster, CombatSide.Enemy, slot));
                monster.SetUpForCombat();
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CombatManager.Instance.Reset(true);
            RunManager.Instance.CleanUp(false);
            MegaCrit.Sts2.Core.Context.LocalContext.NetId = _previousNetId;
        }
    }

    // Compare before/after within one target. The beta API serializes RNG state;
    // peek at a clone so observing it never advances the actual gameplay RNG.
    internal static ulong RngPosition(Rng rng)
    {
#if STS2_PUBLIC
        return (ulong)rng.Counter;
#else
        return new Rng(rng.ToSerializable()).NextUnsignedLong();
#endif
    }

    internal static IDisposable LoadAtlases(params string[] names)
    {
        var loader = new AtlasResourceLoader();
        try
        {
            foreach (string name in names) AtlasManager.LoadAtlas(name);
            Godot.ResourceLoader.AddResourceFormatLoader(loader, atFront: true);
            return new AtlasScope(loader);
        }
        catch { loader.Dispose(); throw; }
    }

    private sealed class AtlasScope(AtlasResourceLoader loader) : IDisposable
    {
        public void Dispose()
        {
            Godot.ResourceLoader.RemoveResourceFormatLoader(loader);
            loader.Dispose();
        }
    }
}
