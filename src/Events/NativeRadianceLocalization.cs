using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace AllMatters.Events;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class NativeRadianceLocalization
{
    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        bool zh = __instance.Language == "zhs";
        if (name == "cards") __result.MergeWith(new Dictionary<string, string>
        {
            ["ALL_MATTERS_BRILLIANCE.title"] = zh ? "光辉" : "Brilliance",
            ["ALL_MATTERS_BRILLIANCE.description"] = zh ? "造成等同于本场战斗中你所获得的正面效果总层数的伤害。{InCombat:\n（造成{CalculatedDamage:diff()}点伤害。）|}" : "Deal damage equal to the total positive Power stacks you gained this combat.{InCombat:\n(Deal {CalculatedDamage:diff()} damage.)|}",
        });
        else if (name == "events") __result.MergeWith(new Dictionary<string, string>
        {
            ["DROWNING_BEACON.pages.INITIAL.options.AM_LIGHT.title"] = zh ? "要有光" : "Let There Be Light",
            ["DROWNING_BEACON.pages.INITIAL.options.AM_LIGHT.description"] = zh ? "失去[red]{AmPaymentPotion}[/red]，获得[gold]光辉[/gold]。" : "Lose [red]{AmPaymentPotion}[/red]. Obtain [gold]Brilliance[/gold].",
            ["DROWNING_BEACON.pages.AM_LIGHT.description"] = zh ? "你爬向灯塔顶端，看着它一直稳步下沉，心里直害怕。\n\n进入到[gold]灯具室[/gold]后，你拿出一个玻璃瓶挡在[purple]逆流光[/purple]与透镜之间，\n\n神奇的是，这些光芒竟聚集在一起使瓶子改变了形质！" : "You climb toward the top, frightened as the lighthouse steadily sinks.\n\nIn the [gold]lamp room[/gold], you hold a glass bottle between the [purple]counterflow light[/purple] and the lens.\n\nMiraculously, the gathered light transforms the bottle!",
        });
    }
}
