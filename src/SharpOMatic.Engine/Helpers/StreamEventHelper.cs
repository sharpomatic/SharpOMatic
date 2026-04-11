namespace SharpOMatic.Engine.Helpers;

public class StreamEventHelper
{
    private readonly ProcessContext _processContext;

    public StreamEventHelper(ProcessContext processContext)
    {
        _processContext = processContext ?? throw new ArgumentNullException(nameof(processContext));
    }

    public Task<List<StreamEvent>> AddTextStartAsync(StreamMessageRole role, string messageId, string? metadata = null)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextStart,
                MessageId = RequireMessageId(messageId),
                MessageRole = role,
                Metadata = metadata,
            }
        );
    }

    public Task<List<StreamEvent>> AddTextContentAsync(string messageId, string delta, string? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(delta))
            throw new SharpOMaticException("Text content delta cannot be empty or whitespace.");

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextContent,
                MessageId = RequireMessageId(messageId),
                TextDelta = delta,
                Metadata = metadata,
            }
        );
    }

    public Task<List<StreamEvent>> AddTextEndAsync(string messageId, string? metadata = null)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextEnd,
                MessageId = RequireMessageId(messageId),
                Metadata = metadata,
            }
        );
    }

    public async Task AddTextMessageAsync(StreamMessageRole role, string messageId, string text, string? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new SharpOMaticException("Text message cannot be empty or whitespace.");

        messageId = RequireMessageId(messageId);

        await AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextStart,
                MessageId = messageId,
                MessageRole = role,
                Metadata = metadata,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextContent,
                MessageId = messageId,
                TextDelta = text,
                Metadata = metadata,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextEnd,
                MessageId = messageId,
                Metadata = metadata,
            }
        );
    }

    private Task<List<StreamEvent>> AddEventsAsync(params StreamEventWrite[] events)
    {
        return _processContext.AppendStreamEvents(events);
    }

    private static string RequireMessageId(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            throw new SharpOMaticException("MessageId must be a non-empty string.");

        return messageId;
    }
}
