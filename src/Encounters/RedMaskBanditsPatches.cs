// SPDX-License-Identifier: GPL-3.0-only
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Encounters;

#if STS2_PUBLIC
// 0.107.1 has no ModelDb.EventEncounters or event category in the bestiary.
// Add a separate event section, without putting the encounter in Hive's map pool.
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Bestiary.NBestiary), "CreateEntries")]
internal static class BanditsBestiary
{
    private static void Postfix(MegaCrit.Sts2.Core.Nodes.Screens.Bestiary.NBestiary __instance,
        Godot.VBoxContainer ____bestiaryList, HashSet<ModelId> ____discoveredMonsterIds,
        HashSet<ModelId> ____discoveredEncounterIds)
    {
        var encounter = ModelDb.Encounter<RedMaskBandits>();
        var title = new MegaCrit.Sts2.Core.Localization.LocString("encounters", encounter.Id.Entry + ".groupTitle");
        ____bestiaryList.AddChild(new Godot.Label { Text = title.GetFormattedText() });
        foreach (var monster in encounter.AllPossibleMonsters)
        {
            var data = MegaCrit.Sts2.Core.Nodes.Screens.Bestiary.BestiaryEntry.FromMonster(monster, encounter, RoomType.Event);
            var entry = MegaCrit.Sts2.Core.Nodes.Screens.Bestiary.NBestiaryEntry.Create(data,
                data.IsDiscovered(____discoveredMonsterIds, ____discoveredEncounterIds));
            ____bestiaryList.AddChild(entry);
            entry.Connect(MegaCrit.Sts2.Core.Nodes.GodotExtensions.NClickableControl.SignalName.Released,
                Godot.Callable.From<MegaCrit.Sts2.Core.Nodes.Screens.Bestiary.NBestiaryEntry>(
                    selected => __instance.Call("OnMonsterClicked", selected)));
        }
    }
}
#else
[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.EventEncounters), MethodType.Getter)]
internal static class BanditsBestiary
{
    private static void Postfix(ref IEnumerable<EncounterModel> __result)
    {
        var encounter = ModelDb.Encounter<RedMaskBandits>();
        if (!__result.Any(model => model.Id == encounter.Id))
            __result = __result.Append(encounter);
    }
}
#endif

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class BanditsGoldReward
{
    private static void Postfix(RewardsSet __instance, AbstractRoom room)
    {
        if (room is not CombatRoom { Encounter: RedMaskBandits } combat) return;
        // Replace only the normal gold reward. Keep inherited cards and potion
        // odds, and leave later native reward-modification hooks intact.
        combat.ExtraRewards.TryGetValue(__instance.Player, out var explicitRewards);
        __instance.Rewards.RemoveAll(reward => reward is GoldReward &&
            (explicitRewards == null || !explicitRewards.Contains(reward)));
    }
}
