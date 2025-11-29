namespace SharpOMatic.Engine.Models;

public record class CodeCheckResultModel(DiagnosticSeverity Severity, int From, int To, string Id, string Message);

