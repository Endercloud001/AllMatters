using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Relics;

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_DEAD_BRANCH")]
public sealed class DeadBranch : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/dead_branch.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/dead_branch.png",
        BigIconPath: "res://AllMatters/images/relics/big/dead_branch.png");

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner != Owner) return;
        IEnumerable<CardPoolModel> pools = Owner.GetRelic<PrismaticGem>() != null
            ? Owner.UnlockState.CharacterCardPools.Append(ModelDb.CardPool<ColorlessCardPool>()).Distinct()
            : [Owner.Character.CardPool];
        var candidates = pools.SelectMany(pool => pool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint));
        var generated = CardFactory.GetForCombat(Owner, candidates, 1, Owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
        if (generated == null) return;
        if (causedByEthereal) generated.GiveSingleTurnRetain();
        Flash();
        await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, Owner);
    }
}
