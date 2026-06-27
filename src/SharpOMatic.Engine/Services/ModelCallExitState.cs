namespace SharpOMatic.Engine.Services;

public sealed class ModelCallExitState
{
    public ContextObject? Context { get; private set; }
    public string ContextPath { get; private set; } = "exit";

    public void Capture(ModelCallExitException exception)
    {
        if (exception.Context is null)
            return;

        Context = exception.Context;
        ContextPath = exception.ContextPath;
    }
}
