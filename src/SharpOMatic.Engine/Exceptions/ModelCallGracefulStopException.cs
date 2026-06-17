namespace SharpOMatic.Engine.Exceptions;

public sealed class ModelCallGracefulStopException(string message) : Exception(message)
{
    public ModelCallGracefulStopException()
        : this("Model call stopped gracefully.") { }
}
