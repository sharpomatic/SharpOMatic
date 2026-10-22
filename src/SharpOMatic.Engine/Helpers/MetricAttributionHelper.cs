namespace SharpOMatic.Engine.Helpers;

public static class MetricAttributionHelper
{
    public const string DefaultTenantIdPath = "auth.tenantId";
    public const string DefaultUserIdPath = "auth.userId";
    public const int MaxAttributionLength = 256;

    public static (string? TenantId, string? UserId) Resolve(WorkflowEntity workflow, ContextObject context)
    {
        return (ResolveValue(context, workflow.TenantIdPath, DefaultTenantIdPath), ResolveValue(context, workflow.UserIdPath, DefaultUserIdPath));
    }

    public static (string? ReferenceId, string? ReferenceType) ResolveReference(WorkflowEntity workflow, ContextObject context)
    {
        var referenceId = ResolveValue(context, workflow.ReferenceIdPath, null);
        var referenceType = ResolveValue(context, workflow.ReferenceTypePath, null);

        return referenceId is null || referenceType is null ? (null, null) : (referenceId, referenceType);
    }

    private static string? ResolveValue(ContextObject context, string? path, string? defaultPath)
    {
        if (string.IsNullOrWhiteSpace(path))
            path = defaultPath;

        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!context.TryGet<object?>(path.Trim(), out var value))
            return null;

        var attribution = value switch
        {
            Guid guid => guid.ToString(),
            string text => Normalize(text),
            _ => null,
        };

        if (attribution is null || attribution.Length > MaxAttributionLength)
            return null;

        return attribution;
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Guid.TryParse(value, out var guid) ? guid.ToString() : value.Trim();
    }
}
