namespace SharpOMatic.Engine.Helpers;

internal static class ActivityJsonPatchHelper
{
    internal static List<Dictionary<string, object?>> BuildPatch(string oldJson, string newJson, bool allowRootReplace = false)
    {
        using var oldDocument = JsonDocument.Parse(oldJson);
        using var newDocument = JsonDocument.Parse(newJson);

        List<Dictionary<string, object?>> operations = [];
        AppendDiffOperations(oldDocument.RootElement, newDocument.RootElement, string.Empty, operations, allowRootReplace);
        return operations;
    }

    private static void AppendDiffOperations(JsonElement oldValue, JsonElement newValue, string path, List<Dictionary<string, object?>> operations, bool allowRootReplace)
    {
        if (JsonElement.DeepEquals(oldValue, newValue))
            return;

        if (oldValue.ValueKind != newValue.ValueKind)
        {
            AddReplaceOperation(path, newValue, operations, allowRootReplace);
            return;
        }

        switch (oldValue.ValueKind)
        {
            case JsonValueKind.Object:
                AppendObjectDiffOperations(oldValue, newValue, path, operations, allowRootReplace);
                return;
            case JsonValueKind.Array:
                AppendArrayDiffOperations(oldValue, newValue, path, operations, allowRootReplace);
                return;
            default:
                AddReplaceOperation(path, newValue, operations, allowRootReplace);
                return;
        }
    }

    private static void AppendObjectDiffOperations(JsonElement oldObject, JsonElement newObject, string path, List<Dictionary<string, object?>> operations, bool allowRootReplace)
    {
        Dictionary<string, JsonElement> oldProperties = [];
        foreach (var property in oldObject.EnumerateObject())
            oldProperties[property.Name] = property.Value;

        Dictionary<string, JsonElement> newProperties = [];
        foreach (var property in newObject.EnumerateObject())
            newProperties[property.Name] = property.Value;

        foreach (var property in oldObject.EnumerateObject())
        {
            if (!newProperties.ContainsKey(property.Name))
                operations.Add(CreatePatchOperation("remove", BuildJsonPointer(path, property.Name)));
        }

        foreach (var property in newObject.EnumerateObject())
        {
            var propertyPath = BuildJsonPointer(path, property.Name);

            if (!oldProperties.TryGetValue(property.Name, out var oldPropertyValue))
            {
                operations.Add(CreatePatchOperation("add", propertyPath, DeserializeJsonElementValue(property.Value)));
                continue;
            }

            AppendDiffOperations(oldPropertyValue, property.Value, propertyPath, operations, allowRootReplace);
        }
    }

    private static void AppendArrayDiffOperations(JsonElement oldArray, JsonElement newArray, string path, List<Dictionary<string, object?>> operations, bool allowRootReplace)
    {
        var oldLength = oldArray.GetArrayLength();
        var newLength = newArray.GetArrayLength();

        if (oldLength != newLength)
        {
            AddReplaceOperation(path, newArray, operations, allowRootReplace);
            return;
        }

        for (var i = 0; i < oldLength; i++)
        {
            AppendDiffOperations(oldArray[i], newArray[i], BuildJsonPointer(path, i), operations, allowRootReplace);
        }
    }

    private static Dictionary<string, object?> CreatePatchOperation(string operation, string path, object? value = null)
    {
        Dictionary<string, object?> patchOperation = [];
        patchOperation["op"] = operation;
        patchOperation["path"] = path;

        if (operation is "add" or "replace")
            patchOperation["value"] = value;

        return patchOperation;
    }

    private static void AddReplaceOperation(string path, JsonElement value, List<Dictionary<string, object?>> operations, bool allowRootReplace)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (!allowRootReplace)
                throw new SharpOMaticException("Activity diff cannot replace the root activity object.");
        }

        operations.Add(CreatePatchOperation("replace", path, DeserializeJsonElementValue(value)));
    }

    private static object? DeserializeJsonElementValue(JsonElement value)
    {
        return ContextHelpers.FastDeserializeString(value.GetRawText());
    }

    private static string BuildJsonPointer(string path, string propertyName)
    {
        return $"{path}/{EscapeJsonPointerSegment(propertyName)}";
    }

    private static string BuildJsonPointer(string path, int index)
    {
        return $"{path}/{index}";
    }

    private static string EscapeJsonPointerSegment(string segment)
    {
        return segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }
}
