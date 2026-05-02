namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.EventTemplate)]
public sealed class EventTemplateNode(ThreadContext threadContext, EventTemplateNodeEntity node) : RunNode<EventTemplateNodeEntity>(threadContext, node)
{
    protected override async Task<NodeExecutionResult> RunInternal()
    {
        var expanded = await ContextHelpers.SubstituteValuesAsync(
            Node.Template ?? string.Empty,
            ThreadContext.NodeContext,
            ProcessContext.RepositoryService,
            ProcessContext.AssetStore,
            ProcessContext.Run.RunId,
            ProcessContext.Run.ConversationId
        );

        if (string.IsNullOrWhiteSpace(expanded))
            return NodeExecutionResult.Continue("Event template produced no output.", ResolveOptionalSingleOutput(ThreadContext));

        var helper = new StreamEventHelper(ProcessContext, ThreadContext.NodeContext);
        var messageId = $"event-template-{Guid.NewGuid():N}";

        switch (Node.OutputMode)
        {
            case EventTemplateOutputMode.Text:
                if (Node.TextRole == StreamMessageRole.Reasoning)
                    throw new SharpOMaticException("Event Template text output cannot use the Reasoning role.");

                await helper.AddTextMessageAsync(Node.TextRole, messageId, expanded, silent: Node.Silent);
                return NodeExecutionResult.Continue("Text event template emitted.", ResolveOptionalSingleOutput(ThreadContext));

            case EventTemplateOutputMode.Reasoning:
                await helper.AddReasoningMessageAsync(messageId, expanded, silent: Node.Silent);
                return NodeExecutionResult.Continue("Reasoning event template emitted.", ResolveOptionalSingleOutput(ThreadContext));

            default:
                throw new SharpOMaticException($"Unsupported Event Template output mode '{Node.OutputMode}'.");
        }
    }
}
