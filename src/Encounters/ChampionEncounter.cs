using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Encounters;

[RegisterGlobalEncounter]
public sealed class ChampionEncounter : ModEncounterTemplate
{
    public override bool IsValidForAct(ActModel act) => false;
    public override RoomType RoomType => RoomType.Boss;
    public override int MinGoldReward => ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.DecimillipedeElite>().MinGoldReward;
    public override int MaxGoldReward => ModelDb.Encounter<MegaCrit.Sts2.Core.Models.Encounters.DecimillipedeElite>().MaxGoldReward;
    public override EncounterAssetProfile AssetProfile => new(EncounterScenePath: "res://AllMatters/scenes/champion.tscn");
    public override IReadOnlyList<string> Slots => ["champion"];
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<Monsters.Champion>()];
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
        => [(ModelDb.Monster<Monsters.Champion>().ToMutable(), "champion")];
}
