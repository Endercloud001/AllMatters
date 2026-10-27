using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using AllMatters.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Unlocks;

internal static class NativeNlothTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Native Nloth plain choice uses act-scaled HP and an integer gold roll before multiplication", Plain),
        ("Native Nloth face suppresses only its owner in native chest generation and voting then stays spent", Face),
        ("Native Nloth gift triples base rarity before independent offset and leaves shops alone", Gift),
    ];
    private static async Task Plain()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (int act in new[] { 1, 2, 3 })
        {
            var p = NativeEventTests.Player();
            ((RunState)p.RunState).CurrentActIndex = act - 1;
            int hp = p.Creature.CurrentHp, gold = p.Gold;
            var e = await NativeEventTests.Begin<ThisOrThat>(p);
            await e.CurrentOptions.Single(o => o.TextKey.EndsWith(".PLAIN")).Chosen();
            NativeEventTests.Check(p.Creature.CurrentHp == hp - 6 * (4 - act), "Life cost must depend on current act.");
            int gained = p.Gold - gold;
            NativeEventTests.Check(gained % act == 0 && gained / act is >= 41 and <= 68, "Roll41–68 first, then multiply by act.");
        }
    }

    private static async Task Face()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        var run = RunState.CreateForTest([p, other], acts: [], seed: "NLOTH_CHEST");
        RunManager.Instance.SetUpTest(run, new NetSingleplayerGameService());
        try
        {
            var face = await RelicCmd.Obtain<NlothsHungryFace>(p);
            var room = new TreasureRoom(0); run.PushRoom(room);
            await Hook.AfterRoomEntered(run, room);
            NativeEventTests.Check(face.IsUsedUp && !Hook.ShouldGenerateTreasure(run, p) && Hook.ShouldGenerateTreasure(run, other), "The next chest is empty only for the holder.");
            var sync = new TreasureRoomRelicSynchronizer(run, 2, RunManager.Instance.ActionQueueSynchronizer, run.SharedRelicGrabBag, run.Rng.Niche);
            List<RelicPickingResult>? awards = null;
            sync.RelicsAwarded += results => awards = results;
            sync.BeginRelicPicking();
            NativeEventTests.Check(sync.CurrentRelics!.Count == 1 && sync.GetPlayerVote(p).voteReceived && !sync.GetPlayerVote(p).index.HasValue,
                "Generate one teammate relic and automatically skip the holder's native vote.");
            sync.OnPicked(p, 0);
            sync.OnPicked(other, 0);
            NativeEventTests.Check(awards != null && awards.All(a => a.player != p) && awards.Any(a => a.player == other), "A holder cannot take a teammate's relic or receive consolation.");
            var restored = Player.FromSerializable(p.ToSerializable());
            NativeEventTests.Check(restored.Relics.OfType<NlothsHungryFace>().Single().Spent, "The used state must survive native player restoration.");
            run.PopCurrentRoom();
            var next = new TreasureRoom(1); run.PushRoom(next);
            await Hook.AfterRoomEntered(run, next);
            NativeEventTests.Check(Hook.ShouldGenerateTreasure(run, p), "The following chest works normally.");
        }
        finally { RunManager.Instance.CleanUp(false); }
    }

    private static async Task Gift()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var p = NativeEventTests.Player("NLOTH_ODDS");
        var gift = await RelicCmd.Obtain<NlothsGift>(p);
        foreach (var (type, offset, rare, uncommon) in new[]
        {
            (CardRarityOddsType.RegularEncounter, 0f, .09f, .37f),
            (CardRarityOddsType.RegularEncounter, .2f, .29f, .37f),
            (CardRarityOddsType.EliteEncounter, .1f, .4f, .4f),
            (CardRarityOddsType.Shop, 0f, .09f, .37f),
        })
        for (int i = 0; i < 20; i++)
        {
            var expectedRng = PlayerRngSet.FromSerializable(p.PlayerRng.ToSerializable()).Rewards;
            float roll = expectedRng.NextFloat();
            var actual = p.PlayerOdds.CardRarity.RollWithoutChangingFutureOdds(type, offset);
            var expected = roll < rare ? CardRarity.Rare : roll < rare + uncommon ? CardRarity.Uncommon : CardRarity.Common;
            NativeEventTests.Check(actual == expected, "Rare threshold changes at the confirmed stage without multiplying the independent pity offset.");
            NativeEventTests.Check(p.PlayerRng.Rewards.NextInt() == expectedRng.NextInt(), "Rarity adjustment must consume one original random roll.");
        }
        await RelicCmd.Remove(gift);
        var clone = PlayerRngSet.FromSerializable(p.PlayerRng.ToSerializable()).Rewards;
        float nextRoll = clone.NextFloat();
        var normal = p.PlayerOdds.CardRarity.RollWithoutChangingFutureOdds(CardRarityOddsType.RegularEncounter, 0);
        NativeEventTests.Check((normal == CardRarity.Rare) == (nextRoll < .03f), "Removing the gift restores normal odds.");
    }
}
