namespace SharpOMatic.Engine.Nodes;

public class CodeNode(RunContext runContext, CodeNodeEntity node)
    : RunNode<CodeNodeEntity>(runContext, node)
{
    public override async Task<NodeEntity?> Run()
    {
        await base.Run();

        try
        {
            if (!string.IsNullOrWhiteSpace(Node.Code))
            {
                var options = ScriptOptions.Default
                                  .WithImports("System", "System.Threading.Tasks", "SharpOMatic.Engine.Contexts")
                                  .WithReferences(typeof(Task).Assembly, typeof(ContextObject).Assembly);

                try
                {
                    var result = await CSharpScript.RunAsync(Node.Code, options, new ScriptCodeContext() { Context = RunContext.NodeContext }, typeof(ScriptCodeContext));
                }
                catch (CompilationErrorException e1)
                {
                    // Return the first 3 errors only
                    StringBuilder sb = new();
                    sb.AppendLine($"Code node failed compilation.\n");
                    foreach (var diagnostic in e1.Diagnostics.Take(3))
                        sb.AppendLine(diagnostic.ToString());

                    throw new SharpOMaticException(sb.ToString());
                }
                catch (InvalidOperationException e2)
                {
                    StringBuilder sb = new();
                    sb.AppendLine($"Code node failed during execution.\n");
                    sb.Append(e2.Message);
                    throw new SharpOMaticException(sb.ToString());
                }
            }

            var nextNode = RunContext.ResolveSingleOutput(Node);

            Trace.NodeStatus = NodeStatus.Success;
            Trace.Finished = DateTime.Now;
            Trace.Message = "Code executed";
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
