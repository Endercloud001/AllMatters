using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Events;

[HarmonyPatch(typeof(JungleMazeAdventure), "SafetyInNumbers")]
internal static class NativeJungleMazeFight
{
    private static bool Prefix(JungleMazeAdventure __instance, ref Task __result)
    {
        __result = Task.CompletedTask;
        if (__instance.IsFinished) return false;
        var bonusRewards = new List<Reward>();
#if STS2_PUBLIC
        foreach (var player in __instance.Owner!.RunState.Players)
#else
        foreach (var player in new[] { __instance.Owner! })
#endif
            bonusRewards.Add(new GoldReward(35, 64, player));

        EncounterModel encounter = ModelDb.Encounter<Encounters.JungleMazeEncounter>();
#if STS2_PUBLIC
        encounter = encounter.ToMutable();
#endif
        NativeEventActions.EnterCombat(__instance, encounter, bonusRewards, true);
        return false;
    }
}

[HarmonyPatch(typeof(EventModel), nameof(EventModel.Resume))]
internal static class NativeJungleMazeReturn
{
    private static void Postfix(EventModel __instance, AbstractRoom exitedRoom)
    {
        if (__instance is JungleMazeAdventure && exitedRoom is CombatRoom
            {
                Encounter: Encounters.JungleMazeEncounter,
                ParentEventId: var parentEventId
            } && parentEventId == __instance.Id)
        {
            NativeEventActions.Finish(__instance, "JUNGLE_MAZE_ADVENTURE.pages.JOIN_FORCES.description");
        }
    }
}

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class NativeJungleMazeLocalization
{
    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        bool zh = __instance.Language == "zhs";
        if (name == "events")
        {
            __result.MergeWith(new Dictionary<string, string>
            {
                ["JUNGLE_MAZE_ADVENTURE.pages.INITIAL.options.JOIN_FORCES.description"] = zh
                    ? "与两名哨卫[red]战斗[/red]以取得战利品。"
                    : "[red]Fight[/red] two Sentries to claim the spoils.",
                ["JUNGLE_MAZE_ADVENTURE.pages.JOIN_FORCES.description"] = zh
                    ? "每个人的脸上都乐开了花，包括你自己！\n这群人里居然还有个吟游诗人！\n你们迈着轻快的步伐走入迷宫，[blue]乌贼巫师阿奎德[/blue]手到擒来地解除了所有陷阱，你和一位[red]格拉索里安战士[/red]则轻轻松松地解决掉了那些[gold]构装体守卫[/gold]。\n这是你这辈子最愉快的一次冒险，你也真的留下了[jitter]美好的回忆[/jitter]。"
                    : "Everyone is beaming, including you!\nThere was even a bard among you!\nYou stroll through the maze. The [blue]squid wizard Aquied[/blue] effortlessly disarms every trap, while you and a [red]Graxorian warrior[/red] make short work of the [gold]construct guards[/gold].\nIt was the most enjoyable adventure of your life, and you truly made [jitter]wonderful memories[/jitter].",
            });
        }
        else if (name == "monsters")
        {
            __result.MergeWith(new Dictionary<string, string>
            {
                ["ALL_MATTERS_MONSTER_SENTRY.name"] = zh ? "哨卫" : "Sentry",
                ["ALL_MATTERS_MONSTER_SENTRY.moves.BOLT.title"] = zh ? "充能" : "Bolt",
                ["ALL_MATTERS_MONSTER_SENTRY.moves.BEAM.title"] = zh ? "激光" : "Beam",
            });
        }
    }
}
