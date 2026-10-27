using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Utils;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Relics;

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_INK_BOTTLE")]
public sealed class InkBottle : ModRelicTemplate
{
    private static readonly SavedAttachedState<RelicModel, int> Plays = new("AllMattersInkPlays");
    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool ShowCounter => true;
    public override int DisplayAmount => Plays.GetValueOrDefault(this);
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/ink_bottle.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/ink_bottle.png",
        BigIconPath: "res://AllMatters/images/relics/ink_bottle.png");
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner) return;
        int count = DisplayAmount + 1;
        Plays[this] = count % 10;
        InvokeDisplayAmountChanged();
        if (count == 10) await CardPileCmd.Draw(choiceContext, 1, Owner);
    }
}

internal static class NativeVirusCounter
{
    internal static readonly SavedAttachedState<RelicModel, int> Plays = new("AllMattersVirusPlays");
}

[HarmonyPatch(typeof(SymbioticVirus), nameof(SymbioticVirus.AfterSideTurnStart))]
internal static class NativeVirusRemoveDark
{
    private static bool Prefix(ref Task __result) { __result = Task.CompletedTask; return false; }
}

[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.BeforeHandDraw))]
internal static class NativeVirusDraw
{
    private static void Postfix(AbstractModel __instance, Player player, PlayerChoiceContext choiceContext, ref Task __result)
    {
        if (__instance is SymbioticVirus virus && player == virus.Owner) __result = Draw(__result, choiceContext, player);
    }
    private static async Task Draw(Task original, PlayerChoiceContext context, Player player)
    {
        await original;
        await CardPileCmd.Draw(context, 1, player);
    }
}

[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.AfterCardPlayed))]
internal static class NativeVirusPlay
{
    private static void Postfix(AbstractModel __instance, CardPlay cardPlay, ref Task __result)
    {
        if (__instance is SymbioticVirus virus && cardPlay.Card.Owner == virus.Owner && cardPlay.Card.Type != CardType.Attack)
            __result = Count(virus, __result);
    }
    private static async Task Count(SymbioticVirus virus, Task original)
    {
        await original;
        int count = NativeVirusCounter.Plays.GetValueOrDefault(virus) + 1;
        NativeVirusCounter.Plays[virus] = count % 5;
        if (count == 5) await CardPileCmd.AddToCombatAndPreview<Dazed>(virus.Owner.Creature, PileType.Draw, 1, virus.Owner, CardPilePosition.Random);
    }
}

[HarmonyPatch(typeof(HistoryCourse), nameof(HistoryCourse.AfterAutoPrePlayPhaseEntered))]
internal static class NativeHistorySkill
{
    private static bool Prefix(HistoryCourse __instance, PlayerChoiceContext choiceContext, Player player, ref Task __result)
    {
        __result = Replay(__instance, choiceContext, player);
        return false;
    }
    private static async Task Replay(HistoryCourse relic, PlayerChoiceContext context, Player player)
    {
        if (player != relic.Owner || player.PlayerCombatState!.TurnNumber <= 1) return;
        var card = CombatManager.Instance.History.CardPlaysFinished.LastOrDefault(e => e.CardPlay.Card.Owner == player
            && e.HappenedLastPlayerTurn(player) && e.CardPlay.Card.Type is CardType.Attack or CardType.Skill
            && !e.CardPlay.Card.IsDupe)?.CardPlay.Card;
        if (card == null) return;
#if STS2_PUBLIC
        await CardCmd.AutoPlay(context, card.CreateDupe(), null);
#else
        await CardCmd.AutoPlay(context, card.CreateDupe(player), null);
#endif
    }
}
