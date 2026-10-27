using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Encounters;

[RegisterGlobalEncounter]
public sealed class WhisperingHollowEncounter : ModEncounterTemplate
{
    public override bool IsValidForAct(ActModel act) => false;
    public override RoomType RoomType => RoomType.Monster;
    // Reward tier is independent of monster identity; retain native elite poverty scaling.
    public override int MinGoldReward => ModelDb.Encounter<JungleMazeEncounter>().MinGoldReward;
    public override int MaxGoldReward => ModelDb.Encounter<JungleMazeEncounter>().MaxGoldReward;
    public override EncounterAssetProfile AssetProfile => new(EncounterScenePath: "res://AllMatters/scenes/whispering_hollow.tscn");
    public override IReadOnlyList<string> Slots => ["mass"];
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<Monsters.WrithingMass>()];
    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters()
        => [(ModelDb.Monster<Monsters.WrithingMass>().ToMutable(), "mass")];
}
