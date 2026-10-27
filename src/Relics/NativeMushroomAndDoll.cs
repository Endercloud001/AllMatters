using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Relics;

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_STRANGE_MUSHROOM")]
public sealed class StrangeMushroom : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/strange_mushroom.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/strange_mushroom.png",
        BigIconPath: "res://AllMatters/images/relics/strange_mushroom.png");
}

// Change Vulnerable's starting multiplier, before its native relic/power
// adjustments. Unpowered damage and other targets still bypass Vulnerable.
[HarmonyPatch(typeof(VulnerablePower), nameof(VulnerablePower.ModifyDamageMultiplicative))]
internal static class StrangeMushroomVulnerable
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var getter = AccessTools.PropertyGetter(typeof(MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar), "BaseValue");
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(getter)) continue;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return CodeInstruction.Call(typeof(StrangeMushroomVulnerable), nameof(StartingMultiplier));
        }
    }
    private static decimal StartingMultiplier(decimal original, VulnerablePower power)
        => power.Owner.Player?.Relics.OfType<StrangeMushroom>().Any() == true ? original - .25m : original;
}

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_DU_VU_DOLL")]
public sealed class DuVuDoll : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/du_vu_doll.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/du_vu_doll.png",
        BigIconPath: "res://AllMatters/images/relics/du_vu_doll.png");
    public override async Task BeforeCombatStart()
    {
        int curses = Owner.Deck.Cards.Count(c => c.Type == CardType.Curse);
        if (curses > 0) await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, curses, Owner.Creature, null);
    }
}
