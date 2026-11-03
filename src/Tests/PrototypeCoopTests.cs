// THROWAWAY #68 probe. Only retained on prototype/coop-stage-recovery.
using System.Reflection;
using System.Text.Json;
using STS2RitsuLib.RunData;
using STS2RitsuLib.Networking.Sidecar;

internal static class PrototypeCoopTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [ ("PrototypeCoop descriptor registration and codec under Godot", Registration),
      ("PrototypeCoop saved slot payload with initialized test run", Save) ];

    public sealed class Snapshot
    {
        public string Phase { get; set; } = "generating";
        public int Entry { get; set; } = 2;
        public string Destination { get; set; } = "shop";
        public int Budget { get; set; } = 0;
        public int[] Masks { get; set; } = [1, 1, 1];
        public int[] Counts { get; set; } = [11, 3, 8];
    }

    private static Task Registration()
    {
        var codec = new RitsuLibSidecarJsonSerializer<Snapshot>();
        var descriptor = new RitsuLibSidecarMessageDescriptor<Snapshot>(
            "allmatters-prototype68-godot", "checkpoint", codec.Serialize, codec.Deserialize);
        var first = RitsuLibSidecarTypedMessageRegistry.Register(descriptor);
        if (first != RitsuLibSidecarTypedMessageRegistry.Register(descriptor)) throw new Exception("Repeated registration changed identity");
        using var subscription = RitsuLibSidecarTypedMessageRegistry.Subscribe(descriptor, context => { });
        var input = new Snapshot();
        if (JsonSerializer.Serialize(input) != JsonSerializer.Serialize(codec.Deserialize(codec.Serialize(input))))
            throw new Exception("Actual codec mismatch");
        Console.WriteLine("PrototypeCoop: descriptor registered/subscribed; codec roundtrip; NO native transport invoked");
        return Task.CompletedTask;
    }

    private static Task Save()
    {
        var slot = RunSavedDataStore.For("allmatters-prototype68-godot").Register<Snapshot>("checkpoint");
        var run = HeadlessTestSetup.PlayerWithRun("PROTOTYPE68_SAVE").RunState;
        slot.Set((MegaCrit.Sts2.Core.Runs.RunState)run, new Snapshot());
        var registry = typeof(RunSavedDataStore).Assembly.GetType("STS2RitsuLib.RunData.RunSavedDataRegistry", true)!;
        var payload = (string)registry.GetMethod("BuildPayload", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, [run])!;
        var scratch = Path.Combine(Path.GetTempPath(), "PROTOTYPE68-Godot-wipe-me-" + Guid.NewGuid());
        Directory.CreateDirectory(scratch);
        File.WriteAllText(Path.Combine(scratch, "checkpoint.json"), payload);
        var restored = HeadlessTestSetup.PlayerWithRun("PROTOTYPE68_RESTORE").RunState;
        registry.GetMethod("ImportPayloadIntoRun", BindingFlags.Static | BindingFlags.Public)!.Invoke(null,
            [restored, File.ReadAllText(Path.Combine(scratch, "checkpoint.json"))]);
        if (JsonSerializer.Serialize(slot.Get((MegaCrit.Sts2.Core.Runs.RunState)restored)) != JsonSerializer.Serialize(new Snapshot()))
            throw new Exception("Actual slot mismatch");
        Console.WriteLine("PrototypeCoop: initialized run slot payload roundtrip; scratch=" + scratch);
        return Task.CompletedTask;
    }
}
