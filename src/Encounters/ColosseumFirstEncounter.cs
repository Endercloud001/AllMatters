// SPDX-License-Identifier: GPL-3.0-only
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Encounters;

[RegisterGlobalEncounter]
public sealed class ColosseumFirstEncounter : ModEncounterTemplate
{
    public override RoomType RoomType => RoomType.Monster;
    public override bool IsValidForAct(ActModel act) => false;
    public override bool IsWeak => false;
    public override bool ShouldGiveRewards => false;
    public override EncounterAssetProfile AssetProfile => new(EncounterScenePath: "res://AllMatters/scenes/colosseum_first.tscn");
    public override IReadOnlyList<string> Slots => ["fog", "cultist"];
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<LivingFog>(), ModelDb.Monster<DampCultist>()];
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
    [
        (ModelDb.Monster<LivingFog>().ToMutable(), "fog"),
        (ModelDb.Monster<DampCultist>().ToMutable(), "cultist"),
    ];
}
