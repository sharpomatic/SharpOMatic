namespace SharpOMatic.Engine.DTO;

public sealed class AgUiAgentResumeInput : NodeResumeInput
{
    public required ContextObject Agent { get; init; }
    public ContextObject? Context { get; init; }
}
