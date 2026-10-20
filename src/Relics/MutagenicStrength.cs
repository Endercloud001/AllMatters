// SPDX-License-Identifier: GPL-3.0-only
// Adapted from Balance MutagenicStrength (GPLv3).
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using AllMatters.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Relics;

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_MUTAGENIC_STRENGTH")]
public sealed class MutagenicStrength : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<StrengthPower>(3), new PowerVar<MutagenicDecayPower>(3)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.FromPower<MutagenicDecayPower>()];
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/mutagenic_strength.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/mutagenic_strength.png",
        BigIconPath: "res://AllMatters/images/relics/big/mutagenic_strength.png");

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom) return;
        Flash();
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, null);
        await PowerCmd.Apply<MutagenicDecayPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, DynamicVars["MutagenicDecayPower"].BaseValue, Owner.Creature, null);
    }
}
