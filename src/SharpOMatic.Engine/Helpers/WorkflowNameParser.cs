namespace SharpOMatic.Engine.Helpers;

public static class WorkflowNameParser
{
    public static WorkflowNameParts Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new SharpOMaticException("Workflow name cannot be empty or whitespace.");

        var trimmed = raw.Trim();
        if (trimmed.Contains('\\', StringComparison.Ordinal))
            throw new SharpOMaticException("Workflow name cannot contain '\\'. Use 'Folder/Workflow' to reference a foldered workflow.");

        var separator = trimmed.IndexOf('/');
        if (separator < 0)
            return new WorkflowNameParts(null, trimmed);

        if (separator == 0 || separator == trimmed.Length - 1 || trimmed.IndexOf('/', separator + 1) >= 0)
            throw new SharpOMaticException("Workflow name must be either 'Workflow' or 'Folder/Workflow'.");

        var folderName = trimmed[..separator].Trim();
        var workflowName = trimmed[(separator + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(folderName) || string.IsNullOrWhiteSpace(workflowName))
            throw new SharpOMaticException("Workflow name must be either 'Workflow' or 'Folder/Workflow'.");

        return new WorkflowNameParts(folderName, workflowName);
    }
}

public sealed record WorkflowNameParts(string? FolderName, string WorkflowName);
