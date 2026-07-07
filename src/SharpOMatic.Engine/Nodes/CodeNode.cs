namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Code)]
public class CodeNode(ThreadContext threadContext, CodeNodeEntity node) : RunNode<CodeNodeEntity>(threadContext, node)
{
    protected override async Task<NodeExecutionResult> RunInternal()
    {
        if (!string.IsNullOrWhiteSpace(Node.Code))
        {
            var globals = new CodeNodeScriptContext()
            {
                Context = ThreadContext.NodeContext,
                ServiceProvider = ProcessContext.ServiceScope.ServiceProvider,
                Assets = new AssetHelper(ProcessContext.RepositoryService, ProcessContext.AssetStore, ProcessContext.Run.RunId, ProcessContext.Run.ConversationId),
                Templates = new TemplateHelper(ThreadContext.NodeContext, ProcessContext.RepositoryService, ProcessContext.AssetStore, ProcessContext.Run.RunId, ProcessContext.Run.ConversationId),
                Debug = new DebugInformationHelper(Trace, Informations),
                Events = new StreamEventHelper(ProcessContext, ThreadContext.NodeContext),
            };

            try
            {
                // Compiled once and cached per (code, globals type); reused on every execution.
                var runner = ProcessContext.ScriptOptionsService.GetScriptRunner(Node.Code, typeof(CodeNodeScriptContext));
                var result = await runner(globals);
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

        return NodeExecutionResult.Continue("Code executed", ResolveOptionalSingleOutput(ThreadContext));
    }
}
