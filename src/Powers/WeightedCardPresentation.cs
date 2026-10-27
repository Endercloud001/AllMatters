using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace AllMatters.Powers;

// Grasp's old Weighted appearance is cosmetic here. The power remains the sole
// energy penalty, and a card's actual affliction and overlay remain untouched.
[HarmonyPatch(typeof(NCard), "ReloadOverlay")]
internal static class WeightedCardPresentation
{
    private const string OverlayName = "AllMattersWeighted";
    private const string CardGroup = "AllMattersWeightedCardViews";
    private const string OverlayPath = "res://AllMatters/scenes/weighted.tscn";

    internal static bool IsActive(CardModel? card)
        => card?.IsMutable == true && card.CombatState is { } combat
            && combat.ContainsCard(card) && combat.Enemies.Any(c => c.IsAlive && c.HasPower<TightGripPower>());

    private static void Postfix(NCard __instance)
    {
        __instance.AddToGroup(CardGroup);
        Refresh(__instance);
    }

    private static void Refresh(NCard node)
    {
        var container = node.OverlayContainer;
        if (container == null) return;
        var overlay = container.GetNodeOrNull<Control>(OverlayName);
        if (IsActive(node.Model))
        {
            if (overlay != null) return;
            overlay = ResourceLoader.Load<PackedScene>(OverlayPath).Instantiate<Control>();
            overlay.Name = OverlayName;
            container.AddChild(overlay);
        }
        else if (overlay != null)
        {
            container.RemoveChild(overlay);
            overlay.QueueFree();
        }
    }

    internal static void RefreshViews()
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        // Native groups follow node lifetime and include open pile browsers and
        // previews, which FindOnTable deliberately excludes.
        foreach (var node in tree.GetNodesInGroup(CardGroup).OfType<NCard>()) Refresh(node);
    }
}

[HarmonyPatch(typeof(CardModel), "get_HoverTips")]
internal static class WeightedCardHoverTip
{
    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (WeightedCardPresentation.IsActive(__instance))
            __result = __result.Append(new HoverTip(new LocString("powers", "ALL_MATTERS_WEIGHTED.title"),
                new LocString("powers", "ALL_MATTERS_WEIGHTED.description")));
    }
}
