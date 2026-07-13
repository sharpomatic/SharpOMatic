namespace SharpOMatic.Engine.Helpers;

public static class ModelFallbackFailureClassifier
{
    public static ModelFallbackFailure Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var statusCode = TryGetStatusCode(exception);
        if (statusCode.HasValue)
            return ClassifyStatusCode(statusCode.Value);

        var category = exception switch
        {
            _ when Find<OperationCanceledException>(exception) is not null => ModelFallbackFailureCategory.Cancellation,
            _ when Find<TimeoutException>(exception) is not null || Find<TaskCanceledException>(exception) is not null => ModelFallbackFailureCategory.Timeout,
            _ when Find<HttpRequestException>(exception) is not null || Find<IOException>(exception) is not null => ModelFallbackFailureCategory.Network,
            _ when Find<SharpOMaticException>(exception) is not null => ModelFallbackFailureCategory.Configuration,
            _ => ModelFallbackFailureCategory.Unknown,
        };

        var isTransient = category is ModelFallbackFailureCategory.RateLimited or ModelFallbackFailureCategory.ProviderUnavailable or ModelFallbackFailureCategory.Timeout or ModelFallbackFailureCategory.Network;
        return new ModelFallbackFailure(category, statusCode, RetryAfter: null, isTransient);
    }

    public static ModelFallbackFailure ClassifyStatusCode(int statusCode, TimeSpan? retryAfter = null)
    {
        var category = statusCode switch
        {
            401 or 403 => ModelFallbackFailureCategory.Authentication,
            408 => ModelFallbackFailureCategory.Timeout,
            429 => ModelFallbackFailureCategory.RateLimited,
            >= 500 and <= 599 => ModelFallbackFailureCategory.ProviderUnavailable,
            >= 400 and <= 499 => ModelFallbackFailureCategory.InvalidRequest,
            _ => ModelFallbackFailureCategory.Unknown,
        };
        var isTransient = category is ModelFallbackFailureCategory.RateLimited or ModelFallbackFailureCategory.ProviderUnavailable or ModelFallbackFailureCategory.Timeout;
        return new ModelFallbackFailure(category, statusCode, retryAfter, isTransient);
    }

    public static TException? Find<TException>(Exception exception)
        where TException : Exception
    {
        return Enumerate(exception).OfType<TException>().FirstOrDefault();
    }

    private static int? TryGetStatusCode(Exception exception)
    {
        foreach (var current in Enumerate(exception))
        {
            if (current is ClientResultException clientResultException)
                return clientResultException.Status;

            if (current is RequestFailedException requestFailedException)
                return requestFailedException.Status;

            if (current is HttpRequestException { StatusCode: not null } httpRequestException)
                return (int)httpRequestException.StatusCode.Value;
        }

        return null;
    }

    private static IEnumerable<Exception> Enumerate(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            yield return current;

            if (current is AggregateException aggregateException)
            {
                foreach (var inner in aggregateException.Flatten().InnerExceptions)
                {
                    foreach (var nested in Enumerate(inner))
                        yield return nested;
                }
            }
        }
    }
}
