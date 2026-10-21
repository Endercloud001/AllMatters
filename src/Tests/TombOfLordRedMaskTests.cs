using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rooms;
using System.Text.Json;

internal static class TombOfLordRedMaskTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Red Mask Tomb admits Act 3 with a masked teammate and charges exactly 150 once", Payment),
        ("Red Mask Tomb locks 149 gold, accepts 150, and leaves inventory unchanged", PaymentBoundaryAndLeave),
        ("Red Mask Tomb masked owner claims 222 independently without buying another mask", WearMask),
        ("Red Mask Tomb native entry and completed inventory snapshots do not stack rewards", SaveBoundaries),
        ("Red Mask Tomb portrait and bilingual options load from the shipped PCK", Presentation),
    ];

    private static EventModel Canonical => ModelDb.AllEvents.SingleOrDefault(model => model.Id.Entry == "ALL_MATTERS_EVENT_TOMB_OF_LORD_RED_MASK")
        ?? throw new Exception("Red Mask Tomb is missing from registered events.");

    private static async Task Payment()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
        var other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
        other.AddRelicInternal(ModelDb.Relic<RedMask>().ToMutable());
        var run = RunState.CreateForTest([player, other], acts: [], seed: "RED_MASK_TOMB");
        foreach (int act in new[] { 0, 1, 2 })
        {
            run.CurrentActIndex = act;
            Check(Canonical.IsAllowed(run) == (act == 2), "Only Act 3 is eligible, even when a teammate has the mask.");
        }
        player.Gold = 407;
        int otherGold = other.Gold;
        int relics = player.Relics.Count;
        var model = await Begin(player);
        Check(!model.IsShared, "Each player chooses independently.");
        var choices = model.CurrentOptions.ToArray();
        Check(choices.Single(option => option.TextKey.EndsWith(".WEAR_MASK_LOCKED")).IsLocked, "An unmasked player cannot claim gold.");
        await choices.Single(option => option.TextKey.EndsWith(".PAY_RESPECTS")).Chosen();
        Check(player.Gold == 257 && player.Relics.Count == relics + 1 && player.Relics.Count(relic => relic is RedMask) == 1,
            "Pay exactly 150 and obtain one native mask.");
        Check(model.IsFinished && model.CurrentOptions.Count == 0 && other.Gold == otherGold, "Finish only the owner's service.");
        foreach (var option in choices) await option.Chosen();
        Check(player.Gold == 257 && player.Relics.Count == relics + 1, "Stale choices must not repeat payment or grant rewards.");
    }

    private static async Task PaymentBoundaryAndLeave()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (int gold in new[] { 149, 150 })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            RunState.CreateForTest([player], acts: [], seed: "TOMB_BOUNDARY");
            player.Gold = gold;
            int relics = player.Relics.Count;
            var model = await Begin(player);
            var payment = model.CurrentOptions.Single(option => option.TextKey.Contains(".PAY_RESPECTS"));
            Check(payment.IsLocked == (gold == 149), "The fixed offering requires at least 150 gold.");
            if (payment.IsLocked)
            {
                await payment.Chosen();
                Check(player.Gold == 149 && player.Relics.Count == relics && !model.IsFinished, "A locked offering must have no effect.");
                await model.CurrentOptions.Single(option => option.TextKey.EndsWith(".LEAVE")).Chosen();
                Check(model.IsFinished && player.Gold == 149 && player.Relics.Count == relics, "Leave must be free.");
            }
            else
            {
                await payment.Chosen();
                Check(model.IsFinished && player.Gold == 0 && player.Relics.Count(relic => relic is RedMask) == 1, "Exactly 150 gold is sufficient.");
            }
        }
    }

    private static async Task WearMask()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (bool leave in new[] { false, true })
        {
            var owner = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            var other = Player.CreateForNewRun<Silent>(UnlockState.all, 2);
            owner.AddRelicInternal(ModelDb.Relic<RedMask>().ToMutable());
            var run = RunState.CreateForTest([owner, other], acts: [], seed: "TOMB_MASK");
            run.CurrentActIndex = 2;
            owner.Gold = 0;
            other.Gold = 180;
            int relics = owner.Relics.Count;
            var model = await Begin(owner);
            var teammate = await Begin(other);
            Check(model.CurrentOptions.Count == 2 && model.CurrentOptions.All(option => !option.IsLocked), "A masked owner can wear or leave, never buy a duplicate.");
            Check(teammate.CurrentOptions.Count == 3 && teammate.CurrentOptions[0].IsLocked, "Options depend on the owner, not the teammate's mask.");
            var choices = model.CurrentOptions.ToArray();
            await choices.Single(option => option.TextKey.EndsWith(leave ? ".LEAVE" : ".WEAR_MASK")).Chosen();
            foreach (var option in choices) await option.Chosen();
            Check(model.IsFinished && owner.Gold == (leave ? 0 : 222) && owner.Relics.Count == relics, "Claim exactly 222 once, or leave unchanged.");
            Check(other.Gold == 180 && !teammate.IsFinished && other.Relics.All(relic => relic is not RedMask), "The teammate keeps independent inventory and choices.");
            await teammate.CurrentOptions.Single(option => option.TextKey.EndsWith(".PAY_RESPECTS")).Chosen();
            Check(other.Gold == 30 && other.Relics.Count(relic => relic is RedMask) == 1 && owner.Gold == (leave ? 0 : 222), "The unmasked teammate can pay independently.");
        }
    }

    private static async Task SaveBoundaries()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        foreach (bool masked in new[] { false, true })
        {
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
            if (masked) player.AddRelicInternal(ModelDb.Relic<RedMask>().ToMutable());
            var run = RunState.CreateForTest([player], acts: [], seed: "TOMB_SAVE");
            run.CurrentActIndex = 2;
            player.Gold = 407;
            var entry = player.ToSerializable();
            var room = new EventRoom(Canonical).ToSerializable();
            Check(new EventRoom(room).CanonicalEvent.Id == Canonical.Id && !room.IsPreFinished, "The native ordinary-event room retains its identity and entry semantics.");
            var model = await Begin(player);
            string choice = masked ? ".WEAR_MASK" : ".PAY_RESPECTS";
            await model.CurrentOptions.Single(option => option.TextKey.EndsWith(choice)).Chosen();
            int expectedGold = masked ? 629 : 257;
            var completed = Player.FromSerializable(player.ToSerializable());
            Check(completed.Gold == expectedGold && completed.Relics.Count(relic => relic is RedMask) == 1, "Restoring a completed inventory must not invoke pickup or gold effects again.");
            var loaded = Player.FromSerializable(entry);
            var loadedRun = RunState.CreateForTest([loaded], acts: [], seed: "TOMB_SAVE");
            loadedRun.CurrentActIndex = 2;
            var restarted = await Begin(loaded);
            Check(loaded.Gold == 407 && loaded.Relics.Any(relic => relic is RedMask) == masked, "Reloading the entry restores pre-choice inventory.");
            await restarted.CurrentOptions.Single(option => option.TextKey.EndsWith(choice)).Chosen();
            Check(loaded.Gold == expectedGold && loaded.Relics.Count(relic => relic is RedMask) == 1 && restarted.IsFinished,
                "Replaying from the supported entry snapshot must produce one reward, not stack the discarded attempt.");
        }
    }

    private static async Task Presentation()
    {
        using var scope = HeadlessCombatInProgress.EnterEvent();
        Check(Canonical.CreateInitialPortrait().GetWidth() > 0, "The tomb portrait must load from the exported PCK.");
        try
        {
            foreach (string language in new[] { "eng", "zhs" })
            {
                LocManager.Instance.SetLanguage(language);
                var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(Godot.FileAccess.GetFileAsString($"res://AllMatters/localization/{language}/events.json"))!;
                foreach (var entry in entries.Where(entry => entry.Key.StartsWith(Canonical.Id.Entry + ".")))
                    Check(new LocString("events", entry.Key).GetRawText() == entry.Value, "Every tomb page and option must be localized.");
                var player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1);
                RunState.CreateForTest([player], acts: [], seed: "TOMB_TEXT");
                player.Gold = 150;
                var model = await Begin(player);
                Check(model.Title.GetFormattedText() == (language == "zhs" ? "红面具大人之墓" : "Tomb of Lord Red Mask"), "The title must use the selected language.");
                var offering = model.CurrentOptions.Single(option => option.TextKey.EndsWith(".PAY_RESPECTS")).Description;
                model.DynamicVars.AddTo(offering);
                Check(offering.GetFormattedText().Contains("150") && !offering.GetFormattedText().Contains('{'), "The offering must display the fixed price.");
                player.AddRelicInternal(ModelDb.Relic<RedMask>().ToMutable());
                var masked = await Begin(player);
                var reward = masked.CurrentOptions.Single(option => option.TextKey.EndsWith(".WEAR_MASK")).Description;
                masked.DynamicVars.AddTo(reward);
                Check(reward.GetFormattedText().Contains("222") && !reward.GetFormattedText().Contains('{'), "The mask option must display the actual reward.");
            }
        }
        finally { LocManager.Instance.SetLanguage("eng"); }
    }

    private static async Task<EventModel> Begin(Player player)
    {
        var model = Canonical.ToMutable();
#if STS2_PUBLIC
        await model.BeginEvent(player, false);
#else
        await model.BeginEvent(player, null, false);
#endif
        return model;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
