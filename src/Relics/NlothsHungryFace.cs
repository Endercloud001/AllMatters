using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Utils;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Relics;

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_NLOTHS_HUNGRY_FACE")]
public sealed class NlothsHungryFace : ModRelicTemplate
{
    private static readonly SavedAttachedState<RelicModel, bool> SpentState = new("AllMattersNlothSpent");
    private TreasureRoom? _emptyRoom;
    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsUsedUp => Spent;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/nloths_hungry_face.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/nloths_hungry_face.png",
        BigIconPath: "res://AllMatters/images/relics/nloths_hungry_face.png");

    public bool Spent
    {
        get => SpentState.GetValueOrDefault(this);
        set { AssertMutable(); SpentState[this] = value; if (value) Status = RelicStatus.Disabled; }
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (Spent) Status = RelicStatus.Disabled;
        if (!Spent && room is TreasureRoom treasure)
        {
            _emptyRoom = treasure;
            Spent = true;
            Flash();
        }
        return Task.CompletedTask;
    }

    public bool Blocks(Player player) => player == Owner && _emptyRoom != null && ReferenceEquals(player.RunState.CurrentRoom, _emptyRoom);
    public override bool ShouldGenerateTreasure(Player player) => !Blocks(player);
    internal static bool IsEmptyFor(Player player) => player.Relics.OfType<NlothsHungryFace>().Any(face => face.Blocks(player));
}
