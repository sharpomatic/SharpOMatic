namespace SharpOMatic.Engine.Nodes;

public class SwitchNode(RunContext runContext, SwitchNodeEntity node)
    : RunNode<SwitchNodeEntity>(runContext, node)
{
    public override async Task<NodeEntity?> Run()
    {
        await base.Run();

        try
        {
            int? matchingIndex = null;

            // Check each switch that has linked code
            for (int i = 0; i < Node.Switches.Length; i++)
            {
                var switcher = Node.Switches[i];

                if (!string.IsNullOrWhiteSpace(switcher.Code))
                {
                    var options = ScriptOptions.Default
                                      .WithImports("System", "System.Threading.Tasks", "SharpOMatic.Engine.Contexts")
                                      .WithReferences(typeof(Task).Assembly, typeof(ContextObject).Assembly);

                    try
                    {
                        var result = await CSharpScript.EvaluateAsync(switcher.Code, options, new ScriptCodeContext() { Context = RunContext.NodeContext }, typeof(ScriptCodeContext));
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
            var nextNode = RunContext.ResolveOutput(Node, Node.Outputs[matchingIndex.Value]);

            Trace.NodeStatus = NodeStatus.Success;
            Trace.Finished = DateTime.Now;
            Trace.Message = $"Switched to {Node.Switches[matchingIndex.Value].Name}";
            Trace.OutputContext = RunContext.TypedSerialization(RunContext.NodeContext);
            await NodeUpdated();

            return nextNode;
        }
        catch (Exception ex)
        {
            Trace.NodeStatus = NodeStatus.Failed;
            Trace.Finished = DateTime.Now;
            Trace.Message = "Failed";
            Trace.Error = ex.Message;
            Trace.OutputContext = RunContext.TypedSerialization(RunContext.NodeContext);
            await NodeUpdated();

            throw;
        }
    }
}
