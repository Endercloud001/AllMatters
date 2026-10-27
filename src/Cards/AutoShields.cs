using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using AllMatters.Powers;

namespace AllMatters.Cards;

[RegisterCard(typeof(EventCardPool), FullPublicEntry = "ALL_MATTERS_AUTO_SHIELDS")]
public sealed class AutoShields : ModCardTemplate
{
    public AutoShields() : base(1, CardType.Skill, CardRarity.Event, TargetType.Self) { }
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<DefectCardPool>();
    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://AllMatters/images/card_portraits/auto_shields.png");
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(11, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.Block == 0)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);
}
