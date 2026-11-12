using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;

namespace AllMatters.Jigsaw;

[RegisterCard(typeof(EventCardPool), FullPublicEntry = "ALL_MATTERS_KNOWLEDGE_CALAMITY")]
public sealed class KnowledgeCalamityChoice : CardModel, KnowledgeDemon.IChoosable
{
    public KnowledgeCalamityChoice() : base(-1, CardType.Status, CardRarity.Status, TargetType.None) { }
    public override string Title => ModelDb.Card<Calamity>().Title;
    public override string PortraitPath => ModelDb.Card<Calamity>().PortraitPath;
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override int MaxUpgradeLevel => 0;
    public Task OnChosen() => PowerCmd.Apply<CalamityPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, 1, Owner.Creature, this);
}

[RegisterCard(typeof(EventCardPool), FullPublicEntry = "ALL_MATTERS_KNOWLEDGE_TENDER")]
public sealed class KnowledgeTenderChoice : CardModel, KnowledgeDemon.IChoosable
{
    public KnowledgeTenderChoice() : base(-1, CardType.Status, CardRarity.Status, TargetType.None) { }
    public override string Title => ModelDb.Power<TenderPower>().Title.GetFormattedText();
    public override string PortraitPath => "res://AllMatters/images/card_portraits/tender.png";
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override int MaxUpgradeLevel => 0;
    protected override void AddExtraArgsToDescription(LocString description) => description.Add("Amount", 1);
    public Task OnChosen() => PowerCmd.Apply<TenderPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, 1, Owner.Creature, this);
}
[HarmonyPatch(typeof(CardModel), nameof(CardModel.Description), MethodType.Getter)]
internal static class JigsawKnowledgeChoiceDescription
{
    private static void Postfix(CardModel __instance, ref LocString __result)
    {
        if (__instance is KnowledgeCalamityChoice)
            __result = ModelDb.Card<Calamity>().Description;
        if (__instance is KnowledgeTenderChoice)
            __result = ModelDb.Power<TenderPower>().Description;
    }
}

// Change only this action's candidate input. The native command retains its
// player/choice IDs, UI, networking and selection, and native ChooseCurse still
// awaits OnChosen inside the original per-player task and outer WhenAll.
[HarmonyPatch]
internal static class JigsawKnowledgeCandidates
{
    private static Type Machine => AccessTools.Method(typeof(KnowledgeDemon), "ChooseCurse", [typeof(MegaCrit.Sts2.Core.Entities.Creatures.Creature)])
        .GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
    private static MethodBase TargetMethod() => Machine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo nativeChoose = AccessTools.Method(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseACardScreen),
        [typeof(PlayerChoiceContext), typeof(IReadOnlyList<CardModel>), typeof(Player), typeof(bool)]);

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int matches = 0;
        foreach (var instruction in instructions)
        {
            if (!instruction.Calls(nativeChoose))
            { yield return instruction; continue; }
            matches++;
            // Preserve branch/exception metadata on the first instruction.
            var owner = new CodeInstruction(OpCodes.Ldarg_0);
            owner.labels.AddRange(instruction.labels);
            owner.blocks.AddRange(instruction.blocks);
            yield return owner;
            yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(Machine, "<>4__this"));
            yield return CodeInstruction.Call(typeof(JigsawKnowledgeCandidates), nameof(Choose));
        }
        if (matches != 1)
            throw new InvalidOperationException($"KnowledgeDemon.ChooseCurse: expected one native card choice call, found {matches}.");
    }

    private static Task<CardModel?> Choose(PlayerChoiceContext context, IReadOnlyList<CardModel> cards, Player player, bool canSkip, KnowledgeDemon demon)
    {
        if (demon.CombatState?.RunState is RunState native && native.Act is Hive && native.CurrentRoomCount == 1 &&
            native.CurrentRoom is CombatRoom { ParentEventId: null, Encounter: KnowledgeDemonBoss } room &&
            ReferenceEquals(room.CombatState, demon.CombatState) && ReferenceEquals(player.RunState, native) &&
            JigsawHuntingNative.RootIsHunted(player) && cards.Count == 2 && cards[0] is Disintegration)
        {
            if (cards[1] is MindRot)
                cards = [demon.CombatState.CreateCard<MindRot>(player), demon.CombatState.CreateCard<KnowledgeCalamityChoice>(player)];
            else if (cards[1] is Sloth)
                cards = [demon.CombatState.CreateCard<Sloth>(player), demon.CombatState.CreateCard<KnowledgeTenderChoice>(player)];
            else if (cards[1] is WasteAway)
            {
                var damage = demon.CombatState.CreateCard<Disintegration>(player);
                damage.DynamicVars["DisintegrationPower"].BaseValue = 21;
                cards = [demon.CombatState.CreateCard<WasteAway>(player), damage];
            }
        }
        return CardSelectCmd.FromChooseACardScreen(context, cards, player, canSkip);
    }
}
