using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace AllMatters.Encounters;

// Identity follows monster content, independently of the current room's reward tier.
// The native secondary-enemy flag excludes ordinary summoned helpers.
internal static class EnemyIdentity
{
    internal static RoomType Kind(Creature creature)
    {
        if (!creature.IsPrimaryEnemy || creature.Monster == null) return RoomType.Monster;
        if (creature.Monster is AllMatters.Monsters.Sentry or AllMatters.Monsters.Lagavulin) return RoomType.Elite;
        var encounters = ModelDb.AllEncounters.Where(e => e.RoomType is RoomType.Elite or RoomType.Boss
            && e.AllPossibleMonsters.Any(m => m.Id == creature.Monster.Id));
        if (encounters.Any(e => e.RoomType == RoomType.Boss)) return RoomType.Boss;
        return encounters.Any() ? RoomType.Elite : RoomType.Monster;
    }
}
