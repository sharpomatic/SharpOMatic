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

    // Compiled scripts are cached per (code, globalsType). Lazy with ExecutionAndPublication guarantees the
    // (expensive) Roslyn compilation runs at most once per key, even under concurrent access from many threads.
    // It also caches a thrown exception, so GetScriptRunner evicts entries that failed for a transient reason.
    private readonly ConcurrentDictionary<(string Code, Type GlobalsType), Lazy<ScriptRunner<object>>> _runnerCache = new();
    private readonly Func<string, Type, ScriptRunner<object>> _compile;

    public ScriptOptionsService(IEnumerable<Assembly> assemblies, IEnumerable<string> imports)
        : this(assemblies, imports, null) { }

    // The compile step is replaceable so tests can simulate a transient compile failure.
    internal ScriptOptionsService(IEnumerable<Assembly> assemblies, IEnumerable<string> imports, Func<string, Type, ScriptRunner<object>>? compile)
    {
        _compile = compile ?? CompileWithRoslyn;

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

    public ScriptRunner<object> GetScriptRunner(string code, Type globalsType)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(globalsType);

        var cacheKey = (code, globalsType);
        var lazy = _runnerCache.GetOrAdd(
            cacheKey,
            key => new Lazy<ScriptRunner<object>>(() => _compile(key.Code, key.GlobalsType), LazyThreadSafetyMode.ExecutionAndPublication)
        );

        try
        {
            return lazy.Value;
        }
        catch (Exception ex) when (ex is not CompilationErrorException)
        {
            // A compilation error is deterministic, so the cached failure stays and bad code is not recompiled on
            // every run. Anything else (e.g. OutOfMemoryException) is transient: evict so the next call retries.
            // Removing the exact Lazy leaves any newer entry for the same key alone.
            _runnerCache.TryRemove(new KeyValuePair<(string Code, Type GlobalsType), Lazy<ScriptRunner<object>>>(cacheKey, lazy));
            throw;
        }
    }

    private ScriptRunner<object> CompileWithRoslyn(string code, Type globalsType) => CSharpScript.Create<object>(code, _options, globalsType).CreateDelegate();
}
