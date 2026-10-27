using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace AllMatters.Events;

// Native events are sealed. Keep access to their protected state transitions here;
// options still use the game's normal choice, synchronization and cleanup path.
internal static class NativeEventActions
{
    internal static void Finish(EventModel model, string key)
        => AccessTools.Method(typeof(EventModel), "SetEventFinished").Invoke(model, [new LocString("events", key)]);

    internal static void State(EventModel model, string key, IEnumerable<EventOption> options)
        => AccessTools.Method(typeof(EventModel), "SetEventState").Invoke(model, [new LocString("events", key), options]);

    internal static void EnterCombat(EventModel model, EncounterModel encounter, IReadOnlyList<Reward> rewards, bool resume)
        => AccessTools.Method(typeof(EventModel), "EnterCombatWithoutExitingEvent",
            [typeof(EncounterModel), typeof(IReadOnlyList<Reward>), typeof(bool)])
            .Invoke(model, [encounter, rewards, resume]);

    internal static async Task Leave(EventModel model)
    {
        if (model is MegaCrit.Sts2.Core.Models.Events.PunchOff)
        {
            // Opening the map does not exit the room or raise RoomExited.
            AccessTools.Method(model.GetType(), "OnRoomExited").Invoke(model, null);
            NEventRoom.Instance?.EmbeddedCombatRoom?.Hide();
        }
        Finish(model, "ALL_MATTERS_NATIVE_EMPTY");
        if (NEventRoom.Instance != null) await NEventRoom.Proceed();
    }

    internal static EventOption Option(EventModel model, string key, Func<Task> action, params IHoverTip[] tips)
        => new(model, async () => { if (!model.IsFinished) await action(); }, key, tips);

    internal static Task LoseHp(EventModel model, decimal amount)
        => CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), model.Owner!.Creature, amount,
            ValueProp.Unblockable | ValueProp.Unpowered, null, null);
}

// Native RelicOption uses only the relic's keyword tips. Include the relic
// itself as well, so ordinary event rewards can be inspected before choosing.
[HarmonyPatch(typeof(EventOption), nameof(EventOption.FromRelic))]
internal static class NativeRelicOptionPreview
{
    private static void Postfix(RelicModel relic, EventModel eventModel, EventOption __result)
    {
        if (eventModel is not AncientEventModel)
            __result.HoverTips = HoverTipFactory.FromRelic(relic).ToArray();
    }
}
