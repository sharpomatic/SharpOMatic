namespace SharpOMatic.Engine.DTO;

public record class CodeCheckResult(DiagnosticSeverity Severity, int From, int To, string Id, string Message);
