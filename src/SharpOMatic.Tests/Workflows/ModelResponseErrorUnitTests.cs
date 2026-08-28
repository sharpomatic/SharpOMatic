namespace SharpOMatic.Tests.Workflows;

public sealed class ModelResponseErrorUnitTests
{
    private const string RateLimitMessage = "Your requests to gpt-5.4 for gpt-5.4 in eastus2 have exceeded rate limit.";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Error_content_response_throws_regardless_of_json_output(bool jsonOutput)
    {
        var response = CreateResponse(new ErrorContent(RateLimitMessage) { ErrorCode = "429" });

        var exception = Assert.Throws<ModelResponseErrorException>(() => new ErrorResponseTestModelCaller().Convert(jsonOutput, response));

        Assert.Equal("429", exception.ErrorCode);
        Assert.Equal(429, exception.StatusCode);
        Assert.Contains(RateLimitMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Error_content_is_detected_when_mixed_with_text_content()
    {
        var response = CreateResponse(new TextContent("{\"partial\":true}"), new ErrorContent(RateLimitMessage) { ErrorCode = "429" });

        Assert.Throws<ModelResponseErrorException>(() => new ErrorResponseTestModelCaller().Convert(jsonOutput: true, response));
    }

    [Fact]
    public void Text_only_response_is_unaffected()
    {
        var response = CreateResponse(new TextContent("plain text"));

        Assert.Equal("plain text", new ErrorResponseTestModelCaller().Convert(jsonOutput: false, response));
    }

    [Fact]
    public void Numeric_error_code_classifies_as_rate_limited_and_transient()
    {
        var failure = ModelFallbackFailureClassifier.Classify(new ModelResponseErrorException("429", RateLimitMessage, null));

        Assert.Equal(ModelFallbackFailureCategory.RateLimited, failure.Category);
        Assert.Equal(429, failure.StatusCode);
        Assert.True(failure.IsTransient);
    }

    [Fact]
    public void Non_numeric_rate_limit_error_code_classifies_as_rate_limited_and_transient()
    {
        var failure = ModelFallbackFailureClassifier.Classify(new ModelResponseErrorException("rate_limit_exceeded", RateLimitMessage, null));

        Assert.Equal(ModelFallbackFailureCategory.RateLimited, failure.Category);
        Assert.Null(failure.StatusCode);
        Assert.True(failure.IsTransient);
    }

    [Fact]
    public void Content_filter_error_classifies_as_invalid_request_and_is_not_retried()
    {
        var failure = ModelFallbackFailureClassifier.Classify(new ModelResponseErrorException("content_filter", "The response was filtered.", null));

        Assert.Equal(ModelFallbackFailureCategory.InvalidRequest, failure.Category);
        Assert.False(failure.IsTransient);
    }

    [Fact]
    public void Unknown_in_band_error_classifies_as_provider_unavailable_and_transient()
    {
        var failure = ModelFallbackFailureClassifier.Classify(new ModelResponseErrorException("server_error", "Something went wrong upstream.", null));

        Assert.Equal(ModelFallbackFailureCategory.ProviderUnavailable, failure.Category);
        Assert.True(failure.IsTransient);
    }

    [Fact]
    public void Error_response_is_wrapped_in_an_aggregate_and_still_classified()
    {
        var failure = ModelFallbackFailureClassifier.Classify(new AggregateException(new ModelResponseErrorException("429", RateLimitMessage, null)));

        Assert.Equal(ModelFallbackFailureCategory.RateLimited, failure.Category);
        Assert.True(failure.IsTransient);
    }

    // Guards the mechanism this fix exists for: ChatMessage.Text only concatenates TextContent, so an error-only
    // response reports empty text and previously reached the json parser as an empty string.
    [Fact]
    public void Error_content_is_invisible_to_chat_message_text()
    {
        var message = new ChatMessage(ChatRole.Assistant, new List<AIContent> { new ErrorContent(RateLimitMessage) { ErrorCode = "429" } });

        Assert.True(string.IsNullOrEmpty(message.Text));
        Assert.Single(message.Contents);
    }

    private static AgentResponse CreateResponse(params AIContent[] contents)
    {
        return new AgentResponse(new ChatMessage(ChatRole.Assistant, new List<AIContent>(contents)));
    }

    private sealed class ErrorResponseTestModelCaller : BaseModelCaller
    {
        public object? Convert(bool jsonOutput, AgentResponse response) => ResponseToOutputValue(jsonOutput, response);

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
