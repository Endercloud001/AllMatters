using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Jigsaw;

// FromSerializable runs before native setup owns the new network service. Only
// activate the validated map after that asynchronous setup has finished.
[HarmonyPatch(typeof(RunManager), nameof(RunManager.SetUpSavedMultiplayer))]
internal static class JigsawMultiplayerRestore
{
    private static readonly ConditionalWeakTable<RunState, LoadRunLobby> loading = new();

    private static void Prefix(RunState state, LoadRunLobby lobby, out bool __state)
    {
        __state = false;
        if (!JigsawSaveSource.IsJigsaw(lobby.Run) && !JigsawCheckpoint.Slot.TryGet(state, out _))
            return;
        loading.Add(state, lobby);
        __state = true;
    }

    private static void Postfix(ref Task __result, RunState state, LoadRunLobby lobby, bool __state)
    {
        if (__state)
            __result = Finish(__result, state, lobby);
    }

    private static async Task Finish(Task setup, RunState state, LoadRunLobby lobby)
    {
        try
        {
            await setup;
            JigsawNativeSession.Restore(state, JigsawSaveSource.IsJigsaw(lobby.Run), lobby.Run);
        }
        finally { loading.Remove(state); }
    }

    private static void Finalizer(Exception? __exception, RunState state, bool __state)
    {
        if (__exception != null && __state)
            loading.Remove(state);
    }

#if STS2_PUBLIC
    internal static IEnumerable<ulong>? ConnectedPlayers(IPlayerCollection players) =>
        players is RunState state && loading.TryGetValue(state, out var lobby) ? lobby.ConnectedPlayerIds : null;
#endif
}

#if STS2_PUBLIC
// 0.107.1 initializes RunLobby from every saved character. Keep those characters
// in RunState, while giving RunLobby the actual membership of the load lobby.
[HarmonyPatch(typeof(RunLobby), MethodType.Constructor,
    [typeof(GameMode),
     typeof(MegaCrit.Sts2.Core.Multiplayer.Game.INetGameService), typeof(IRunLobbyListener),
     typeof(IPlayerCollection), typeof(IEnumerable<ulong>)])]
internal static class JigsawLoadedLobbyPlayers
{
    private static void Prefix(IPlayerCollection playerCollection, ref IEnumerable<ulong> connectedPlayerIds)
    {
        if (JigsawMultiplayerRestore.ConnectedPlayers(playerCollection) is { } connected)
            connectedPlayerIds = connected.ToArray();
    }
}
#endif
