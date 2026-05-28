namespace SharpOMatic.Engine.Interfaces;

public interface IModelCaller
{
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
