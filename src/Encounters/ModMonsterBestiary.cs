// SPDX-License-Identifier: GPL-3.0-only
using AllMatters.Monsters;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Encounters;

// The stock list only visits act and known event encounters. Keep these supplemental
// rows local to the screen so discovery and encounter pools still use the native data.
[HarmonyPatch(typeof(NBestiary), "CreateEntries")]
internal static class ModMonsterBestiary
{
    private static void Postfix(NBestiary __instance, VBoxContainer ____bestiaryList,
        HashSet<ModelId> ____discoveredMonsterIds, HashSet<ModelId> ____discoveredEncounterIds)
    {
        var listed = ____bestiaryList.GetChildren().OfType<NBestiaryEntry>()
            .Select(row => row.Entry.monsterModel?.Id).OfType<ModelId>().ToHashSet();

        EncounterModel[] events =
        [
            ModelDb.Encounter<RedMaskBandits>(),
            ModelDb.Encounter<ChampionEncounter>(),
            ModelDb.Encounter<HolesEncounter>(),
            ModelDb.Encounter<JungleMazeEncounter>(),
            ModelDb.Encounter<MindBloomEncounter>(),
            ModelDb.Encounter<WhisperingHollowEncounter>(),
        ];
        var entries = events.SelectMany(encounter => encounter.AllPossibleMonsters.Select(
                monster => BestiaryEntry.FromMonster(monster, encounter, RoomType.Event)))
            .Where(entry => entry.monsterModel is { ShouldShowInCompendium: true } monster && listed.Add(monster.Id))
            .OrderBy(entry => entry.GetEntryTitle(), StringComparer.CurrentCulture)
            .ToArray();
#if STS2_PUBLIC
        if (entries.Length > 0)
        {
            var heading = new LocString("encounters", events[0].Id.Entry + ".groupTitle").GetFormattedText();
            ____bestiaryList.AddChild(new Label { Text = heading });
        }
#endif
        foreach (var data in entries)
            AddEntry(__instance, ____bestiaryList, data, ____discoveredMonsterIds, ____discoveredEncounterIds);
    }

    internal static NBestiaryEntry AddEntry(NBestiary screen, VBoxContainer list, BestiaryEntry data,
        HashSet<ModelId> discoveredMonsters, HashSet<ModelId> discoveredEncounters)
    {
        var entry = NBestiaryEntry.Create(data, data.IsDiscovered(discoveredMonsters, discoveredEncounters));
        list.AddChild(entry);
        entry.Connect(NClickableControl.SignalName.Released,
            Callable.From<NBestiaryEntry>(selected => screen.Call("OnMonsterClicked", selected)));
        return entry;
    }
}

[HarmonyPatch(typeof(NBestiary), "AddAct")]
internal static class ModActMonsterBestiary
{
    private static void Prefix(VBoxContainer ____bestiaryList, out int __state) =>
        __state = ____bestiaryList.GetChildCount();

    private static void Postfix(NBestiary __instance, ActModel act, VBoxContainer ____bestiaryList,
        HashSet<ModelId> ____discoveredMonsterIds, HashSet<ModelId> ____discoveredEncounterIds, int __state)
    {
        // The native method adds no section for an undiscovered act.
        if (____bestiaryList.GetChildCount() == __state)
            return;

        switch (act)
        {
            case Overgrowth:
                Add(ModelDb.Monster<FungusBeast>(), act.AllEncounters.First(e => e.RoomType == RoomType.Monster));
                break;
            case Glory:
                Add(ModelDb.Monster<Mystic>(), act.AllEncounters.First(e => e.RoomType == RoomType.Monster));
                Add(ModelDb.Monster<TorchHead>(), ModelDb.Encounter<QueenBoss>());
                break;
        }

        void Add(MonsterModel monster, EncounterModel encounter)
        {
            var rows = ____bestiaryList.GetChildren().Skip(__state).OfType<NBestiaryEntry>();
            if (!monster.ShouldShowInCompendium || rows.Any(row => row.Entry.monsterModel?.Id == monster.Id))
                return;

            var data = BestiaryEntry.FromMonster(monster, encounter, encounter.RoomType);
            var next = rows.FirstOrDefault(row => row.Entry.roomType > data.roomType);
            var entry = ModMonsterBestiary.AddEntry(__instance, ____bestiaryList, data,
                ____discoveredMonsterIds, ____discoveredEncounterIds);
            if (next != null)
                ____bestiaryList.MoveChild(entry, next.GetIndex());
        }
    }
}
