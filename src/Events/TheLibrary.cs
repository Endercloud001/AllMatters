// SPDX-License-Identifier: GPL-3.0-only
// Adapted from Balance TheLibrary (GPLv3); restricted cross-character reading and bottles.
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using AllMatters.Relics;

namespace AllMatters.Events;

[RegisterSharedEvent]
public sealed class TheLibrary : ModEventTemplate
{
    private bool _choosing;
    public override bool IsShared => false;
    public override bool IsAllowed(IRunState runState) => runState.CurrentActIndex == 1;
    public override EventAssetProfile AssetProfile => new(InitialPortraitPath: "res://AllMatters/images/events/the_library.png");
    protected override IEnumerable<DynamicVar> CanonicalVars => [new HealVar(0)];
    public override void CalculateVars() => DynamicVars.Heal.BaseValue = Math.Floor((Owner?.Creature.MaxHp ?? 0) * .33m);

    private List<CardModel> ReadingPool()
    {
        CharacterModel[] characters = [ModelDb.Character<Ironclad>(), ModelDb.Character<Silent>(),
            ModelDb.Character<Regent>(), ModelDb.Character<Necrobinder>(), ModelDb.Character<Defect>()];
        return characters.Where(character => character.Id != Owner!.Character.Id)
            .SelectMany(character => character.CardPool.GetUnlockedCards(Owner!.UnlockState, Owner.RunState.CardMultiplayerConstraint))
            .Where(card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .DistinctBy(card => card.Id).ToList();
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        bool canRead = ReadingPool().Count > 0;
        bool canClean = AvailableBottles().Count > 0;
        return
        [
            new(this, canRead ? Read : null, InitialOptionKey(canRead ? "READ" : "READ_LOCKED"), []),
            new(this, Sleep, InitialOptionKey("SLEEP"), []),
            new(this, canClean ? Clean : null, InitialOptionKey(canClean ? "CLEAN" : "CLEAN_LOCKED"), []),
        ];
    }

    private List<BottledCard> AvailableBottles() =>
        new BottledCard[] { ModelDb.Relic<BottledFlame>(), ModelDb.Relic<BottledLightning>(), ModelDb.Relic<BottledTornado>() }
            .Where(bottle => bottle.IsAvailable(Owner!)).ToList();

    private async Task Clean()
    {
        if (!StartChoice()) return;
        var bottles = AvailableBottles();
        await RelicCmd.Obtain(bottles[Rng.NextInt(bottles.Count)].ToMutable(), Owner!);
        SetEventFinished(PageDescription("CLEAN"));
    }

    private bool StartChoice()
    {
        if (IsFinished || _choosing) return false;
        _choosing = true;
        return true;
    }

    private async Task Read()
    {
        if (!StartChoice()) return;
        var owner = Owner!;
        var pool = ReadingPool();
        var choices = new List<CardCreationResult>();
        while (choices.Count < 20 && pool.Count > 0)
        {
            int index = owner.PlayerRng.Rewards.NextInt(pool.Count);
            // Balance's ForNonCombatWithDefaultOdds sets NoUpgradeRoll in both targets.
            // Keep that generation state, but sample uniformly per card as the scope requires.
            choices.Add(new(owner.RunState.CreateCard(pool[index], owner)));
            pool.RemoveAt(index);
        }
        var prefs = new CardSelectorPrefs(L10NLookup($"{Id.Entry}.pages.READ.selectionScreenPrompt"), 1) { Cancelable = false };
        var selected = (await CardSelectCmd.FromSimpleGridForRewards(new BlockingPlayerChoiceContext(), choices, owner, prefs)).Single();
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(selected, PileType.Deck));
        SetEventFinished(L10NLookup($"{Id.Entry}.pages.READ.description_{Rng.NextInt(3) + 1}"));
    }

    private async Task Sleep()
    {
        if (!StartChoice()) return;
        await CreatureCmd.Heal(Owner!.Creature, DynamicVars.Heal.BaseValue);
        SetEventFinished(PageDescription("SLEEP"));
    }
}
