using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Encounters;

[RegisterGlobalEncounter]
public sealed class HolesEncounter : ModEncounterTemplate
{
    public override bool IsValidForAct(ActModel act) => false;
    public override RoomType RoomType => RoomType.Monster;
    public override EncounterAssetProfile AssetProfile => new(EncounterScenePath: "res://AllMatters/scenes/holes.tscn");
    public override IReadOnlyList<string> Slots => ["transient"];
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<Monsters.Transient>()];
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
        => [(ModelDb.Monster<Monsters.Transient>().ToMutable(), "transient")];
}
