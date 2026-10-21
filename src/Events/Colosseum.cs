// SPDX-License-Identifier: GPL-3.0-only
// Adapted from Balance Colosseum; specified native monsters and complete elite encounters.
using AllMatters.Encounters;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Events;

[RegisterSharedEvent]
public sealed class Colosseum : ModEventTemplate
{
    private enum Stage { Initial, FirstCombat, Intermission, SecondCombat, Finished }
    private Stage _stage;

    public override bool IsShared => true;
    public override bool IsAllowed(IRunState runState) => runState.CurrentActIndex == 2;
    public override EventAssetProfile AssetProfile => new(InitialPortraitPath: "res://AllMatters/images/events/colosseum.png");

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (isPreFinished)
        {
            _stage = Stage.Finished;
            SetEventFinished(PageDescription("FLEE"));
            return;
        }
        // Native victory saves retain the parent event and room history, not the
        // event instance. Rebuild the intermission when restoring that stack.
        if (Owner!.RunState.CurrentMapPointHistoryEntry?.Rooms.Any(r =>
                r.ModelId == ModelDb.Encounter<ColosseumFirstEncounter>().Id && r.TurnsTaken > 0) == true)
        {
            ShowIntermission();
            return;
        }
        base.SetInitialEventState(false);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new(this, Fight, InitialOptionKey("FIGHT"), []),
    ];

    private Task Fight()
    {
        if (_stage != Stage.Initial) return Task.CompletedTask;
        _stage = Stage.FirstCombat;
        EnterCombatWithoutExitingEvent<ColosseumFirstEncounter>([], true);
        return Task.CompletedTask;
    }

    public override Task Resume(AbstractRoom exitedRoom)
    {
        if (exitedRoom is CombatRoom { Encounter: ColosseumFirstEncounter } &&
            _stage is Stage.FirstCombat or Stage.Intermission)
            ShowIntermission();
        return Task.CompletedTask;
    }

    private void ShowIntermission()
    {
        _stage = Stage.Intermission;
        SetEventState(PageDescription("POST_FIRST"),
        [
            new(this, FightAgain, $"{Id.Entry}.pages.POST_FIRST.options.FIGHT", []),
            new(this, Flee, $"{Id.Entry}.pages.POST_FIRST.options.FLEE", []),
        ]);
    }

    private Task FightAgain()
    {
        if (_stage != Stage.Intermission) return Task.CompletedTask;
        _stage = Stage.SecondCombat;
        // Shared event RNG is identical for every participant. Its seed is
        // restored natively; include the visit so separate visits can differ.
        for (int i = 0; i < Owner!.RunState.TotalFloor; i++) Rng.NextInt(1);
        EncounterModel encounter = Rng.NextInt(3) switch
        {
            0 => ModelDb.Encounter<DecimillipedeElite>(),
            1 => ModelDb.Encounter<EntomancerElite>(),
            _ => ModelDb.Encounter<InfestedPrismsElite>(),
        };
#if STS2_PUBLIC
        EnterCombatWithoutExitingEvent(encounter.ToMutable(), [], false);
#else
        EnterCombatWithoutExitingEvent(encounter, [], false);
#endif
        return Task.CompletedTask;
    }

    private Task Flee()
    {
        if (_stage != Stage.Intermission) return Task.CompletedTask;
        _stage = Stage.Finished;
        SetEventFinished(PageDescription("FLEE"));
        return Task.CompletedTask;
    }

    internal static bool IsSecondCombat(CombatRoom room) => room.ParentEventId == ModelDb.Event<Colosseum>().Id &&
        room.Encounter is DecimillipedeElite or EntomancerElite or InfestedPrismsElite;
}
