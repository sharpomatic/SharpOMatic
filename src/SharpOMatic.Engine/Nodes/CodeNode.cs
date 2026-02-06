namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Code)]
public class CodeNode(ThreadContext threadContext, CodeNodeEntity node) : RunNode<CodeNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        if (!string.IsNullOrWhiteSpace(Node.Code))
        {
            var options = ProcessContext.ScriptOptionsService.GetScriptOptions();
            var globals = new ScriptCodeContext()
            {
                Context = ThreadContext.NodeContext,
                ServiceProvider = ProcessContext.ServiceScope.ServiceProvider,
                Assets = new AssetHelper(ProcessContext.RepositoryService, ProcessContext.AssetStore, ProcessContext.Run.RunId),
            };

            try
            {
                var result = await CSharpScript.RunAsync(Node.Code, options, globals, typeof(ScriptCodeContext));
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
            catch (Exception e3)
            {
                StringBuilder sb = new();
                sb.AppendLine($"Code node failed during execution.\n");
                sb.Append(e3.Message);
                throw new SharpOMaticException(sb.ToString());
            }
        }

        return ("Code executed", ResolveOptionalSingleOutput(ThreadContext));
    }
}
