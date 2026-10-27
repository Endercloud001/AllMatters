using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.RunData;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace AllMatters.Events;

[HarmonyPatch(typeof(EventModel), "get_IsShared")]
internal static class WhisperingSharedPage
{
    public sealed class PageState
    {
        public int Floor { get; set; } = -1;
        public bool Shared { get; set; }
    }
    private static readonly RunSavedData<PageState> Sharing = RunSavedDataStore.For(ModEntry.ModId).Register<PageState>("hollowPage");
    internal static void Initialize() => _ = Sharing;
    private static void Postfix(EventModel __instance, ref bool __result)
    {
        if (__instance is not WhisperingHollow || __instance.Owner == null) return;
        var run = (RunState)__instance.Owner.RunState;
        int floor = run.MapPointHistory.Sum(points => points.Count);
        var page = Sharing.Get(run);
        if (page.Floor != floor)
            page = Sharing.Modify(run, state =>
            {
                state.Floor = floor;
                state.Shared = run.Players.All(p => p.Creature.CurrentHp * 2 >= p.Creature.MaxHp);
            });
        __result = page.Shared;
    }
}

// The native synchronizer reads the canonical model, which has no owning run.
// Use its already initialized player model for this conditional shared event.
[HarmonyPatch(typeof(EventSynchronizer), "get_IsShared")]
internal static class ConditionalSharedSynchronization
{
    private static void Postfix(EventSynchronizer __instance, ref bool __result)
    {
        if (__instance.Events.FirstOrDefault() is WhisperingHollow or FieldOfManSizedHoles) __result = __instance.Events[0].IsShared;
    }
}

[HarmonyPatch(typeof(EventSynchronizer), "ChooseSharedEventOption")]
internal static class EventBattleUnanimousChoice
{
    private static bool Prefix(EventSynchronizer __instance, List<uint?> ____playerVotes)
        => __instance.Events.FirstOrDefault() is not (WhisperingHollow or FieldOfManSizedHoles or RoundTeaParty or Trial)
            || (____playerVotes.All(vote => vote.HasValue) && ____playerVotes.Distinct().Count() == 1);
}

[HarmonyPatch(typeof(WhisperingHollow), nameof(WhisperingHollow.CalculateVars))]
internal static class WhisperingCosts
{
    private static void Postfix(WhisperingHollow __instance)
    {
        __instance.DynamicVars.Gold.BaseValue -= 10;
        __instance.DynamicVars.HpLoss.BaseValue = __instance.Rng.NextInt(2) == 0 ? 9 : 18;
    }
}

[HarmonyPatch(typeof(WhisperingHollow), "GenerateInitialOptions")]
internal static class WhisperingHug
{
    private static void Postfix(WhisperingHollow __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        int count = model.DynamicVars.HpLoss.IntValue / 9;
        bool legal = model.Owner!.Deck.Cards.Count(c => c.IsTransformable) >= count;
        var options = __result.ToList();
        options[1] = new EventOption(model, legal ? async () =>
        {
            var cards = await CardSelectCmd.FromDeckForTransformation(model.Owner!, new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, count));
            foreach (var card in cards) await CardCmd.TransformToRandom(card, model.Rng, CardPreviewStyle.EventLayout);
            await NativeEventActions.LoseHp(model, model.DynamicVars.HpLoss.BaseValue);
            NativeEventActions.Finish(model, "WHISPERING_HOLLOW.pages.HUG.description");
        } : null, "WHISPERING_HOLLOW.pages.INITIAL.options.AM_HUG_" + count, HoverTipFactory.Static(StaticHoverTip.Transform))
            .ThatDoesDamage(model.DynamicVars.HpLoss.BaseValue);
        if (model.IsShared)
            options.Add(NativeEventActions.Option(model, "WHISPERING_HOLLOW.pages.INITIAL.options.AM_BURN", () =>
            {
                EncounterModel encounter = ModelDb.Encounter<Encounters.WhisperingHollowEncounter>();
#if STS2_PUBLIC
                encounter = encounter.ToMutable();
#endif
                NativeEventActions.EnterCombat(model, encounter, [], true);
                return Task.CompletedTask;
            }, HoverTipFactory.FromRelic<Relics.DeadBranch>().ToArray()));
        __result = options;
    }
}

[HarmonyPatch(typeof(EventModel), nameof(EventModel.Resume))]
internal static class WhisperingBurnReturn
{
    private static void Postfix(EventModel __instance, MegaCrit.Sts2.Core.Rooms.AbstractRoom exitedRoom)
    {
        if (__instance is WhisperingHollow && exitedRoom is MegaCrit.Sts2.Core.Rooms.CombatRoom
            { Encounter: Encounters.WhisperingHollowEncounter, ParentEventId: var parent } && parent == __instance.Id)
            NativeEventActions.Finish(__instance, "WHISPERING_HOLLOW.pages.AM_BURN.description");
    }
}
