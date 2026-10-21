// SPDX-License-Identifier: GPL-3.0-only
using AllMatters.Monsters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Encounters;

[RegisterGlobalEncounter]
public sealed class MindBloomEncounter : ModEncounterTemplate
{
    public override RoomType RoomType => RoomType.Monster;
    public override bool IsValidForAct(ActModel act) => false;
    public override bool IsWeak => false;
    public override EncounterAssetProfile AssetProfile => new(EncounterScenePath: "res://AllMatters/scenes/mind_bloom.tscn");
    public override IReadOnlyList<string> Slots => ["lagavulin_left", "lagavulin_right"];
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<Lagavulin>()];
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<Lagavulin>().ToMutable(), "lagavulin_left"),
        (ModelDb.Monster<Lagavulin>().ToMutable(), "lagavulin_right"),
    ];
}
