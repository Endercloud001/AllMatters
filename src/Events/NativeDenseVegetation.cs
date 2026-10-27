using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace AllMatters.Events;

[HarmonyPatch(typeof(DenseVegetation), "GenerateInitialOptions")]
internal static class NativeDenseVegetation
{
    private sealed class Visit
    {
        internal required RestSiteOption[] Actions;
        internal required EventOption Trudge;
        internal bool Choosing;
        internal bool Completed;
    }

    private static readonly ConditionalWeakTable<DenseVegetation, Visit> Visits = new();

    private static void Postfix(DenseVegetation __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        if (!Visits.TryGetValue(model, out var visit))
        {
            var actions = RestSiteOption.Generate(model.Owner!)
                .Where(o => o is not MendRestSiteOption and not HatchRestSiteOption && o.IsEnabled).ToList();
            var selected = new List<RestSiteOption>();
            while (actions.Count > 0 && selected.Count < 3)
            {
                int index = model.Rng.NextInt(actions.Count);
                selected.Add(actions[index]);
                actions.RemoveAt(index);
            }
            visit = new Visit { Actions = selected.ToArray(), Trudge = __result.Single(o => o.TextKey.EndsWith(".TRUDGE_ON")) };
            Visits.Add(model, visit);
        }
        __result = Options(model, visit);
    }

    private static IReadOnlyList<EventOption> Options(DenseVegetation model, Visit visit)
    {
        if (visit.Completed) return model.CurrentOptions;
        var options = visit.Actions.Select(action =>
        {
            string key = "DENSE_VEGETATION.pages.INITIAL.options.AM_REST_" + action.OptionId;
            var description = new LocString("events", "DENSE_VEGETATION.AM_ACTION.description");
            description.Add("ActionName", action.Title.GetFormattedText());
            description.Add("ActionDescription", action.Description.GetFormattedText());
            return new EventOption(model, async () =>
            {
                if (visit.Choosing || visit.Completed || !action.IsEnabled) return;
                visit.Choosing = true;
                try
                {
                    // OnSelect owns the effect, selection UI and action-specific hooks.
                    // Rest-site completion policy (notably Miniature Tent) does not apply here.
                    if (await action.OnSelect())
                    {
                        visit.Completed = true;
                        NativeEventActions.State(model, "DENSE_VEGETATION.pages.AM_REST.description",
                            [new EventOption(model, () => (Task)AccessTools.Method(typeof(DenseVegetation), "Fight").Invoke(model, null)!,
                                "DENSE_VEGETATION.pages.REST.options.FIGHT")]);
                    }
                    else
                        NativeEventActions.State(model, "DENSE_VEGETATION.pages.INITIAL.description", Options(model, visit));
                }
                finally { visit.Choosing = false; }
            }, new LocString("events", "DENSE_VEGETATION.pages.INITIAL.options.REST.title"), description, key, []);
        }).ToList();
        options.Add(new EventOption(model, async () =>
        {
            if (visit.Choosing || visit.Completed) return;
            visit.Choosing = visit.Completed = true;
            await visit.Trudge.Chosen();
        }, visit.Trudge.Title, visit.Trudge.Description, visit.Trudge.TextKey, visit.Trudge.HoverTips)
            .ThatDoesDamage(model.DynamicVars.HpLoss.BaseValue));
        return options;
    }
}

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class DenseVegetationLocalization
{
    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        if (name != "events") return;
        bool zh = __instance.Language == "zhs";
        __result.MergeWith(new Dictionary<string, string>
        {
            ["DENSE_VEGETATION.AM_ACTION.description"] = zh
                ? "[gold]{ActionName}[/gold]：{ActionDescription}\n进入战斗。"
                : "[gold]{ActionName}[/gold]: {ActionDescription}\nEnter combat.",
            ["DENSE_VEGETATION.pages.AM_REST.description"] = zh
                ? "你完成了休整。\n\n忽然，身旁的植被开始剧烈摇晃。你并不孤单……"
                : "You finish tending to yourself.\n\nSuddenly, the vegetation around you begins to shake. You are not alone…",
        });
    }
}
