using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Relics;

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_ARCHITECT_MASK")]
public sealed class ArchitectMask : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsAllowed(IRunState runState) => false;
    public override bool IsAllowedInShops => false;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/architect_mask.png",
        IconOutlinePath: "res://AllMatters/images/relics/architect_mask.png",
        BigIconPath: "res://AllMatters/images/relics/architect_mask.png");
}
