
namespace SharpOMatic.Engine.Contexts;

public class ThreadContext
{
    public ThreadContext(ProcessContext processContext, ExecutionContext currentContext, ContextObject nodeContext)
    {
        ProcessContext = processContext;
        CurrentContext = currentContext;
        NodeContext = nodeContext;
        ThreadId = processContext.GetNextThreadId();
    }

    public ProcessContext ProcessContext { get; }
    public ExecutionContext CurrentContext { get; set; }
    public ContextObject NodeContext { get; set; }
    public int ThreadId { get; }
    public Guid NodeId { get; set; }
    public int? BatchIndex { get; set; }
    public WorkflowContext WorkflowContext => CurrentContext.GetWorkflowContext();
}
