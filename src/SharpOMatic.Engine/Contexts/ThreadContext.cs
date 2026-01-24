
namespace SharpOMatic.Engine.Contexts;

public class ThreadContext(ProcessContext processContext, ExecutionContext currentContext, ContextObject nodeContext)
{

    public ProcessContext ProcessContext { get; } = processContext;
    public ExecutionContext CurrentContext { get; set; } = currentContext;
    public ContextObject NodeContext { get; set; } = nodeContext;
    public int ThreadId { get; } = processContext.GetNextThreadId();
    public Guid NodeId { get; set; }
    public int? BatchIndex { get; set; }
    public WorkflowContext WorkflowContext => CurrentContext.GetWorkflowContext();
}
