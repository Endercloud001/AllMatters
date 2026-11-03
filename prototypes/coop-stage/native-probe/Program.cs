using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.RunData;
using STS2RitsuLib.Networking.Sidecar;

// Executed in a separate .NET process, no Godot/Steam bootstrap or game save paths.
class Snapshot { public string Phase {get;set;}="自动"; public int Budget {get;set;}=0; public string Map {get;set;}="determined"; public string Destination {get;set;}="shop"; public Dictionary<string,int> Masks {get;set;}=new(){{"A",1},{"B",1},{"C",1}}; public int Round {get;set;}=6; public int[] Counts {get;set;}=[11,3,8]; }
class Program {
 static void Main(string[] args) {
  var roots=args.Select(Path.GetFullPath).ToArray();
  System.Runtime.Loader.AssemblyLoadContext.Default.Resolving+=(ctx,name)=>{foreach(var root in roots){var p=Path.Combine(root,name.Name+".dll");if(File.Exists(p))return ctx.LoadFromAssemblyPath(p);}return null;};
  Run();
 }
 [MethodImpl(MethodImplOptions.NoInlining)] static void Run(){
  Console.WriteLine("runtime="+Environment.Version);
  Console.WriteLine("ritsu="+typeof(RunSavedDataStore).Assembly.Location);
  Console.WriteLine("game="+typeof(RunState).Assembly.Location+"; product="+System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(RunState).Assembly.Location).ProductVersion);
  var service=typeof(RunState).Assembly.GetType("MegaCrit.Sts2.Core.Multiplayer.Game.INetGameService",true)!;
  Console.WriteLine("native message API="+string.Join(",",service.GetMethods().Where(m=>m.Name.Contains("Message")).Select(m=>m.Name)));
  var rest=typeof(RunState).Assembly.GetType("MegaCrit.Sts2.Core.Multiplayer.Game.RestSiteSynchronizer",true)!;
  Console.WriteLine("native AfterAllRestSitesCompleted="+(rest.GetMethod("AfterAllRestSitesCompleted")!=null));
  var slot=RunSavedDataStore.For("allmatters-prototype68").Register<Snapshot>("checkpoint");
  var state=(RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
  slot.Set(state,new Snapshot());
  var registry=typeof(RunSavedDataStore).Assembly.GetType("STS2RitsuLib.RunData.RunSavedDataRegistry",true)!;
  // Internal reflection is probe-only; no commitment to an integration entry point.
  var payload=(string)registry.GetMethod("BuildPayload",BindingFlags.Static|BindingFlags.Public)!.Invoke(null,[state])!;
  var scratch=Path.Combine(Path.GetTempPath(),"PROTOTYPE-ritsu-wipe-me-"+Guid.NewGuid());Directory.CreateDirectory(scratch);
  File.WriteAllText(Path.Combine(scratch,"checkpoint.json"),payload);
  var other=(RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
  registry.GetMethod("ImportPayloadIntoRun",BindingFlags.Static|BindingFlags.Public)!.Invoke(null,[other,File.ReadAllText(Path.Combine(scratch,"checkpoint.json"))]);
  var actual=slot.Get(other);if(JsonSerializer.Serialize(actual)!=JsonSerializer.Serialize(slot.Get(state)))throw new Exception("slot roundtrip mismatch");
  Console.WriteLine("PASS actual RitsuLib slot payload disk roundtrip; scratch="+scratch);
  var serializer=new RitsuLibSidecarJsonSerializer<Snapshot>();
  var descriptor=new RitsuLibSidecarMessageDescriptor<Snapshot>("allmatters-prototype68","snapshot",serializer.Serialize,serializer.Deserialize);
  if(JsonSerializer.Serialize(serializer.Deserialize(serializer.Serialize(actual)))!=JsonSerializer.Serialize(actual))throw new Exception("message codec mismatch");
  Console.WriteLine("PASS actual message codec; transport not invoked");
  if(Environment.GetEnvironmentVariable("PROTOTYPE_TRY_REGISTRATION")!="1")return;
  var code=RitsuLibSidecarTypedMessageRegistry.Register(descriptor);
  if(RitsuLibSidecarTypedMessageRegistry.Register(descriptor)!=code)throw new Exception("registration changed");
  using var subscription=RitsuLibSidecarTypedMessageRegistry.Subscribe(descriptor,ctx=>{});
  if(JsonSerializer.Serialize(serializer.Deserialize(serializer.Serialize(actual)))!=JsonSerializer.Serialize(actual))throw new Exception("message codec mismatch");
  Console.WriteLine("PASS actual descriptor registration/subscription/disposal and message codec; NO network transport invoked");
 }
}
