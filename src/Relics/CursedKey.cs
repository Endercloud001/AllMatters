using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace AllMatters.Relics;

[RegisterRelic(typeof(EventRelicPool), FullPublicEntry = "ALL_MATTERS_CURSED_KEY")]
public sealed class CursedKey : ModRelicTemplate
{
    private TreasureRoom? _lastOpened;
    public override RelicRarity Rarity => RelicRarity.Event;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://AllMatters/images/relics/cursed_key.png",
        IconOutlinePath: "res://AllMatters/images/relics/outlines/cursed_key.png",
        BigIconPath: "res://AllMatters/images/relics/cursed_key.png");

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
        => player == Owner ? PlayerCmd.GainEnergy(1, Owner) : Task.CompletedTask;

    internal async Task Open(TreasureRoom room)
    {
        if (ReferenceEquals(_lastOpened, room)) return;
        _lastOpened = room;
        if (Owner.PlayerRng.Rewards.NextInt(2) != 0) return;
        var curses = ModelDb.CardPool<CurseCardPool>().GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.CanBeGeneratedByModifiers).ToArray();
        if (curses.Length == 0) return;
        var curse = curses[Owner.PlayerRng.Rewards.NextInt(curses.Length)];
        await CardPileCmd.AddCursesToDeck([curse], Owner);
    }
}

[HarmonyPatch(typeof(OneOffSynchronizer), "DoTreasureRoomRewards")]
internal static class CursedKeyChestOpened
{
    private static void Postfix(Player player, ref Task<int> __result)
    {
        if (player.RunState.CurrentRoom is not TreasureRoom room) return;
        var key = player.Relics.OfType<CursedKey>().FirstOrDefault();
        if (key != null) __result = OpenAfterGold(__result, key, room);
    }
    private static async Task<int> OpenAfterGold(Task<int> original, CursedKey key, TreasureRoom room)
    {
        int gold = await original;
        await key.Open(room);
        return gold;
    }
}
