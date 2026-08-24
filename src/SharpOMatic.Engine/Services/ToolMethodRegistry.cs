namespace SharpOMatic.Engine.Services;

public class ToolMethodRegistry : IToolMethodRegistry
{
    private readonly List<Delegate> _methods;
    private readonly List<ToolMethodDescriptor> _descriptors;
    private readonly Dictionary<string, ToolMethodResolution> _resolutionsByQualifiedName;
    private readonly Dictionary<string, ToolMethodResolution> _resolutionsByToolName;

    public ToolMethodRegistry(IEnumerable<Delegate> methods)
    {
        _methods = methods.ToList();
        _descriptors = [];
        _resolutionsByQualifiedName = new Dictionary<string, ToolMethodResolution>(StringComparer.Ordinal);
        _resolutionsByToolName = new Dictionary<string, ToolMethodResolution>(StringComparer.Ordinal);

        foreach (var method in _methods)
        {
            var toolName = GetToolName(method);

            // Tools sharing a name are allowed as long as they come from different classes. The first one registered
            // is what an unqualified selection resolves to, which is how workflows saved before class names existed
            // keep running.
            var isUnqualifiedMatch = !_resolutionsByToolName.ContainsKey(toolName);
            var descriptor = new ToolMethodDescriptor(GetClassName(method), toolName, isUnqualifiedMatch);
            var resolution = new ToolMethodResolution(descriptor, method);

            if (!_resolutionsByQualifiedName.TryAdd(descriptor.QualifiedName, resolution))
                throw new InvalidOperationException($"Duplicate tool method '{descriptor.QualifiedName}'.");

            _descriptors.Add(descriptor);

            if (isUnqualifiedMatch)
                _resolutionsByToolName.Add(toolName, resolution);
        }
    }

    public IReadOnlyList<Delegate> GetMethods() => _methods.AsReadOnly();

    public IReadOnlyList<string> GetToolDisplayNames()
    {
        return [.. _resolutionsByToolName.Keys.OrderBy(name => name, StringComparer.Ordinal)];
    }

    public IReadOnlyList<ToolMethodDescriptor> GetToolMethods()
    {
        return [.. _descriptors.OrderBy(descriptor => descriptor.ToolName, StringComparer.Ordinal).ThenBy(descriptor => descriptor.ClassName, StringComparer.Ordinal)];
    }

    public Delegate? GetToolFromDisplayName(string displayName)
    {
        return ResolveTool(displayName)?.Method;
    }

    public ToolMethodResolution? ResolveTool(string selectedTool)
    {
        if (string.IsNullOrWhiteSpace(selectedTool))
            return null;

        var trimmed = selectedTool.Trim();

        // An exact qualified match is tried first so that a class qualified selection always wins. Falling back to the
        // whole entry as a tool name covers bare selections and also tool names that themselves contain a dot.
        return _resolutionsByQualifiedName.GetValueOrDefault(trimmed) ?? _resolutionsByToolName.GetValueOrDefault(trimmed);
    }

    private static string GetToolName(Delegate method)
    {
        var displayName = method.Method.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
        return string.IsNullOrWhiteSpace(displayName) ? method.Method.Name : displayName;
    }

    private static string GetClassName(Delegate method)
    {
        var declaringType = method.Method.DeclaringType;
        if (declaringType is null)
            return string.Empty;

        // Lambdas and local functions are compiled onto generated closure types whose names are not meaningful to a
        // user, so those tools stay unqualified.
        if (declaringType.IsDefined(typeof(CompilerGeneratedAttribute), false) || declaringType.Name.StartsWith('<'))
            return string.Empty;

        return declaringType.Name;
    }
}
