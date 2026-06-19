namespace SharpOMatic.Engine.Services;

public class ToolMethodRegistry : IToolMethodRegistry
{
    private readonly List<Delegate> _methods;
    private readonly Dictionary<string, Delegate> _methodsByDisplayName;

    public ToolMethodRegistry(IEnumerable<Delegate> methods)
    {
        _methods = methods.ToList();

        var duplicateDisplayName = _methods.GroupBy(GetToolDisplayName, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).FirstOrDefault();

        if (duplicateDisplayName is not null)
            throw new InvalidOperationException($"Duplicate tool method display name '{duplicateDisplayName}'.");

        _methodsByDisplayName = _methods.ToDictionary(GetToolDisplayName, StringComparer.Ordinal);
    }

    public IReadOnlyList<Delegate> GetMethods() => _methods.AsReadOnly();

    public IReadOnlyList<string> GetToolDisplayNames()
    {
        return [.. _methodsByDisplayName.Keys.OrderBy(m => m)];
    }

    public Delegate? GetToolFromDisplayName(string displayName)
    {
        return _methodsByDisplayName.GetValueOrDefault(displayName);
    }

    private static string GetToolDisplayName(Delegate method)
    {
        var displayName = method.Method.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
        return string.IsNullOrWhiteSpace(displayName) ? method.Method.Name : displayName;
    }
}
