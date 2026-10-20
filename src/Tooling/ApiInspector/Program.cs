using System.Globalization;
using System.Reflection;

return Inspect(args);

static int Inspect(string[] arguments)
{
    try
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length)
            {
                throw new ArgumentException("Every option requires a value.");
            }
            options.Add(arguments[index], arguments[index + 1]);
        }
        string assemblyPath = Path.GetFullPath(options["--assembly"]);
        int limit = options.TryGetValue("--limit", out string? limitText) ? int.Parse(limitText, CultureInfo.InvariantCulture) : 40;
        if (limit < 1)
        {
            throw new ArgumentException("Limit must be positive.");
        }
        IEnumerable<string> assemblyPaths = Directory.EnumerateFiles(Path.GetDirectoryName(assemblyPath)!, "*.dll");
        if (options.TryGetValue("--dependency", out string? dependencyDirectory) && Directory.Exists(dependencyDirectory))
        {
            assemblyPaths = assemblyPaths.Concat(Directory.EnumerateFiles(dependencyDirectory, "*.dll"));
        }
        assemblyPaths = assemblyPaths.Concat(Directory.EnumerateFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll"))
            .Prepend(assemblyPath).DistinctBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
        // Metadata-only inspection never runs game static constructors or Godot native code.
        using var context = new MetadataLoadContext(new PathAssemblyResolver(assemblyPaths));
        Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);
        Console.WriteLine("Assembly: " + assemblyPath);
        if (options.TryGetValue("--find", out string? query))
        {
            string[] matches = assembly.GetTypes().Where(type => type.FullName != null && !type.FullName.Contains('<')
                    && type.FullName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(type => type.FullName!).Order(StringComparer.Ordinal).ToArray();
            foreach (string match in matches.Take(limit))
            {
                Console.WriteLine(match);
            }
            Console.WriteLine($"Matches: {matches.Length}; shown: {Math.Min(limit, matches.Length)}");
            return matches.Length == 0 ? 3 : 0;
        }
        string typeName = options["--type"];
        Type type = assembly.GetType(typeName, throwOnError: true)!;
        string memberQuery = options.GetValueOrDefault("--member", "");
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var lines = new List<(string Name, string Declaration)>();
        foreach (ConstructorInfo constructor in type.GetConstructors(flags).Where(Visible))
        {
            lines.Add((".ctor", $"{Access(constructor)} {type.Name}({Parameters(constructor)})"));
        }
        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            MethodInfo? accessor = new[] { property.GetMethod, property.SetMethod }
                .FirstOrDefault(method => method != null && Visible(method));
            if (accessor == null || !Visible(accessor)) continue;
            string accessors = PropertyAccessor(property.GetMethod, accessor, "get") + PropertyAccessor(property.SetMethod, accessor, "set");
            lines.Add((property.Name, $"{Access(accessor)} {FormatType(property.PropertyType)} {property.Name} {{ {accessors}}}"));
        }
        foreach (MethodInfo method in type.GetMethods(flags).Where(method => !method.IsSpecialName && Visible(method)))
        {
            string generics = method.IsGenericMethod ? "<" + string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name)) + ">" : "";
            string constraints = string.Concat(method.GetGenericArguments().Select(GenericConstraint));
            lines.Add((method.Name, $"{Access(method)} {FormatType(method.ReturnType)} {method.Name}{generics}({Parameters(method)}){constraints}"));
        }
        foreach (FieldInfo field in type.GetFields(flags).Where(field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly))
        {
            lines.Add((field.Name, $"{(field.IsPublic ? "public" : "protected")} {(field.IsStatic ? "static " : "")}{FormatType(field.FieldType)} {field.Name}"));
        }
        foreach (EventInfo eventInfo in type.GetEvents(flags))
        {
            if (eventInfo.AddMethod is { } add && Visible(add))
            {
                lines.Add((eventInfo.Name, $"{Access(add)} event {FormatType(eventInfo.EventHandlerType!)} {eventInfo.Name}"));
            }
        }
        var selected = lines.Where(line => line.Name.Contains(memberQuery, StringComparison.OrdinalIgnoreCase))
            .OrderBy(line => line.Name, StringComparer.Ordinal).ThenBy(line => line.Declaration, StringComparer.Ordinal).ToArray();
        Console.WriteLine("Type: " + type.FullName);
        foreach (var line in selected.Take(limit))
        {
            Console.WriteLine(line.Declaration);
        }
        Console.WriteLine($"Members: {selected.Length}; shown: {Math.Min(limit, selected.Length)} (declared public/protected only; nullable reference annotations omitted)");
        return selected.Length == 0 ? 3 : 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine("API inspection failed: " + exception.Message);
        return 2;
    }
}

static bool Visible(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

static string Access(MethodBase method)
{
    string visibility = method.IsPublic ? "public" : method.IsFamilyOrAssembly ? "protected internal" : "protected";
    return visibility + (method.IsStatic ? " static" : "");
}

static string PropertyAccessor(MethodInfo? method, MethodInfo primary, string operation)
{
    if (method == null) return "";
    string visibility = method.IsPrivate ? "private" : method.IsAssembly ? "internal"
        : method.IsPublic ? "public" : method.IsFamilyOrAssembly ? "protected internal" : "protected";
    string primaryVisibility = primary.IsPublic ? "public" : primary.IsFamilyOrAssembly ? "protected internal" : "protected";
    return (visibility == primaryVisibility ? "" : visibility + " ") + operation + "; ";
}

static string FormatType(Type type)
{
    if (type.IsByRef) return FormatType(type.GetElementType()!) + "&";
    if (type.IsPointer) return FormatType(type.GetElementType()!) + "*";
    if (type.IsArray) return FormatType(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
    if (type.IsGenericType)
    {
        return type.Name.Split('`')[0] + "<" + string.Join(", ", type.GetGenericArguments().Select(FormatType)) + ">";
    }
    return type.FullName switch
    {
        "System.Void" => "void", "System.Boolean" => "bool", "System.Int32" => "int", "System.String" => "string",
        "System.Decimal" => "decimal", "System.Single" => "float", "System.Double" => "double", "System.Object" => "object",
        _ => type.Name
    };
}

static string Parameters(MethodBase method) => string.Join(", ", method.GetParameters().Select(parameter =>
{
    string modifier = parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? "ref " : "";
    Type type = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
    string value = "";
    if (parameter.HasDefaultValue)
    {
        object? raw = parameter.RawDefaultValue;
        value = " = " + (raw switch
        {
            null => "null", bool boolean => boolean ? "true" : "false", string text => "\"" + text.Replace("\"", "\\\"") + "\"",
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture), _ => "default"
        });
    }
    return modifier + FormatType(type) + " " + parameter.Name + value;
}));

static string GenericConstraint(Type argument)
{
    var constraints = new List<string>();
    var attributes = argument.GenericParameterAttributes;
    if (attributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint)) constraints.Add("class");
    if (attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint)) constraints.Add("struct");
    constraints.AddRange(argument.GetGenericParameterConstraints().Where(type => type.FullName != "System.ValueType").Select(FormatType));
    if (attributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint)
        && !attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint)) constraints.Add("new()");
    return constraints.Count == 0 ? "" : " where " + argument.Name + " : " + string.Join(", ", constraints);
}
