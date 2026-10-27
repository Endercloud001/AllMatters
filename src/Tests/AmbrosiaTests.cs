using AllMatters.Encounters;
using AllMatters.Potions;
using AllMatters.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

internal static class AmbrosiaTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Ambrosia wellspring bottles one potion or leaves and retains Bathe", Event),
        ("Ambrosia actual damage is single triple powered attacks and expires on own turn", Damage),
        ("Ambrosia forfeits all personal fixed combat loot but retains allies healing and next combat", Rewards),
    ];

    private static async Task Event()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player();
        var e = await NativeEventTests.Begin<Wellspring>(p);
        NativeEventTests.Check(e.CurrentOptions.Count == 3 && e.CurrentOptions[1].TextKey.EndsWith(".BATHE"), "Bathe remains and Leave is added.");
        await e.CurrentOptions[0].Chosen();
        NativeEventTests.Check(e.IsFinished && p.Potions.OfType<Ambrosia>().Count() == 1, "Bottle actually gives one Ambrosia.");
        var leave = await NativeEventTests.Begin<Wellspring>(p);
        await leave.CurrentOptions[2].Chosen();
        NativeEventTests.Check(leave.IsFinished && p.Potions.Count() == 1, "Leave finishes without another potion.");
    }

    private static async Task Drink(Player p)
    {
        await PotionCmd.TryToProcure<Ambrosia>(p);
        await p.Potions.OfType<Ambrosia>().Last().OnUseWrapper(new ThrowingPlayerChoiceContext(), p.Creature);
    }

    private static async Task Damage()
    {
        var (p, combat) = NativeEventTests.Combat();
        var enemy = combat.CreateCreature(ModelDb.Monster<AllMatters.Monsters.Lagavulin>().ToMutable(), CombatSide.Enemy, "ambrosia_target");
        combat.AddCreature(enemy); enemy.SetMaxHpInternal(999); enemy.SetCurrentHpInternal(999);
        var context = new ThrowingPlayerChoiceContext();
        await Drink(p); await Drink(p);
        int hp = enemy.CurrentHp;
        await CreatureCmd.Damage(context, enemy, 10, ValueProp.Move, p.Creature);
        NativeEventTests.Check(enemy.CurrentHp == hp - 30 && p.Creature.GetPower<AmbrosiaPower>()!.Amount == 1, "Two drinks still multiply by3 exactly once.");
        foreach (var props in new[] { ValueProp.Unpowered, ValueProp.Move | ValueProp.Unpowered })
        {
            hp = enemy.CurrentHp;
            await CreatureCmd.Damage(context, enemy, 10, props, p.Creature);
            NativeEventTests.Check(enemy.CurrentHp == hp - 10, "Unpowered orb poison and relic damage bypass multiplier.");
        }
        await PowerCmd.Apply<VulnerablePower>(context, enemy, 2, p.Creature, null);
        hp = enemy.CurrentHp;
        await CreatureCmd.Damage(context, enemy, 10, ValueProp.Move, p.Creature);
        NativeEventTests.Check(enemy.CurrentHp == hp - 45, "Native Vulnerable still stacks multiplicatively.");
        var power = p.Creature.GetPower<AmbrosiaPower>()!;
        await power.AfterSideTurnEnd(context, CombatSide.Enemy, [enemy]);
        NativeEventTests.Check(p.Creature.HasPower<AmbrosiaPower>(), "Enemy turn does not clear owner's power.");
        await power.AfterSideTurnEnd(context, CombatSide.Player, [p.Creature]);
        hp = enemy.CurrentHp;
        await CreatureCmd.Damage(context, enemy, 10, ValueProp.Move, p.Creature);
        NativeEventTests.Check(enemy.CurrentHp == hp - 15 && !PersonalCombatRewards.IsEligible(p), "Turn end clears multiplier but retains combat forfeiture.");
    }

    private static async Task Rewards()
    {
        var p = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var ally = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([p, ally], acts: [], seed: "AMBROSIA_REWARDS");
        RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
        var room = new CombatRoom(ModelDb.Encounter<AllMatters.Encounters.RedMaskBandits>().ToMutable(), run);
        room.CombatState.AddPlayer(p); room.CombatState.AddPlayer(ally); p.ResetCombatState(); ally.ResetCombatState();
        // The existing playable event's fixed gold and guaranteed Red Mask.
        room.AddExtraReward(p, new GoldReward(25, 35, p));
        room.AddExtraReward(p, new RelicReward(ModelDb.Relic<RedMask>().ToMutable(), p));
        room.AddExtraReward(ally, new GoldReward(25, 35, ally));
        room.AddExtraReward(ally, new RelicReward(ModelDb.Relic<RedMask>().ToMutable(), ally));
        await Drink(p);
        var rewards = new RewardsSet(p).WithRewardsFromRoom(room);
        // A guaranteed potion added after room generation must also be filtered.
        rewards.WithCustomRewards([new PotionReward(ModelDb.Potion<Ambrosia>().ToMutable(), p)]);
        await rewards.GenerateWithoutOffering();
        NativeEventTests.Check(rewards.Rewards.Count == 0, "No personal gold card potion or guaranteed relic survives reward hooks.");
        var teamRewards = new RewardsSet(ally).WithRewardsFromRoom(room);
        NativeEventTests.Check(teamRewards.Rewards.OfType<GoldReward>().Any() && teamRewards.Rewards.OfType<RelicReward>().Any() && teamRewards.Rewards.OfType<CardReward>().Any(), "Ally keeps normal and guaranteed loot.");
        p.Creature.SetCurrentHpInternal(10); await CreatureCmd.Heal(p.Creature, 5);
        NativeEventTests.Check(p.Creature.CurrentHp == 15, "Healing remains normal.");
        var story = new RewardsSet(p).WithCustomRewards([new GoldReward(7, 7, p)]);
        await story.GenerateWithoutOffering();
        NativeEventTests.Check(story.Rewards.Count == 1, "Out of combat story reward remains eligible.");
        var restoredRun = RunState.FromSerializable(RunManager.Instance.ToSave(null));
        var restored = restoredRun.Players.Single(player => player.NetId == p.NetId);
        NativeEventTests.Check(!PersonalCombatRewards.IsEligible(restored), "Forfeiture survives serialization.");
        p.ResetCombatState();
        NativeEventTests.Check(PersonalCombatRewards.IsEligible(p) && new RewardsSet(p).WithRewardsFromRoom(room).Rewards.OfType<RelicReward>().Any(), "Next combat restores personal loot.");
    }
}
