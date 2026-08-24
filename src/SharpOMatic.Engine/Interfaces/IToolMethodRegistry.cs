namespace SharpOMatic.Engine.Interfaces;

public interface IToolMethodRegistry
{
    public IReadOnlyList<Delegate> GetMethods();
    public IReadOnlyList<string> GetToolDisplayNames();
    public Delegate? GetToolFromDisplayName(string displayName);

    /// <summary>
    /// Describes every registered tool method including the declaring class name, so the editor can tell apart tools
    /// that share a tool name. Implemented by default in terms of <see cref="GetToolDisplayNames"/> so existing custom
    /// registries keep compiling; they simply report no class names.
    /// </summary>
    public IReadOnlyList<ToolMethodDescriptor> GetToolMethods()
    {
        return [.. GetToolDisplayNames().Select(displayName => new ToolMethodDescriptor(string.Empty, displayName))];
    }

    /// <summary>
    /// Resolves a stored <c>selected_tools</c> entry, which is either a class qualified <c>Class.Tool</c> name or a
    /// bare tool name. A bare name matches the first registered tool with that name. Implemented by default in terms of
    /// <see cref="GetToolFromDisplayName"/> so existing custom registries keep compiling.
    /// </summary>
    public ToolMethodResolution? ResolveTool(string selectedTool)
    {
        var method = GetToolFromDisplayName(selectedTool);
        return method is null ? null : new ToolMethodResolution(new ToolMethodDescriptor(string.Empty, selectedTool), method);
    }
}
