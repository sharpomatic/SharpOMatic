namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.StepEnd)]
public sealed class StepEndNode(ThreadContext threadContext, StepEndNodeEntity node) : RunNode<StepEndNodeEntity>(threadContext, node)
{
    protected override async Task<NodeExecutionResult> RunInternal()
    {
        var stepName = RequireStepName(Node.StepName);

        await AppendStreamEvents(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.StepEnd,
                TextDelta = stepName,
            }
        );

        return NodeExecutionResult.Continue($"Step end '{stepName}' emitted.", ResolveOptionalSingleOutput(ThreadContext));
    }

    private static string RequireStepName(string stepName)
    {
        if (string.IsNullOrWhiteSpace(stepName))
            throw new SharpOMaticException("Step name must be a non-empty string.");

        return stepName;
    }
}
