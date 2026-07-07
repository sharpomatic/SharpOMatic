namespace SharpOMatic.Engine.Interfaces;

public interface IScriptOptionsService
{
    IReadOnlyCollection<Assembly> GetAssemblies();
    IReadOnlyCollection<string> GetImports();
    ScriptOptions GetScriptOptions();

    /// <summary>
    /// Returns a compiled, cached <see cref="ScriptRunner{T}"/> for the given script <paramref name="code"/> and
    /// <paramref name="globalsType"/>. The script is compiled with Roslyn exactly once per distinct
    /// (code, globalsType) pair and the runner is reused for all subsequent executions — avoiding the very
    /// expensive per-execution recompilation (and the non-collectible dynamic-assembly leak) that
    /// <c>CSharpScript.RunAsync</c>/<c>EvaluateAsync</c> incur when called with a raw source string each time.
    /// The returned runner is thread-safe to invoke concurrently; pass fresh globals on each call.
    /// Compilation errors surface as <see cref="CompilationErrorException"/> from this call.
    /// </summary>
    ScriptRunner<object> GetScriptRunner(string code, Type globalsType);
}
