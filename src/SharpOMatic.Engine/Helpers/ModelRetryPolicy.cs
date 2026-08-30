namespace SharpOMatic.Engine.Helpers;

public static class ModelRetryPolicy
{
    public static (ModelRetryDecision Decision, string Reason) Decide(ModelFallbackFailure failure, int tryNumber, ModelRetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxTries <= 1)
            return (new ModelRetryDecision(false, TimeSpan.Zero), "Retries are disabled.");

        if (tryNumber >= options.MaxTries)
            return (new ModelRetryDecision(false, TimeSpan.Zero), $"Try {tryNumber} reached the maximum of {options.MaxTries}.");

        if (!options.RetryableCategories.Contains(failure.Category))
            return (new ModelRetryDecision(false, TimeSpan.Zero), $"Failure category '{failure.Category}' is not retryable.");

        var delay = CalculateDelay(failure, tryNumber, options);
        return (new ModelRetryDecision(true, delay), $"Retryable failure category '{failure.Category}'.");
    }

    public static TimeSpan CalculateDelay(ModelFallbackFailure failure, int tryNumber, ModelRetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(options);

        if (failure.RetryAfter is { } retryAfter && retryAfter > TimeSpan.Zero)
            return retryAfter < options.MaxDelay ? retryAfter : options.MaxDelay;

        var scaled = options.BaseDelay.TotalMilliseconds * Math.Pow(options.BackoffFactor, Math.Max(0, tryNumber - 1));
        var jitter = 1 + ((Random.Shared.NextDouble() * 2) - 1) * options.JitterFactor;
        var milliseconds = Math.Min(scaled * jitter, options.MaxDelay.TotalMilliseconds);
        return milliseconds <= 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(milliseconds);
    }
}
