namespace SharpOMatic.Engine.Helpers;

public class StreamEventHelper
{
    private readonly ProcessContext _processContext;

    public StreamEventHelper(ProcessContext processContext)
    {
        _processContext = processContext ?? throw new ArgumentNullException(nameof(processContext));
    }

    public Guid CreateMessageId()
    {
        return Guid.NewGuid();
    }

    public Task<List<StreamEvent>> AddTextStartAsync(StreamMessageRole role, Guid messageId, string? metadata = null)
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

    public Task<List<StreamEvent>> AddTextContentAsync(Guid messageId, string delta, string? metadata = null)
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

    public Task<List<StreamEvent>> AddTextEndAsync(Guid messageId, string? metadata = null)
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

    public async Task<Guid> AddTextMessageAsync(StreamMessageRole role, string text, string? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new SharpOMaticException("Text message cannot be empty or whitespace.");

        var messageId = Guid.NewGuid();

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

        return messageId;
    }

    private Task<List<StreamEvent>> AddEventsAsync(params StreamEventWrite[] events)
    {
        return _processContext.AppendStreamEvents(events);
    }

    private static Guid RequireMessageId(Guid messageId)
    {
        if (messageId == Guid.Empty)
            throw new SharpOMaticException("MessageId must be a non-empty GUID.");

        return messageId;
    }
}
