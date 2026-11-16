using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using AllMatters.Jigsaw;
using AllMatters.Jigsaw.Presentation;
using AllMatters.Relics;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Lobby;
using MegaCrit.Sts2.Core.Multiplayer.Transport.ENet;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Saves.Validation;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using STS2RitsuLib.Networking.ManagedActions;

internal static class JigsawHuntingRestRelicTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        (PunishedName, Punished),
        (SafeName, SafeDig)
    ];

    private static readonly JsonSerializerOptions CompleteJson = new() { IncludeFields = true };
    private const string PunishedName = "Jigsaw hunting real rest relics reject native Dig and Smith while Tent preserves actual Heal then targeted Mend and departure";
    private const string SafeName = "Jigsaw hunting real rest relics keep safe native Shovel Dig and its original relic delivery";
    private static INetGameService? network;
    private static string? directory;
    private static Process? childProcess;
    private static bool IsChild => System.Environment.GetEnvironmentVariable("JIGSAW_REST_RELIC_ROLE") == "2";
    private static string Role => IsChild ? "2" : "host";
    private static bool SkipHost() => false;
    private static bool SeenFtue(ref bool __result) { __result = true; return false; }
    private static void VisualFactoryPrefix(out bool __state) { __state = TestMode.IsOn; TestMode.IsOn = false; }
    private static Exception? VisualFactoryFinalizer(Exception? __exception, bool __state)
    {
        TestMode.IsOn = __state;
        return __exception;
    }

    private static async Task Punished()
    {
        await WithRest(true, async session =>
        {
            if (IsChild) { await PeerPunished(session); return; }
            var run = session.Native;
            var owner = run.Players[0];
            var target = run.Players[1];
            var sync = RunManager.Instance.RestSiteSynchronizer;
            var options = sync.GetLocalOptions();
            CheckGenerated(run);
            Check(run.Players.All(player => player.Relics.OfType<ArchitectMask>().Count() == 1),
                "Both legal run roles receive exactly one hunting mask after actual rest-root entry.");
            var before = Effects(run);
            foreach (var forbidden in new[] { typeof(DigRestSiteOption), typeof(SmithRestSiteOption) })
            {
                int index = options.ToList().FindIndex(option => option.GetType() == forbidden);
                Check(!await sync.ChooseLocalOption(index), "The actual local sender and receiver reject native " + forbidden.Name + ".");
                Equal(Effects(run), before, "Rejected native options preserve complete players, decks, relic bags, RNG, history, root and chosen/hovered options.");
            }

            using var ui = new RestUi();
            await ui.Mount(session);
            foreach (var option in options.Where(option => option is DigRestSiteOption or SmithRestSiteOption))
            {
                var button = ui.Room.GetButtonForOption(option)!;
                Check((bool)AccessTools.Field(typeof(NRestSiteButton), "_isUnclickable").GetValue(button)!,
                    "The genuine relic-added/native forbidden button has its actual disabled execution state.");
            }

            int ownerHp = owner.Creature.CurrentHp;
            int targetHp = target.Creature.CurrentHp;
            int ownerHeal = (int)HealRestSiteOption.GetHealAmount(owner);
            var originalMend = options.OfType<MendRestSiteOption>().Single();
            await ui.Click(ui.Room.GetButtonForOption(options.OfType<HealRestSiteOption>().Single())!);
            Console.WriteLine($"REST_RELICS_HEAL_CLICK role={Role} hp={owner.Creature.CurrentHp} history={string.Join(',', Choices(owner))}");
            await Until(() => Choices(owner).SequenceEqual(new[] { "HEAL" }) &&
                ui.Room.GetButtonForOption(originalMend) is { IsEnabled: true }, "The original Heal button finishes its real post-selection flow and restores remaining buttons.");
            Check(owner.Creature.CurrentHp == Math.Min(owner.Creature.MaxHp, ownerHp + ownerHeal) && target.Creature.CurrentHp == targetHp &&
                !options.Any(option => option is HealRestSiteOption) && options.Count == 3 && options.Contains(originalMend) &&
                options.Count(option => option is DigRestSiteOption or SmithRestSiteOption) == 2 && JigsawHuntingServices.RestChoicesRemain(owner),
                "The actual owner-only Miniature Tent removes only the used Heal and retains the original Mend, Smith and Shovel Dig instances.");

            Signal("host-healed");
            var otherOptions = sync.GetOptionsForPlayer(target);
            int otherHeal = (int)HealRestSiteOption.GetHealAmount(target);
            await Until(() => Signaled("2", "other-healed") && Choices(target).SequenceEqual(new[] { "HEAL" }),
                "The actual child endpoint chooses its local original Heal and the host receives that native option message.");
            Check(target.Creature.CurrentHp == Math.Min(target.Creature.MaxHp, targetHp + otherHeal) && otherOptions.Count == 0 &&
                Choices(target).SequenceEqual(new[] { "HEAL" }),
                "The role without Tent clears its original remaining choices after one Heal; it never borrows the owner's extra operation.");

            var choices = new List<(ulong Owner, uint Id, ulong? Target)>();
            Action<Player, uint, NetPlayerChoiceResult> observed = (player, id, result) =>
                choices.Add((player.NetId, id, PlayerChoiceResult.FromNetData(player, run, result).AsPlayerId()));
            var choiceSync = RunManager.Instance.PlayerChoiceSynchronizer;
            choiceSync.PlayerChoiceReceived += observed;
            try
            {
                int beforeMendOwnerHp = owner.Creature.CurrentHp;
                int beforeMendTargetHp = target.Creature.CurrentHp;
                int mendHeal = (int)MendRestSiteOption.GetHealAmount(target);
                uint nextChoice = choiceSync.ChoiceIds.Count == 0 ? 0 : choiceSync.ChoiceIds[0];
                await ui.Click(ui.Room.GetButtonForOption(originalMend)!);
                await Until(() => ui.Target.IsInSelection, "The actual Mend button enters the native local target-selection branch.");
                Check(!ui.Target.AllowedToTargetNode(ui.Room.GetCharacterForPlayer(owner)!) &&
                    ui.Target.AllowedToTargetNode(ui.Room.GetCharacterForPlayer(target)!) && choices.Count == 0 &&
                    Choices(owner).SequenceEqual(new[] { "HEAL" }),
                    "Mend excludes its own owner and waits for a real target without prematurely syncing or writing history.");
                var character = ui.Room.GetCharacterForPlayer(target)!;
                await Motion(Vector2.Zero);
                await Motion(character.Hitbox.GetGlobalTransform() * (character.Hitbox.Size * 0.5f));
                Check(ReferenceEquals(AccessTools.Property(typeof(NTargetManager), "HoveredNode").GetValue(ui.Target), character),
                    "Godot mouse motion hits the actual other-role rest character and its native target hover handler.");
                await ui.Click(character.Hitbox);
                await Until(() => !ui.Target.IsInSelection && Choices(owner).SequenceEqual(new[] { "HEAL", "MEND" }) &&
                    ui.Room.GetButtonForOption(originalMend) == null && ui.Room.ProceedButton.IsEnabled,
                    "The actual target release completes Mend, refreshes the original remaining options and enables native Proceed.");
                Check(choices.SequenceEqual(new[] { (owner.NetId, nextChoice, (ulong?)target.NetId) }) &&
                    owner.Creature.CurrentHp == beforeMendOwnerHp &&
                    target.Creature.CurrentHp == Math.Min(target.Creature.MaxHp, beforeMendTargetHp + mendHeal) &&
                    options.Count == 2 && options.All(option => option is DigRestSiteOption or SmithRestSiteOption) &&
                    !JigsawHuntingServices.RestChoicesRemain(owner),
                    "Exactly one original choice-id sync targets role two; only that role receives the independently sampled native Mend heal, leaving only forbidden actions.");
                await MatchPeerEvidence("mend-effects", MendEvidence(run, beforeMendOwnerHp, beforeMendTargetHp, mendHeal, choices));
            }
            finally { choiceSync.PlayerChoiceReceived -= observed; }
            await Until(() => Signaled("2", "mend-observed"), "The other real run receives the original Mend choice and healing before departure.");

            var afterHealing = Effects(run);
            foreach (int index in Enumerable.Range(0, options.Count))
                Check(!await sync.ChooseLocalOption(index), "Tent capacity still cannot authorize a remaining forbidden option after real Mend.");
            Equal(Effects(run), afterHealing, "Post-Mend forbidden retries cannot reroll a relic, upgrade a card, consume a slot or append history.");
            int root = session.Map!.CurrentNode!.Value;
            var descriptor = EditorDescriptor();
            var wrongOwner = EditorMessage(session, JigsawEditorOperation.CompleteRoom, 1, completedOwner: target.NetId);
            Check(!RitsuLibManagedNetActions.Request(RunManager.Instance, descriptor, wrongOwner, target.NetId),
                "The actual host sender refuses an explicit other-role owner instead of impersonating the child endpoint.");
            await ReceiveEditor(owner, wrongOwner);
            Check(session.Map.RoomCompletedPlayers.Count == 0 && session.Map.CurrentNode == root,
                "The real serialized action receiver rejects role one claiming role two's completion.");
            Equal(Effects(run), afterHealing, "The rejected wrong-owner completion preserves all sampled effects and original remaining options.");
#if !STS2_PUBLIC
            Task completed = sync.AfterAllRestSitesCompleted();
            Check(!completed.IsCompleted, "The real beta completion still waits for the owner's two remaining forbidden options before actual departure.");
#endif
            RunManager.Instance.ActionExecutor.Unpause();
            await ui.Click(ui.Room.ProceedButton);
            await Until(() => ui.Screen.Visible && session.Map.RoomCompletedPlayers.Contains(owner.NetId),
                "The original Proceed callback opens the production puzzle map and its original sender records only the local completion.");
            Check(session.Map.CurrentNode == root && !session.Map.RoomCompletedPlayers.Contains(target.NetId),
                "One real local Proceed cannot invent the other role's departure or complete the shared root.");
            Signal("local-proceeded");
            await Until(() => session.Map.RoomCompletedPlayers.Contains(target.NetId),
                "The real child calls its own original completion sender after the host's first departure was observed.");
            Check(session.Map.CurrentNode == root && session.Map.RoomCompletedPlayers.Order().SequenceEqual(run.Players.Select(player => player.NetId).Order()),
                "The production receiver accepts role two only after its real Heal finished; both owners complete before any destination vote.");
            int destination = session.Map.VotingDestinations.First();
            File.WriteAllText(Path.Combine(directory!, "destination"), destination.ToString());
            Ok(JigsawEditorAction.Submit(session, JigsawEditorOperation.VoteDestination, nodeId: destination));
            await Until(() => session.Map.DestinationVotes.TryGetValue(owner.NetId, out int voted) && voted == destination,
                "The actual local sender and ordered action queue record the local destination ballot.");
            ui.UnmountRoom();
            ui.ReleaseSceneOwnership();
            Signal("host-voted");
            await Until(() => session.Map.CurrentNode == destination && session.Map.PendingEntry == null && run.CurrentRoom is not RestSiteRoom,
                "The real second-owner ballot triggers original host finalization and native entry into the selected next root.");
            if (session.EntryRecoveryTask != null) await session.EntryRecoveryTask;
            await session.WaitForSave();
#if !STS2_PUBLIC
            Check(completed.IsCompletedSuccessfully && options.Count == 0,
                "The original beta rest-room exit skips its real leftover options and releases the all-role completion task.");
#endif
            Check(run.CurrentRoom is not RestSiteRoom && session.Map.Completed.Contains(root),
                "The actual production next-root entry leaves the real rest room without executing Smith or Dig.");
            Console.WriteLine("REST_RELICS hunted=true roles=2 runs=2 enet-endpoints=2 game-ui=host-only generated=HEAL,SMITH,MEND,DIG owner=1 target=2 heal=1 mend=1 choice-sync=1 forbidden=2 wrong-completed-owner=rejected next-root=1 hook-substitute=0 option-injection=0");
        });
    }

    private static Task SafeDig() => WithRest(false, async session =>
    {
        if (IsChild) { await PeerSafeDig(session); return; }
        var run = session.Native;
        var owner = run.Players[0];
        var sync = RunManager.Instance.RestSiteSynchronizer;
        CheckGenerated(run);
        Check(!run.Players.Any(player => player.Relics.OfType<ArchitectMask>().Any()) && !JigsawHuntingServices.RestBlocked(owner),
            "The exhausted safe root retains its authoritative safe snapshot and has no hunting masks.");
        var originalOptions = sync.GetLocalOptions().ToArray();
        var observeDig = PrepareDigObservation(session);
        await Until(() => Signaled("2", "safe-ready"), "The real child's independent Dig observation starts before the host chooses Dig.");
        int index = Array.FindIndex(originalOptions, option => option is DigRestSiteOption);
        Check(await sync.ChooseLocalOption(index), "The unmodified local sender executes the real Shovel-generated Dig in a safe root.");
        var evidence = observeDig();
        Signal("dig-selected");
        await MatchPeerEvidence("dig-effects", evidence);
        await Until(() => Signaled("2", "dig-observed"), "The actual other run receives native Dig and the original relic delivery before cleanup.");
        Console.WriteLine($"REST_RELICS hunted=false roles=2 runs=2 enet-endpoints=2 game-ui=none generated=HEAL,SMITH,MEND,DIG dig=1 relic={evidence["Obtained"]!.GetValue<string>()} hook-substitute=0 option-injection=0");
    });

    private static async Task PeerPunished(JigsawNativeSession session)
    {
        var run = session.Native;
        var owner = run.Players[0];
        var me = run.Players[1];
        var sync = RunManager.Instance.RestSiteSynchronizer;
        CheckGenerated(run);
        await Until(() => Choices(owner).SequenceEqual(new[] { "HEAL" }),
            "The child receives the host's real original Heal before taking its own rest choice.");
        Console.WriteLine($"REST_RELICS_HEAL_HISTORY role={Role} owner={owner.NetId} hp={owner.Creature.CurrentHp} history={string.Join(',', Choices(owner))}");
        await Until(() => Signaled("host", "host-healed"), "The host finishes its original local Heal UI flow before the child takes its own option.");
        int hp = me.Creature.CurrentHp;
        int heal = (int)HealRestSiteOption.GetHealAmount(me);
        Check(await sync.ChooseLocalOption(sync.GetLocalOptions().ToList().FindIndex(option => option is HealRestSiteOption)) &&
            me.Creature.CurrentHp == Math.Min(me.Creature.MaxHp, hp + heal) && sync.GetLocalOptions().Count == 0,
            "The actual client sender heals only its own real role and clears its non-Tent original choices.");
        int beforeMendOwnerHp = owner.Creature.CurrentHp;
        int beforeMend = me.Creature.CurrentHp;
        int mend = (int)MendRestSiteOption.GetHealAmount(me);
        var choiceSync = RunManager.Instance.PlayerChoiceSynchronizer;
        uint nextChoice = choiceSync.ChoiceIds.Count == 0 ? 0 : choiceSync.ChoiceIds[0];
        var choices = new List<(ulong Owner, uint Id, ulong? Target)>();
        Action<Player, uint, NetPlayerChoiceResult> observed = (player, id, result) =>
            choices.Add((player.NetId, id, PlayerChoiceResult.FromNetData(player, run, result).AsPlayerId()));
        choiceSync.PlayerChoiceReceived += observed;
        try
        {
            Signal("other-healed");
            await Until(() => Choices(owner).SequenceEqual(new[] { "HEAL", "MEND" }) &&
                me.Creature.CurrentHp == Math.Min(me.Creature.MaxHp, beforeMend + mend),
                "The child remote Mend branch receives the host's actual synchronized target and reproduces only role two's original healing.");
            Check(choices.SequenceEqual(new[] { (owner.NetId, nextChoice, (ulong?)me.NetId) }) &&
                owner.Creature.CurrentHp == beforeMendOwnerHp && sync.GetOptionsForPlayer(owner).Count == 2 &&
                sync.GetOptionsForPlayer(owner).All(option => option is DigRestSiteOption or SmithRestSiteOption),
                "The remote endpoint independently receives exactly the reserved owner/choice-id/target once, keeps the owner's HP unchanged and retains only forbidden choices.");
            await MatchPeerEvidence("mend-effects", MendEvidence(run, beforeMendOwnerHp, beforeMend, mend, choices));
        }
        finally { choiceSync.PlayerChoiceReceived -= observed; }
        Signal("mend-observed");
        await Until(() => Signaled("host", "local-proceeded") && session.Map!.RoomCompletedPlayers.Contains(1UL),
            "The child observes only the host's first genuine departure before sending its own completion.");
        session.CompleteCurrentRoom(true);
        await Until(() => session.Map!.RoomCompletedPlayers.Count == 2 && Signaled("host", "host-voted") &&
            session.Map.DestinationVotes.ContainsKey(1UL), "Both actual native role completions precede the child's destination ballot.");
        int destination = int.Parse(File.ReadAllText(Path.Combine(directory!, "destination")));
        Ok(JigsawEditorAction.Submit(session, JigsawEditorOperation.VoteDestination, nodeId: destination));
        await Until(() => session.Map!.CurrentNode == destination && session.Map.PendingEntry == null && run.CurrentRoom is not RestSiteRoom,
            "The child naturally reports its original fade/native room readiness and enters the same finalized next root.");
        if (session.EntryRecoveryTask != null) await session.EntryRecoveryTask;
        await session.WaitForSave();
        Console.WriteLine("REST_RELICS_CHILD heal=local mend=remote-choice complete=local vote=local fade=original ready=original next-root=1");
    }

    private static async Task PeerSafeDig(JigsawNativeSession session)
    {
        var owner = session.Native.Players[0];
        int relics = owner.Relics.Count;
        CheckGenerated(session.Native);
        var observeDig = PrepareDigObservation(session);
        Signal("safe-ready");
        await Until(() => Signaled("host", "dig-selected") && Choices(owner).SequenceEqual(new[] { "DIG" }) && owner.Relics.Count == relics + 1,
            "The actual child receives native host Dig and obtains exactly one original relic in its own run.");
        await MatchPeerEvidence("dig-effects", observeDig());
        Signal("dig-observed");
    }

    private static Func<JsonNode> PrepareDigObservation(JigsawNativeSession session)
    {
        var run = session.Native;
        var owner = run.Players[0];
        var other = run.Players[1];
        var sync = RunManager.Instance.RestSiteSynchronizer;
        var options = sync.GetOptionsForPlayer(owner).ToArray();
        var relics = owner.Relics.ToArray();
        var before = Effects(run);
        var otherBefore = JsonSerializer.SerializeToNode(other.ToSerializable(), CompleteJson)!;
        var bagBefore = JsonSerializer.SerializeToNode(owner.RelicGrabBag.ToSerializable(), CompleteJson)!;
        var rngBefore = JsonSerializer.SerializeToNode(owner.PlayerRng.ToSerializable(), CompleteJson)!;
        var mapBefore = JsonSerializer.SerializeToNode(session.Map!.CreateSave(), CompleteJson)!;
        var expectedRng = PlayerRngSet.FromSerializable(owner.PlayerRng.ToSerializable());
        var expectedBag = new RelicGrabBag();
        expectedBag.LoadFromSerializable(owner.RelicGrabBag.ToSerializable());
        var expectedSharedBag = new RelicGrabBag();
        expectedSharedBag.LoadFromSerializable(run.SharedRelicGrabBag.ToSerializable());
        var expected = expectedBag.PullFromFront(RelicFactory.RollRarity(expectedRng.Rewards), _ => true, run) ?? RelicFactory.FallbackRelic;
        expectedSharedBag.Remove(expected);
        Equal(Effects(run), before, "Independent native bag/RNG clone prediction never consumes the actual run's reward state.");
        return () =>
        {
            var obtained = owner.Relics.Except(relics).ToArray();
            Check(obtained.Length == 1 && obtained[0].IsMutable && obtained[0].Id == expected.Id && owner.Relics.Count == relics.Length + 1 &&
                !JsonNode.DeepEquals(bagBefore, JsonSerializer.SerializeToNode(owner.RelicGrabBag.ToSerializable(), CompleteJson)) &&
                !JsonNode.DeepEquals(rngBefore, JsonSerializer.SerializeToNode(owner.PlayerRng.ToSerializable(), CompleteJson)) &&
                Choices(owner).SequenceEqual(new[] { "DIG" }) &&
                sync.GetOptionsForPlayer(owner).SequenceEqual(options.Where(option => option is not DigRestSiteOption)),
                "Each actual endpoint independently obtains the exact native-clone-predicted mutable relic, consumes bag/RNG, writes DIG once and retains Tent's original other options.");
            Equal(JsonSerializer.SerializeToNode(owner.RelicGrabBag.ToSerializable(), CompleteJson)!,
                JsonSerializer.SerializeToNode(expectedBag.ToSerializable(), CompleteJson)!, "Dig consumes precisely the independently cloned native bag result.");
            Equal(JsonSerializer.SerializeToNode(run.SharedRelicGrabBag.ToSerializable(), CompleteJson)!,
                JsonSerializer.SerializeToNode(expectedSharedBag.ToSerializable(), CompleteJson)!, "Dig removes precisely the predicted reward from the shared bag.");
            Equal(RngState(owner.PlayerRng.Rewards), RngState(expectedRng.Rewards), "Dig's actual full reward RNG state equals one original rarity roll on the independent clone.");
            Equal(JsonSerializer.SerializeToNode(other.ToSerializable(), CompleteJson)!, otherBefore,
                "Dig preserves the other role's entire serialized player, including its own relics, bag, RNG, HP and resources.");
            Equal(JsonSerializer.SerializeToNode(session.Map.CreateSave(), CompleteJson)!, mapBefore,
                "Dig preserves the actual accepted root, exploration cost and hunting snapshot.");
            return JsonSerializer.SerializeToNode(new { Before = before, Obtained = obtained[0].Id.Entry, After = Effects(run) }, CompleteJson)!;
        };
    }

    private static JsonNode RngState(Rng rng) =>
#if STS2_PUBLIC
        JsonSerializer.SerializeToNode(new { rng.Seed, rng.Counter }, CompleteJson)!;
#else
        JsonSerializer.SerializeToNode(rng.ToSerializable(), CompleteJson)!;
#endif

    private static JsonNode MendEvidence(RunState run, int ownerHp, int targetHp, int heal,
        IEnumerable<(ulong Owner, uint Id, ulong? Target)> choices) => JsonSerializer.SerializeToNode(new
    {
        Before = new { OwnerHp = ownerHp, TargetHp = targetHp, Heal = heal },
        Choices = choices.Select(choice => new { choice.Owner, choice.Id, choice.Target }).ToArray(),
        Players = run.Players.Select(player => player.ToSerializable()).ToArray(),
        History = run.Players.Select(player => Choices(player).ToArray()).ToArray(),
        Options = run.Players.Select(player => RunManager.Instance.RestSiteSynchronizer.GetOptionsForPlayer(player)
            .Select(option => option.OptionId).ToArray()).ToArray()
    }, CompleteJson)!;

    private static async Task MatchPeerEvidence(string stage, JsonNode actual)
    {
        File.WriteAllText(Path.Combine(directory!, Role + "." + stage + ".json"), actual.ToJsonString());
        Signal(stage);
        string peer = IsChild ? "host" : "2";
        await Until(() => Signaled(peer, stage), "Both independent native runs sample the same completed " + stage + " stage.");
        var remote = JsonNode.Parse(File.ReadAllText(Path.Combine(directory!, peer + "." + stage + ".json")))!;
        Equal(SynchronizedEvidence(actual), SynchronizedEvidence(remote),
            "Both endpoints' independently validated actual " + stage + " results agree before either endpoint advances.");
    }

    private static JsonNode SynchronizedEvidence(JsonNode evidence)
    {
        var synchronized = evidence.DeepClone();
        foreach (var stage in new[] { synchronized, synchronized["Before"], synchronized["After"] })
        {
            if (stage is not JsonObject value || value["Players"] is not JsonArray players) continue;
            foreach (var player in players)
            {
                // RelicCmd.Obtain -> ProgressSaveManager.MarkRelicAsSeen records
                // discoveries only for LocalContext.IsMine. Keep this field in
                // raw evidence and same-endpoint invariants, outside peer equality.
                player!.AsObject().Remove("discovered_relics");
            }
        }
        return synchronized;
    }

    private static Action ObserveRelicDiscoveries(RunState run)
    {
        var profileBefore = SaveManager.Instance.Progress.DiscoveredRelics.ToHashSet();
        var ownedBefore = run.Players.Select(player => player.Relics.Select(relic => relic.Id).ToHashSet()).ToArray();
        var discoveredBefore = run.Players.Select(player => player.DiscoveredRelics.ToArray()).ToArray();
        return () =>
        {
            var expectedProfile = profileBefore.ToHashSet();
            for (int index = 0; index < run.Players.Count; index++)
            {
                var player = run.Players[index];
                // Original RelicCmd.Obtain gates MarkRelicAsSeen on IsMe;
                // ProgressSaveManager appends only a newly seen local relic.
                var newlySeen = player.Relics.Where(relic => !ownedBefore[index].Contains(relic.Id) &&
                    LocalContext.IsMe(player) && !profileBefore.Contains(relic.Id)).Select(relic => relic.Id).Distinct().ToArray();
                Check(player.DiscoveredRelics.SequenceEqual(discoveredBefore[index].Concat(newlySeen)),
                    "Native discovery bookkeeping records only newly obtained local relics and never marks the remote owner's discoveries.");
                expectedProfile.UnionWith(newlySeen);
            }
            Check(expectedProfile.SetEquals(SaveManager.Instance.Progress.DiscoveredRelics),
                "The isolated local profile discovers exactly the actually obtained local relic IDs, independently of peer comparison.");
        };
    }

    private static void CheckGenerated(RunState run)
    {
        var sync = RunManager.Instance.RestSiteSynchronizer;
        var owner = run.Players[0];
        Check(owner.GetRelic<MiniatureTent>() != null && owner.GetRelic<Shovel>() != null &&
            sync.GetOptionsForPlayer(owner).Select(option => option.OptionId).SequenceEqual(new[] { "HEAL", "SMITH", "MEND", "DIG" }) &&
            sync.GetOptionsForPlayer(run.Players[1]).Select(option => option.OptionId).SequenceEqual(new[] { "HEAL", "SMITH", "MEND" }) &&
            !run.Players[1].Relics.Any(relic => relic is MiniatureTent or Shovel),
            "Actual native two-role room generation retains Heal/Smith/Mend for both and adds Shovel Dig only to its real Tent/Shovel owner.");
    }

    private static RitsuLibManagedNetActionDescriptor<JigsawEditorIntent> EditorDescriptor() =>
        (RitsuLibManagedNetActionDescriptor<JigsawEditorIntent>)AccessTools.Field(typeof(JigsawEditorAction), "descriptor").GetValue(null)!;
    private static JigsawEditorIntent EditorMessage(JigsawNativeSession session, JigsawEditorOperation operation, long sequence,
        ulong? completedOwner = null, int? nodeId = null) => new(session.Native.CurrentActIndex, operation,
            NodeId: nodeId, Sequence: sequence, EditRevision: session.Map!.EditRevision, CompletedOwner: completedOwner,
            EntryRevision: session.Map.CreateSave().NextEntry, EntryRound: session.EntryRound);
    private static Task ReceiveEditor(Player player, JigsawEditorIntent message)
    {
        var descriptor = EditorDescriptor();
        var action = new RitsuLibManagedGameAction(player, RitsuLibManagedNetActions.Register(descriptor),
            descriptor.ActionType, descriptor.Serialize(message));
        // Original ExecuteAction creates its own real player-choice context and
        // deserializes its original payload before the production receiver.
        return (Task)AccessTools.Method(typeof(RitsuLibManagedGameAction), "ExecuteAction").Invoke(action, null)!;
    }
    private static IEnumerable<string> Choices(Player player) => player.RunState.CurrentMapPointHistoryEntry!.GetEntry(player.NetId).RestSiteChoices;
    private static JsonNode Effects(RunState run) => JsonSerializer.SerializeToNode(new
    {
        Players = run.Players.Select(player => player.ToSerializable()).ToArray(),
        Rng = run.Rng.ToSerializable(),
        SharedBag = run.SharedRelicGrabBag.ToSerializable(),
        Map = JigsawNativeSession.TryGet(run)!.Map!.CreateSave(),
        History = run.Players.Select(player => Choices(player).ToArray()).ToArray(),
        Options = run.Players.Select(player => new
        {
            player.NetId,
            Chosen = RunManager.Instance.RestSiteSynchronizer.GetChosenOptionIndex(player.NetId),
            Hovered = RunManager.Instance.RestSiteSynchronizer.GetHoveredOptionIndex(player.NetId),
            Choices = RunManager.Instance.RestSiteSynchronizer.GetOptionsForPlayer(player).Select(option => new { option.OptionId, option.IsEnabled }).ToArray()
        }).ToArray()
    }, CompleteJson)!;

    private static async Task WithRest(bool hunted, Func<JigsawNativeSession, Task> test)
    {
        var manager = RunManager.Instance;
        var previousNetId = LocalContext.NetId;
        var previousSave = JigsawNativeSaveHost.Save;
        var progress = SaveManager.Instance.Progress;
        var profile = JsonSerializer.SerializeToNode(progress.ToSerializable(), CompleteJson)!;
        var host = new Harmony("AllMatters.Tests.RestRelicProfile");
        NetHostGameService? hostNet = null;
        NetClientGameService? clientNet = null;
        HeadlessProcessOutput? child = null;
        var previousNetwork = network;
        var previousDirectory = directory;
        var previousChild = childProcess;
        Exception? bodyFailure = null;
        var failures = new List<Exception>();
        void Cleanup(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        using var atlases = HeadlessTestSetup.LoadAtlases("card_atlas", "relic_atlas", "ui_atlas", "compressed");
        using var events = HeadlessCombatInProgress.EnterEvent();
        try
        {
            var data = JsonSerializer.Deserialize<SerializableProgress>(profile.ToJsonString(), CompleteJson)!;
            var context = new DeserializationContext();
            SaveManager.Instance.Progress = ProgressState.FromSerializable(data, context);
            Check(!context.Errors.Any() && !ReferenceEquals(SaveManager.Instance.Progress, progress),
                "All real relic discoveries use an independent deep-cloned native profile.");
            Equal(JsonSerializer.SerializeToNode(SaveManager.Instance.Progress.ToSerializable(), CompleteJson)!, profile,
                "The native isolated profile starts with every original serialized field.");
            host.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SaveProgressFile)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingRestRelicTests), nameof(SkipHost)));
            var io = new MockGodotFileIo("hunting-rest-relic-memory-only");
            var saves = new RunSaveManager(82, io, new MigrationManager(io), forceSynchronous: true);
            JigsawNativeSaveHost.Save = room => saves.SaveRun(manager.ToSave(room), false);
            MegaCrit.Sts2.Core.Multiplayer.Serialization.MessageTypes.Initialize();
#if STS2_PUBLIC
            hostNet = IsChild ? null : new NetHostGameService();
            clientNet = IsChild ? new NetClientGameService() : null;
#else
            hostNet = IsChild ? null : new NetHostGameService(PeerVersionInfo.LocalDefault());
            clientNet = IsChild ? new NetClientGameService(PeerVersionInfo.LocalDefault()) : null;
#endif
            network = (INetGameService?)hostNet ?? clientNet!;
            if (hostNet != null)
            {
                directory = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "../../.local/rest-relic-enet", Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(directory);
                using var socket = new System.Net.Sockets.UdpClient(0);
                ushort port = (ushort)((System.Net.IPEndPoint)socket.Client.LocalEndPoint!).Port;
                socket.Close();
                Check(!hostNet.StartENetHost(port, 1).HasValue, "The actual two-role ENet host binds its loopback port.");
                hostNet.ClientConnected += id => hostNet.SetPeerReadyForBroadcasting(id);
                child = Launch(hunted ? PunishedName : SafeName, directory, port);
                childProcess = child.Process;
                await Until(() => hostNet.ConnectedPeers.Count == 1 && hostNet.ConnectedPeers[0].peerId == 2,
                    "The actual native host authenticates the real child role before two-role setup.");
            }
            else
            {
                directory = System.Environment.GetEnvironmentVariable("JIGSAW_REST_RELIC_DIR")!;
                var transport = new ENetClient(clientNet!);
                clientNet!.Initialize(transport, PlatformType.None);
                clientNet.RegisterMessageHandler<InitialGameInfoMessage>((_, _) => { });
                var connecting = transport.ConnectToHost(2, "127.0.0.1",
                    ushort.Parse(System.Environment.GetEnvironmentVariable("JIGSAW_REST_RELIC_PORT")!), CancellationToken.None);
                await Until(() => connecting.IsCompleted, "The real child finishes its original native transport handshake.");
                Check(!(await connecting).HasValue, "The real child transport connects without a native error.");
                await Until(() => clientNet.IsConnected, "The original client authentication completes before creating its actual second run.");
            }
            Signal("connected");
            await Until(() => Signaled("host", "connected") && Signaled("2", "connected"), "Both real transport endpoints are connected before their original run lobbies are constructed.");
            var players = new[] { Player.CreateForNewRun<Ironclad>(UnlockState.all, 1), Player.CreateForNewRun<Silent>(UnlockState.all, 2) };
            var run = RunState.CreateForTest(players, acts: [ModelDb.Act<Overgrowth>()], seed: "HUNTING82_REST_RELICS");
            manager.SetUpTest(run, network);
            manager.CombatReplayWriter.IsEnabled = false;
            LocalContext.NetId = network.NetId;
            manager.GenerateRooms();
            await manager.GenerateMap();
            run.AppendToMapPointHistory(MegaCrit.Sts2.Core.Map.MapPointType.Ancient, RoomType.Event, null);
            Check(run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord), "The source run owns a real native map coordinate before room entry.");
            var verifyDiscoveries = ObserveRelicDiscoveries(run);
            await RelicCmd.Obtain<MiniatureTent>(players[0]);
            await RelicCmd.Obtain<Shovel>(players[0]);
            foreach (var player in players)
            {
                player.PopulateRelicGrabBagIfNecessary(run.Rng.UpFront);
                player.Creature.SetCurrentHpInternal(10);
            }
#if !STS2_PUBLIC
            Check(RestSiteOption.generateForTests == null, "Native option generation uses its original body, not the beta test injection seam.");
#endif
            var (map, restTarget) = BeforeRest(hunted);
            // Prepare an accepted two-owner source root, as in the existing
            // native rest fixture. Actual native projection/entry generates the
            // options; subsequent shared departure uses original peer readiness.
            var accepted = map.BeginEnter(restTarget)!;
            Ok(map.ConfirmEnter(accepted.Id));
            int row = Math.Max(0, (run.MapPointHistory.ElementAtOrDefault(run.CurrentActIndex)?.Count ?? 1) - 1);
            var projection = JigsawEntryProjection.Append(run.Map, row, JigsawRoom.Rest);
            Check(!run.VisitedMapCoords.Contains(projection.Point.coord),
                "The source rest uses the next chronological native projection coordinate, never an already visited point.");
            run.Map = projection.Map;
            await manager.EnterMapCoord(projection.Point.coord);
            JigsawCheckpoint.Capture(run, map);
            JigsawNativeSession.Restore(run);
            var session = JigsawNativeSession.Current!;
            await JigsawHuntingNative.SynchronizeMasks(session);
            verifyDiscoveries();
            verifyDiscoveries = ObserveRelicDiscoveries(run);
            Check(run.CurrentRoom is RestSiteRoom && run.CurrentRoomCount == 1 && session.Map!.CurrentRoomHunted == hunted &&
                session.Map.RemainingSteps == 0 && session.Map.ActivePlayers.Order().SequenceEqual(new[] { 1UL, 2UL }) &&
                manager.RunLobby != null && network.IsConnected,
                "Actual native rest entry generates original options and binds its accepted safe/punished root only within a connected two-role native run lobby.");
            manager.ActionExecutor.Unpause();
            Signal("source-ready");
            await Until(() => Signaled("host", "source-ready") && Signaled("2", "source-ready"), "Both independent native runs own the same actual two-role rest source before any test input.");
            var restSync = manager.RestSiteSynchronizer;
            void BeforeOption(RestSiteOption option, ulong owner)
            {
                if (option is HealRestSiteOption)
                    Console.WriteLine($"REST_RELICS_HEAL_EVENT role={Role} phase=before owner={owner} hp={run.GetPlayer(owner)!.Creature.CurrentHp} history={string.Join(',', Choices(run.GetPlayer(owner)!))}");
            }
            void AfterOption(RestSiteOption option, bool success, ulong owner)
            {
                if (option is HealRestSiteOption)
                    Console.WriteLine($"REST_RELICS_HEAL_EVENT role={Role} phase=after-select owner={owner} success={success} hp={run.GetPlayer(owner)!.Creature.CurrentHp} history-before-append={string.Join(',', Choices(run.GetPlayer(owner)!))}");
            }
            restSync.BeforePlayerOptionChosen += BeforeOption;
            restSync.AfterPlayerOptionChosen += AfterOption;
            try { await test(session); verifyDiscoveries(); }
            finally { restSync.BeforePlayerOptionChosen -= BeforeOption; restSync.AfterPlayerOptionChosen -= AfterOption; }
            Signal("body-done");
            await Until(() => Signaled("host", "body-done") && Signaled("2", "body-done"), "Both native runs finish the applicable real rest effects before shared cleanup.");
            if (hostNet != null)
            {
                Signal("finish");
                await Until(() => hostNet.ConnectedPeers.Count == 0,
                    "The host receives the child's original graceful disconnect and removes its native peer before shutdown.");
                Signal("peer-released");
                await Until(() => child!.Process.HasExited, "The real child finishes its own run cleanup and exits.");
                Check(child!.Process.ExitCode == 0, "The actual child endpoint reports successful original rest behavior and cleanup.");
            }
            else
            {
                await Until(() => Signaled("host", "finish"), "The child retains its live peer until the host has checked both completed bodies.");
                clientNet!.Disconnect(NetError.Quit);
                await Until(() => Signaled("host", "peer-released"),
                    "The child keeps its process alive until the host has consumed the original disconnect packet and released its native peer.");
            }
        }
        catch (Exception error) { bodyFailure = error; throw; }
        finally
        {
            Cleanup(() => JigsawNativeSession.Current?.Close());
            Cleanup(() => CombatManager.Instance.Reset(true));
            Cleanup(() => manager.CleanUp(false));
            Cleanup(() => { if (network?.IsConnected == true) network.Disconnect(NetError.Quit); });
            if (child != null)
            {
                try
                {
                    int exit = await child.Finish(Path.Combine(directory!, "2.log"), Path.Combine(directory!, "2.err"));
                    Check(exit == 0, "The actual child exit is zero; evidence=" + directory);
                }
                catch (Exception error) { failures.Add(error); }
            }
            network = previousNetwork; directory = previousDirectory; childProcess = previousChild;
            JigsawNativeSaveHost.Save = previousSave;
            LocalContext.NetId = previousNetId;
            Cleanup(() => host.UnpatchAll(host.Id));
            Cleanup(() => SaveManager.Instance.Progress = progress);
            Cleanup(() => Check(ReferenceEquals(SaveManager.Instance.Progress, progress), "Cleanup restores the exact shared profile reference."));
            Cleanup(() => Equal(JsonSerializer.SerializeToNode(progress.ToSerializable(), CompleteJson)!, profile,
                "The complete original profile remains unchanged after real native relic generation and discovery."));
            if (failures.Count > 0) throw new AggregateException("Rest relic cleanup failed.", bodyFailure == null ? failures : new[] { bodyFailure }.Concat(failures));
        }
    }

    private static HeadlessProcessOutput Launch(string name, string evidence, ushort port)
    {
        var start = new ProcessStartInfo(OS.GetExecutablePath())
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { "--headless", "--path", ProjectSettings.GlobalizePath("res://"), "--", name }) start.ArgumentList.Add(argument);
        start.Environment["JIGSAW_REST_RELIC_ROLE"] = "2";
        start.Environment["JIGSAW_REST_RELIC_DIR"] = evidence;
        start.Environment["JIGSAW_REST_RELIC_PORT"] = port.ToString();
        return new HeadlessProcessOutput(start);
    }
    private static void Signal(string stage) => File.WriteAllText(Path.Combine(directory!, Role + "." + stage), "ready");
    private static bool Signaled(string role, string stage) => File.Exists(Path.Combine(directory!, role + "." + stage));

    private static (JigsawRun Map, int Target) BeforeRest(bool hunted)
    {
        var map = JigsawRun.Create(75, players: [1UL, 2UL]);
        foreach (ulong player in new[] { 1UL, 2UL }) Ok(map.ChooseEnd(player, EndChoice.Automatic));
        Ok(map.FinishAutomatic(map.AutomaticOperation!, JigsawAutomatic.Solve(map, 0)));
        foreach (ulong player in new[] { 1UL, 2UL }) Ok(map.CompleteAncient(player));
        foreach (ulong player in new[] { 1UL, 2UL }) Ok(map.Continue(player));
        var pending = new Queue<int>(); pending.Enqueue(0);
        var previous = new Dictionary<int, int> { [0] = -1 };
        while (pending.TryDequeue(out int current))
            foreach (var edge in map.Edges.Where(edge => edge.Touches(current)))
            {
                int next = edge.First == current ? edge.Second : edge.First;
                if (map.Nodes.Single(node => node.Id == next).Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss) && previous.TryAdd(next, current))
                    pending.Enqueue(next);
            }
        int target = map.Nodes.First(node => node.Room == JigsawRoom.Rest && previous.ContainsKey(node.Id) && previous[node.Id] != 0).Id;
        var path = new List<int>();
        for (int node = target; node != 0; node = previous[node]) path.Add(node);
        path.Reverse();
        Ok(map.SetRemainingSteps(hunted ? 1 : 100));
        foreach (int node in path.SkipLast(1))
        {
            var entry = map.BeginEnter(node)!;
            Ok(map.ConfirmEnter(entry.Id)); Ok(map.CompleteRoom(entry.Id));
        }
        if (!hunted) Ok(map.SetRemainingSteps(1));
        return (map, target);
    }

    private sealed class RestUi : IDisposable
    {
        private readonly Harmony host = new("AllMatters.Tests.RestRelicUi");
        private readonly List<Node> owned = [];
        private readonly List<StringName> addedActions = [];
        private readonly Window viewport = ((SceneTree)Engine.GetMainLoop()).Root;
        private readonly Vector2I size = ((SceneTree)Engine.GetMainLoop()).Root.Size;
        private readonly Vector2I content = ((SceneTree)Engine.GetMainLoop()).Root.ContentScaleSize;
        private readonly Input.MouseModeEnum mouseMode = Input.MouseMode;
        private JigsawNativeMapUiHost? ui;
        private NGame? gameWithShake;
        private object? previousScreenShake;
        internal NRestSiteRoom Room = null!;
        internal NTargetManager Target = null!;
        internal JigsawScreen Screen = null!;
        private T Own<T>(T node) where T : Node { owned.Add(node); return node; }

        internal async Task Mount(JigsawNativeSession session)
        {
            JigsawHuntingNativeTests.RegisterScripts();
            foreach (string name in MegaInput.AllInputs.Concat(new[] { "debug_hide_rest_site", "debug_hide_targeting_ui", "debug_hide_mp_health_bars" }))
            {
                StringName action = name;
                if (!InputMap.HasAction(action)) { InputMap.AddAction(action); addedActions.Add(action); }
            }
            viewport.Size = viewport.ContentScaleSize = new Vector2I(1920, 1080);
            ui = new JigsawNativeMapUiHost();
            Screen = await ui.Open(session); session.HideMap();
            var game = NGame.Instance!;
            var run = NRun.Instance!;
            run.GlobalUi.MouseFilter = Control.MouseFilterEnum.Ignore;
            gameWithShake = game;
            previousScreenShake = AccessTools.Field(typeof(NGame), "_screenShake").GetValue(game);
            AccessTools.Field(typeof(NGame), "_screenShake").SetValue(game,
                Own(new MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.NScreenShake()));
            host.Patch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.SeenFtue)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingRestRelicTests), nameof(SeenFtue)));
            host.Patch(AccessTools.Method(typeof(SfxCmd), nameof(SfxCmd.Play), [typeof(string), typeof(float)]),
                prefix: new HarmonyMethod(typeof(JigsawHuntingRestRelicTests), nameof(SkipHost)));
            // The real character UI assumes an actual image thought bubble;
            // native TestMode otherwise makes this synchronous factory return null.
            host.Patch(AccessTools.GetDeclaredMethods(typeof(NThoughtBubbleVfx)).Single(method =>
                method.Name == nameof(NThoughtBubbleVfx.Create) && method.GetParameters()[0].ParameterType == typeof(Texture2D)),
                prefix: new HarmonyMethod(typeof(JigsawHuntingRestRelicTests), nameof(VisualFactoryPrefix)),
                finalizer: new HarmonyMethod(typeof(JigsawHuntingRestRelicTests), nameof(VisualFactoryFinalizer)));
            var hotkeys = Own(new NHotkeyManager());
            AccessTools.Property(typeof(NGame), nameof(NGame.HotkeyManager)).SetValue(game, hotkeys);
            await HeadlessUiHost.Mount(hotkeys);
            var controller = Own(new NControllerManager());
            AccessTools.Property(typeof(NInputManager), "ControllerManager").SetValue(game.InputManager, controller);
            AccessTools.Property(typeof(NGame), nameof(NGame.CursorManager)).SetValue(game, Own(new NCursorManager()));
            // The rest character hover uses only Highlight/Unhighlight's native
            // empty-list behavior; no unrelated multiplayer top-bar replicas.
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MultiplayerPlayerContainer)).SetValue(run.GlobalUi, Own(new NMultiplayerPlayerStateContainer()));
            Target = Own(new NTargetManager { ProcessMode = Node.ProcessModeEnum.Always });
            foreach (string path in NTargetingArrow.AssetPaths) PreloadManager.Cache.GetTexture2D(path);
            var arrow = PreloadManager.Cache.GetScene("res://scenes/combat/targeting_arrow.tscn").Instantiate<NTargetingArrow>();
            arrow.Name = "TargetingArrow"; Target.AddChild(arrow);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.TargetManager)).SetValue(run.GlobalUi, Target);
            run.GlobalUi.AddChild(Target);
            var map = Own(new NMapScreen { Visible = false });
            AccessTools.Field(typeof(NMapScreen), "_runState").SetValue(map, session.Native);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.MapScreen)).SetValue(run.GlobalUi, map);
            var capstone = Own(new NCapstoneContainer());
            var overlays = Own(new NOverlayStack());
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.CapstoneContainer)).SetValue(run.GlobalUi, capstone);
            AccessTools.Property(typeof(NGlobalUi), nameof(NGlobalUi.Overlays)).SetValue(run.GlobalUi, overlays);
            AccessTools.Property(typeof(NRun), nameof(NRun.ScreenStateTracker)).SetValue(run, new ScreenStateTracker(map, capstone, overlays));
            var rooms = Own(new NSceneContainer { ProcessMode = Node.ProcessModeEnum.Always });
            AccessTools.Field(typeof(NRun), "_roomContainer").SetValue(run, rooms);
            run.AddChild(rooms); rooms.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            Room = PreloadManager.Cache.GetScene("res://scenes/rooms/rest_site_room.tscn").Instantiate<NRestSiteRoom>();
            Room.ProcessMode = Node.ProcessModeEnum.Always;
            AccessTools.Field(typeof(NRestSiteRoom), "_room").SetValue(Room, session.Native.CurrentRoom);
            AccessTools.Field(typeof(NRestSiteRoom), "_runState").SetValue(Room, session.Native);
            AccessTools.Property(typeof(NSceneContainer), nameof(NSceneContainer.CurrentScene)).SetValue(rooms, Room);
            rooms.AddChild(Room); Room.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await Frames(4);
            Check(ReferenceEquals(NRestSiteRoom.Instance, Room) && Room.Characters.Count == 2 &&
                Room.Options.Count == 4 && Room.Characters.All(character => character.Hitbox.IsInsideTree()),
                "Actual rest scene ownership exposes both native characters, four original buttons and the original local Mend target manager.");
        }

        internal async Task Click(Control control)
        {
            if (control is NClickableControl clickable)
                await Until(() => clickable.IsEnabled && clickable.MouseFilter == Control.MouseFilterEnum.Stop,
                    "The original native button enables mouse input after its own animation: " + control.Name);
            Vector2 point = control.GetGlobalTransform() * (control.Size * 0.5f);
            await Motion(point);
            Check(control.IsVisibleInTree() && new Rect2(Vector2.Zero, control.Size).HasPoint(control.GetGlobalTransform().AffineInverse() * point),
                "The actual input point lies inside the original local bounds of the visible native control, including native character mirroring.");
            if (control is NClickableControl button)
                Check((bool)AccessTools.Property(typeof(NClickableControl), "IsFocused").GetValue(button)!,
                    "Actual mouse motion focuses the original native button before press/release: " + control.Name + "; point=" + point);
            viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point, GlobalPosition = point }, true);
            await Frames(1);
            viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point }, true);
            await Frames(2);
        }
        internal void UnmountRoom() { if (Room != null && GodotObject.IsInstanceValid(Room)) Room.Free(); }
        internal void ReleaseSceneOwnership() => ui!.ReleaseSceneOwnership();
        public void Dispose()
        {
            try
            {
                if (Target != null && GodotObject.IsInstanceValid(Target)) Target.CancelTargeting();
                UnmountRoom();
                if (gameWithShake != null && GodotObject.IsInstanceValid(gameWithShake))
                    AccessTools.Field(typeof(NGame), "_screenShake").SetValue(gameWithShake, previousScreenShake);
                for (int index = owned.Count - 1; index >= 0; index--)
                    if (GodotObject.IsInstanceValid(owned[index])) owned[index].Free();
            }
            finally
            {
                try { ui?.Dispose(); }
                finally
                {
                    host.UnpatchAll(host.Id);
                    foreach (var action in addedActions) InputMap.EraseAction(action);
                    viewport.Size = size; viewport.ContentScaleSize = content; Input.MouseMode = mouseMode;
                }
            }
        }
    }

    private static async Task Motion(Vector2 point)
    {
        ((SceneTree)Engine.GetMainLoop()).Root.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        await Frames(2);
    }
    private static async Task Frames(int count)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int frame = 0; frame < count; frame++)
        {
            network?.Update();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }
    private static async Task Until(Func<bool> condition, string reason)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (childProcess is { HasExited: true } && !Signaled("2", "body-done"))
                throw new InvalidOperationException("The real rest child exited before " + reason + "; evidence=" + directory);
            if (timer.Elapsed.TotalSeconds > 20)
                throw new TimeoutException("Rest relic " + Role + ": " + reason + "; evidence=" + directory);
            await Frames(1);
        }
    }
    private static void Equal(JsonNode actual, JsonNode expected, string reason) => Check(JsonNode.DeepEquals(actual, expected),
        reason + " expected=" + expected.ToJsonString() + " actual=" + actual.ToJsonString());
    private static void Ok(JigsawResult result) => Check(result.Success, result.Reason);
    private static void Check(bool condition, string reason) => NativeEventTests.Check(condition, reason);
}
