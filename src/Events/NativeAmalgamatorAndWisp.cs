using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Events;

[HarmonyPatch]
internal static class NativeRemovalBranches
{
    private static IEnumerable<MethodBase> TargetMethods() => new[] { typeof(Amalgamator), typeof(LostWisp) }.Select(t => AccessTools.Method(t, "GenerateInitialOptions"));
    private static void Postfix(EventModel __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        var player = model.Owner!;
        var options = __result.ToList();
        if (model is Amalgamator)
        {
            var pool = CardCreationOptions.ForNonCombatWithUniformOdds([player.Character.CardPool, ModelDb.CardPool<ColorlessCardPool>()], c => c.Rarity == CardRarity.Rare)
                .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoCardPoolModifications).GetPossibleCards(player).DistinctBy(c => c.Id).ToArray();
            bool legal = player.Deck.Cards.Count(c => c.IsRemovable) >= 2 && pool.Length > 0;
            options.Add(new EventOption(model, legal ? async () =>
            {
                var removed = player.Deck.Cards.Where(c => c.IsRemovable).ToList().StableShuffle(model.Rng).Take(2).ToArray();
                if (removed.Length < 2) return;
                await CardPileCmd.RemoveFromDeck(removed);
                var card = player.RunState.CreateCard(pool[model.Rng.NextInt(pool.Length)], player);
                CardCmd.Upgrade(card, CardPreviewStyle.None);
                CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck));
                NativeEventActions.Finish(model, "AMALGAMATOR.pages.AM_FUSE.description");
            } : null, "AMALGAMATOR.pages.INITIAL.options.AM_FUSE"));
        }
        else
        {
            decimal cost = player.Creature.MaxHp * .1m;
            options.Add(new EventOption(model, player.Deck.Cards.Any(c => c.IsRemovable) ? async () =>
            {
                await NativeEventActions.LoseHp(model, cost);
                if (!player.Creature.IsDead)
                {
                    var selected = await CardSelectCmd.FromDeckForRemoval(player, new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1) { Cancelable = false });
                    await CardPileCmd.RemoveFromDeck(selected.ToList());
                }
                NativeEventActions.Finish(model, "LOST_WISP.pages.AM_BURN.description");
            } : null, "LOST_WISP.pages.INITIAL.options.AM_BURN").ThatDoesDamage(cost));
        }
        __result = options;
    }
}
