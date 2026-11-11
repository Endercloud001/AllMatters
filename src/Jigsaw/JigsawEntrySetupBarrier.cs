using System.Text.Json;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using STS2RitsuLib.Networking.Sidecar;

namespace AllMatters.Jigsaw;

internal static class JigsawEntrySetupBarrier
{
    private sealed record Message(long Round, bool Released);
    private static readonly RitsuLibSidecarMessageDescriptor<Message> descriptor = new(
        "AllMatters", "jigsaw-entry-setup",
        message => JsonSerializer.SerializeToUtf8Bytes(message),
        bytes => JsonSerializer.Deserialize<Message>(bytes) ?? throw new InvalidDataException("Missing entry setup message."),
        RitsuLibSidecarDeliverySemantics.StableSync, true);

    internal static async Task Wait(INetGameService service, IReadOnlyCollection<ulong> active, long round)
    {
        var members = active.ToHashSet();
        if (!members.Contains(service.NetId))
            throw new InvalidOperationException("进入恢复的联机成员不完整。");
        bool host = service is NetHostGameService;
        ulong hostId = service switch
        {
            NetHostGameService => service.NetId,
            NetClientGameService client => client.HostNetId,
            _ => throw new InvalidOperationException("进入恢复需要联机传输。")
        };
        if (members.Count == 1)
        {
            if (!host)
                throw new InvalidOperationException("进入恢复缺少 Host。");
            return;
        }
        var received = new HashSet<ulong> { service.NetId };
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = RitsuLibSidecarTypedMessageRegistry.Subscribe(descriptor, context =>
        {
            if (context.Message.Round != round || !members.Contains(context.SenderNetId))
                return;
            if (host && !context.Message.Released)
            {
                received.Add(context.SenderNetId);
                if (members.All(received.Contains))
                    done.TrySetResult();
            }
            else if (!host && context.SenderNetId == hostId && context.Message.Released)
                done.TrySetResult();
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        if (host)
            await done.Task.WaitAsync(timeout.Token);
        else
        {
            while (!done.Task.IsCompleted)
            {
                if (!service.IsConnected)
                    throw new IOException("联机连接已断开，无法完成进入恢复。");
                RitsuLibSidecarTypedMessageRegistry.SendToHost(service, descriptor, new Message(round, false));
                await Task.WhenAny(done.Task, Task.Delay(250, timeout.Token));
                timeout.Token.ThrowIfCancellationRequested();
            }
            await done.Task;
        }
        if (host)
            foreach (ulong peer in members.Where(id => id != hostId))
                if (!RitsuLibSidecarTypedMessageRegistry.SendToPeer(service, peer, descriptor, new Message(round, true)))
                    throw new IOException("无法通知队友进入恢复就绪。");
    }
}
