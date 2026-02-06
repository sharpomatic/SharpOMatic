namespace SharpOMatic.Engine.Services;

public class RunNodeFactory : IRunNodeFactory
{
    private readonly IReadOnlyDictionary<NodeType, Type> _nodeRunners;

    public RunNodeFactory()
    {
        _nodeRunners = typeof(RunNodeAttribute)
            .Assembly.GetTypes()
            .Where(t => typeof(IRunNode).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<RunNodeAttribute>() != null)
            .ToDictionary(t => t.GetCustomAttribute<RunNodeAttribute>()!.NodeType, t => t);
    }

    public IRunNode Create(ThreadContext threadContext, NodeEntity node)
    {
        if (!_nodeRunners.TryGetValue(node.NodeType, out var runnerType))
            throw new SharpOMaticException($"Unrecognized node type '{node.NodeType}'");

        return (IRunNode)
            ActivatorUtilities.CreateInstance(
                threadContext.ProcessContext.ServiceScope.ServiceProvider,
                runnerType,
                threadContext,
                node
            );
    }
}
