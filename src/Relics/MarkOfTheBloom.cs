// SPDX-License-Identifier: GPL-3.0-only
// Adapted from Balance MarkOfTheBloom (GPLv3); upstream credits ActsFromThePast.
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Combat.Healing;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Relics;

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_MARK_OF_THE_BLOOM")]
public sealed class MarkOfTheBloom : ModRelicTemplate, IHealHookListener
{
    public override RelicRarity Rarity => RelicRarity.Event;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/mark_of_the_bloom.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/mark_of_the_bloom.png",
        BigIconPath: "res://AllMatters/images/relics/big/mark_of_the_bloom.png");

    public decimal ModifyHealMultiplicative(HealContext context, decimal amount)
    {
        if (context.Creature.Player != Owner) return 1m;
        if (amount > 0) Flash();
        return 0m;
    }
}
