// SPDX-License-Identifier: GPL-3.0-only
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;

namespace AllMatters.Monsters;

public abstract class BanditMonster : ModMonsterTemplate
{
    // Balance's scenes have Node2D roots. Convert through the existing RitsuLib
    // factory so the native creature code receives NCreatureVisuals and its slots.
    protected override NCreatureVisuals? TryCreateCreatureVisuals() =>
        RitsuGodotNodeFactories.CreateFromScene<NCreatureVisuals>(
            ResourceLoader.Load<PackedScene>(CustomVisualsPath!));
}
