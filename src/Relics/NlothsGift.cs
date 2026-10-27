using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Relics;

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_NLOTHS_GIFT")]
public sealed class NlothsGift : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/nloths_gift.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/nloths_gift.png",
        BigIconPath: "res://AllMatters/images/relics/nloths_gift.png");

    internal static readonly ConditionalWeakTable<CardRarityOdds, Player> Owners = new();
    internal static void Track(Player player)
    {
        var odds = player.PlayerOdds.CardRarity;
        Owners.Remove(odds); Owners.Add(odds, player);
    }
    public override Task AfterObtained() { Track(Owner); return Task.CompletedTask; }
}

[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForReward), typeof(Player), typeof(int), typeof(CardCreationOptions))]
internal static class NlothRewardOwner
{
    // Store ownership on each actual odds instance, so lazy reward enumeration
    // and another player's generation cannot overwrite a global current owner.
    private static void Prefix(Player player) => NlothsGift.Track(player);
}

[HarmonyPatch(typeof(CardRarityOdds), nameof(CardRarityOdds.RollWithoutChangingFutureOdds), typeof(CardRarityOddsType), typeof(float))]
internal static class NlothRareOdds
{
    private static void Prefix(CardRarityOdds __instance, CardRarityOddsType type, ref float offset)
    {
        if (type is not (CardRarityOddsType.RegularEncounter or CardRarityOddsType.EliteEncounter)) return;
        if (!NlothsGift.Owners.TryGetValue(__instance, out var player) || !player.Relics.OfType<NlothsGift>().Any()) return;
        // STS1 modifies the base rare threshold before its independent pity
        // adjustment. Keep that stage and the native roll/saturation behavior.
        float baseRare = type == CardRarityOddsType.RegularEncounter ? CardRarityOdds.RegularRareOdds : CardRarityOdds.EliteRareOdds;
        offset += 2 * baseRare;
    }
}
