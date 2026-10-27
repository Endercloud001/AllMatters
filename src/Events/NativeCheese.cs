using AllMatters.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Events;

[HarmonyPatch(typeof(RoomFullOfCheese), "Gorge")]
internal static class NativeCheeseCards
{
    private static bool Prefix(RoomFullOfCheese __instance, ref Task __result)
    {
        __result = Gorge(__instance);
        return false;
    }
    private static async Task Gorge(RoomFullOfCheese model)
    {
        var player = model.Owner!;
        CardCreationOptions Options(CardRarity rarity) => CardCreationOptions.ForNonCombatWithUniformOdds([player.Character.CardPool], c => c.Rarity == rarity)
            .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoCardPoolModifications);
        var cards = CardFactory.CreateForReward(player, 6, Options(CardRarity.Common))
            .Concat(CardFactory.CreateForReward(player, 2, Options(CardRarity.Uncommon))).ToList();
        var prefs = new CardSelectorPrefs(new LocString("events", "ROOM_FULL_OF_CHEESE.pages.GORGE.selectionScreenPrompt"), 2);
#if STS2_PUBLIC
        foreach (var card in await CardSelectCmd.FromSimpleGridForRewards(new BlockingPlayerChoiceContext(), cards, player, prefs))
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck));
#else
        await (Task)AccessTools.Method(typeof(EventModel), "SelectCardsToAddToDeckFromGrid").Invoke(model, [cards, prefs])!;
#endif
        NativeEventActions.Finish(model, "ROOM_FULL_OF_CHEESE.pages.GORGE.description");
    }
}

[HarmonyPatch(typeof(RoomFullOfCheese), "GenerateInitialOptions")]
internal static class NativeCheeseCandle
{
    private static void Postfix(RoomFullOfCheese __instance, ref IReadOnlyList<EventOption> __result)
    {
        __result = __result.Append(NativeEventActions.Option(__instance, "ROOM_FULL_OF_CHEESE.pages.INITIAL.options.AM_CANDLE", async () =>
        {
            await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), __instance.Owner!.Creature, 14, isFromCard: false);
            if (!__instance.Owner.Creature.IsDead) await RelicCmd.Obtain<BlueCandle>(__instance.Owner);
            NativeEventActions.Finish(__instance, "ROOM_FULL_OF_CHEESE.pages.AM_CANDLE.description");
        }, HoverTipFactory.FromRelic<BlueCandle>().ToArray()).ThatDecreasesMaxHp(14)).ToArray();
    }
}
