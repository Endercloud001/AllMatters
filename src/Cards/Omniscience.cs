using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using AllMatters.Powers;
namespace AllMatters.Cards;
[RegisterCard(typeof(EventCardPool), FullPublicEntry = "ALL_MATTERS_OMNISCIENCE")]
public sealed class Omniscience : ModCardTemplate
{
    public Omniscience() : base(4, CardType.Skill, CardRarity.Event, TargetType.Self) { }
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<ColorlessCardPool>();
    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://AllMatters/images/card_portraits/omniscience.png");
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var selected = await CardSelectCmd.FromCombatPile(choiceContext, Owner.PlayerCombatState!.DrawPile, Owner,
            new MegaCrit.Sts2.Core.CardSelection.CardSelectorPrefs(new MegaCrit.Sts2.Core.Localization.LocString("card_selection", "ALL_MATTERS_OMNISCIENCE"), 1));
        foreach (var card in selected)
        {
            // Reserve the chosen card before another queued Omniscience opens its selector.
            await CardPileCmd.Add(card, PileType.Play);
            OmniscienceResolution.Enqueue(Owner, card);
        }
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

// Native OnPlayWrapper must finish (including Exhaust and after-play hooks) before
// a chosen Omniscience opens its next selector. The per-owner FIFO drains only at
// that boundary; nested plays append work instead of recursively draining it.
internal sealed class OmniscienceResolution
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MegaCrit.Sts2.Core.Entities.Players.Player, OmniscienceResolution> Pending = new();
    private readonly Queue<CardModel> _cards = new();
    private bool _draining;
    private CardModel? _repeating;
    internal static bool IsRepeating(CardModel card)
        => Pending.TryGetValue(card.Owner, out var queue) && queue._repeating == card;
    internal static void Enqueue(MegaCrit.Sts2.Core.Entities.Players.Player owner, CardModel card)
        => Pending.GetOrCreateValue(owner)._cards.Enqueue(card);
    internal static async Task Complete(Task nativePlay, Omniscience source, PlayerChoiceContext context)
    {
        var queue = Pending.GetOrCreateValue(source.Owner);
        try
        {
            await nativePlay;
            if (queue._draining) return;
            queue._draining = true;
            try
            {
                while (queue._cards.TryDequeue(out var card))
                {
                    card.CanPlay(out var reason, out _);
                    if ((reason & ~(UnplayableReason.EnergyCostTooHigh | UnplayableReason.StarCostTooHigh)) == UnplayableReason.None)
                    {
                        queue._repeating = card;
                        try { await CardCmd.AutoPlay(context, card, null); }
                        finally { queue._repeating = null; }
                    }
                    // Native Power resolution removes the card from combat. Preserve that
                    // result; ordinary/exhaust cards finish in the Exhaust pile.
                    if (card.Pile != null && card.Pile.Type != PileType.Exhaust)
                        await CardCmd.Exhaust(context, card);

                }
            }
            finally { queue._draining = false; }
        }
        catch { queue._cards.Clear(); throw; }
    }
}

[HarmonyLib.HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class OmniscienceAfterNativePlay
{
    private static void Postfix(CardModel __instance, PlayerChoiceContext choiceContext, ref Task __result)
    {
        if (__instance is Omniscience source)
            __result = OmniscienceResolution.Complete(__result, source, choiceContext);
    }
}

[HarmonyLib.HarmonyPatch(typeof(CardModel), "GeneratePlayCount")]
internal static class OmniscienceNativeRepeat
{
    private static void Postfix(CardModel __instance, ref Task<int> __result)
    {
        if (OmniscienceResolution.IsRepeating(__instance)) __result = AddOnePlay(__result);
    }
    private static async Task<int> AddOnePlay(Task<int> nativeCount) => await nativeCount + 1;
}

