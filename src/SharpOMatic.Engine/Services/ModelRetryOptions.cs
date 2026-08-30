namespace SharpOMatic.Engine.Services;

public class ModelRetryOptions
{
    public int MaxTries { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    public double BackoffFactor { get; set; } = 2;
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    public double JitterFactor { get; set; } = 0.2;

    public HashSet<ModelFallbackFailureCategory> RetryableCategories { get; set; } =
        new()
        {
            ModelFallbackFailureCategory.RateLimited,
            ModelFallbackFailureCategory.ProviderUnavailable,
            ModelFallbackFailureCategory.Timeout,
            ModelFallbackFailureCategory.Network,
        };
}
