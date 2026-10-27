using AllMatters.Encounters;
using AllMatters.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.PotionPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Potions;

[RegisterPotion(typeof(EventPotionPool), FullPublicEntry = "ALL_MATTERS_AMBROSIA")]
public sealed class Ambrosia : ModPotionTemplate
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;
    public override PotionAssetProfile AssetProfile => new(
        ImagePath: "res://AllMatters/images/potions/ambrosia.png",
        OutlinePath: "res://AllMatters/images/potions/ambrosia_outline.png");
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<AmbrosiaPower>()];
    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PersonalCombatRewards.Forfeit(Owner);
        if (!Owner.Creature.HasPower<AmbrosiaPower>())
            await PowerCmd.Apply<AmbrosiaPower>(choiceContext, Owner.Creature, 1, Owner.Creature, null);
    }
}
