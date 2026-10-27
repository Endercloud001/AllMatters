using System.Runtime.CompilerServices;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;

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
    private sealed class SharedActions
    {
        internal readonly Dictionary<Player, Dictionary<string, RestSiteOption>> ByPlayer = new();
        internal readonly HashSet<Player> Joined = [];
    }
    private static readonly ConditionalWeakTable<IRunState, SharedActions> Groups = new();
    private static readonly AsyncLocal<bool> SharedSelection = new();
    internal static bool RequiresSelection => SharedSelection.Value;

    private static RestSiteOption[] GenerateActions(DenseVegetation model)
    {
        var owner = model.Owner!;
        var run = owner.RunState;
        // Each owner joins once. Seeing the same owner again starts a new visit,
        // so revisiting cannot retain old deck/relic eligibility.
        if (!Groups.TryGetValue(run, out var group) || group.Joined.Contains(owner))
        {
            group = new SharedActions();
            foreach (var player in run.Players)
                group.ByPlayer.Add(player, RestSiteOption.Generate(player)
                    .Where(o => o is not MendRestSiteOption and not HatchRestSiteOption && o.IsEnabled)
                    .ToDictionary(o => o.OptionId));
            Groups.Remove(run);
            Groups.Add(run, group);
        }
        group.Joined.Add(owner);
        // The native shared vote executes one index for every owner. Only offer
        // actions everyone can execute, in identical order, with each owner's hooks.
        var ids = group.ByPlayer[owner].Keys
            .Where(id => group.ByPlayer.Values.All(actions => actions.ContainsKey(id)))
            .Order(StringComparer.Ordinal).ToList();
        var selected = new List<RestSiteOption>();
        while (ids.Count > 0 && selected.Count < 3)
        {
            int index = model.Rng.NextInt(ids.Count);
            selected.Add(group.ByPlayer[owner][ids[index]]);
            ids.RemoveAt(index);
        }
        return selected.ToArray();
    }

    private static void Postfix(DenseVegetation __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        if (!Visits.TryGetValue(model, out var visit))
        {
            visit = new Visit { Actions = GenerateActions(model), Trudge = __result.Single(o => o.TextKey.EndsWith(".TRUDGE_ON")) };
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
            description.Add("ActionDescription", action.Description.GetFormattedText());
            return new EventOption(model, async () =>
            {
                if (visit.Choosing || visit.Completed || !action.IsEnabled) return;
                visit.Choosing = true;
                bool previousSelection = SharedSelection.Value;
                SharedSelection.Value = model.Owner!.RunState.Players.Count > 1;
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
                finally
                {
                    SharedSelection.Value = previousSelection;
                    visit.Choosing = false;
                }
            }, action.Title, description, key, []);
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

// A shared vote has committed every player to the same action. A cancelled
// selection for only one owner would put the team's event pages out of sync.
[HarmonyPatch]
internal static class DenseVegetationSharedSelection
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForUpgrade));
        yield return AccessTools.Method(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForRemoval));
    }

    private static void Prefix(ref CardSelectorPrefs prefs)
    {
        if (NativeDenseVegetation.RequiresSelection) prefs = prefs with { Cancelable = false };
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
                ? "{ActionDescription}进入战斗。"
                : "{ActionDescription} Enter combat.",
            ["DENSE_VEGETATION.pages.AM_REST.description"] = zh
                ? "你完成了休整。\n\n忽然，身旁的植被开始剧烈摇晃。你并不孤单……"
                : "You finish tending to yourself.\n\nSuddenly, the vegetation around you begins to shake. You are not alone…",
        });
    }
}
