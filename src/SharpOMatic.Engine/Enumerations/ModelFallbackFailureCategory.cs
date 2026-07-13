namespace SharpOMatic.Engine.Enumerations;

public enum ModelFallbackFailureCategory
{
    Unknown,
    RateLimited,
    ProviderUnavailable,
    Timeout,
    Network,
    Authentication,
    InvalidRequest,
    Configuration,
    Cancellation,
}
