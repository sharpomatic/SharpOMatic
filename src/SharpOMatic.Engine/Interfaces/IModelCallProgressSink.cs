namespace SharpOMatic.Engine.Interfaces;

public interface IModelCallProgressSink
{
    Task OnTextStartAsync(string messageId);
    Task OnTextDeltaAsync(string messageId, string textDelta);
    Task OnTextEndAsync(string messageId);
    Task OnReasoningAsync(string reasoningId, string text);
    Task OnToolCallAsync(string toolCallId, string? toolName, string? argsSnapshot = null, string? parentMessageId = null, string? data = null);
    Task OnToolCallResultAsync(string messageId, string toolCallId, string content);
    Task CompleteAsync();
    Task PersistAsync();
}
