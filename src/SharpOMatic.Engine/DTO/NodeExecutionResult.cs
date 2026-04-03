namespace SharpOMatic.Engine.DTO;

public sealed class NodeExecutionResult
{
    public required NodeExecutionOutcome Outcome { get; init; }
    public required string Message { get; init; }
    public required List<NextNodeData> NextNodes { get; init; }
    public string? ResumeStateJson { get; init; }

    public static NodeExecutionResult Continue(string message, List<NextNodeData> nextNodes)
    {
        return new NodeExecutionResult()
        {
            Outcome = NodeExecutionOutcome.Continue,
            Message = message,
            NextNodes = nextNodes,
        };
    }

    public static NodeExecutionResult Suspend(string message, string? resumeStateJson = null)
    {
        return new NodeExecutionResult()
        {
            Outcome = NodeExecutionOutcome.Suspend,
            Message = message,
            NextNodes = [],
            ResumeStateJson = resumeStateJson,
        };
    }
}
