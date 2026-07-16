namespace SharpOMatic.Tests.Workflows;

public sealed class ModelFallbackProviderClassificationUnitTests
{
    [Theory]
    [InlineData(401, ModelFallbackFailureCategory.Authentication, false)]
    [InlineData(429, ModelFallbackFailureCategory.RateLimited, true)]
    [InlineData(503, ModelFallbackFailureCategory.ProviderUnavailable, true)]
    public void OpenAI_caller_classifies_ClientResultException(int statusCode, ModelFallbackFailureCategory category, bool transient)
    {
        var response = new Mock<System.ClientModel.Primitives.PipelineResponse>();
        response.SetupGet(item => item.Status).Returns(statusCode);
        var exception = new System.ClientModel.ClientResultException(response.Object, null);

        var failure = new OpenAIModelCaller().ModelFallbackFailureOverride(new AggregateException(exception));

        Assert.NotNull(failure);
        Assert.Equal(category, failure.Category);
        Assert.Equal(statusCode, failure.StatusCode);
        Assert.Equal(transient, failure.IsTransient);
    }

    [Fact]
    public void OpenAI_caller_classifies_Azure_RequestFailedException()
    {
        var exception = new Azure.RequestFailedException(503, "Azure OpenAI unavailable");

        var failure = new OpenAIModelCaller().ModelFallbackFailureOverride(exception);

        Assert.NotNull(failure);
        Assert.Equal(ModelFallbackFailureCategory.ProviderUnavailable, failure.Category);
        Assert.Equal(503, failure.StatusCode);
        Assert.True(failure.IsTransient);
    }

    [Theory]
    [InlineData(401, ModelFallbackFailureCategory.Authentication, false)]
    [InlineData(429, ModelFallbackFailureCategory.RateLimited, true)]
    [InlineData(529, ModelFallbackFailureCategory.ProviderUnavailable, true)]
    public void Anthropic_caller_classifies_sdk_api_exceptions(int statusCode, ModelFallbackFailureCategory category, bool transient)
    {
        var httpException = new HttpRequestException("Anthropic error", null, (System.Net.HttpStatusCode)statusCode);
        Anthropic.Exceptions.AnthropicApiException exception = statusCode switch
        {
            401 => new Anthropic.Exceptions.AnthropicUnauthorizedException(httpException) { StatusCode = (System.Net.HttpStatusCode)statusCode, ResponseBody = string.Empty },
            429 => new Anthropic.Exceptions.AnthropicRateLimitException(httpException) { StatusCode = (System.Net.HttpStatusCode)statusCode, ResponseBody = string.Empty },
            _ => new Anthropic.Exceptions.AnthropicUnexpectedStatusCodeException(httpException) { StatusCode = (System.Net.HttpStatusCode)statusCode, ResponseBody = string.Empty },
        };

        var failure = new AnthropicModelCaller().ModelFallbackFailureOverride(new InvalidOperationException("Agent wrapper", exception));

        Assert.NotNull(failure);
        Assert.Equal(category, failure.Category);
        Assert.Equal(statusCode, failure.StatusCode);
        Assert.Equal(transient, failure.IsTransient);
    }

    [Fact]
    public void Anthropic_caller_classifies_sdk_io_exception_as_network_failure()
    {
        var exception = new Anthropic.Exceptions.AnthropicIOException("Anthropic transport failed", new HttpRequestException("network"));

        var failure = new AnthropicModelCaller().ModelFallbackFailureOverride(exception);

        Assert.NotNull(failure);
        Assert.Equal(ModelFallbackFailureCategory.Network, failure.Category);
        Assert.Null(failure.StatusCode);
        Assert.True(failure.IsTransient);
    }

    [Fact]
    public void Anthropic_caller_classifies_workload_identity_failure_as_authentication()
    {
        var exception = new Anthropic.Credentials.WorkloadIdentityException("identity failed");

        var failure = new AnthropicModelCaller().ModelFallbackFailureOverride(exception);

        Assert.NotNull(failure);
        Assert.Equal(ModelFallbackFailureCategory.Authentication, failure.Category);
        Assert.False(failure.IsTransient);
    }

    [Theory]
    [InlineData(400, ModelFallbackFailureCategory.InvalidRequest, false)]
    [InlineData(429, ModelFallbackFailureCategory.RateLimited, true)]
    public void Google_caller_classifies_ClientError(int statusCode, ModelFallbackFailureCategory category, bool transient)
    {
        var exception = new Google.GenAI.ClientError("Google client error", statusCode, "status");

        var failure = new GoogleGenAIModelCaller().ModelFallbackFailureOverride(new AggregateException(exception));

        Assert.NotNull(failure);
        Assert.Equal(category, failure.Category);
        Assert.Equal(statusCode, failure.StatusCode);
        Assert.Equal(transient, failure.IsTransient);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    public void Google_caller_classifies_ServerError_as_transient(int statusCode)
    {
        var exception = new Google.GenAI.ServerError("Google server error", statusCode, "status");

        var failure = new GoogleGenAIModelCaller().ModelFallbackFailureOverride(exception);

        Assert.NotNull(failure);
        Assert.Equal(ModelFallbackFailureCategory.ProviderUnavailable, failure.Category);
        Assert.Equal(statusCode, failure.StatusCode);
        Assert.True(failure.IsTransient);
    }

    [Fact]
    public void Google_caller_classifies_statusless_ServerError_as_provider_unavailable()
    {
        var failure = new GoogleGenAIModelCaller().ModelFallbackFailureOverride(new Google.GenAI.ServerError("Google unavailable"));

        Assert.NotNull(failure);
        Assert.Equal(ModelFallbackFailureCategory.ProviderUnavailable, failure.Category);
        Assert.Null(failure.StatusCode);
        Assert.True(failure.IsTransient);
    }

    [Fact]
    public void Google_caller_classifies_WebSocketException_as_network_failure()
    {
        var exception = new System.Net.WebSockets.WebSocketException(System.Net.WebSockets.WebSocketError.ConnectionClosedPrematurely);

        var failure = new GoogleGenAIModelCaller().ModelFallbackFailureOverride(exception);

        Assert.NotNull(failure);
        Assert.Equal(ModelFallbackFailureCategory.Network, failure.Category);
        Assert.True(failure.IsTransient);
    }
}
