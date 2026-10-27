using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;

namespace AllMatters.Cards;

internal static class TrashHeapPoolMigration
{
    internal static bool IsMigrated(CardModel card) => card is Rebound or Stack or RipAndTear or DualWield or Entrench;
    internal static bool IsMigrated(RelicModel relic) => relic is MawBank or TheBoot;
}

[HarmonyPatch]
internal static class TrashHeapNormalCardPools
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { typeof(EventCardPool), typeof(DefectCardPool), typeof(IroncladCardPool) }
        .Select(type => AccessTools.Method(type, "GenerateAllCards"));

    private static void Postfix(CardPoolModel __instance, ref CardModel[] __result)
    {
        __result = __instance switch
        {
            EventCardPool => __result.Where(card => !TrashHeapPoolMigration.IsMigrated(card)).ToArray(),
            DefectCardPool => __result.Concat(new CardModel[] { ModelDb.Card<Rebound>(), ModelDb.Card<Stack>(), ModelDb.Card<RipAndTear>() })
                .DistinctBy(card => card.Id).ToArray(),
            IroncladCardPool => __result.Concat(new CardModel[] { ModelDb.Card<DualWield>(), ModelDb.Card<Entrench>() })
                .DistinctBy(card => card.Id).ToArray(),
            _ => __result,
        };
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.Rarity), MethodType.Getter)]
internal static class TrashHeapCardRarity
{
    private static void Postfix(CardModel __instance, ref CardRarity __result)
    {
        if (__instance is Stack) __result = CardRarity.Common;
        else if (__instance is Rebound or RipAndTear or DualWield or Entrench) __result = CardRarity.Uncommon;
    }
}

// Portraits remain in the game's event atlas even after their actual pool changes.
[HarmonyPatch]
internal static class TrashHeapCardArtwork
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { "PortraitPath", "BetaPortraitPath", "PortraitPngPath", "BetaPortraitPngPath" }
        .Select(name => AccessTools.PropertyGetter(typeof(CardModel), name));

    private static void Postfix(CardModel __instance, ref string __result)
    {
        if (TrashHeapPoolMigration.IsMigrated(__instance))
            __result = __result.Replace("/" + __instance.Pool.Title.ToLowerInvariant() + "/", "/event/", StringComparison.Ordinal);
    }
}

[HarmonyPatch]
internal static class TrashHeapNormalRelicPools
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { typeof(EventRelicPool), typeof(SharedRelicPool) }
        .Select(type => AccessTools.Method(type, "GenerateAllRelics"));

    private static void Postfix(RelicPoolModel __instance, ref IEnumerable<RelicModel> __result)
    {
        if (__instance is EventRelicPool) __result = __result.Where(relic => !TrashHeapPoolMigration.IsMigrated(relic));
        else __result = __result.Concat(new RelicModel[] { ModelDb.Relic<MawBank>(), ModelDb.Relic<TheBoot>() }).DistinctBy(relic => relic.Id);
    }
}

[HarmonyPatch]
internal static class TrashHeapRelicRarity
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { typeof(MawBank), typeof(TheBoot) }
        .Select(type => AccessTools.PropertyGetter(type, nameof(RelicModel.Rarity)));

    private static void Postfix(ref RelicRarity __result) => __result = RelicRarity.Common;
}
