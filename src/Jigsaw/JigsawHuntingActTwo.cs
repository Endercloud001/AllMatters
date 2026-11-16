using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Jigsaw;

[HarmonyPatch]
internal static class JigsawHuntingCrabOpening
{
    private static readonly ConditionalWeakTable<ArtifactPower, object> fixedArtifacts = new();

    private static IEnumerable<MethodBase> TargetMethods() =>
    [ AccessTools.Method(typeof(Crusher), nameof(Crusher.AfterAddedToRoom)),
      AccessTools.Method(typeof(Rocket), nameof(Rocket.AfterAddedToRoom)) ];

    private static bool IsCurrent(MonsterModel monster, RunState native, CombatRoom room) =>
        ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), native) &&
        ReferenceEquals(native.CurrentRoom, room) && native.CurrentRoomCount == 1 &&
        room.ParentEventId == null && room.Encounter is KaiserCrabBoss &&
        ReferenceEquals(room.CombatState, monster.CombatState) &&
        room.CombatState.ContainsCreature(monster.Creature) && monster.Creature.IsAlive && !CombatManager.Instance.IsEnding &&
        native.Players.FirstOrDefault() is { } owner && JigsawHuntingNative.RootIsHunted(owner) &&
        native.Act is Hive;

    private static void Postfix(MonsterModel __instance, ref Task __result)
    {
        if (__instance.CombatState?.RunState is RunState native && native.CurrentRoom is CombatRoom room &&
            IsCurrent(__instance, native, room))
            __result = GrantArtifact(__result, __instance, native, room);
    }

    private static async Task GrantArtifact(Task original, MonsterModel monster, RunState native, CombatRoom room)
    {
        var map = JigsawNativeSession.TryGet(native)!.Map!;
        int? node = map.CurrentNode;
        var pending = map.PendingEntry?.Id;
        await original;
        if (!IsCurrent(monster, native, room) || !ReferenceEquals(JigsawNativeSession.TryGet(native)?.Map, map) ||
            map.CurrentNode != node || map.PendingEntry?.Id != pending)
            return;
        var artifact = (ArtifactPower)ModelDb.Power<ArtifactPower>().ToMutable();
        fixedArtifacts.Add(artifact, new object());
        try
        {
            await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), artifact, monster.Creature, 3, monster.Creature, null);
        }
        finally { fixedArtifacts.Remove(artifact); }
    }

    internal static bool ConsumeFixedArtifact(ArtifactPower artifact) => fixedArtifacts.Remove(artifact);
}

[HarmonyPatch(typeof(ArtifactPower), nameof(ArtifactPower.GetScaledAmountForMultiplayer))]
internal static class JigsawHuntingCrabArtifactScaling
{
    private static bool Prefix(ArtifactPower __instance, decimal amount, ref decimal __result)
    {
        if (!JigsawHuntingCrabOpening.ConsumeFixedArtifact(__instance))
            return true;
        __result = amount;
        return false;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class JigsawHuntingHiveOpening
{
    private static void Postfix(IRunState runState, ICombatState? combatState, ref Task __result)
    {
        if (runState is RunState native && combatState != null && native.CurrentRoom is CombatRoom room &&
            IsCurrent(native, room, combatState))
            __result = ApplyOpening(__result, native, room, combatState);
    }

    private static bool IsCurrent(RunState native, CombatRoom room, ICombatState combatState)
    {
        if (!ReferenceEquals(native.CurrentRoom, room) ||
            !ReferenceEquals(room.CombatState, combatState) ||
            !ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), native) ||
            native.Players.FirstOrDefault() is not { } owner || !JigsawHuntingNative.RootIsHunted(owner) ||
            native.Act is not Hive)
            return false;
        if (room.ParentEventId == null)
            return native.CurrentRoomCount == 1 && room.RoomType is RoomType.Monster or RoomType.Elite;
        return native.CurrentRoomCount == 2 && native.BaseRoom is EventRoom parent &&
            room.ParentEventId == parent.CanonicalEvent.Id &&
            JigsawNativeSession.TryGet(native)?.Map is { CurrentNode: not null } map &&
            map.Nodes.Single(node => node.Id == map.CurrentNode).Room == JigsawRoom.Unknown;
    }

    private static async Task ApplyOpening(Task original, RunState native, CombatRoom room, ICombatState combatState)
    {
        var map = JigsawNativeSession.TryGet(native)!.Map!;
        int? node = map.CurrentNode;
        var pending = map.PendingEntry?.Id;
        var destination = map.PendingEntry?.NodeId;
        var parent = native.BaseRoom;
        bool StillCurrent() => IsCurrent(native, room, combatState) && ReferenceEquals(native.BaseRoom, parent) &&
            ReferenceEquals(JigsawNativeSession.TryGet(native)?.Map, map) &&
            // Native RoomEntered can confirm this pending destination while a power animation awaits.
            (map.CurrentNode == node && map.PendingEntry?.Id == pending ||
             pending != null && map.PendingEntry == null && map.CurrentNode == destination);
        await original;
        if (!StillCurrent())
            return;
        bool elite = room.ParentEventId == null && room.RoomType == RoomType.Elite;
        foreach (var player in combatState.Players)
        {
            if (!StillCurrent())
                return;
            var context = new ThrowingPlayerChoiceContext();
            await PowerCmd.Apply<WeakPower>(context, player.Creature, elite ? 2 : 1, null, null);
            if (!StillCurrent())
                return;
            if (elite)
                continue;
            await PowerCmd.Apply<FrailPower>(context, player.Creature, 1, null, null);
            if (!StillCurrent())
                return;
            await PowerCmd.Apply<VulnerablePower>(context, player.Creature, 1, null, null);
        }
    }
}

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
#if STS2_PUBLIC
            bool amount = instruction.opcode == OpCodes.Ldc_I4_4 && next.opcode == OpCodes.Newobj &&
                next.operand is ConstructorInfo { DeclaringType: { } type } && type == typeof(decimal);
#else
            bool amount = instruction.Calls(AccessTools.PropertyGetter(typeof(TheInsatiable), "SandpitAmount"));
#endif
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
        if (JigsawHuntingActOne.BoundCreature(monster)?.CombatState?.RunState is not RunState native || native.Act is not Hive || native.CurrentRoomCount != 1 ||
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
        if (JigsawHuntingActOne.BoundCreature(__instance)?.CombatState?.RunState is not RunState native || native.Act is not Hive || native.CurrentRoomCount != 1 ||
            native.CurrentRoom is not CombatRoom { ParentEventId: null, Encounter: KnowledgeDemonBoss } room ||
            !ReferenceEquals(room.CombatState, __instance.CombatState) || native.Players.FirstOrDefault() is not { } player ||
            !JigsawHuntingNative.RootIsHunted(player))
            return;
        __result = __originalMethod.Name == "get_SlapDamage" ? (native.AscensionLevel >= 9 ? 16 : 15)
            : (native.AscensionLevel >= 9 ? 7 : 6);
    }
}
