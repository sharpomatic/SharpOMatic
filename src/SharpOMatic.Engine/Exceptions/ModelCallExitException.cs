namespace SharpOMatic.Engine.Exceptions;

public sealed class ModelCallExitException(ContextObject? context = null, string contextPath = "exit")
    : Exception("Model call exited.")
{
    public ContextObject? Context { get; } = context;

    public string ContextPath { get; } = string.IsNullOrWhiteSpace(contextPath)
        ? "exit"
        : contextPath.Trim();
}
