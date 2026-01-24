namespace SharpOMatic.Engine.Services;

public class GoogleModelCaller : BaseModelCaller
{
    public override async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, ContextObject)> Call(
        Model model,
        ModelConfig modelConfig,
        Connector connector,
        ConnectorConfig connectorConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node)
    {
        throw new SharpOMaticException("GoogleModelCaller not implemented");
    }
}
