using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;

namespace AllMatters.Events;

// Later content groups append providers here. Providers only enumerate candidates;
// the event consumes RNG once, when the player actually chooses an option.
public static class TrashHeapCandidates
{
    private static readonly List<Func<Player, IEnumerable<CardModel>>> CardGroups =
    [
        _ => [ModelDb.Card<AllMatters.Cards.Electrodynamics>(), ModelDb.Card<AllMatters.Cards.AutoShields>(),
            ModelDb.Card<AllMatters.Cards.Bullseye>(), ModelDb.Card<AllMatters.Cards.CoreSurge>()],
        _ => [ModelDb.Card<AllMatters.Cards.EndlessAgony>(), ModelDb.Card<AllMatters.Cards.Terror>(), ModelDb.Card<AllMatters.Cards.Dropkick>(),
            ModelDb.Card<AllMatters.Cards.Reaper>(), ModelDb.Card<AllMatters.Cards.Disarm>(), ModelDb.Card<AllMatters.Cards.GhostlyArmor>()],
        _ => [ModelDb.Card<AllMatters.Cards.TalkToTheHand>(), ModelDb.Card<AllMatters.Cards.Fasting>(), ModelDb.Card<AllMatters.Cards.Alpha>(), ModelDb.Card<AllMatters.Cards.ForeignInfluence>()],
    ];
    private static readonly List<Func<Player, IEnumerable<RelicModel>>> RelicGroups = [];

    public static void RegisterCards(Func<Player, IEnumerable<CardModel>> group) => CardGroups.Add(group);
    public static void RegisterRelics(Func<Player, IEnumerable<RelicModel>> group) => RelicGroups.Add(group);

    public static IReadOnlyList<CardModel> Cards(Player player) => new CardModel[]
    {
        ModelDb.Card<Caltrops>(), ModelDb.Card<Clash>(), ModelDb.Card<Distraction>(),
        ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.HelloWorld>(), ModelDb.Card<Outmaneuver>(),
    }.Concat(CardGroups.SelectMany(group => group(player))).DistinctBy(card => card.Id).ToArray();

    public static IReadOnlyList<RelicModel> Relics(Player player) => new RelicModel[]
    {
        ModelDb.Relic<DarkstonePeriapt>(), ModelDb.Relic<DreamCatcher>(), ModelDb.Relic<HandDrill>(),
    }.Concat(RelicGroups.SelectMany(group => group(player))).DistinctBy(relic => relic.Id).ToArray();
}

[HarmonyPatch(typeof(TrashHeap), "CanonicalVars", MethodType.Getter)]
internal static class TrashHeapGold
{
    private static void Postfix(ref IEnumerable<DynamicVar> __result)
        => __result = __result.Select(variable => variable is GoldVar ? new GoldVar(50) : variable);
}

[HarmonyPatch(typeof(TrashHeap), "Grab")]
internal static class TrashHeapGrab
{
    private static bool Prefix(TrashHeap __instance, ref Task __result)
    {
        __result = Grab(__instance);
        return false;
    }

    private static async Task Grab(TrashHeap model)
    {
        if (model.IsFinished) return;
        var owner = model.Owner!;
        await PlayerCmd.GainGold(model.DynamicVars.Gold.BaseValue, owner);
        var card = owner.RunState.CreateCard(model.Rng.NextItem(TrashHeapCandidates.Cards(owner)), owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 2f);
        NativeEventActions.Finish(model, "TRASH_HEAP.pages.GRAB.description");
    }
}

[HarmonyPatch(typeof(TrashHeap), "DiveIn")]
internal static class TrashHeapDive
{
    private static bool Prefix(TrashHeap __instance, ref Task __result)
    {
        __result = Dive(__instance);
        return false;
    }

    private static async Task Dive(TrashHeap model)
    {
        if (model.IsFinished) return;
        await NativeEventActions.LoseHp(model, model.DynamicVars.HpLoss.IntValue);
        if (!model.Owner!.Creature.IsDead)
            await RelicCmd.Obtain(model.Rng.NextItem(TrashHeapCandidates.Relics(model.Owner)).ToMutable(), model.Owner);
        NativeEventActions.Finish(model, "TRASH_HEAP.pages.DIVE_IN.description");
    }
}
