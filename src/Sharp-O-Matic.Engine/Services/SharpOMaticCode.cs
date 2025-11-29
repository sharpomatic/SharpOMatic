namespace SharpOMatic.Engine.Services;

public class SharpOMaticCode : ISharpOMaticCode
{
    private static readonly List<Assembly> s_assemblies = [];
    private static readonly List<MetadataReference> s_metadataReference = [];
    private static readonly List<string> s_excludeNamespace = ["Internal", "Microsoft.Win32.SafeHandles", "Microsoft.CodeAnalysis"];
    private static readonly List<string> s_namespaces = [];

    static SharpOMaticCode()
    {
        s_assemblies = [.. AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))];
        s_metadataReference = [.. s_assemblies.Select(a => a.Location).Select(l => MetadataReference.CreateFromFile(l)).Cast<MetadataReference>()];
        s_namespaces = GetNamespacesFromAssemblies();
    }

    public Task<List<CodeCheckResultModel>> CodeCheck(CodeCheckRequestModel request)
    {
        List<CodeCheckResultModel> results = [];

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var options = ScriptOptions.Default.AddReferences(s_assemblies).AddImports(s_namespaces);

            try
            {
                Script script = CSharpScript.Create(request.Code, options, globalsType: typeof(ScriptCodeContext));
                Compilation compilation = script.GetCompilation();
                ImmutableArray<Diagnostic> diagnostics = compilation.GetDiagnostics();
                foreach (Diagnostic diagnostic in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning))
                    results.Add(new CodeCheckResultModel(diagnostic.Severity,
                                                         diagnostic.Location.SourceSpan.Start,
                                                         diagnostic.Location.SourceSpan.End,
                                                         diagnostic.Id,
                                                         diagnostic.GetMessage()));
            }
            catch (CompilationErrorException ex)
            {
                foreach (Diagnostic diagnostic in ex.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning))
                    results.Add(new CodeCheckResultModel(diagnostic.Severity,
                                                         diagnostic.Location.SourceSpan.Start,
                                                         diagnostic.Location.SourceSpan.End,
                                                         diagnostic.Id,
                                                         diagnostic.GetMessage()));
            }
        }

        return Task.FromResult(results);
    }

    private static List<string> GetNamespacesFromAssemblies()
    {
        var namespaces = new HashSet<string>();

        foreach (var assembly in s_assemblies)
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                if (!string.IsNullOrEmpty(type.Namespace))
                {
                    bool isExcluded = false;
                    foreach (var excluded in s_excludeNamespace)
                        if (type.Namespace.StartsWith(excluded))
                        {
                            isExcluded = true;
                            break;
                        }

                    if (!isExcluded)
                        namespaces.Add(type.Namespace);
                }
            }
        }

        return namespaces.ToList();
    }
}
