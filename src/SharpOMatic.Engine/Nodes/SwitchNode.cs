namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Switch)]
public class SwitchNode(ThreadContext threadContext, SwitchNodeEntity node)
    : RunNode<SwitchNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        int? matchingIndex = null;

        // Check each switch that has linked code
        for (int i = 0; i < Node.Switches.Length; i++)
        {
            var switcher = Node.Switches[i];

            if (!string.IsNullOrWhiteSpace(switcher.Code))
            {
                var options = RunContext.ScriptOptionsService.GetScriptOptions();

                try
                {
                    var result = await CSharpScript.EvaluateAsync(switcher.Code, options, new ScriptCodeContext() { Context = ThreadContext.NodeContext }, typeof(ScriptCodeContext));
                    if (result is null)
                        throw new SharpOMaticException($"Switch node entry '{switcher.Name}' returned null instead of a boolean value.");

                    if (result is not bool)
                        throw new SharpOMaticException($"Switch node entry '{switcher.Name}' return type '{result.GetType()}' instead of a boolean value.");

                    if ((bool)result)
                    {
                        matchingIndex = i;
                        break;
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

        matchingIndex ??= Node.Switches.Length - 1;
        return ($"Switched to {Node.Switches[matchingIndex.Value].Name}", [new NextNodeData(ThreadContext, RunContext.ResolveSingleOutput(Node))]);
    }
}
