using System.Reflection;
using System.Runtime.Loader;
using Godot;

public partial class TestRunner : Node
{
    public override async void _Ready()
    {
        GetTree().CreateTimer(60).Timeout += () =>
        {
            Console.Error.WriteLine("FAIL: Test run timed out.");
            GetTree().Quit(1);
        };
        try
        {
            string directory = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "DependencyDirectory").Value!;
            AssemblyLoadContext.GetLoadContext(typeof(TestRunner).Assembly)!.Resolving += (_, name) =>
            {
                string path = Path.Combine(directory, name.Name + ".dll");
                if (File.Exists(path)) return AssemblyLoadContext.GetLoadContext(typeof(TestRunner).Assembly)!
                    .LoadFromAssemblyPath(Path.GetFullPath(path));
                string bundle = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
                    .Single(attribute => attribute.Key == "RitsuLibDirectory").Value!;
                string target = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
                    .Single(attribute => attribute.Key == "RitsuLibTarget").Value!;
                foreach (string folder in new[] { Path.Combine(bundle, "compat", target), Path.Combine(bundle, "shared") })
                {
                    path = Path.Combine(folder, name.Name + ".dll");
                    if (File.Exists(path)) return AssemblyLoadContext.GetLoadContext(typeof(TestRunner).Assembly)!
                        .LoadFromAssemblyPath(Path.GetFullPath(path));
                }
                return null;
            };
            string pack = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "GameResourcePack").Value!;
            if (!ProjectSettings.LoadResourcePack(Path.GetFullPath(pack), replaceFiles: false))
            {
                throw new Exception("Cannot mount the beta game's resources for headless tests.");
            }
            string modPack = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "ModResourcePack").Value!;
            if (!ProjectSettings.LoadResourcePack(Path.GetFullPath(modPack), replaceFiles: false))
                throw new Exception("Build the AllMatters resource pack before running event tests.");
            int result = await HelloWorldTests.Run(OS.GetCmdlineUserArgs());
            GetTree().Quit(result);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            GetTree().Quit(1);
        }
    }
}
