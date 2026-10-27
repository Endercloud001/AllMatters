using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
namespace AllMatters.Cards;

// The two supported targets attach CardPlay at different points of attack construction.
internal static class ReturningCardAttack
{
    internal static async Task Single(CardModel card, CardPlay play, PlayerChoiceContext context)
    {
#if STS2_PUBLIC
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card).Targeting(play.Target!).Execute(context);
#else
        await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card, play).Targeting(play.Target!).Execute(context);
#endif
    }
}
