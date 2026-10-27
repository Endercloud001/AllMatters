using HarmonyLib;
using AllMatters.Encounters;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Events;

[HarmonyPatch(typeof(EventModel), "get_IsShared")]
internal static class TeaTrialSharedPage
{
    private static void Postfix(EventModel __instance, ref bool __result)
    {
        if (__instance is RoundTeaParty or Trial) __result = true;
    }
}

[HarmonyPatch(typeof(RoundTeaParty), "GenerateInitialOptions")]
internal static class TeaChampionOption
{
    private static void Postfix(RoundTeaParty __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        __result = __result.Append(NativeEventActions.Option(model, "ROUND_TEA_PARTY.pages.INITIAL.options.AM_FIGHT", () =>
        {
            EncounterModel encounter = ModelDb.Encounter<ChampionEncounter>();
#if STS2_PUBLIC
            encounter = encounter.ToMutable();
#endif
            NativeEventActions.EnterCombat(model, encounter, [], true);
            return Task.CompletedTask;
        }, HoverTipFactory.FromRelic<Relics.ChampionBelt>().ToArray())).ToArray();
    }
}

[HarmonyPatch(typeof(Trial), "GenerateInitialOptions")]
internal static class TrialChallengeOption
{
    private static void Postfix(Trial __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        __result = __result.Append(NativeEventActions.Option(model, "TRIAL.pages.INITIAL.options.AM_CHALLENGE", () =>
        {
            NativeEventActions.State(model, "TRIAL.pages.AM_CHALLENGE.description",
            [NativeEventActions.Option(model, "TRIAL.pages.AM_CHALLENGE.options.AM_CONTINUE", () =>
            {
                EncounterModel encounter = ModelDb.Encounter<TrialEncounter>();
#if STS2_PUBLIC
                encounter = encounter.ToMutable();
#endif
                NativeEventActions.EnterCombat(model, encounter, [], true);
                return Task.CompletedTask;
            })]);
            return Task.CompletedTask;
        })).ToArray();
    }
}

[HarmonyPatch(typeof(EventModel), nameof(EventModel.Resume))]
internal static class TeaTrialReturn
{
    private static void Postfix(EventModel __instance, AbstractRoom exitedRoom)
    {
        if (exitedRoom is not CombatRoom { ParentEventId: var parent } combat || parent != __instance.Id) return;
        if (__instance is RoundTeaParty && combat.Encounter is ChampionEncounter)
            NativeEventActions.Finish(__instance, "ROUND_TEA_PARTY.pages.CONTINUE_FIGHT.description");
        else if (__instance is Trial && combat.Encounter is TrialEncounter)
            NativeEventActions.Finish(__instance, "TRIAL.pages.AM_VICTORY.description");
    }
}
