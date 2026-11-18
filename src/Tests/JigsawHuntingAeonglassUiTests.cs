using AllMatters.Jigsaw;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Pooling;
using MegaCrit.Sts2.Core.Nodes.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.addons.mega_text;
using System.Text.Json;

using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
internal static class JigsawHuntingAeonglassUiTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Jigsaw Aeonglass native counter UI single owner eng", () => InLanguage("eng", 1)),
        ("Jigsaw Aeonglass native counter UI four combat roles overflow zhs", () => InLanguage("zhs", 4))
    ];
    private static void Check(bool value, string reason) => NativeEventTests.Check(value, reason);
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static Task Frames(int count) => HuntingLayoutNativeHost.Frames(count);
    private static string context = "Aeonglass native counter UI";
    private static readonly JsonSerializerOptions rngJson = new() { IncludeFields = true };

    private static async Task InLanguage(string language, int count)
    {
        using var cardPool = new CardPoolScope();
        string previous = LocManager.Instance.Language;
        try
        {
            LocManager.Instance.SetLanguage(language);
            await CounterUi(count);
        }
        finally
        {
            // Native card nodes may return themselves through deferred QueueFreeSafely.
            // Keep their pool alive through room disposal and locale notifications.
            await Frames(4);
            LocManager.Instance.SetLanguage(previous);
            await Frames(2);
        }
    }

    private static async Task CounterUi(int count)
    {
        // A safe opening in the same PCK/language supplies diagnostic comparison,
        // not an oracle that could bless hunted's original six-card description.
        using (var safe = new Battle(count, false))
        using (var host = new HuntingLayoutNativeHost())
        {
            await Mount(host, safe);
            var power = LocalPower(safe);
            var icon = Icon(power);
            Check(power.Amount == 6 && power.DisplayAmount == 6, "Safe native counter stays six.");
            await Observe(host, safe, icon, 6, "safe opening", false);
        }
        using var battle = new Battle(count, true);
        using var native = new HuntingLayoutNativeHost();
        await Mount(native, battle);
        var local = battle.Combat.Players[0];
        var counter = LocalPower(battle);
        var node = Icon(counter);
        var counters = battle.Combat.Enemies.Single().Powers.OfType<WitheringPresencePower>().ToArray();
        Check(counters.Length == count && counters.Count(power => power.IsVisible) == 1,
            "All role models exist; only the actual local target's counter is visible.");
        Check(Powers().Count(power => power.Model is WitheringPresencePower) == 1,
            "The native container creates exactly one local presence node.");
        await Checkpoint(12, "opening", true);
        if (count == 4)
        {
            await Play(battle, battle.Combat.Players[1]);
            Check(counters.Single(power => power.Target.Player == battle.Combat.Players[1]).DisplayAmount == 11,
                "An actual nonlocal role play decrements its own model.");
            await Checkpoint(12, "remote play", false);
        }
        var originalHand = new List<CardModel>();
        if (count == 4)
            for (int index = 0; index < 9; index++)
            {
                var card = battle.Combat.CreateCard<DefendIronclad>(local);
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, local);
                originalHand.Add(card);
            }
        for (int play = 1; play <= 13; play++)
        {
            await Play(battle, local);
            int expected = play < 12 ? 12 - play : play == 12 ? 12 : 11;
            await Frames(3);
            Check(node.GetNode<MegaLabel>("%AmountLabel").Text == expected.ToString(),
                $"Completed actual play {play} updates the subscribed native amount label to {expected}.");
            if (play is 1 or 11 or 12 or 13)
                await Checkpoint(expected, $"play {play}", play is 11 or 12);
            if (play == 12)
            {
                var hand = PileType.Hand.GetPile(local).Cards;
                var statuses = hand.Concat(PileType.Discard.GetPile(local).Cards)
                    .Concat(PileType.Draw.GetPile(local).Cards)
                    .Where(card => card is Wound or Burn or MegaCrit.Sts2.Core.Models.Cards.Void or Dazed or Wither).ToArray();
                Check(statuses.Length == 5 && statuses[0] is Wound && statuses[1] is Burn &&
                    statuses[2] is Wither && statuses[3] is Dazed && statuses[4] is MegaCrit.Sts2.Core.Models.Cards.Void,
                    "The actual twelfth play produces one of each status.");
                Check(statuses[4].Pile == PileType.Draw.GetPile(local) && statuses[3].Pile == PileType.Discard.GetPile(local),
                    "Void goes to the draw pile and Dazed goes to the discard pile.");
                Check(count != 1 ? hand.Count == 10 && originalHand.All(card => hand.Contains(card)) && statuses[0].Pile == PileType.Hand.GetPile(local) &&
                    statuses.Skip(1).Take(2).All(card => card.Pile == PileType.Discard.GetPile(local)) :
                    hand.Count == 3 && statuses.Take(3).All(card => card.Pile == PileType.Hand.GetPile(local)),
                    "Native insertion puts Wound, Burn and Wither in hand, with Burn and Wither spilling when only one slot is open.");
            }
        }
        async Task Checkpoint(int value, string phase, bool resize)
        {
            Check(ReferenceEquals(Icon(counter), node) && counter.Amount == 6 && counter.DisplayAmount == value,
                phase + ": the same icon consumes CardsLeft, without changing native Amount six.");
            await Observe(native, battle, node, value, phase, true);
            if (resize)
            {
                await ObserveAt(new Vector2I(1680, 720));
                await ObserveAt(new Vector2I(1280, 720));
            }
            async Task ObserveAt(Vector2I size)
            {
                string before = State(battle);
                native.Window.Size = size;
                await Frames(5);
                await Observe(native, battle, node, value, phase + " resize " + size, true);
                Check(before == State(battle), phase + ": resizing has no gameplay side effects.");
            }
        }
    }

    private static async Task Mount(HuntingLayoutNativeHost host, Battle battle)
    {
        await host.Mount(battle.Run, battle.Combat, async () =>
        {
            battle.SelectNativeBosses();
            await PreloadManager.LoadRoomCombatAssets(battle.Encounter, battle.Run);
        }, room => room.SetUpBackground(battle.Run));
        foreach (var creature in battle.Combat.Creatures.ToArray())
            await CombatManager.Instance.AfterCreatureAdded(creature);
        await HuntingLayoutNativeHost.Until(() => Powers().Any(power => power.Model is WitheringPresencePower) &&
            Powers().Where(power => power.Model is WitheringPresencePower).All(power => power.Modulate.A > 0.99f),
            "Aeonglass native power nodes finish their actual opening fade.");
        await Frames(5);
    }
    private static IEnumerable<NPower> Powers() => Descendants(HuntingLayoutNativeHost.CombatRoom!).OfType<NPower>();
    private static NPower Icon(WitheringPresencePower power) => Powers().Single(node => ReferenceEquals(node.Model, power));
    private static WitheringPresencePower LocalPower(Battle battle) => battle.Combat.Enemies.Single().Powers
        .OfType<WitheringPresencePower>().Single(power => power.Target.Player == battle.Combat.Players[0]);
    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static async Task Play(Battle battle, Player player)
    {
        var card = battle.Combat.CreateCard<DefendIronclad>(player);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Discard, player);
        await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), card, null, skipCardPileVisuals: true);
    }
    private static async Task Motion(Window window, Vector2 point)
    {
        window.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        await Frames(12);
    }
    private static async Task Observe(HuntingLayoutNativeHost host, Battle battle, NPower node, int value, string phase, bool hunted)
    {
        string before = State(battle);
        GD.Print($"AEONGLASS_UI source description={node.Model.Description.GetRawText()} smart={node.Model.SmartDescription.GetRawText()} remote={node.Model.RemoteDescription.GetRawText()}");
        var identities = battle.Combat.Enemies.Select(enemy => (enemy, enemy.Monster, enemy.Monster!.NextMove)).ToArray();
        // First record the still-hovered native contents after card hooks or resize.
        GD.Print($"AEONGLASS_UI phase={phase} language={LocManager.Instance.Language} hunted={hunted} size={host.Window.Size} roles={battle.Combat.Players.Count} local={LocalContext.NetId} held={TooltipText()} Amount={node.Model.Amount} CardsLeft={node.Model.DynamicVars["CardsLeft"].IntValue}");
        await Motion(host.Window, new Vector2(5, 5));
        await Motion(host.Window, node.GetGlobalRect().GetCenter());
        var hovered = host.Window.GuiGetHoveredControl();
        var originalIcon = node.GetNode<TextureRect>("%Icon");
        // Public's scene routes through its Pass icon; beta routes to the root.
        // Both must be this exact model instance and reach the native handler.
        Check(ReferenceEquals(hovered, node) || ReferenceEquals(hovered, originalIcon) &&
            originalIcon.MouseFilter == Control.MouseFilterEnum.Pass && ReferenceEquals(originalIcon.GetParent(), node),
            phase + ": real viewport motion reaches this NPower's exact native route; actual=" + hovered?.GetPath());
        await HuntingLayoutNativeHost.Until(() => Descendants(NGame.Instance!.HoverTipsContainer).OfType<NHoverTipSet>().Any(tip => tip.IsVisibleInTree()),
            phase + ": actual power mouse handler mounts a real hover tip set.");
        Check(node.GetNode<MegaLabel>("%AmountLabel").Text == value.ToString(), phase + ": visible label matches personal remaining cards.");
        var icon = node.GetNode<TextureRect>("%Icon");
        Check(icon.Texture != null && icon.Texture == node.Model.Icon && icon.IsVisibleInTree(), phase + ": actual original power texture is visible.");
        foreach (var control in new Control[] { node, icon, node.GetNode<MegaLabel>("%AmountLabel") }
            .Concat(Descendants(NGame.Instance!.HoverTipsContainer).OfType<RichTextLabel>().Where(label => label.IsVisibleInTree())))
            Check(control.IsVisibleInTree() && control.Modulate.A > 0.95f && host.Window.GetVisibleRect().Encloses(control.GetGlobalRect()),
                phase + ": hovered transformed native content is visible and inside the viewport: " + control.GetPath());
        string text = TooltipText();
        GD.Print($"AEONGLASS_UI phase={phase} rehover={text} label={value} texture={icon.Texture.ResourcePath}");
        Check(before == State(battle) && identities.All(item => battle.Combat.Enemies.Contains(item.enemy) &&
            ReferenceEquals(item.enemy.Monster, item.Monster) && ReferenceEquals(item.enemy.Monster!.NextMove, item.NextMove)),
            phase + ": pure hover preserves exact identities, moves, full RNG and ordered player piles.");
        if (hunted)
        {
            Check(System.Text.RegularExpressions.Regex.IsMatch(text, $@"(?<!\d){value}(?!\d)"), phase + ": actual parsed tooltip states the exact current personal remaining value, not a substring of twelve.");
            Check(System.Text.RegularExpressions.Regex.IsMatch(text, @"(?<!\d)12(?!\d)"), phase + ": actual tooltip explains the twelve-play rule independently of native Amount six.");
            foreach (var card in new CardModel[] { ModelDb.Card<Wound>(), ModelDb.Card<Burn>(), ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.Void>(), ModelDb.Card<Dazed>(), ModelDb.Card<Wither>() })
                Check(text.Contains(card.Title), phase + ": actual tooltip explains generated " + card.Id);
        }
    }
    private static string TooltipText() => string.Join(" | ", Descendants(NGame.Instance!.HoverTipsContainer)
        .OfType<RichTextLabel>().Where(label => label.IsVisibleInTree()).Select(label => label.GetParsedText()));
    private static string State(Battle battle) => JsonSerializer.Serialize(new
    {
        RunRng = battle.Run.Rng.ToSerializable(),
        Players = battle.Combat.Players.Select(player => new
        {
            Data = player.ToSerializable(), player.PlayerCombatState!.Energy, player.Creature.Block,
            Powers = player.Creature.Powers.Select(power => new { power.Id, power.Amount }).ToArray(),
            Piles = new[] { PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust, PileType.Play }
                .Select(pile => pile.GetPile(player).Cards.Select(card => card.ToSerializable()).ToArray()).ToArray()
        }).ToArray(),
        Enemies = battle.Combat.Enemies.Select(enemy => new
        {
            enemy.CurrentHp, enemy.MaxHp, enemy.Block, enemy.Monster!.NextMove.Id,
            Rng = MonsterRng(enemy.Monster),
            WitherUpgradeCount = ((Aeonglass)enemy.Monster).WitherUpgradeCount,
            Powers = enemy.Powers.Select(power => new { power.Id, power.Amount, power.DisplayAmount }).ToArray()
        }).ToArray()
    }, rngJson);

    private static string MonsterRng(MonsterModel monster) =>
#if STS2_PUBLIC
        $"{monster.Rng.Seed}/{monster.Rng.Counter}";
#else
        JsonSerializer.Serialize(monster.Rng.ToSerializable(), rngJson);
#endif

    private sealed class CardPoolScope : IDisposable
    {
        private readonly System.Collections.IDictionary pools = (System.Collections.IDictionary)AccessTools.Field(typeof(NodePool), "_pools").GetValue(null)!;
        private readonly object? previous;
        private readonly NodePool<NCard> pool;
        internal CardPoolScope()
        {
            previous = pools[typeof(NCard)];
            pools.Remove(typeof(NCard));
            pool = NodePool.Init<NCard>(NCard.AssetPaths.First(), 0);
        }
        public void Dispose()
        {
            try
            {
                var used = (IEnumerable<NCard>)AccessTools.Field(typeof(NodePool<NCard>), "_usedObjects").GetValue(pool)!;
                foreach (var node in used.Concat(pool.DebugFreeObjects).Distinct().ToArray())
                    if (GodotObject.IsInstanceValid(node)) node.Free();
            }
            finally
            {
                pools.Remove(typeof(NCard));
                if (previous != null) pools[typeof(NCard)] = previous;
            }
        }
    }

    private sealed class Battle : IDisposable
    {
        private readonly ulong? previousNetId = LocalContext.NetId;
        internal RunState Run { get; }
        internal CombatState Combat { get; }
        internal EncounterModel Encounter { get; }
        internal bool Hunted { get; }

        internal Battle(int count, bool hunted)
        {
            Hunted = hunted;
            var players = Enumerable.Range(1, count).Select(id => Player.CreateForNewRun<Ironclad>(UnlockState.all, (ulong)id)).ToArray();
            Run = RunState.CreateForTest([players[0]], acts: [ModelDb.Act<Overgrowth>(), ModelDb.Act<Hive>(), ModelDb.Act<Glory>()], seed: "HUNTING87_AEONGLASS_UI");
            Run.CurrentActIndex = 2;
            Encounter = ModelDb.Encounter<AeonglassBoss>().ToMutable();
            Combat = new CombatState(Encounter, Run) { RoundNumber = 1 };
            try
            {
                RunManager.Instance.SetUpTest(Run, new NetSingleplayerGameService());
                LocalContext.NetId = players[0].NetId;
                CombatManager.Instance.Reset(true);
                Combat.AddPlayer(players[0]); players[0].ResetCombatState();
                CombatManager.Instance.SetUpCombat(Combat);
                foreach (var player in players.Skip(1)) { player.RunState = Run; Combat.AddPlayer(player); player.ResetCombatState(); }
                Run.PushRoom(new CombatRoom(Combat));
                JigsawCheckpoint.Capture(Run, AcceptedRoot(players[0].NetId, hunted, JigsawRoom.Boss)); JigsawNativeSession.Restore(Run);
                var projection = JigsawEntryProjection.Append(Run.Map, 2, JigsawRoom.Boss);
                Run.Map = projection.Map;
                Check(Run.AddVisitedMapCoord(projection.Point.coord), context + ": native monster RNG has an actual visited map coordinate.");
                Encounter.GenerateMonstersWithSlots(Run);
                foreach (var (monster, slot) in Encounter.MonstersWithSlots)
                {
                    Combat.AddCreature(Combat.CreateCreature(monster, CombatSide.Enemy, slot));
                    CombatManager.Instance.AddCreature(monster.Creature);
                    monster.RollMove(Combat.PlayerCreatures);
                }
                Check(Run.Players.Count == 1 && Combat.Players.Count == count && Run.Act is Glory &&
                    JigsawHuntingNative.RootIsHunted(players[0]) == hunted,
                    context + ": one legal run owner binds all actual combat roles to the accepted top-level Glory Boss root; these are not remote clients.");
            }
            catch { Dispose(); throw; }
        }

        internal void SelectNativeBosses()
        {
            var act = Run.Act;
            act.SetBossEncounter(ModelDb.Encounter<AeonglassBoss>());
            act.SetSecondBossEncounter(null);
            Check(act.PullNextEncounter(RoomType.Boss) is AeonglassBoss && !act.HasSecondBoss &&
                act.ToSave().SerializableRooms.BossEncountersVisited == 0,
                context + ": the native Glory act selects the displayed Aeonglass at its actual first Boss room.");
        }

        public void Dispose()
        {
            JigsawNativeSession.TryGet(Run)?.Close(); CombatManager.Instance.Reset(true);
            RunManager.Instance.CleanUp(false); LocalContext.NetId = previousNetId;
        }
    }

    private static JigsawRun AcceptedRoot(ulong owner, bool hunted, JigsawRoom roomType)
    {
        var map = JigsawRun.Create(75, players: [owner]);
        Ok(map.ChooseEnd(owner, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        Ok(map.CompleteAncient(owner));
        Ok(map.Continue(owner));
        var previous = new Dictionary<int, int> { [0] = -1 };
        var pending = new Queue<int>();
        pending.Enqueue(0);
        while (pending.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (map.Nodes.Single(node => node.Id == next).Room != JigsawRoom.SecondBoss &&
                    previous.TryAdd(next, current)) pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == roomType && previous.ContainsKey(node.Id)).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        Ok(map.SetRemainingSteps(hunted ? 0 : 100));
        foreach (int node in path)
        {
            if (!hunted && node == target) Ok(map.SetRemainingSteps(1));
            var entry = map.BeginEnter(node)!;
            Ok(map.ConfirmEnter(entry.Id));
            if (node != target) Ok(map.CompleteRoom(entry.Id));
        }
        Check(map.CurrentNode == target && map.CurrentRoomHunted == hunted, "Real room-entry transitions establish the requested root.");
        return map;
    }
}
