namespace SharpOMatic.Engine.Interfaces;

public interface IRunNode
{
    Task<NodeExecutionResult> Execute(NodeExecutionRequest request);
}
