using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace AllMatters.Events;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class ClericLocalization
{
    private static readonly Dictionary<string, Dictionary<string, string>> Tables = [];

    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        if (name != "events") return;
        string language = __instance.Language == "zhs" ? "zhs" : "eng";
        if (!Tables.TryGetValue(language, out var entries))
        {
            string json = Godot.FileAccess.GetFileAsString($"res://AllMatters/localization/{language}/events.json");
            entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
            Tables.Add(language, entries);
        }
        __result.MergeWith(entries);
    }
}
