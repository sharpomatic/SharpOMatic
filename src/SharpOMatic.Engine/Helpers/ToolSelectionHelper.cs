namespace SharpOMatic.Engine.Helpers;

/// <summary>
/// Helpers for the model call node tool selection format. A selection entry is either a bare tool name, as written by
/// older versions of the editor, or a class qualified <c>Class.Tool</c> name. The per-tool side settings on the node
/// are keyed by the same entry, so lookups fall back to matching on the tool name when a node mixes the two forms.
/// </summary>
public static class ToolSelectionHelper
{
    public static IReadOnlyList<string> ParseSelectedTools(string? selectedTools)
    {
        if (string.IsNullOrWhiteSpace(selectedTools))
            return [];

        return [.. selectedTools.Split(',').Select(entry => entry.Trim()).Where(entry => entry.Length > 0)];
    }

    /// <summary>
    /// Returns the tool name portion of a selection entry, which is the whole entry when it is not class qualified.
    /// </summary>
    public static string GetToolName(string? selectedTool)
    {
        if (string.IsNullOrWhiteSpace(selectedTool))
            return string.Empty;

        var trimmed = selectedTool.Trim();
        var separator = trimmed.LastIndexOf('.');

        return (separator < 0) || (separator == trimmed.Length - 1) ? trimmed : trimmed[(separator + 1)..];
    }

    /// <summary>
    /// Looks up a per-tool setting for a selection entry. An exact key match wins, otherwise any key sharing the same
    /// tool name is used so a node saved with bare keys still honours its settings once entries become qualified, and
    /// so a lookup by the model facing tool name finds a qualified key.
    /// </summary>
    public static bool TryGetToolSetting<TValue>(IDictionary<string, TValue>? settings, string? selectedTool, out TValue? value)
    {
        value = default;

        if ((settings is null) || (settings.Count == 0) || string.IsNullOrWhiteSpace(selectedTool))
            return false;

        var trimmed = selectedTool.Trim();
        if (settings.TryGetValue(trimmed, out var exact))
        {
            value = exact;
            return true;
        }

        var toolName = GetToolName(trimmed);
        foreach (var setting in settings)
        {
            if (string.Equals(GetToolName(setting.Key), toolName, StringComparison.Ordinal))
            {
                value = setting.Value;
                return true;
            }
        }

        return false;
    }
}
