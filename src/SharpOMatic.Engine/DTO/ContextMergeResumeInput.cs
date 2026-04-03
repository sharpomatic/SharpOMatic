namespace SharpOMatic.Engine.DTO;

public sealed class ContextMergeResumeInput : NodeResumeInput
{
    public required ContextObject Context { get; init; }
}
