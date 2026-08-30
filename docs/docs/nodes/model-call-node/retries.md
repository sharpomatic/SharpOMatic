---
title: Retries
sidebar_position: 2
---

Before a **ModelCall** gives up on a model and moves to the next [fallback model](./fallback.md), it can call the same model again. Providers regularly reject a request for reasons that clear within seconds — rate limiting, capacity shedding, a dropped connection — and re-sending the request is usually faster and cheaper than switching to a different model.

By default a model is tried up to **3 times**: the original attempt plus 2 retries.

## Retry and fallback together

Retries are exhausted on the current model before the next model is considered:

```text
Model 1: try 1 -> try 2 -> try 3     all failed
Model 2: try 1                       succeeded
```

Both mechanisms use the same failure classification, so anything that qualifies for fallback also qualifies for a retry. A failure that is not transient stops the node immediately without either.

## When a retry happens

A retry is used for the transient failure categories:

- provider rate limiting, including HTTP 429
- provider HTTP 5xx responses
- network failures
- timeouts

Configuration errors, invalid requests, missing credentials, cancellation, local context errors, and tool errors are never retried.

Like fallback, retrying is refused once an attempt has produced observable effects. SharpOMatic does not call a model again after assistant/reasoning/tool output or a tool invocation has begun, because the visible output would be duplicated and tool side effects would be repeated. Retries therefore apply to failures that happen before the first response byte, which is where rate limits and capacity errors occur.

## Delay between tries

If the provider supplied a retry-after value it is used directly, clamped to the maximum delay. Otherwise the delay grows exponentially with jitter, starting from the base delay.

With the default settings the delays are approximately 1 second before the second try and 2 seconds before the third.

The delay is not interruptible, so it also delays how quickly a cancelled run stops. Keep `MaxDelay` modest unless long pauses are acceptable.

## Configuration

Retry behavior is configured once when the engine is registered:

```csharp
builder.Services.AddSharpOMaticEngine()
    .AddModelRetry(options =>
    {
        options.MaxTries = 3;
        options.BaseDelay = TimeSpan.FromSeconds(1);
        options.BackoffFactor = 2;
        options.MaxDelay = TimeSpan.FromSeconds(30);
        options.JitterFactor = 0.2;
        options.RetryableCategories =
        [
            ModelFallbackFailureCategory.RateLimited,
            ModelFallbackFailureCategory.ProviderUnavailable,
            ModelFallbackFailureCategory.Timeout,
            ModelFallbackFailureCategory.Network,
        ];
    });
```

| Option | Default | Meaning |
| --- | --- | --- |
| `MaxTries` | 3 | Total tries per model, including the first. Set to 1 to disable retries. |
| `BaseDelay` | 1 second | Delay before the second try. |
| `BackoffFactor` | 2 | Multiplier applied for each later try. |
| `MaxDelay` | 30 seconds | Upper bound on any delay, including a provider retry-after value. |
| `JitterFactor` | 0.2 | Random spread applied to a calculated delay, as a fraction. |
| `RetryableCategories` | transient categories | Failure categories eligible for a retry. |

Removing `RateLimited` from `RetryableCategories` is a reasonable choice when fallback models use independent providers, because moving to another provider immediately is usually better than waiting out a rate limit.

## Customizing the decision

Provider callers can encode SDK-specific retry knowledge by overriding `ModelRetryOverride`. Returning null keeps the built-in policy:

```csharp
public override ModelRetryDecision? ModelRetryOverride(ModelRetryDecisionContext context)
{
    if (ModelFallbackFailureClassifier.Find<ServerError>(context.Exception) is { StatusCode: 504 })
        return new ModelRetryDecision(false, TimeSpan.Zero);

    return null;
}
```

The built-in Google caller uses exactly this rule. Google reports both capacity shedding and a genuinely overrunning request as `Deadline expired before operation could complete`, separated only by the status code. A 503 is shed before generation and normally clears on the next try, but a 504 means the request could not finish inside Google's own deadline, so repeating it unchanged simply spends another deadline.

Hosts can override any decision with [`IEngineNotification.ModelRetryOverride`](../../programmatic/run-workflow.md#model-retry-override), which wins over both the provider caller and the built-in policy.

No override can retry after response output or a tool invocation has started, and none can push a model past `MaxTries`.

## Metrics

Every try writes its own `ModelCallMetric` row. `AttemptNumber` identifies the model in the configured list and `TryNumber` identifies the try against that model, so a retried primary model is still `AttemptNumber` 1. All rows for one logical model call share a `LogicalCallId`.

```text
LogicalCallId  AttemptNumber  TryNumber  Succeeded
abc...              1             1        false
abc...              1             2        false
abc...              1             3        false
abc...              2             1        true
```

Model-call counts therefore describe provider tries rather than logical calls. Group by `LogicalCallId` to count logical calls, and use `AttemptNumber > 1` to identify fallback usage. Because `TryNumber` is separate, retrying the primary model does not inflate fallback totals.

A logical call is reported as recovered whenever an earlier row failed and the final row succeeded, so a call rescued by a retry counts as recovered in the same way as one rescued by a fallback model.

The metrics dashboard marks these rows with a **Retry** badge alongside the existing **Fallback** badge.
