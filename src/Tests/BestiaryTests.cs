// SPDX-License-Identifier: GPL-3.0-only
using AllMatters.Monsters;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;

internal static class BestiaryTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("AllMatters bestiary creates native entries for event and hunted monsters", BuildsNativeEntries),
    ];

    private static bool SkipStockEntries() => false;

    private static NBestiaryEntry? selected;
    private static bool ObserveSelection(NBestiaryEntry entry)
    {
        selected = entry;
        return false;
    }

    private static Task BuildsNativeEntries()
    {
        JigsawHuntingNativeTests.RegisterScripts();
        var screen = new NBestiary();
        var sidebar = new VBoxContainer();
        screen.AddChild(sidebar);
        var pointy = ModelDb.Monster<Pointy>();
        var sentry = ModelDb.Monster<Sentry>();
        var torch = ModelDb.Monster<TorchHead>();
        var bandits = ModelDb.Encounter<AllMatters.Encounters.RedMaskBandits>();
        var previous = NBestiaryEntry.Create(BestiaryEntry.FromMonster(pointy, bandits, bandits.RoomType), true);
        sidebar.AddChild(previous);
        var discovered = new HashSet<ModelId> { pointy.Id, sentry.Id, torch.Id };
        AccessTools.Field(typeof(NBestiary), "_bestiaryList").SetValue(screen, sidebar);
        AccessTools.Field(typeof(NBestiary), "_discoveredMonsterIds").SetValue(screen, discovered);
        AccessTools.Field(typeof(NBestiary), "_discoveredEncounterIds").SetValue(screen, new HashSet<ModelId>());

        var harmony = new Harmony("AllMatters.Tests.Bestiary");
        var buildEntries = AccessTools.Method(typeof(NBestiary), "CreateEntries");
        // Replace the stock scene and profile setup. The mod's patch and native entry scene run.
        harmony.Patch(buildEntries, prefix: new HarmonyMethod(typeof(BestiaryTests), nameof(SkipStockEntries)));
        harmony.Patch(AccessTools.Method(typeof(NBestiary), "OnMonsterClicked"),
            prefix: new HarmonyMethod(typeof(BestiaryTests), nameof(ObserveSelection)));
        try
        {
            CheckActSection(ModelDb.Act<Overgrowth>(), ModelDb.Monster<FungusBeast>());
            CheckActSection(ModelDb.Act<Glory>(), ModelDb.Monster<Mystic>(), torch);
            buildEntries.Invoke(screen, null);
            NativeEventTests.Check(!sidebar.GetChildren().OfType<Label>().Any(label => label.Text == "AllMatters"),
                "The hunted enemies must not have a separate mod section.");
            MonsterModel[] expected =
            [
                pointy, ModelDb.Monster<Bear>(), ModelDb.Monster<Romeo>(), sentry,
                ModelDb.Monster<Lagavulin>(), ModelDb.Monster<Transient>(), ModelDb.Monster<WrithingMass>(),
                ModelDb.Monster<Champion>(), ModelDb.Monster<FungusBeast>(), ModelDb.Monster<Mystic>(), torch,
            ];
            var expectedIds = expected.Select(monster => monster.Id).ToHashSet();
            var rows = sidebar.GetChildren().OfType<NBestiaryEntry>()
                .Where(row => row.Entry.monsterModel is { } monster && expectedIds.Contains(monster.Id)).ToArray();
            NativeEventTests.Check(rows.Select(row => row.Entry.monsterModel!.Id).ToHashSet()
                .SetEquals(expectedIds) && rows.Length == expected.Length,
                "Each AllMatters enemy appears exactly once, including enemies without their own encounter.");
            NativeEventTests.Check(ReferenceEquals(rows[0], previous), "Preserve a monster already listed by the game.");
            foreach (var row in rows.Skip(1))
            {
                var id = row.Entry.monsterModel!.Id;
                NativeEventTests.Check(row.IsDiscovered == discovered.Contains(id), "Native discovery still controls access.");
                if (!row.IsDiscovered)
                    continue;
                selected = null;
                row.EmitSignal(NClickableControl.SignalName.Released, row);
                NativeEventTests.Check(ReferenceEquals(selected, row), "A discovered row reaches the native monster selection handler.");
            }
        }
        finally
        {
            selected = null;
            harmony.UnpatchAll(harmony.Id);
            screen.Free();
        }
        return Task.CompletedTask;

        void CheckActSection(ActModel act, params MonsterModel[] monsters)
        {
            var discoveredActs = (HashSet<ModelId>)SaveManager.Instance.Progress.DiscoveredActs;
            bool wasDiscovered = discoveredActs.Remove(act.Id);
            var addAct = AccessTools.Method(typeof(NBestiary), "AddAct");
            int before = sidebar.GetChildCount();
            try
            {
                addAct.Invoke(screen, [act]);
                NativeEventTests.Check(sidebar.GetChildCount() == before, "Undiscovered acts remain hidden.");
                discoveredActs.Add(act.Id);
                addAct.Invoke(screen, [act]);
                var ids = sidebar.GetChildren().Skip(before).OfType<NBestiaryEntry>()
                    .Select(row => row.Entry.monsterModel?.Id).ToHashSet();
                NativeEventTests.Check(monsters.All(monster => ids.Contains(monster.Id)),
                    $"The native {act.Id} section contains its hunted enemies.");
            }
            finally
            {
                if (!wasDiscovered)
                    discoveredActs.Remove(act.Id);
            }
        }
    }
}
