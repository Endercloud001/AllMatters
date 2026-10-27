// SPDX-License-Identifier: GPL-3.0-only
using AllMatters.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Encounters;

[RegisterGlobalEncounter]
public sealed class JungleMazeEncounter : ModEncounterTemplate
{
    public override bool IsValidForAct(ActModel act) => false;
    public override RoomType RoomType => RoomType.Elite;
    public override EncounterAssetProfile AssetProfile => new(EncounterScenePath: "res://AllMatters/scenes/jungle_maze_sentries.tscn");
    public override IReadOnlyList<string> Slots => ["sentry_left", "sentry_right"];
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<Sentry>()];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
    {
        var left = (Sentry)ModelDb.Monster<Sentry>().ToMutable();
        var right = (Sentry)ModelDb.Monster<Sentry>().ToMutable();
        left.BoltFirst = true;
        right.BoltFirst = false;
        return [(left, "sentry_left"), (right, "sentry_right")];
    }
}
