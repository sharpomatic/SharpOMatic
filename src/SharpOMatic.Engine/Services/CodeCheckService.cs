namespace SharpOMatic.Engine.Services;

public class CodeCheckService(IScriptOptionsService ScriptOptionsService) : ICodeCheck
{
    public Task<List<CodeCheckResult>> CodeCheck(CodeCheckRequest request)
    {
        List<CodeCheckResult> results = [];

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var options = ScriptOptionsService.GetScriptOptions();

            try
            {
                Script script = CSharpScript.Create(
                    request.Code,
                    options,
                    globalsType: typeof(ScriptCodeContext)
                );
                Compilation compilation = script.GetCompilation();
                ImmutableArray<Diagnostic> diagnostics = compilation.GetDiagnostics();
                foreach (
                    Diagnostic diagnostic in diagnostics.Where(d =>
                        d.Severity == DiagnosticSeverity.Error
                        || d.Severity == DiagnosticSeverity.Warning
                    )
                )
                    results.Add(
                        new CodeCheckResult(
                            diagnostic.Severity,
                            diagnostic.Location.SourceSpan.Start,
                            diagnostic.Location.SourceSpan.End,
                            diagnostic.Id,
                            diagnostic.GetMessage()
                        )
                    );
            }
            catch (CompilationErrorException ex)
            {
                foreach (
                    Diagnostic diagnostic in ex.Diagnostics.Where(d =>
                        d.Severity == DiagnosticSeverity.Error
                        || d.Severity == DiagnosticSeverity.Warning
                    )
                )
                    results.Add(
                        new CodeCheckResult(
                            diagnostic.Severity,
                            diagnostic.Location.SourceSpan.Start,
                            diagnostic.Location.SourceSpan.End,
                            diagnostic.Id,
                            diagnostic.GetMessage()
                        )
                    );
            }
        }

        return Task.FromResult(results);
    }
}
