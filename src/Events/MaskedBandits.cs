// SPDX-License-Identifier: GPL-3.0-only
// Adapted from MT-SUPER-POWER/STS2-Balance-MOD; upstream credits ActsFromThePast.
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Scaffolding.Content;
using AllMatters.Encounters;
using AllMatters.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;

namespace AllMatters.Events;

// ======================== 红面具强盗事件 ========================

/// <summary>
/// STS1-EVENT — 红面具三人帮：面对强盗团伙，交钱或开战。
/// 参考 ActsFromThePast.Acts.TheCity.Events.MaskedBandits 移植。
/// </summary>
[RegisterActEvent(typeof(Hive))]
public sealed class MaskedBandits : ModEventTemplate
{
  private static readonly LocString EmptyDescription =
    new("events", "ALL_MATTERS_EVENT_MASKED_BANDITS.pages.EMPTY.description");

  private NSpeechBubbleVfx? _speechBubble;

  public override bool IsShared => true;

  public override EventLayoutType LayoutType => EventLayoutType.Combat;

  public override EncounterModel CanonicalEncounter => ModelDb.Encounter<RedMaskBandits>();

  /// <summary>
  /// 第 3 层（城市）且未持有红面具时触发
  /// </summary>
  public override bool IsAllowed(IRunState runState)
  {
    if (runState.CurrentActIndex != 1)
      return false;         // NOTE: 第 2 幕（索引 1）
    if (runState.TotalFloor < 23)
      return false;              // NOTE: 至少第 23 层
    return !runState.Players.Any(p => p.Relics.Any(r => r is RedMask));
  }

  protected override IReadOnlyList<EventOption> GenerateInitialOptions()
  {
    return
    [
      new(this, Pay, InitialOptionKey("PAY"), []),
      new(this, Fight, InitialOptionKey("FIGHT"), []),
    ];
  }

  private async Task Pay()
  {
    Player? owner = Owner;
    if (owner == null)
      return;

    int goldToLose = owner.Gold;
    if (goldToLose > 0)
      await PlayerCmd.LoseGold(goldToLose, owner, GoldLossType.Stolen);

    SetEventState(EmptyDescription,
    [
      new EventOption(this, Paid2, $"{Id.Entry}.pages.PAID_1.options.CONTINUE", []),
    ]);
    PlayPaidLine<Pointy>("PAID_1");
  }

  private Task Paid2()
  {
    SetEventState(EmptyDescription,
    [
      new EventOption(this, Paid3, $"{Id.Entry}.pages.PAID_2.options.CONTINUE", []),
    ]);
    PlayPaidLine<Romeo>("PAID_2");
    return Task.CompletedTask;
  }

  private Task Paid3()
  {
    SetEventFinished(EmptyDescription);
    PlayPaidLine<Romeo>("PAID_3");
    return Task.CompletedTask;
  }

  private void PlayPaidLine<TMonster>(string pageKey) where TMonster : MonsterModel
  {
    if (!LocalContext.IsMe(Owner))
      return;

    if (_speechBubble != null)
    {
      _ = _speechBubble.AnimOut();
      _speechBubble = null;
    }

    Creature? speaker = FindCreature<TMonster>();
    if (speaker == null)
      return;

    _speechBubble = TalkCmd.Play(PageDescription(pageKey), speaker, VfxColor.Red, VfxDuration.Forever);
  }

  private static Creature? FindCreature<TMonster>() where TMonster : MonsterModel
  {
    return NCombatRoom.Instance?.CreatureNodes
      .FirstOrDefault(n => n.Entity.Monster is TMonster)
      ?.Entity;
  }

  private Task Fight()
  {
    Player? owner = Owner;
    if (owner == null)
      return Task.CompletedTask;

    var rewards = new List<Reward>();
#if STS2_PUBLIC
    // 0.107.1 creates the room only from the local event instance; it does not
    // collect rewards from the other players' instances as 0.111.0 does.
    foreach (var participant in owner.RunState.Players)
#else
    foreach (var participant in new[] { owner })
#endif
    {
      rewards.Add(new GoldReward(25, 35, participant));
      rewards.Add(new RelicReward(ModelDb.Relic<RedMask>().ToMutable(), participant));
    }
    EnterCombatWithoutExitingEvent<RedMaskBandits>(rewards, false);
    return Task.CompletedTask;
  }
}
