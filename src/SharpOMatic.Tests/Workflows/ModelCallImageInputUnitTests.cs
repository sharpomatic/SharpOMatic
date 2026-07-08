namespace SharpOMatic.Tests.Workflows;

public sealed class ModelCallImageInputUnitTests
{
    [Fact]
    public async Task Image_input_path_accepts_https_image_url()
    {
        var chat = await InvokeAddImageMessages("https://example.com/images/plan.png?version=1");

        var message = Assert.Single(chat);
        Assert.Equal(ChatRole.User, message.Role);

        var content = Assert.IsType<UriContent>(Assert.Single(message.Contents));
        Assert.Equal(new Uri("https://example.com/images/plan.png?version=1"), content.Uri);
        Assert.Equal("image/png", content.MediaType);
    }

    [Fact]
    public async Task Image_input_path_accepts_image_urls_inside_context_list()
    {
        ContextList imageInputs = ["https://example.com/images/elevation.jpg", "https://example.com/images/floor-plan.webp"];

        var chat = await InvokeAddImageMessages(imageInputs);

        Assert.Equal(2, chat.Count);

        var firstContent = Assert.IsType<UriContent>(Assert.Single(chat[0].Contents));
        Assert.Equal(new Uri("https://example.com/images/elevation.jpg"), firstContent.Uri);
        Assert.Equal("image/jpeg", firstContent.MediaType);

        var secondContent = Assert.IsType<UriContent>(Assert.Single(chat[1].Contents));
        Assert.Equal(new Uri("https://example.com/images/floor-plan.webp"), secondContent.Uri);
        Assert.Equal("image/webp", secondContent.MediaType);
    }

    [Fact]
    public async Task Image_input_path_rejects_missing_context_value()
    {
        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() => InvokeAddImageMessages(null, setImageValue: false));

        Assert.Equal("Image input path 'input.image' could not be resolved.", exception.Message);
    }

    [Fact]
    public async Task Image_input_path_ignores_null_context_value()
    {
        var chat = await InvokeAddImageMessages(null);

        Assert.Empty(chat);
    }

    [Fact]
    public async Task Image_input_path_rejects_string_that_is_not_url()
    {
        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() => InvokeAddImageMessages("library/plan.png"));

        Assert.Equal("Image input path 'input.image' must be an asset, asset list, or file URL.", exception.Message);
    }

    [Fact]
    public async Task Image_input_path_accepts_document_urls()
    {
        ContextList documentInputs =
        [
            "https://example.com/files/specification.pdf",
            "https://example.com/files/notes.txt",
            "https://example.com/files/report.docx",
            "https://example.com/files/budget.xlsx",
            "https://example.com/files/payload.json",
            "https://example.com/files/feed.xml",
            "https://example.com/files/pipeline.yml",
            "https://example.com/files/export.tsv",
            "https://example.com/files/events.jsonl",
            "https://example.com/files/page.html",
        ];

        var chat = await InvokeAddImageMessages(documentInputs);

        Assert.Equal(10, chat.Count);
        Assert.Equal("application/pdf", Assert.IsType<UriContent>(Assert.Single(chat[0].Contents)).MediaType);
        Assert.Equal("text/plain", Assert.IsType<UriContent>(Assert.Single(chat[1].Contents)).MediaType);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", Assert.IsType<UriContent>(Assert.Single(chat[2].Contents)).MediaType);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Assert.IsType<UriContent>(Assert.Single(chat[3].Contents)).MediaType);
        Assert.Equal("application/json", Assert.IsType<UriContent>(Assert.Single(chat[4].Contents)).MediaType);
        Assert.Equal("application/xml", Assert.IsType<UriContent>(Assert.Single(chat[5].Contents)).MediaType);
        Assert.Equal("application/yaml", Assert.IsType<UriContent>(Assert.Single(chat[6].Contents)).MediaType);
        Assert.Equal("text/tab-separated-values", Assert.IsType<UriContent>(Assert.Single(chat[7].Contents)).MediaType);
        Assert.Equal("application/x-ndjson", Assert.IsType<UriContent>(Assert.Single(chat[8].Contents)).MediaType);
        Assert.Equal("text/html", Assert.IsType<UriContent>(Assert.Single(chat[9].Contents)).MediaType);
    }

    [Fact]
    public async Task Image_input_path_rejects_url_with_unsupported_extension()
    {
        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() => InvokeAddImageMessages("https://example.com/files/archive.zip"));

        Assert.Equal("Image input URL 'https://example.com/files/archive.zip' must resolve to a supported media type.", exception.Message);
    }

    private static async Task<List<ChatMessage>> InvokeAddImageMessages(object? imageValue, bool setImageValue = true)
    {
        using var provider = WorkflowRunner.BuildProvider();
        using var scope = provider.CreateScope();

        var run = new Run()
        {
            RunId = Guid.NewGuid(),
            WorkflowId = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            RunStatus = RunStatus.Running,
        };
        var processContext = new ProcessContext(scope, run, 100, null);
        var workflowContext = new WorkflowContext(processContext, new WorkflowBuilder().AddStart().Build());
        ContextObject nodeContext = [];
        if (setImageValue)
            nodeContext.Set("input.image", imageValue);

        var threadContext = new ThreadContext(processContext, workflowContext, nodeContext);
        var node = CreateModelCallNode();
        var caller = new ImageInputTestModelCaller();

        return await caller.InvokeAddImageMessages(CreateModel(), CreateImageModelConfig(), processContext, threadContext, node);
    }

    private static ModelCallNodeEntity CreateModelCallNode()
    {
        return new ModelCallNodeEntity()
        {
            Id = Guid.NewGuid(),
            Version = 1,
            NodeType = NodeType.ModelCall,
            Title = "model",
            Top = 0,
            Left = 0,
            Width = 80,
            Height = 80,
            Inputs = [],
            Outputs = [],
            ModelId = null,
            BatchOutput = false,
            DropToolCalls = false,
            DisableStreamUser = false,
            DisableStreamTool = false,
            DisableStreamReasoning = false,
            DisableStreamAssistantText = false,
            Instructions = string.Empty,
            Prompt = string.Empty,
            ChatInputPath = string.Empty,
            ChatOutputPath = string.Empty,
            TextOutputPath = "output.text",
            ImageInputPath = "input.image",
            ImageOutputPath = "output.image",
            ParameterValues = [],
        };
    }

    private static Model CreateModel()
    {
        return new Model()
        {
            Version = 1,
            ModelId = Guid.NewGuid(),
            Name = "model",
            Description = "model",
            ConfigId = "model-config",
            ConnectorId = Guid.NewGuid(),
            CustomCapabilities = [],
            ParameterValues = [],
        };
    }

    private static ModelConfig CreateImageModelConfig()
    {
        return new ModelConfig()
        {
            Version = 1,
            ConfigId = "model-config",
            DisplayName = "model",
            Description = "model",
            ConnectorConfigId = "connector",
            IsCustom = false,
            Information = null,
            Capabilities = [new ModelCapability() { Name = "SupportsImageIn", DisplayName = "Image input" }],
            ParameterFields = [],
        };
    }

    private sealed class ImageInputTestModelCaller : BaseModelCaller
    {
        public async Task<List<ChatMessage>> InvokeAddImageMessages(Model model, ModelConfig modelConfig, ProcessContext processContext, ThreadContext threadContext, ModelCallNodeEntity node)
        {
            List<ChatMessage> chat = [];
            await AddImageMessages(chat, model, modelConfig, processContext, threadContext, node);
            return chat;
        }

        public override Task<ModelCallResult> Call(
            Model model,
            ModelConfig modelConfig,
            Connector connector,
            ConnectorConfig connectorConfig,
            ProcessContext processContext,
            ThreadContext threadContext,
            ModelCallNodeEntity node,
            IModelCallProgressSink progressSink
        )
        {
            throw new NotSupportedException();
        }
    }
}
