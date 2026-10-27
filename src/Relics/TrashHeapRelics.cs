using AllMatters.Encounters;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Utils;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Relics;

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_OMAMORI")]
public sealed class Omamori : ModRelicTemplate
{
    private static readonly SavedAttachedState<RelicModel, int> Uses = new("AllMattersOmamoriUses");
    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool ShowCounter => true;
    public override int DisplayAmount => 2 - Uses.GetValueOrDefault(this);
    public override bool IsUsedUp => DisplayAmount == 0;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/omamori.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/omamori.png",
        BigIconPath: "res://AllMatters/images/relics/omamori.png");
    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (DisplayAmount <= 0 || card.Owner != Owner || card.Type != CardType.Curse || card.Pile?.Type != PileType.Deck || oldPileType == PileType.Deck) return;
        Uses[this] = Uses.GetValueOrDefault(this) + 1;
        InvokeDisplayAmountChanged();
        Flash();
        await CardPileCmd.RemoveFromDeck(card);
    }
}

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_PRESERVED_INSECT")]
public sealed class PreservedInsect : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/preserved_insect.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/preserved_insect.png",
        BigIconPath: "res://AllMatters/images/relics/preserved_insect.png");
    // The native generic Shrink tooltip hardcodes three turns, regardless of amount.
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [new HoverTip(ModelDb.Power<ShrinkPower>(), new MegaCrit.Sts2.Core.Localization.LocString("relics", Id.Entry + ".shrinkDescription").GetFormattedText(), isSmart: false)];
    public override async Task BeforeCombatStart()
    {
        foreach (var enemy in Owner.Creature.CombatState!.Enemies.Where(c => EnemyIdentity.Kind(c) == RoomType.Elite).ToArray())
        {
            if (enemy.GetPower<ArtifactPower>() is { } artifact) await PowerCmd.Remove(artifact);
            await PowerCmd.Apply<ShrinkPower>(new ThrowingPlayerChoiceContext(), enemy, 99, Owner.Creature, null);
        }
    }
}

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_INSERTER")]
public sealed class Inserter : ModRelicTemplate
{
    private static readonly SavedAttachedState<RelicModel, int> Turns = new("AllMattersInserterTurns");
    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool ShowCounter => true;
    public override int DisplayAmount => Turns.GetValueOrDefault(this);
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/inserter.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/inserter.png",
        BigIconPath: "res://AllMatters/images/relics/inserter.png");
#if STS2_PUBLIC
    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> creatures, ICombatState combatState)
        => side == CombatSide.Player && creatures.Contains(Owner.Creature) ? CountTurn() : Task.CompletedTask;
#else
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
        => player == Owner ? CountTurn() : Task.CompletedTask;
#endif
    private async Task CountTurn()
    {
        int turns = DisplayAmount + 1;
        Turns[this] = turns % 2;
        InvokeDisplayAmountChanged();
        if (turns == 2) { Flash(); await OrbCmd.AddSlots(Owner, 1); }
    }
}

[HarmonyPatch(typeof(SwordOfStone), nameof(SwordOfStone.AfterCombatVictory))]
internal static class SwordOfStoneStopRoomCount
{
    private static bool Prefix(ref Task __result) { __result = Task.CompletedTask; return false; }
}

[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.AfterDeath))]
internal static class SwordOfStoneUnitCount
{
    private static void Postfix(AbstractModel __instance, Creature creature, bool wasRemovalPrevented, ref Task __result)
    {
        if (__instance is SwordOfStone sword && !wasRemovalPrevented && creature.IsDead
            && EnemyIdentity.Kind(creature) is RoomType.Elite or RoomType.Boss)
            __result = Count(__result, sword);
    }
    private static async Task Count(Task native, SwordOfStone sword)
    {
        await native;
        if (!sword.Owner.Relics.Contains(sword)) return;
        sword.ElitesDefeated++;
        sword.Flash();
        if (sword.ElitesDefeated >= 5) await RelicCmd.Replace(sword, ModelDb.Relic<SwordOfJade>().ToMutable());
    }
}
