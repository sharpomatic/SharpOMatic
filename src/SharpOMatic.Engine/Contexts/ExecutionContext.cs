namespace SharpOMatic.Engine.Contexts;

public abstract class ExecutionContext
{
    protected ExecutionContext(ExecutionContext? parent)
    {
        Parent = parent;
        if (parent is null)
        {
            if (this is ProcessContext processContext)
                RootProcess = processContext;
            else
                throw new SharpOMaticException("Execution context requires a process context.");
        }
        else
        {
            RootProcess = parent.RootProcess;
            RootProcess.TrackContext(this);
        }
    }

    public ExecutionContext? Parent { get; }
    public ProcessContext RootProcess { get; }

    public WorkflowContext GetWorkflowContext()
    {
        var current = this;
        while (true)
        {
            if (current is WorkflowContext workflowContext)
                return workflowContext;

            if (current.Parent is null)
                throw new SharpOMaticException("Execution context is missing a workflow context.");

            current = current.Parent;
        }
    }
}
