namespace SharpOMatic.Engine.Interfaces;

public interface IModelCaller
{
    ModelFallbackFailure? ModelFallbackFailureOverride(Exception exception) => null;

    ModelRetryDecision? ModelRetryOverride(ModelRetryDecisionContext context) => null;

    Task<ModelCallResult> Call(
        Model model,
        ModelConfig modelConfig,
        Connector connector,
        ConnectorConfig connectorConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node,
        IModelCallProgressSink progressSink
    );
}
