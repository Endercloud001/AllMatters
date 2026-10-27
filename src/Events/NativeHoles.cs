using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.RunData;

namespace AllMatters.Events;

[HarmonyPatch(typeof(EventModel), "get_IsShared")]
internal static class HolesSharedPage
{
    public sealed class PageState
    {
        public int Floor { get; set; } = -1;
        public bool Shared { get; set; }
    }
    private static readonly RunSavedData<PageState> Sharing = RunSavedDataStore.For(ModEntry.ModId).Register<PageState>("holesPage");
    internal static void Initialize() => _ = Sharing;
    private static void Postfix(EventModel __instance, ref bool __result)
    {
        if (__instance is not FieldOfManSizedHoles || __instance.Owner == null) return;
        var run = (RunState)__instance.Owner.RunState;
        int floor = run.MapPointHistory.Sum(points => points.Count);
        var page = Sharing.Get(run);
        if (page.Floor != floor)
        {
            // This is queried before EventModel initializes its owner-dependent RNG.
            // A separate native RNG gives every peer the same floor roll without
            // consuming another event's or player's random stream.
            var rng = new Rng(run.Rng.Seed);
            for (int i = 0; i < floor; i++) rng.NextInt(100);
            bool shared = rng.NextInt(100) < 33;
            page = Sharing.Modify(run, state => { state.Floor = floor; state.Shared = shared; });
        }
        __result = page.Shared;
    }
}

[HarmonyPatch(typeof(FieldOfManSizedHoles), "GenerateInitialOptions")]
internal static class HolesFightOption
{
    private static void Postfix(FieldOfManSizedHoles __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (!__instance.IsShared) return;
        var model = __instance;
        __result = __result.Append(NativeEventActions.Option(model, "FIELD_OF_MAN_SIZED_HOLES.pages.INITIAL.options.AM_FIGHT", () =>
        {
            EncounterModel encounter = ModelDb.Encounter<Encounters.HolesEncounter>();
#if STS2_PUBLIC
            encounter = encounter.ToMutable();
#endif
            NativeEventActions.EnterCombat(model, encounter, [], true);
            return Task.CompletedTask;
        })).ToArray();
    }
}

[HarmonyPatch(typeof(EventModel), nameof(EventModel.Resume))]
internal static class HolesFightReturn
{
    private static void Postfix(EventModel __instance, AbstractRoom exitedRoom)
    {
        if (__instance is FieldOfManSizedHoles && exitedRoom is CombatRoom
            { Encounter: Encounters.HolesEncounter, ParentEventId: var parent } && parent == __instance.Id)
            NativeEventActions.Finish(__instance, "FIELD_OF_MAN_SIZED_HOLES.pages.AM_FIGHT.description");
    }
}
