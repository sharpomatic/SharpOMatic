namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Switch)]
public class SwitchNode(ThreadContext threadContext, SwitchNodeEntity node)
    : RunNode<SwitchNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        var lastIndex = Node.Switches.Length - 1;
        if (!IsOutputConnected(Node.Outputs[lastIndex]))
            throw new SharpOMaticException("Switch node must have an output connection on the last output connector.");

        var globals = new ScriptCodeContext()
        {
            Context = ThreadContext.NodeContext,
            ServiceProvider = ProcessContext.ServiceScope.ServiceProvider,
            Assets = new AssetHelper(ProcessContext.RepositoryService, ProcessContext.AssetStore, ProcessContext.Run.RunId)
        };

        // Check each switch that has linked code
        for (int i = 0; i < Node.Switches.Length; i++)
        {
            var switcher = Node.Switches[i];

            if (!string.IsNullOrWhiteSpace(switcher.Code))
            {
                var options = ProcessContext.ScriptOptionsService.GetScriptOptions();

                try
                {
                    var result = await CSharpScript.EvaluateAsync(switcher.Code, options, globals, typeof(ScriptCodeContext));
                    if (result is null)
                        throw new SharpOMaticException($"Switch node entry '{switcher.Name}' returned null instead of a boolean value.");

                    if (result is not bool)
                        throw new SharpOMaticException($"Switch node entry '{switcher.Name}' return type '{result.GetType()}' instead of a boolean value.");

                    if ((bool)result)
                    {
                        if (!IsOutputConnected(Node.Outputs[i]))
                            continue;

                        return ($"Switched to {switcher.Name}", [new NextNodeData(ThreadContext, WorkflowContext.ResolveOutput(Node.Outputs[i]))]);
                    }
                }
                catch (CompilationErrorException e1)
                {
                    // Return the first 3 errors only
                    StringBuilder sb = new();
                    sb.AppendLine($"Switch node entry '{switcher.Name}' failed compilation.\n");
                    foreach (var diagnostic in e1.Diagnostics.Take(3))
                        sb.AppendLine(diagnostic.ToString());

                    throw new SharpOMaticException(sb.ToString());
                }
                catch (InvalidOperationException e2)
                {
                    StringBuilder sb = new();
                    sb.AppendLine($"Switch node entry '{switcher.Name}' failed during execution.\n");
                    sb.Append(e2.Message);
                    throw new SharpOMaticException(sb.ToString());
                }
            }
        }

        return ($"Switched to {Node.Switches[lastIndex].Name}", [new NextNodeData(ThreadContext, WorkflowContext.ResolveOutput(Node.Outputs[lastIndex]))]);
    }
}
