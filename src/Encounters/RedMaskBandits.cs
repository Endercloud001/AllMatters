// SPDX-License-Identifier: GPL-3.0-only
// Adapted from MT-SUPER-POWER/STS2-Balance-MOD.
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Scaffolding.Content;

using AllMatters.Monsters;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;

namespace AllMatters.Encounters;

// ======================== 红面具三人帮遭遇 ========================

/// <summary>
/// STS1-EVENT — 红面具强盗遭遇战。
/// 包含 Pointy（尖头）、Romeo（罗密欧）、Bear（熊）三个怪物。
/// 未注册到任何 Act 的遭遇池，仅通过 MaskedBandits 事件触发。
/// </summary>
[RegisterGlobalEncounter]
public sealed class RedMaskBandits : ModEncounterTemplate
{
  public override RoomType RoomType => RoomType.Monster;

  public override bool IsValidForAct(ActModel act) => false;

  public override bool IsWeak => false;

  public override EncounterAssetProfile AssetProfile => new(
    EncounterScenePath: "res://AllMatters/scenes/red_mask_bandits.tscn");

  public override IReadOnlyList<string> Slots => ["pointy", "romeo", "bear"];

  public override IEnumerable<MonsterModel> AllPossibleMonsters =>
  [
    ModelDb.Monster<Pointy>(),
    ModelDb.Monster<Romeo>(),
    ModelDb.Monster<Bear>(),
  ];

  protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
  [
    (ModelDb.Monster<Pointy>().ToMutable(), "pointy"),
    (ModelDb.Monster<Romeo>().ToMutable(), "romeo"),
    (ModelDb.Monster<Bear>().ToMutable(), "bear"),
  ];
}
