namespace SharpOMatic.Engine.Helpers;

public class StreamEventHelper
{
    private readonly ProcessContext _processContext;

    public StreamEventHelper(ProcessContext processContext)
    {
        _processContext = processContext ?? throw new ArgumentNullException(nameof(processContext));
    }

    public Task<List<StreamEvent>> AddTextStartAsync(StreamMessageRole role, string messageId, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextStart,
                MessageId = RequireMessageId(messageId),
                MessageRole = role,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddTextContentAsync(string messageId, string delta, string? metadata = null, bool silent = false)
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
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddTextEndAsync(string messageId, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextEnd,
                MessageId = RequireMessageId(messageId),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public async Task AddTextMessageAsync(StreamMessageRole role, string messageId, string text, string? metadata = null, bool silent = false)
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
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextContent,
                MessageId = messageId,
                TextDelta = text,
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.TextEnd,
                MessageId = messageId,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddReasoningStartAsync(string messageId, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningStart,
                MessageId = RequireMessageId(messageId),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddReasoningMessageStartAsync(string messageId, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningMessageStart,
                MessageId = RequireMessageId(messageId),
                MessageRole = StreamMessageRole.Reasoning,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddReasoningMessageContentAsync(string messageId, string delta, string? metadata = null, bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(delta))
            throw new SharpOMaticException("Reasoning message delta cannot be empty or whitespace.");

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningMessageContent,
                MessageId = RequireMessageId(messageId),
                TextDelta = delta,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddReasoningMessageEndAsync(string messageId, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningMessageEnd,
                MessageId = RequireMessageId(messageId),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddReasoningEndAsync(string messageId, string? metadata = null, bool silent = false)
    {
        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningEnd,
                MessageId = RequireMessageId(messageId),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public async Task AddReasoningMessageAsync(string messageId, string text, string? metadata = null, bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new SharpOMaticException("Reasoning message cannot be empty or whitespace.");

        messageId = RequireMessageId(messageId);

        await AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningStart,
                MessageId = messageId,
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningMessageStart,
                MessageId = messageId,
                MessageRole = StreamMessageRole.Reasoning,
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningMessageContent,
                MessageId = messageId,
                TextDelta = text,
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningMessageEnd,
                MessageId = messageId,
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ReasoningEnd,
                MessageId = messageId,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddToolCallStartAsync(string toolCallId, string toolName, string? parentMessageId = null, string? metadata = null, bool silent = false)
    {
        toolCallId = RequireToolCallId(toolCallId);

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallStart,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = RequireNonEmpty(toolName, "Tool call name cannot be empty or whitespace."),
                ParentMessageId = NormalizeOptionalId(parentMessageId),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddToolCallArgsAsync(string toolCallId, string args, string? metadata = null, bool silent = false)
    {
        toolCallId = RequireToolCallId(toolCallId);

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallArgs,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = RequireNonEmpty(args, "Tool call args cannot be empty or whitespace."),
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddToolCallEndAsync(string toolCallId, string? metadata = null, bool silent = false)
    {
        toolCallId = RequireToolCallId(toolCallId);

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallEnd,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public Task<List<StreamEvent>> AddToolCallResultAsync(string resultMessageId, string toolCallId, string result, string? metadata = null, bool silent = false)
    {
        if (result is null)
            throw new SharpOMaticException("Tool call result cannot be null.");

        return AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallResult,
                MessageId = RequireToolResultMessageId(resultMessageId),
                ToolCallId = RequireToolCallId(toolCallId),
                TextDelta = result,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public async Task AddToolCallAsync(string toolCallId, string toolName, string args, string? parentMessageId = null, string? metadata = null, bool silent = false)
    {
        toolCallId = RequireToolCallId(toolCallId);

        await AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallStart,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = RequireNonEmpty(toolName, "Tool call name cannot be empty or whitespace."),
                ParentMessageId = NormalizeOptionalId(parentMessageId),
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallArgs,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = RequireNonEmpty(args, "Tool call args cannot be empty or whitespace."),
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallEnd,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                Metadata = metadata,
                Silent = silent,
            }
        );
    }

    public async Task AddToolCallWithResultAsync(string toolCallId, string toolName, string args, string resultMessageId, string result, string? parentMessageId = null, string? metadata = null, bool silent = false)
    {
        if (result is null)
            throw new SharpOMaticException("Tool call result cannot be null.");

        toolCallId = RequireToolCallId(toolCallId);
        resultMessageId = RequireToolResultMessageId(resultMessageId);

        await AddEventsAsync(
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallStart,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = RequireNonEmpty(toolName, "Tool call name cannot be empty or whitespace."),
                ParentMessageId = NormalizeOptionalId(parentMessageId),
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallArgs,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                TextDelta = RequireNonEmpty(args, "Tool call args cannot be empty or whitespace."),
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallEnd,
                MessageId = toolCallId,
                ToolCallId = toolCallId,
                Metadata = metadata,
                Silent = silent,
            },
            new StreamEventWrite()
            {
                EventKind = StreamEventKind.ToolCallResult,
                MessageId = resultMessageId,
                ToolCallId = toolCallId,
                TextDelta = result,
                Metadata = metadata,
                Silent = silent,
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

    private static string RequireToolCallId(string toolCallId)
    {
        if (string.IsNullOrWhiteSpace(toolCallId))
            throw new SharpOMaticException("ToolCallId must be a non-empty string.");

        return toolCallId;
    }

    private static string RequireToolResultMessageId(string resultMessageId)
    {
        if (string.IsNullOrWhiteSpace(resultMessageId))
            throw new SharpOMaticException("Tool call result message id must be a non-empty string.");

        return resultMessageId;
    }

    private static string RequireNonEmpty(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new SharpOMaticException(message);

        return value;
    }

    private static string? NormalizeOptionalId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
