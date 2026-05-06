namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    [HttpGet("model-calls")]
    public Task<ModelCallMetricsDashboard> GetModelCallMetrics(
        IRepositoryService repositoryService,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] ModelCallMetricBucket? bucket = null,
        [FromQuery] ModelCallMetricScope scope = ModelCallMetricScope.All,
        [FromQuery] string? scopeKey = null,
        [FromQuery] string? masterSearch = null,
        [FromQuery] int recentSkip = 0,
        [FromQuery] int recentTake = 25
    )
    {
        var normalizedEnd = EnsureUtc(end ?? DateTime.UtcNow);
        var normalizedStart = EnsureUtc(start ?? normalizedEnd.AddDays(-7));
        var normalizedBucket = bucket ?? GetDefaultBucket(normalizedStart, normalizedEnd);

        var request = new ModelCallMetricsDashboardRequest(
            normalizedStart,
            normalizedEnd,
            normalizedBucket,
            scope,
            string.IsNullOrWhiteSpace(scopeKey) ? null : scopeKey.Trim(),
            string.IsNullOrWhiteSpace(masterSearch) ? null : masterSearch.Trim(),
            recentSkip,
            recentTake
        );

        return repositoryService.GetModelCallMetricsDashboard(request);
    }

    private static ModelCallMetricBucket GetDefaultBucket(DateTime start, DateTime end)
    {
        var range = end - start;
        if (range <= TimeSpan.FromDays(2))
            return ModelCallMetricBucket.Hour;

        if (range <= TimeSpan.FromDays(90))
            return ModelCallMetricBucket.Day;

        return ModelCallMetricBucket.Week;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }
}
