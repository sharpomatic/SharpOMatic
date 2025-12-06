namespace SharpOMatic.Engine.DataTransferObjects;

public record class CodeCheckResult(DiagnosticSeverity Severity, int From, int To, string Id, string Message);

