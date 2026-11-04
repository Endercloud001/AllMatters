// THROWAWAY isolated startup-order regression. No game/Steam/user saves.
// Runs before the usual test host injects models; native ModelDb.Init remains intact.
using System.Reflection;
using System.Runtime.Loader;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;

internal static class PrototypeHuntingStartupTests
{
    internal static async Task<int> Run(string mode)
    {
        TestMode.IsOn=true;
        SaveManager.Instance.InitSettingsDataForTest();SaveManager.Instance.InitPrefsDataForTest();
        await ModManager.Initialize(null!,null,null);
        STS2RitsuLib.RitsuLibFramework.Initialize();
#if STS2_PUBLIC
        const string target="public";
#else
        const string target="public-beta";
#endif
        var pack=Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="ModResourcePack").Value!;
        var root=Path.GetDirectoryName(pack)!;
        var names=mode.EndsWith("/queen",StringComparison.Ordinal)?new[]{"Queen"}:new[]{"FiveWorm","Treasure","Shop","Rest","Queen","Presentation"};
        var assemblies=new List<Assembly>();
        Environment.SetEnvironmentVariable("ALLMATTERS_HUNTING_RESOURCE_PACK",Path.GetFullPath(Path.Combine(root,"../.local/resource-search/ActsFromThePast.pck")));
        foreach(string name in names)
        {
            string id="AllMattersHunting"+name+"Probe";
            var path=Path.GetFullPath(Path.Combine(root,target,id,id+".dll"));
            var assembly=AssemblyLoadContext.GetLoadContext(typeof(TestRunner).Assembly)!.LoadFromAssemblyPath(path);assemblies.Add(assembly);
            using(var stream=File.OpenRead(Path.ChangeExtension(path,".json")))
            {
                var manifest=ModManifest.ReadFromStream(stream,out var errors)!;
                if(errors?.Count>0)throw new Exception($"Invalid manifest: {id}");
                var mod=Activator.CreateInstance<Mod>();mod.path=Path.GetDirectoryName(path)!;mod.manifest=manifest;mod.state=ModLoadState.Loaded;
                var plural=AccessTools.Field(typeof(Mod),"assemblies");
                if(plural!=null)((List<Assembly>)plural.GetValue(mod)).Add(assembly);
                else AccessTools.Field(typeof(Mod),"assembly").SetValue(mod,assembly);
                ((List<Mod>)AccessTools.Field(typeof(ModManager),"_mods").GetValue(null)).Add(mod);
            }
            foreach(var type in assembly.GetTypes())
            {
                var entry=type.GetCustomAttribute<ModInitializerAttribute>();
                if(entry!=null)type.GetMethod(entry.initializerMethod)!.Invoke(null,null);
            }
            Console.WriteLine($"STARTUP_LOADED target={target} mod={id} initializerBeforeModelDb=true");
        }
        // Supply the exact loaded-assembly scan input without scanning installed mods.
        AccessTools.Field(typeof(ReflectionHelper),"_modTypes").SetValue(null,assemblies.SelectMany(a=>a.GetTypes()).ToArray());
#if !STS2_PUBLIC
        // The host has no ModManager assembly list. Refresh only type discovery,
        // not model instances, after supplying the actual loaded DLL types.
        AccessTools.Field(typeof(ModelDb),"_allAbstractModelSubtypes").SetValue(null,null);
        AssemblyInfo.Init();
#endif
        LocManager.Initialize();
        Console.WriteLine($"STARTUP_NATIVE_INIT target={target} mods={assemblies.Count} mode={mode}");
        var beforeTypes=assemblies.SelectMany(a=>a.GetTypes()).Where(t=>!t.IsAbstract&&t.IsSubclassOf(typeof(AbstractModel))).ToArray();
        foreach(var t in beforeTypes)Console.WriteLine($"STARTUP_MODEL type={t.FullName} alreadyPresent={ModelDb.Contains(t)} nativeScan={ModelDb.AllAbstractModelSubtypes.Contains(t)}");
#if STS2_PUBLIC
        ModelDb.Init();
#else
        ModelDb.Init();
#endif
        MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache.Init();
        ModelDb.InitIds();
        var models=assemblies.SelectMany(a=>a.GetTypes()).Where(t=>!t.IsAbstract&&t.IsSubclassOf(typeof(AbstractModel))).ToArray();
        foreach(var type in models)
            if(!ModelDb.Contains(type))throw new Exception($"Native startup did not register {type.FullName}");
        Console.WriteLine($"PASS: PrototypeHuntingStartup target={target} mods={assemblies.Count} models={models.Length} nativeModelDbInit/cache/ids=completed; no installed mods scan, no game startup/Steam/save/real UI claim");
        return 0;
    }
}
