namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.StepStart)]
public sealed class StepStartNode(ThreadContext threadContext, StepStartNodeEntity node) : RunNode<StepStartNodeEntity>(threadContext, node)
{
    protected override async Task<NodeExecutionResult> RunInternal()
    {
        var stepName = RequireStepName(Node.StepName);

        await AppendStreamEvents(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.StepStart,
                TextDelta = stepName,
            }
        );

        return NodeExecutionResult.Continue($"Step start '{stepName}' emitted.", ResolveOptionalSingleOutput(ThreadContext));
    }

    private static string RequireStepName(string stepName)
    {
        if (string.IsNullOrWhiteSpace(stepName))
            throw new SharpOMaticException("Step name must be a non-empty string.");

        return stepName;
    }
}
