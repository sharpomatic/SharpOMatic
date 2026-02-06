namespace SharpOMatic.Engine.Services;

public class ScriptOptionsService : IScriptOptionsService
{
    private static readonly string[] s_defaultImports =
    [
        "System",
        "System.Collections",
        "System.Collections.Generic",
        "System.Linq",
        "System.Text.Json",
        "System.Net.Http",
        "System.IO",
        "System.Threading.Tasks",
        "Microsoft.Extensions.AI",
        "Microsoft.Extensions.DependencyInjection",
        "SharpOMatic.Engine.Interfaces",
        "SharpOMatic.Engine.Contexts",
        "SharpOMatic.Engine.FastSerializer",
        "SharpOMatic.Engine.Enumerations",
        "SharpOMatic.Engine.Helpers",
    ];

    private static readonly Assembly[] s_defaultAssemblies =
    [
        typeof(Task).Assembly,
        typeof(Enumerable).Assembly,
        typeof(List<>).Assembly,
        typeof(ArrayList).Assembly,
        typeof(JsonSerializer).Assembly,
        typeof(HttpClient).Assembly,
        typeof(File).Assembly,
        typeof(ContextObject).Assembly,
        typeof(ChatMessage).Assembly,
    ];

    private readonly IReadOnlyCollection<Assembly> _assemblies;
    private readonly IReadOnlyCollection<string> _imports;
    private readonly ScriptOptions _options;

    public ScriptOptionsService(IEnumerable<Assembly> assemblies, IEnumerable<string> imports)
    {
        var assemblySet = new HashSet<Assembly>(s_defaultAssemblies);
        foreach (var assembly in assemblies)
            if (assembly is not null)
                assemblySet.Add(assembly);

        var importSet = new HashSet<string>(s_defaultImports, StringComparer.Ordinal);
        foreach (var import in imports)
        {
            if (string.IsNullOrWhiteSpace(import))
                continue;

            importSet.Add(import.Trim());
        }

        _assemblies = assemblySet.ToArray();
        _imports = importSet.ToArray();
        _options = ScriptOptions.Default.WithReferences(_assemblies).WithImports(_imports);
    }

    public IReadOnlyCollection<Assembly> GetAssemblies() => _assemblies;

    public IReadOnlyCollection<string> GetImports() => _imports;

    public ScriptOptions GetScriptOptions() => _options;
}
