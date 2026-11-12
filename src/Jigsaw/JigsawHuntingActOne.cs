using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Reflection.Emit;

namespace AllMatters.Jigsaw;

internal static class JigsawHuntingActOne
{
    internal static bool HasSpecialRules(MonsterModel monster)
    {
        if (monster.CombatState?.RunState is not RunState native || native.CurrentRoomCount != 1 ||
            native.CurrentRoom is not CombatRoom || native.Players.Count == 0 ||
            !JigsawHuntingNative.RootIsHunted(native.Players[0]) ||
            JigsawNativeSession.TryGet(native)?.Map is not { } map)
            return false;
        int? nodeId = map.CurrentNode ?? map.PendingEntry?.NodeId;
        return nodeId != null && native.Act is Overgrowth &&
            map.Nodes.Single(node => node.Id == nodeId).Room is JigsawRoom.Elite or JigsawRoom.Boss or JigsawRoom.SecondBoss;
    }
}

[HarmonyPatch(typeof(Byrdonis), nameof(Byrdonis.AfterAddedToRoom))]
internal static class JigsawHuntingByrdonis
{
    private static void Postfix(Byrdonis __instance, ref Task __result)
    {
        if (JigsawHuntingActOne.HasSpecialRules(__instance))
            __result = AddRegeneration(__instance, __result);
    }
    private static async Task AddRegeneration(Byrdonis monster, Task original)
    {
        await original;
        var power = ModelDb.Power<RegenPower>().ToMutable();
        JigsawHuntingByrdonisFixedRegeneration.Applying.Add(power, new object());
        try
        {
            await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), power, monster.Creature, 6, monster.Creature, null);
        }
        finally { JigsawHuntingByrdonisFixedRegeneration.Applying.Remove(power); }
    }
}

// Regen normally scales with party size. The hunting table specifies six.
[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.GetScaledAmountForMultiplayer))]
internal static class JigsawHuntingByrdonisFixedRegeneration
{
    internal static readonly ConditionalWeakTable<PowerModel, object> Applying = new();
    private static void Postfix(PowerModel __instance, decimal amount, Creature target, ref decimal __result)
    {
        if (Applying.TryGetValue(__instance, out _))
            __result = amount;
    }
}

[HarmonyPatch(typeof(BygoneEffigy), "GenerateMoveStateMachine")]
internal static class JigsawHuntingEffigyWake
{
    private static void Postfix(BygoneEffigy __instance, ref MonsterMoveStateMachine __result)
    {
        if (JigsawHuntingActOne.HasSpecialRules(__instance))
            __result = new MonsterMoveStateMachine(__result.States.Values, __result.States["WAKE_MOVE"]);
    }
}

// Replace only the original orb's command call. Native receiving hooks,
// Artifact, target iteration and stacking remain inside PowerCmd unchanged;
// nested applications made by hooks do not pass through this call site.
[HarmonyPatch]
internal static class JigsawHuntingKinOrbs
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { "OrbOfFrailtyMove", "OrbOfWeaknessMove" }
        .Select(name => (MethodBase)AccessTools.Method(typeof(KinPriest), name)
            .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType
            .GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!);

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int matches = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method &&
                method.DeclaringType == typeof(PowerCmd) && method.Name == nameof(PowerCmd.Apply) && method.IsGenericMethod &&
                method.GetParameters()[1].ParameterType == typeof(IEnumerable<Creature>) &&
                (method.GetGenericArguments()[0] == typeof(FrailPower) || method.GetGenericArguments()[0] == typeof(WeakPower)))
            {
                matches++;
                instruction.operand = AccessTools.Method(typeof(JigsawHuntingKinOrbs), nameof(Apply))
                    .MakeGenericMethod(method.GetGenericArguments());
            }
            yield return instruction;
        }
        if (matches != 1)
            throw new InvalidOperationException($"Expected one native Kin orb power command, found {matches}.");
    }
    private static Task<IReadOnlyList<T>> Apply<T>(PlayerChoiceContext context, IEnumerable<Creature> targets,
        decimal amount, Creature? applier, CardModel? cardSource, bool silent) where T : PowerModel =>
        PowerCmd.Apply<T>(context, targets,
            amount == 1 && applier?.Monster is KinPriest priest && JigsawHuntingActOne.HasSpecialRules(priest) ? 2 : amount,
            applier, cardSource, silent);
}

[HarmonyPatch(typeof(CeremonialBeast), "BeastCryMove")]
internal static class JigsawHuntingBeastCry
{
    private static void Postfix(CeremonialBeast __instance, ref Task __result)
    {
        if (__instance.IsInSecondPhase && JigsawHuntingActOne.HasSpecialRules(__instance))
            __result = AddDebuffs(__instance, __result);
    }
    private static async Task AddDebuffs(CeremonialBeast beast, Task original)
    {
        await original;
        var players = beast.CombatState.PlayerCreatures.Where(creature => creature.IsAlive).ToArray();
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<WeakPower>(context, players, 2, beast.Creature, null);
        await PowerCmd.Apply<FrailPower>(context, players, 2, beast.Creature, null);
    }
}
