namespace SharpOMatic.Tests.Workflows;

public sealed class BackendToolCallNodeUnitTests
{
    [Fact]
    public async Task Backend_tool_call_emits_complete_stream_sequence_and_continues()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBackendToolCall(
                title: "call",
                functionName: "lookup_weather",
                argumentsMode: ToolCallDataMode.FixedJson,
                argumentsJson: """{"city":"Sydney"}""",
                resultMode: ToolCallDataMode.FixedJson,
                resultJson: """{"forecast":"Sunny"}"""
            )
            .AddCode("after", """Context.Set<string>("output.route", "continued");""")
            .AddEnd()
            .Connect("start", "call")
            .Connect("call", "after")
            .Connect("after", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, []);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
            Assert.Equal("continued", output.Get<string>("output.route"));

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            Assert.Equal(
                [
                    StreamEventKind.ToolCallStart,
                    StreamEventKind.ToolCallArgs,
                    StreamEventKind.ToolCallEnd,
                    StreamEventKind.ToolCallResult
                ],
                streamEvents.Select(e => e.EventKind).ToArray()
            );

            var toolCallId = streamEvents[0].ToolCallId;
            Assert.False(string.IsNullOrWhiteSpace(toolCallId));
            Assert.Equal(toolCallId, streamEvents[0].MessageId);
            Assert.Equal(toolCallId, streamEvents[1].ToolCallId);
            Assert.Equal(toolCallId, streamEvents[2].ToolCallId);
            Assert.Equal(toolCallId, streamEvents[3].ToolCallId);
            Assert.Equal("lookup_weather", streamEvents[0].TextDelta);
            Assert.Equal(string.Empty, streamEvents[0].ParentMessageId);
            Assert.Equal("""{"city":"Sydney"}""", streamEvents[1].TextDelta);
            Assert.NotEqual(toolCallId, streamEvents[3].MessageId);
            Assert.StartsWith("backend-tool-result:", streamEvents[3].MessageId);
            Assert.Equal("""{"forecast":"Sunny"}""", streamEvents[3].TextDelta);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Backend_tool_call_serializes_context_path_arguments_and_result()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddEdit(
                "seed",
                WorkflowBuilder.CreateJsonUpsert("input.args", """{"city":"Sydney","days":2}"""),
                WorkflowBuilder.CreateJsonUpsert("input.result", """{"forecast":"Sunny","high":24}""")
            )
            .AddBackendToolCall(
                argumentsMode: ToolCallDataMode.ContextPath,
                argumentsPath: "input.args",
                resultMode: ToolCallDataMode.ContextPath,
                resultPath: "input.result"
            )
            .AddEnd()
            .Connect("start", "seed")
            .Connect("seed", "BE Tool Call")
            .Connect("BE Tool Call", "end")
            .Build();

        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await repositoryService.UpsertWorkflow(workflow);

        using var cts = new CancellationTokenSource();
        var executionService = provider.GetRequiredService<INodeExecutionService>();
        var queueTask = executionService.RunQueueAsync(cts.Token);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var engineService = scope.ServiceProvider.GetRequiredService<IEngineService>();
            var run = await engineService.StartWorkflowRunAndWait(workflow.Id, []);

            Assert.Equal(RunStatus.Success, run.RunStatus);

            var streamEvents = await repositoryService.GetRunStreamEvents(run.RunId);
            AssertJsonEqual("""{"city":"Sydney","days":2}""", streamEvents.Single(e => e.EventKind == StreamEventKind.ToolCallArgs).TextDelta!);
            AssertJsonEqual("""{"forecast":"Sunny","high":24}""", streamEvents.Single(e => e.EventKind == StreamEventKind.ToolCallResult).TextDelta!);
        }
        finally
        {
            cts.Cancel();
            await queueTask;
        }
    }

    [Fact]
    public async Task Backend_tool_call_rejects_invalid_fixed_arguments_json()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBackendToolCall(argumentsMode: ToolCallDataMode.FixedJson, argumentsJson: """{"broken":""")
            .Connect("start", "BE Tool Call")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Backend Tool Call fixed arguments must contain valid JSON.", run.Error);
    }

    [Fact]
    public async Task Backend_tool_call_rejects_invalid_fixed_result_json()
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBackendToolCall(resultMode: ToolCallDataMode.FixedJson, resultJson: """{"broken":""")
            .Connect("start", "BE Tool Call")
            .Build();

        var run = await WorkflowRunner.RunWorkflow([], workflow);
        Assert.Equal(RunStatus.Failed, run.RunStatus);
        Assert.Equal("Backend Tool Call fixed result must contain valid JSON.", run.Error);
    }

    [Fact]
    public async Task Backend_tool_call_chat_persistence_none_leaves_input_chat_unchanged()
    {
        await AssertChatPersistence(
            ToolCallChatPersistenceMode.None,
            chatHistory =>
            {
                Assert.NotNull(chatHistory);
                Assert.Empty(chatHistory);
            }
        );
    }

    [Fact]
    public async Task Backend_tool_call_chat_persistence_function_call_only_keeps_assistant_call()
    {
        await AssertChatPersistence(
            ToolCallChatPersistenceMode.FunctionCallOnly,
            chatHistory =>
            {
                Assert.NotNull(chatHistory);
                Assert.Single(chatHistory);
                var assistantMessage = Assert.IsType<ChatMessage>(chatHistory[0]);
                Assert.Equal(ChatRole.Assistant, assistantMessage.Role);
                Assert.StartsWith("backend-tool-call:", assistantMessage.MessageId);
                var functionCall = Assert.IsType<FunctionCallContent>(assistantMessage.Contents.Single());
                Assert.Equal("lookup_weather", functionCall.Name);
                Assert.Equal("Sydney", functionCall.Arguments!["city"]?.ToString());
            }
        );
    }

    [Fact]
    public async Task Backend_tool_call_chat_persistence_function_call_and_result_keeps_both_messages()
    {
        await AssertChatPersistence(
            ToolCallChatPersistenceMode.FunctionCallAndResult,
            chatHistory =>
            {
                Assert.NotNull(chatHistory);
                Assert.Equal(2, chatHistory.Count);
                Assert.All(chatHistory, item => Assert.IsType<ChatMessage>(item));

                var assistantMessage = (ChatMessage)chatHistory[0]!;
                var toolMessage = (ChatMessage)chatHistory[1]!;
                Assert.Equal(ChatRole.Assistant, assistantMessage.Role);
                Assert.Equal(ChatRole.Tool, toolMessage.Role);

                var functionCall = Assert.IsType<FunctionCallContent>(assistantMessage.Contents.Single());
                var functionResult = Assert.IsType<FunctionResultContent>(toolMessage.Contents.Single());
                Assert.Equal(functionCall.CallId, functionResult.CallId);
                Assert.Contains("Sunny", functionResult.Result?.ToString());
            }
        );
    }

    private static async Task AssertChatPersistence(
        ToolCallChatPersistenceMode chatPersistenceMode,
        Action<ContextList> assertChatHistory
    )
    {
        var workflow = new WorkflowBuilder()
            .AddStart()
            .AddBackendToolCall(
                functionName: "lookup_weather",
                argumentsMode: ToolCallDataMode.FixedJson,
                argumentsJson: """{"city":"Sydney"}""",
                resultMode: ToolCallDataMode.ContextPath,
                resultPath: "input.toolResult",
                chatPersistenceMode: chatPersistenceMode
            )
            .AddEnd()
            .Connect("start", "BE Tool Call")
            .Connect("BE Tool Call", "end")
            .Build();

        var input = new ContextObject();
        input.Set("input.chat", new ContextList());
        input.Set("input.toolResult", ContextHelpers.FastDeserializeString("""{"forecast":"Sunny"}"""));

        var (run, provider) = await WorkflowRunner.RunWorkflowDebug(input, workflow);
        Assert.Equal(RunStatus.Success, run.RunStatus);

        var jsonConverters = provider.GetRequiredService<IJsonConverterService>();
        var output = ContextObject.Deserialize(run.OutputContext, jsonConverters.GetConverters());
        Assert.True(output.TryGet<ContextList>("input.chat", out var chatHistory));
        Assert.NotNull(chatHistory);
        assertChatHistory(chatHistory);
    }

    private static void AssertJsonEqual(string expectedJson, string actualJson)
    {
        using var expected = JsonDocument.Parse(expectedJson);
        using var actual = JsonDocument.Parse(actualJson);
        Assert.Equal(expected.RootElement.ToString(), actual.RootElement.ToString());
    }
}
