using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Jigsaw;

// Replace the healing call within Ponder alone; other sources of healing retain
// the native command, even when they target this same monster during Ponder.
[HarmonyPatch]
internal static class JigsawHuntingKnowledgePonder
{
    private static readonly MethodInfo nativeHeal = AccessTools.Method(typeof(CreatureCmd), nameof(CreatureCmd.Heal),
        [typeof(Creature), typeof(decimal), typeof(bool)]);

    private static MethodBase TargetMethod() => AccessTools.Method(typeof(KnowledgeDemon), "PonderMove")
        .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType
        .GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var calls = code.Where(instruction => instruction.Calls(nativeHeal)).ToArray();
        if (calls.Length != 1)
            throw new InvalidOperationException($"KnowledgeDemon.PonderMove: expected one native healing call, found {calls.Length}.");
        calls[0].opcode = OpCodes.Call;
        calls[0].operand = AccessTools.Method(typeof(JigsawHuntingKnowledgePonder), nameof(Heal));
        return code;
    }

    private static Task Heal(Creature creature, decimal amount, bool playAnim)
    {
        if (creature.Monster is KnowledgeDemon && creature.CombatState?.RunState is RunState native &&
            native.Act is Hive && native.CurrentRoomCount == 1 &&
            native.CurrentRoom is CombatRoom { ParentEventId: null, Encounter: KnowledgeDemonBoss } room &&
            ReferenceEquals(room.CombatState, creature.CombatState) && native.Players.FirstOrDefault() is { } player &&
            JigsawHuntingNative.RootIsHunted(player))
            amount = 20;
        return CreatureCmd.Heal(creature, amount, playAnim);
    }
}

[HarmonyPatch]
internal static class JigsawHuntingInsatiableLiquify
{
    private static Type Machine => AccessTools.Method(typeof(TheInsatiable), "LiquifyMove")
        .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
    private static MethodBase TargetMethod() => Machine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var owner = AccessTools.Field(Machine, "<>4__this");
        int sandpit = 0, split = 0, total = 0;
        for (int index = 0; index < code.Count; index++)
        {
            var instruction = code[index];
            yield return instruction;
            if (index + 1 >= code.Count)
                continue;
            var next = code[index + 1];
            bool amount = instruction.opcode == OpCodes.Ldc_I4_4 && next.opcode == OpCodes.Newobj &&
                next.operand is ConstructorInfo { DeclaringType: { } type } && type == typeof(decimal);
            bool pileSplit = instruction.opcode == OpCodes.Ldc_I4_3 && (next.opcode == OpCodes.Blt || next.opcode == OpCodes.Blt_S);
            bool cardTotal = instruction.opcode == OpCodes.Ldc_I4_6 && (next.opcode == OpCodes.Blt || next.opcode == OpCodes.Blt_S);
            if (!amount && !pileSplit && !cardTotal)
                continue;
            if (amount)
                sandpit++;
            if (pileSplit)
                split++;
            if (cardTotal)
                total++;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, owner);
            yield return CodeInstruction.Call(typeof(JigsawHuntingInsatiableLiquify), nameof(Count));
        }
        if (sandpit != 1 || split != 1 || total != 1)
            throw new InvalidOperationException($"TheInsatiable.LiquifyMove: expected one sandpit amount/pile split/card total; found {sandpit}/{split}/{total}.");
    }

    internal static int Count(int original, TheInsatiable monster)
    {
        if (monster.CombatState.RunState is not RunState native || native.Act is not Hive || native.CurrentRoomCount != 1 ||
            native.CurrentRoom is not CombatRoom { ParentEventId: null, Encounter: TheInsatiableBoss } room ||
            !ReferenceEquals(room.CombatState, monster.CombatState) || native.Players.FirstOrDefault() is not { } player ||
            !JigsawHuntingNative.RootIsHunted(player))
            return original;
        return original switch { 3 => 4, 4 => 5, 6 => 8, _ => original };
    }
}

[HarmonyPatch(typeof(TheInsatiable), "GenerateMoveStateMachine")]
internal static class JigsawHuntingInsatiableIntent
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        int matches = 0;
        for (int index = 0; index < code.Count; index++)
        {
            yield return code[index];
            if (code[index].opcode != OpCodes.Ldc_I4_6 || index + 1 >= code.Count ||
                code[index + 1].opcode != OpCodes.Newobj || code[index + 1].operand is not ConstructorInfo constructor ||
                constructor.DeclaringType != typeof(StatusIntent))
                continue;
            matches++;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return CodeInstruction.Call(typeof(JigsawHuntingInsatiableLiquify), nameof(JigsawHuntingInsatiableLiquify.Count));
        }
        if (matches != 1)
            throw new InvalidOperationException($"TheInsatiable.GenerateMoveStateMachine: expected one opening status intent, found {matches}.");
    }
}

// The native state machine and attack commands both read these getters. Keep
// their original targets, hit count, animation, music and move progression.
[HarmonyPatch]
internal static class JigsawHuntingKnowledgeAttacks
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(KnowledgeDemon), "SlapDamage");
        yield return AccessTools.PropertyGetter(typeof(KnowledgeDemon), "KnowledgeOverwhelmingDamage");
    }

    private static void Postfix(KnowledgeDemon __instance, MethodBase __originalMethod, ref int __result)
    {
        if (__instance.CombatState?.RunState is not RunState native || native.Act is not Hive || native.CurrentRoomCount != 1 ||
            native.CurrentRoom is not CombatRoom { ParentEventId: null, Encounter: KnowledgeDemonBoss } room ||
            !ReferenceEquals(room.CombatState, __instance.CombatState) || native.Players.FirstOrDefault() is not { } player ||
            !JigsawHuntingNative.RootIsHunted(player))
            return;
        __result = __originalMethod.Name == "get_SlapDamage" ? (native.AscensionLevel >= 9 ? 16 : 15)
            : (native.AscensionLevel >= 9 ? 7 : 6);
    }
}
