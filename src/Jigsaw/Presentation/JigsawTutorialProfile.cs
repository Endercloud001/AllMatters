using STS2RitsuLib.Data;
using STS2RitsuLib.Utils.Persistence;
namespace AllMatters.Jigsaw.Presentation;

public interface IJigsawTutorialProfile { int Sentence { get; set; } }
public sealed class MemoryJigsawTutorialProfile : IJigsawTutorialProfile { public int Sentence { get; set; } }
public sealed class JigsawTutorialData { public int Sentence { get; set; } }
/// <summary>Only the local profile owns tutorial progress; run restoration cannot roll it back.</summary>
public sealed class JigsawTutorialProfile : IJigsawTutorialProfile
{
 private static readonly ModDataStore store = ModDataStore.For(ModEntry.ModId);
 private static bool registered;
 public static void Register()
 {
  if (!registered) { store.Register<JigsawTutorialData>("jigsaw_tutorial", "jigsaw_tutorial", SaveScope.Profile); registered = true; }
 }
 public JigsawTutorialProfile()
 {
  Register(); store.InitializeProfileScoped(); store.ReloadIfPathChanged();
 }
 public int Sentence { get => store.Get<JigsawTutorialData>("jigsaw_tutorial").Sentence; set { store.Get<JigsawTutorialData>("jigsaw_tutorial").Sentence = value; store.Save("jigsaw_tutorial"); } }
}
