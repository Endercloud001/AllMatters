// SPDX-License-Identifier: GPL-3.0-only
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;

namespace AllMatters.Relics;

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_BOTTLED_TORNADO")]
public sealed class BottledTornado : BottledCard
{
    protected override CardType BoundType => CardType.Power;
    protected override string IconName => "bottled_tornado";
}
