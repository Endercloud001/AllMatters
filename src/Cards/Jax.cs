// SPDX-License-Identifier: GPL-3.0-only
// Adapted from Balance Jax (GPLv3); default Innate added.
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Interop.AutoRegistration;

namespace AllMatters.Cards;

/// <summary>
/// STS1-CARD-01 — J.A.X.：失去生命并获得力量。
/// 改编自 Balance 的 Jax；默认固有，其余效果沿用。
/// </summary>
[RegisterCard(typeof(EventCardPool), FullPublicEntry = "ALL_MATTERS_JAX")]
public sealed class Jax : ModCardTemplate
{
    public Jax() : base(0, CardType.Skill, CardRarity.Event, TargetType.Self)
    {
    }

    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://AllMatters/images/card_portraits/jax.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HpLossVar(3M),
        new PowerVar<StrengthPower>(2M),
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        VfxCmd.PlayOnCreatureCenter(Owner.Creature, "vfx/vfx_bloody_impact");

        // Match each target's native Bloodletting damage-source API.
#if STS2_PUBLIC
        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, this);
#else
        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, this, cardPlay);
#endif

        await PowerCmd.Apply<StrengthPower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            DynamicVars.Strength.BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade() => DynamicVars.Strength.UpgradeValueBy(1M);
}
