using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using AllMatters.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Jigsaw;

internal static class JigsawHuntingBosses
{
    private sealed class PhaseTwoGrant;
    private static readonly ConditionalWeakTable<SkittishPower, PhaseTwoGrant> fixedSkittish = new();
    private sealed class PhaseThreeClock { internal int FullTurns; }
    private static readonly ConditionalWeakTable<TestSubject, PhaseThreeClock> clocks = new();
    internal static void ArmClock(TestSubject subject) => clocks.GetValue(subject, _ => new PhaseThreeClock());
    internal static async Task StartPlayerTurn(TestSubject subject, PlayerChoiceContext context)
    {
        if (!IsHuntedBoss(subject) || !subject.Creature.IsAlive || !clocks.TryGetValue(subject, out var clock))
            return;
        clock.FullTurns++;
        if (clock.FullTurns % 3 == 0)
            await PowerCmd.Apply<ScrutinyPower>(context, subject.Creature, 1, subject.Creature, null);
    }
    internal static bool ConsumeFixedSkittish(SkittishPower power) => fixedSkittish.Remove(power);

    internal static async Task<PainfulStabsPower?> GrantPhaseTwo(PlayerChoiceContext context, Creature target,
        decimal amount, Creature? applier, CardModel? source, bool silent)
    {
        if (target.Monster is not TestSubject subject || !IsHuntedBoss(subject))
            return await PowerCmd.Apply<PainfulStabsPower>(context, target, amount, applier, source, silent);
        var skittish = (SkittishPower)ModelDb.Power<SkittishPower>().ToMutable();
        fixedSkittish.Add(skittish, new PhaseTwoGrant());
        try
        { await PowerCmd.Apply(context, skittish, target, 10, applier, source, silent); }
        finally { fixedSkittish.Remove(skittish); }
        await PowerCmd.Apply<SuckPower>(context, target, 3, applier, source, silent);
        return null;
    }

    internal static bool IsHuntedBoss(MonsterModel monster) =>
        monster.CombatState.RunState is { Act: Glory, CurrentRoomCount: 1, CurrentRoom: CombatRoom room } run &&
        ReferenceEquals(room.Encounter, monster.CombatState.Encounter) && room.Encounter.RoomType == RoomType.Boss &&
        run.Players.FirstOrDefault() is { } player && JigsawHuntingNative.RootIsHunted(player);

    internal static async Task InitializeAeonglass(Aeonglass monster)
    {
        // Both supported initializers await an empty MonsterModel base, update
        // music, apply one targeted presence per opponent, then grant Artifact.
        NRunMusicController.Instance?.UpdateMusicParameter("queen_progress", 1f);
        foreach (var opponent in monster.CombatState.GetOpponentsOf(monster.Creature))
        {
            var presence = (WitheringPresencePower)ModelDb.Power<WitheringPresencePower>().ToMutable();
            presence.Target = opponent;
            // Set the display source before the instance is applied/presented.
            // Amount remains the original six; it is not the per-owner counter.
            presence.DynamicVars["CardsLeft"].BaseValue = 12;
            await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), presence, monster.Creature, 6, monster.Creature, null);
        }
    }
}

[HarmonyPatch(typeof(TestSubject), "RespawnMove")]
internal static class JigsawHuntingSubjectThirdPhase
{
    private static void Postfix(TestSubject __instance, ref Task __result) => __result = Finish(__result, __instance);
    private static async Task Finish(Task original, TestSubject subject)
    {
        await original;
        if (subject.Creature.CombatState == null || !JigsawHuntingBosses.IsHuntedBoss(subject) ||
            !subject.Creature.HasPower<NemesisPower>() || subject.Creature.HasPower<AdaptablePower>())
            return;
        await PowerCmd.Remove<SkittishPower>(subject.Creature);
        await PowerCmd.Remove<SuckPower>(subject.Creature);
        JigsawHuntingBosses.ArmClock(subject);
    }
}

[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.BeforeSideTurnStart))]
internal static class JigsawHuntingSubjectClock
{
    private static void Postfix(AbstractModel __instance, PlayerChoiceContext choiceContext, CombatSide side, ref Task __result)
    {
        if (__instance is TestSubject subject && side == CombatSide.Player)
            __result = Update(__result, subject, choiceContext);
    }
    private static async Task Update(Task original, TestSubject subject, PlayerChoiceContext context)
    {
        await original;
        await JigsawHuntingBosses.StartPlayerTurn(subject, context);
    }
}

[HarmonyPatch(typeof(SkittishPower), nameof(SkittishPower.GetScaledAmountForMultiplayer))]
internal static class JigsawHuntingSubjectFixedSkittish
{
    private static bool Prefix(SkittishPower __instance, decimal amount, ref decimal __result)
    {
        if (!JigsawHuntingBosses.ConsumeFixedSkittish(__instance))
            return true;
        __result = amount;
        return false;
    }
}

[HarmonyPatch]
internal static class JigsawHuntingSubjectPhaseTwo
{
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(TestSubject), "RespawnMove")
        .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType
        .GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int matches = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(PowerCmd) &&
                method.Name == nameof(PowerCmd.Apply) && method.IsGenericMethod &&
                method.GetGenericArguments()[0] == typeof(PainfulStabsPower) && method.GetParameters()[1].ParameterType == typeof(Creature))
            {
                matches++;
                instruction.operand = AccessTools.Method(typeof(JigsawHuntingBosses), nameof(JigsawHuntingBosses.GrantPhaseTwo));
            }
            yield return instruction;
        }
        if (matches != 1)
            throw new InvalidOperationException($"TestSubject.RespawnMove: expected one phase two grant, found {matches}.");
    }
}

[HarmonyPatch(typeof(Aeonglass), nameof(Aeonglass.AfterAddedToRoom))]
internal static class JigsawHuntingAeonglassOpening
{
    private static bool Prefix(Aeonglass __instance, ref Task __result)
    {
        if (!JigsawHuntingBosses.IsHuntedBoss(__instance))
            return true;
        __result = JigsawHuntingBosses.InitializeAeonglass(__instance);
        return false;
    }
}

// Adapt only this presence's original trigger. The native method still owns
// the owner's decrement, wait, display notifications and reset timing.
[HarmonyPatch]
internal static class JigsawHuntingAeonglassTwelfth
{
    private static Type Machine => AccessTools.Method(typeof(WitheringPresencePower), nameof(WitheringPresencePower.AfterCardPlayed))
        .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
    private static MethodBase TargetMethod() => Machine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var owner = AccessTools.Field(Machine, "<>4__this");
        var nativeAdd = typeof(CardPileCmd).GetMethods().Single(method => method.Name == nameof(CardPileCmd.AddToCombatAndPreview) &&
            method.IsGenericMethodDefinition && method.GetParameters()[0].ParameterType == typeof(Creature)).MakeGenericMethod(typeof(Wither));
        int additions = 0, resets = 0;
        for (int index = 0; index < code.Count; index++)
        {
            var instruction = code[index];
            if (instruction.Calls(nativeAdd))
            {
                additions++;
                var load = new CodeInstruction(OpCodes.Ldarg_0);
                load.labels.AddRange(instruction.labels);
                instruction.labels.Clear();
                yield return load;
                yield return new CodeInstruction(OpCodes.Ldfld, owner);
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(JigsawHuntingAeonglassTwelfth), nameof(AddStatuses));
            }
            yield return instruction;
            if (instruction.opcode != OpCodes.Ldc_I4_6 || index + 1 >= code.Count ||
                code[index + 1].opcode != OpCodes.Newobj || code[index + 1].operand is not ConstructorInfo constructor ||
                constructor.DeclaringType != typeof(decimal))
                continue;
            resets++;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, owner);
            yield return CodeInstruction.Call(typeof(JigsawHuntingAeonglassTwelfth), nameof(Reset));
        }
        if (additions != 1 || resets != 1)
            throw new InvalidOperationException($"WitheringPresencePower.AfterCardPlayed: expected one native status call and reset; found {additions}/{resets}.");
    }

    private static bool IsHunted(WitheringPresencePower presence) => presence.Owner.Monster is Aeonglass monster &&
        JigsawHuntingBosses.IsHuntedBoss(monster);

    private static int Reset(int original, WitheringPresencePower presence) => IsHunted(presence) ? 12 : original;

    private static async Task AddStatuses(Creature target, PileType pile, int count, Player? creator,
        CardPilePosition position, WitheringPresencePower presence)
    {
        if (IsHunted(presence))
        {
            await CardPileCmd.AddToCombatAndPreview<Wound>(target, pile, count, creator, position);
            await CardPileCmd.AddToCombatAndPreview<Burn>(target, pile, count, creator, position);
            await CardPileCmd.AddToCombatAndPreview<MegaCrit.Sts2.Core.Models.Cards.Void>(target, pile, count, creator, position);
            await CardPileCmd.AddToCombatAndPreview<Dazed>(target, pile, count, creator, position);
        }
        await CardPileCmd.AddToCombatAndPreview<Wither>(target, pile, count, creator, position);
    }
}
