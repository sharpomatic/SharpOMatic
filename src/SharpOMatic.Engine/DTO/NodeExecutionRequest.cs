namespace SharpOMatic.Engine.DTO;

public sealed class NodeExecutionRequest
{
    public required NodeInvocationKind InvocationKind { get; init; }
    public NodeResumeInput? ResumeInput { get; init; }
}
