var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors();
builder.Services.AddControllers();

// --------- SharpOMatic Specific Start ------------------------
//
// Assets are stored in the current users profile
builder.Services.Configure<FileSystemAssetStoreOptions>(builder.Configuration.GetSection("AssetStorage:FileSystem"));
builder.Services.AddSingleton<IAssetStore, FileSystemAssetStore>();

// Provide the controllers and signalr needed by the visual editor
// This and the MapSharpOMaticEditor call go together, use both or neither
builder.Services.AddSharpOMaticEditor();

// Provide the controller needed for data transfer (export/import)
// You might want to be able to import even without the visual editor
builder.Services.AddSharpOMaticTransfer();

// Setup the engine and its capabilties
builder
    .Services.AddSharpOMaticEngine()
    .AddSchemaTypes(typeof(SchemaExample))
    .AddToolMethods(ToolCalling.GetGreeting, ToolCalling.GetTime)
    .AddScriptOptions([typeof(CodeExample).Assembly], ["SharpOMatic.DemoServer"])
    .AddJsonConverters(typeof(ClassExampleConverter))
    .AddSqliteRepository(connectionString: $"Data Source={Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sharpomatic.db")}");

// Custom implementation to track when workflows are completed
builder.Services.AddSingleton<IProgressService, ProgressService>();

//
// --------- SharpOMatic Specific End --------------------------

var app = builder.Build();

app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

// SAMPLE
app.MapPost("/agui", async (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Append("X-Accel-Buffering", "no");
    context.Response.ContentType = "text/event-stream";

    string threadId = Guid.NewGuid().ToString("N");
    string runId = Guid.NewGuid().ToString("N");

    if (context.Request.ContentLength is > 0)
    {
        using var requestBody = await JsonDocument.ParseAsync(context.Request.Body);
        if (requestBody.RootElement.TryGetProperty("threadId", out var threadIdElement) &&
            threadIdElement.ValueKind == JsonValueKind.String)
        {
            threadId = threadIdElement.GetString() ?? threadId;
        }

        if (requestBody.RootElement.TryGetProperty("runId", out var runIdElement) &&
            runIdElement.ValueKind == JsonValueKind.String)
        {
            runId = runIdElement.GetString() ?? runId;
        }
    }

    string messageId = $"msg-{runId}";
    string firstReasoningMessageId = $"reasoning-1-{runId}";
    string secondReasoningMessageId = $"reasoning-2-{runId}";
    string toolCallId = $"tool-{runId}";
    string toolResultMessageId = $"tool-result-{runId}";

    await WriteEventAsync(context, new
    {
        type = "RUN_STARTED",
        threadId,
        runId
    });

    await WriteEventAsync(context, new
    {
        type = "REASONING_START",
        messageId = firstReasoningMessageId
    });

    await WriteEventAsync(context, new
    {
        type = "REASONING_MESSAGE_START",
        messageId = firstReasoningMessageId,
        role = "reasoning"
    });

    await WriteEventAsync(context, new
    {
        type = "REASONING_MESSAGE_CONTENT",
        messageId = firstReasoningMessageId,
        delta = "I should call a tool before answering."
    });

    await Task.Delay(3000);

    await WriteEventAsync(context, new
    {
        type = "REASONING_MESSAGE_END",
        messageId = firstReasoningMessageId
    });

    await WriteEventAsync(context, new
    {
        type = "REASONING_END",
        messageId = firstReasoningMessageId
    });

    await WriteEventAsync(context, new
    {
        type = "TOOL_CALL_START",
        toolCallId,
        toolCallName = "get_weather"
    });

    await WriteEventAsync(context, new
    {
        type = "TOOL_CALL_ARGS",
        toolCallId,
        delta = "{\"location\":\"Sydney\"}"
    });

    await WriteEventAsync(context, new
    {
        type = "TOOL_CALL_END",
        toolCallId
    });

    await Task.Delay(3000);

    await WriteEventAsync(context, new
    {
        type = "TOOL_CALL_RESULT",
        messageId = toolResultMessageId,
        toolCallId,
        content = "{\"forecast\":\"Sunny\",\"temperatureC\":24}",
        role = "tool"
    });

    await WriteEventAsync(context, new
    {
        type = "REASONING_START",
        messageId = secondReasoningMessageId
    });

    await WriteEventAsync(context, new
    {
        type = "REASONING_MESSAGE_START",
        messageId = secondReasoningMessageId,
        role = "reasoning"
    });

    await WriteEventAsync(context, new
    {
        type = "REASONING_MESSAGE_CONTENT",
        messageId = secondReasoningMessageId,
        delta = "Now that I have the tool result, I can answer clearly."
    });

    await Task.Delay(3000);

    await WriteEventAsync(context, new
    {
        type = "REASONING_MESSAGE_END",
        messageId = secondReasoningMessageId
    });

    await WriteEventAsync(context, new
    {
        type = "REASONING_END",
        messageId = secondReasoningMessageId
    });

    await Task.Delay(3000);

    await WriteEventAsync(context, new
    {
        type = "TEXT_MESSAGE_START",
        messageId,
        role = "assistant"
    });

    await WriteEventAsync(context, new
    {
        type = "TEXT_MESSAGE_CONTENT",
        messageId,
        delta = "Hello"
    });

    await WriteEventAsync(context, new
    {
        type = "TEXT_MESSAGE_CONTENT",
        messageId,
        delta = " World,"
    });

    await WriteEventAsync(context, new
    {
        type = "TEXT_MESSAGE_CONTENT",
        messageId,
        delta = " Bingo!"
    });

    await WriteEventAsync(context, new
    {
        type = "TEXT_MESSAGE_END",
        messageId
    });

    await WriteEventAsync(context, new
    {
        type = "RUN_FINISHED",
        threadId,
        runId
    });
});

// --------- SharpOMatic Specific Start ------------------------
//
// Provide the visual editor at the /editor relative path
app.MapSharpOMaticEditor("/editor");

//
// --------- SharpOMatic Specific End --------------------------

app.Run();

static async Task WriteEventAsync(HttpContext context, object payload)
{
    await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(payload)}\n\n");
    await context.Response.Body.FlushAsync();
}
