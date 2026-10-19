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
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            string pack = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "GameResourcePack").Value!;
            if (!ProjectSettings.LoadResourcePack(Path.GetFullPath(pack), replaceFiles: false))
            {
                throw new Exception("Cannot mount the beta game's resources for headless tests.");
            }
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
