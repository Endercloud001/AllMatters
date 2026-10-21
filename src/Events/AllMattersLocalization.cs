using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace AllMatters.Events;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class AllMattersLocalization
{
    private static readonly Dictionary<string, Dictionary<string, string>> Tables = [];

    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        if (name is not ("events" or "cards" or "relics" or "powers" or "monsters" or "encounters")) return;
        string language = __instance.Language == "zhs" ? "zhs" : "eng";
        string key = $"{language}/{name}";
        if (!Tables.TryGetValue(key, out var entries))
        {
            string json = Godot.FileAccess.GetFileAsString($"res://AllMatters/localization/{key}.json");
            entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
            Tables.Add(key, entries);
        }
        __result.MergeWith(entries);
    }
}
