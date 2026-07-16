# Model Call Timeouts

This document records how timeout handling currently works for SharpOMatic model calls and proposes a provider-independent timeout design for future implementation.

The term **batch mode** in this document means the non-streaming mode selected by the Model Call node's `BatchOutput` option, or selected internally when AG-UI response streaming is suppressed. It does not mean a provider's asynchronous batch-processing API.

## Summary

SharpOMatic does not currently impose an overall deadline on a Model Call node. It also does not impose a provider-independent idle timeout while reading a streamed response. The effective timeout behavior comes from the provider SDKs, except that SharpOMatic explicitly sets the Google GenAI HTTP timeout to five minutes.

| Connector | Batch behavior per provider request | Streaming behavior per provider request | SharpOMatic override |
| --- | --- | --- | --- |
| OpenAI | 100-second timeout for each network operation | 100-second timeout for each network operation/read; no overall stream deadline | None |
| Azure OpenAI | Same `System.ClientModel` behavior as OpenAI | Same `System.ClientModel` behavior as OpenAI | None |
| Anthropic | Total HTTP-attempt timeout calculated from `max_output_tokens`, from 30 seconds up to 10 minutes | Total HTTP-attempt timeout calculated from `max_output_tokens`, from 10 to 60 minutes | None |
| Azure AI Foundry Anthropic | Same as Anthropic | Same as Anthropic | None |
| Google AI | Five-minute timeout for the complete HTTP request | Five minutes to receive response headers; no SDK timeout while subsequently reading the SSE body | `HttpOptions.Timeout = 300_000` |

These are timeouts for an individual provider request or network operation, not necessarily for the complete Model Call node. A node may make multiple provider requests when host tools are called. Local tool execution, retries, and follow-up requests can make the complete node run substantially longer.

Hosts can also replace provider clients through `IEngineNotification` overrides. A host-provided client may have different timeout behavior from the standard clients described here.

## Common request patterns

### Batch mode

A model turn normally uses one HTTP POST and waits for a complete response body:

```text
SharpOMatic                         Provider

POST request  ------------------->
              <------------------- HTTP response containing the complete result
```

### Streaming mode

A model turn normally uses one HTTP POST whose response stays open as a Server-Sent Events stream. The client does not make a new HTTP request for every token or event.

```text
SharpOMatic                         Provider

POST request  ------------------->
              <------------------- HTTP 200, text/event-stream
              <------------------- event or data chunk
              <------------------- event or data chunk
              <------------------- completion event

SSE response closes
```

### Host tool calls

When a model requests a SharpOMatic host tool, the current provider response finishes, SharpOMatic executes the tool locally, and the function-invocation middleware starts another provider request containing the tool result.

```text
Provider request 1 -> model requests a tool
Local tool execution
Provider request 2 -> model receives the tool result
Optional further tool calls and provider requests
Final model response
```

Each provider request receives a fresh SDK timeout. There is no current SharpOMatic deadline covering the whole sequence. Provider-hosted tools may execute within a single provider request and do not necessarily create a SharpOMatic-to-provider round trip.

## OpenAI

SharpOMatic creates `OpenAIClientOptions` without setting `NetworkTimeout` in `src/SharpOMatic.Engine/Services/OpenAIModelCaller.cs`. The resolved OpenAI SDK uses `System.ClientModel` for its HTTP pipeline.

`System.ClientModel` has a default network timeout of 100 seconds. It configures its shared `HttpClient` with an infinite `HttpClient.Timeout` and applies the 100-second timeout in its own pipeline. The timeout is applied to individual network operations rather than to the total lifetime of a provider request.

Source references:

- [System.ClientModel client pipeline](https://github.com/Azure/azure-sdk-for-net/blob/System.ClientModel_1.13.0/sdk/core/System.ClientModel/src/Pipeline/ClientPipeline.cs)
- [System.ClientModel HTTP transport](https://github.com/Azure/azure-sdk-for-net/blob/System.ClientModel_1.13.0/sdk/core/System.ClientModel/src/Pipeline/HttpClientPipelineTransport.cs)
- [System.ClientModel read-timeout stream](https://github.com/Azure/azure-sdk-for-net/blob/System.ClientModel_1.13.0/sdk/core/System.ClientModel/src/Internal/ReadTimeoutStream.cs)

### OpenAI batch mode

The practical sequence is:

```text
Send request and wait for response headers: up to 100 seconds without progress
Read response body: each individual read may wait up to 100 seconds
```

This is not strictly a 100-second total request deadline. However, a non-streaming model response will commonly withhold its response until generation has completed. In practice, a model that takes more than approximately 100 seconds to produce response headers is likely to time out.

### OpenAI streaming mode

OpenAI returns one SSE response for each model turn. The pipeline applies its timeout to each read from the response stream:

```text
Wait for response headers: up to 100 seconds
Wait for next SSE bytes: up to 100 seconds
Receive bytes: the next read gets a fresh timeout
Repeat until the response completes
```

There is no overall client-side stream deadline. A response can run for an hour or longer as long as no individual read remains without data for 100 seconds. An SSE event, data delta, heartbeat, or other response bytes satisfy the read operation.

### OpenAI retries

The OpenAI SDK automatically retries qualifying failures up to three additional times. This can make the total failure time substantially longer than 100 seconds. An established SSE response that fails after output has already been consumed should not be assumed to resume transparently from the point of failure.

## Azure OpenAI

SharpOMatic creates `AzureOpenAIClient` without supplying `AzureOpenAIClientOptions` in `src/SharpOMatic.Engine/Services/AzureOpenAICaller.cs`, then obtains a Responses client from it.

The resulting Responses client uses the same `System.ClientModel` pipeline behavior as direct OpenAI:

- Batch mode has a 100-second timeout for each network operation, which practically often means approximately 100 seconds to receive the non-streaming response headers.
- Streaming mode has a rolling 100-second timeout for individual network reads and no overall stream deadline.
- Tool-call follow-up requests each receive a fresh timeout.
- Retryable failures can increase the total elapsed time.

Azure infrastructure, gateways, proxies, or service-side limits may terminate a request independently of the client timeout.

## Anthropic

SharpOMatic creates a default `AnthropicClient` without setting its `Timeout` in `src/SharpOMatic.Engine/Services/AnthropicModelCaller.cs`.

The Anthropic SDK disables the underlying `HttpClient.Timeout` and implements a total timeout for each complete HTTP attempt. That timeout includes DNS resolution, connecting, writing the request, server processing, and reading the complete response body. It is not a rolling idle timeout.

For message generation, the SDK calculates a timeout from `max_output_tokens` when no explicit timeout was supplied.

Source references:

- [Anthropic client options and timeout calculation](https://github.com/anthropics/anthropic-sdk-csharp/blob/8a0f0ef19307063292157f8d2f818df91c04107b/src/Anthropic/Core/ClientOptions.cs)
- [Anthropic message service](https://github.com/anthropics/anthropic-sdk-csharp/blob/8a0f0ef19307063292157f8d2f818df91c04107b/src/Anthropic/Services/MessageService.cs)

### Anthropic batch mode

The non-streaming timeout is calculated as:

```text
clamp(30 seconds * max_output_tokens / 1,000, 30 seconds, 10 minutes)
```

Examples:

| Maximum output tokens | Timeout for one HTTP attempt |
| ---: | ---: |
| 1,000 | 30 seconds |
| 4,096 | About 2 minutes 3 seconds |
| 8,192 | About 4 minutes 6 seconds |
| 16,384 | About 8 minutes 12 seconds |
| 20,000 or more | 10 minutes |

SharpOMatic's current Anthropic model definitions generally default to 64,000 or 128,000 maximum output tokens, so their effective batch timeout is the 10-minute maximum.

### Anthropic streaming mode

Anthropic returns one SSE response for each model turn. The streaming timeout is calculated as:

```text
clamp(60 minutes * max_output_tokens / 128,000, 10 minutes, 60 minutes)
```

Examples:

| Maximum output tokens | Timeout for one complete streaming HTTP attempt |
| ---: | ---: |
| 16,384 | 10-minute minimum |
| 32,000 | 15 minutes |
| 64,000 | 30 minutes |
| 128,000 | 60 minutes |

Unlike OpenAI, receiving an SSE event does not restart the overall Anthropic deadline. The complete streaming attempt must finish before the calculated timeout expires.

### Anthropic retries

The Anthropic SDK defaults to two retries for retryable failures. Its timeout applies separately to each attempt and excludes retry backoff, so the complete failure time can be considerably longer than the calculated single-attempt timeout.

## Azure AI Foundry Anthropic

SharpOMatic creates `AnthropicFoundryClient` without specifying different client options in `src/SharpOMatic.Engine/Services/FoundryAnthropicModelCaller.cs`.

`AnthropicFoundryClient` derives from `AnthropicClient`, so it inherits the same timeout calculations and retry behavior as direct Anthropic:

- Batch mode ranges from 30 seconds to 10 minutes per complete HTTP attempt.
- Streaming mode ranges from 10 to 60 minutes per complete HTTP attempt.
- Current SharpOMatic defaults generally result in 10-minute batch requests and 30- or 60-minute streaming requests.
- Tool-call follow-up requests each receive a new calculated timeout.

Source reference: [Anthropic Foundry client](https://github.com/anthropics/anthropic-sdk-csharp/blob/8a0f0ef19307063292157f8d2f818df91c04107b/src/Anthropic.Foundry/AnthropicFoundryClient.cs).

## Google AI

The Google GenAI SDK creates a normal `HttpClient`. If `HttpOptions.Timeout` is unset, it leaves `HttpClient.Timeout` at the .NET default of 100 seconds.

SharpOMatic overrides this when constructing the standard Google client:

```csharp
var httpOptions = new Google.GenAI.Types.HttpOptions { Timeout = 300_000 };
var client = new Client(apiKey: apiKey, httpOptions: httpOptions);
```

This is configured in `src/SharpOMatic.Engine/Services/GoogleGenAIModelCaller.cs` and changes the underlying `HttpClient.Timeout` from 100 seconds to five minutes.

Source references:

- [Google GenAI client construction and timeout application](https://github.com/googleapis/dotnet-genai/blob/082dad6b36920591a39d2d30f7235ddc6d3eedbf/Google.GenAI/ApiClient.cs)
- [Google GenAI streaming HTTP implementation](https://github.com/googleapis/dotnet-genai/blob/082dad6b36920591a39d2d30f7235ddc6d3eedbf/Google.GenAI/HttpApiClient.cs)
- [.NET `HttpCompletionOption` timeout behavior](https://learn.microsoft.com/dotnet/api/system.net.http.httpcompletionoption?view=net-10.0)

### Google batch mode

The SDK uses ordinary `HttpClient.SendAsync` behavior, which waits for the complete response content. SharpOMatic's five-minute override therefore covers the complete HTTP request:

```text
DNS + connect + request upload + server processing + complete response download
```

Without the SharpOMatic override, this would use the .NET default of 100 seconds.

### Google streaming mode

Google returns one SSE response for each model turn. The SDK uses `HttpCompletionOption.ResponseHeadersRead`, then reads the response body manually.

With `ResponseHeadersRead`, `HttpClient.Timeout` applies only until the response headers have arrived. It does not apply while subsequently reading the response content. The Google SDK passes the caller's cancellation token while enumerating the stream but does not add a separate body-read timeout.

The current five-minute override therefore behaves as follows:

```text
Send request and wait for response headers: up to 5 minutes
Read first SSE chunk: no Google SDK timeout
Read later SSE chunks: no Google SDK timeout
```

If Google sends response headers and then the SSE body stops producing data, the current SharpOMatic call can wait indefinitely unless another component cancels or closes the connection. The five-minute value is not an overall streaming model-call timeout and is not a streaming idle timeout.

Removing the SharpOMatic override without replacing it would:

- Reduce the Google batch timeout from five minutes to 100 seconds.
- Reduce the streaming response-header timeout from five minutes to 100 seconds.
- Leave the streamed response body without an idle or overall timeout.

Removing it alone is therefore not recommended.

## Recommended future design

SharpOMatic should eventually provide provider-independent timeout controls rather than exposing only the SDK-specific timeout properties. The SDK properties have incompatible meanings and cannot offer consistent behavior by themselves.

Two independent controls are recommended.

### Overall model-call timeout

The overall timeout is a wall-clock deadline for the complete Model Call node execution:

```text
all provider requests
+ all streamed responses
+ local host tool execution
+ provider retries and backoff
+ follow-up requests containing tool results
```

When the deadline expires, SharpOMatic should cancel the operation and fail the node with a clear message such as:

```text
Model Call node exceeded its overall timeout of 60 minutes.
```

### Streaming idle timeout

The idle timeout is the maximum time SharpOMatic will wait without receiving another provider streaming update. Its timer resets whenever an update arrives.

```text
Receive update -> reset idle timer
Receive update -> reset idle timer
No update before idle timeout -> cancel and fail the node
```

This permits healthy long-running streams while detecting connections that have opened successfully and then stalled. A future default could be two to five minutes, but the initial implementation can leave the value unset to preserve existing behavior.

### Configuration levels

A future implementation should support:

1. Engine-wide defaults configured when SharpOMatic is registered.
2. Optional overrides on each Model Call node.

Conceptually:

```csharp
builder.Services.AddSharpOMaticEngine()
    .ConfigureModelCalls(options =>
    {
        options.Timeout = TimeSpan.FromMinutes(60);
        options.StreamingIdleTimeout = TimeSpan.FromMinutes(2);
    });
```

An empty node setting would inherit the engine default. An explicit `Unlimited` value could be supported where an application intentionally accepts an unbounded operation.

Timeouts are usually task- and model-dependent, so a node-level override is more useful than only a connector-level setting. A connector-level override could still be added later as an intermediate default.

## Possible implementation

### Shared engine changes

The implementation should be centralized around `BaseModelCaller` and the Model Call execution path rather than independently duplicating all logic in every connector.

1. Add nullable overall and streaming-idle timeout options to engine configuration.
2. Add optional timeout overrides to `ModelCallNodeEntity` and the Angular Model Call dialog.
3. Resolve effective values when the node begins execution.
4. Create a linked `CancellationTokenSource` for the overall timeout.
5. Pass its token through agent batch calls, agent streaming calls, provider adapters, and tool invocation.
6. For streaming, enumerate updates through a helper that creates or resets an idle deadline around each `MoveNextAsync` operation.
7. Distinguish expiration of the overall timer, expiration of the idle timer, host shutdown, and user cancellation.
8. Convert timeout expiration into a clear `SharpOMaticException` while preserving the cancellation or timeout exception as its inner exception.
9. Record timeout type and configured duration on the node's OpenTelemetry activity in addition to the existing exception event.

An overall cancellation token is necessary even when an SDK has its own timeout because it is the only way to cover local tools and multiple provider round trips consistently.

The idle timer should be based on provider updates arriving at SharpOMatic, not solely on visible assistant text. Reasoning events, tool-call events, metadata events, and other valid stream activity should reset it. If an SDK consumes transport-level heartbeats without exposing an update, those heartbeats would not reset a SharpOMatic update-level idle timer; a lower-level stream wrapper would be required if transport heartbeats must count.

### OpenAI and Azure OpenAI

- Pass the overall cancellation token into `RunAsync` and `RunStreamingAsync` through the Agent Framework APIs.
- Wrap streaming enumeration with the shared idle-timeout helper.
- Continue allowing the `System.ClientModel` 100-second network-operation timeout to provide lower-level stalled-read protection, or expose it later as an advanced connector setting.
- Do not treat `NetworkTimeout` as the overall timeout because it resets for individual reads and does not cover local tool execution or the complete tool loop.
- If a SharpOMatic idle timeout is shorter than 100 seconds, the SharpOMatic timer will normally fire first. If it is longer, the SDK may still fail a stalled network read at 100 seconds.

### Anthropic and Azure AI Foundry Anthropic

- Pass the overall cancellation token through agent calls and the Anthropic adapter.
- Wrap streaming enumeration with the shared idle-timeout helper.
- Leave the Anthropic token-based per-attempt timeout in place as a provider safety limit unless a future advanced setting intentionally overrides it.
- Do not set the Anthropic SDK `Timeout` to the SharpOMatic overall timeout: the SDK timeout applies independently to every HTTP attempt, while the SharpOMatic timeout should cover the whole node and all tool iterations.
- The effective deadline for a provider attempt will be whichever happens first: the Anthropic SDK attempt timeout or SharpOMatic's remaining overall deadline.

### Google AI

- Pass the overall cancellation token into Google batch and streaming calls.
- Wrap streaming enumeration with the shared idle-timeout helper. This is especially important because the Google SDK currently has no response-body timeout after streaming headers arrive.
- Keep the existing five-minute `HttpOptions.Timeout` initially as the request-establishment and batch-request safety limit.
- Once SharpOMatic's overall and idle timeouts are reliable, consider making the Google `HttpOptions.Timeout` an advanced connector option or setting it high enough not to conflict with the normalized SharpOMatic policy.
- Do not remove the existing override before the normalized controls exist; doing so shortens Google batch requests without fixing stalled streaming bodies.

## Suggested compatibility strategy

The first implementation can introduce both SharpOMatic settings as nullable and leave them unset by default. This preserves all current behavior while allowing hosts and workflows to opt into deterministic limits.

After operational experience, SharpOMatic could adopt defaults such as:

```text
Overall model-call timeout: 60 minutes
Streaming idle timeout:      2 to 5 minutes
```

Changing from unset to finite defaults should be treated as a visible behavior change and documented accordingly.

## Timeout failure and telemetry

A timeout should fail the node rather than look like an ordinary user cancellation. Suggested messages are:

```text
Model Call node exceeded its overall timeout of 60 minutes.
Model Call stream produced no updates for 2 minutes.
```

The node activity should include searchable fields such as:

```text
error.type
sharpomatic.model_call.timeout.kind = overall | idle | provider
sharpomatic.model_call.timeout.seconds
```

The standard OpenTelemetry `exception` event should contain the timeout exception's type, message, and stack trace. The original exception should remain available as the `InnerException` of the workflow-facing `SharpOMaticException`.
