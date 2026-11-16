using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace AllMatters.Jigsaw;

[HarmonyPatch(typeof(NMapScreen), "PingMapCoord")]
internal static class JigsawMapPing
{
    private static bool Prefix(Player player, MapCoord coord)
    {
        if (JigsawNativeSession.Current is not { Map: not null } session)
            return true;
        session.PingMapNode(player, coord);
        return false;
    }
}
