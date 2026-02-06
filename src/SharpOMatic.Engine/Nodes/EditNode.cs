using SharpOMatic.Engine.Services;

namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Edit)]
public class EditNode(ThreadContext threadContext, EditNodeEntity node) : RunNode<EditNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        foreach (var entry in Node.Edits.Entries)
        {
            if (entry.Purpose == ContextEntryPurpose.Move)
            {
                if (string.IsNullOrWhiteSpace(entry.InputPath))
                    throw new SharpOMaticException($"Edit node move 'from' path cannot be empty.");

                if (string.IsNullOrWhiteSpace(entry.OutputPath))
                    throw new SharpOMaticException($"Edit node move 'to' path cannot be empty.");

                if (string.Equals(entry.InputPath, entry.OutputPath, StringComparison.Ordinal))
                    throw new SharpOMaticException($"Edit node cannot move path to itself");

                if (ThreadContext.NodeContext.TryGet<object?>(entry.InputPath, out var mapValue))
                {
                    if (!ThreadContext.NodeContext.TrySet(entry.OutputPath, mapValue))
                        throw new SharpOMaticException($"Edit node move entry '{entry.OutputPath}' could not be assigned the value.");

                    ThreadContext.NodeContext.RemovePath(entry.InputPath);
                }
            }
        }

        foreach (var entry in Node.Edits.Entries)
        {
            if (entry.Purpose == ContextEntryPurpose.Duplicate)
            {
                if (string.IsNullOrWhiteSpace(entry.InputPath))
                    throw new SharpOMaticException($"Edit node duplicate 'from' path cannot be empty.");

                if (string.IsNullOrWhiteSpace(entry.OutputPath))
                    throw new SharpOMaticException($"Edit node duplicate 'to' path cannot be empty.");

                if (string.Equals(entry.InputPath, entry.OutputPath, StringComparison.Ordinal))
                    throw new SharpOMaticException($"Edit node cannot duplicate path to itself");

                if (ThreadContext.NodeContext.TryGet<object?>(entry.InputPath, out var mapValue))
                {
                    if (mapValue is not null)
                    {
                        var typeInfo = mapValue.GetType();
                        var json = JsonSerializer.Serialize(mapValue, new JsonSerializerOptions().BuildOptions(ProcessContext.JsonConverters));
                        mapValue = JsonSerializer.Deserialize(json, typeInfo, new JsonSerializerOptions().BuildOptions(ProcessContext.JsonConverters));
                    }

                    if (!ThreadContext.NodeContext.TrySet(entry.OutputPath, mapValue))
                        throw new SharpOMaticException($"Edit node duplicate entry '{entry.OutputPath}' could not be assigned the duplicate.");
                }
            }
        }

        foreach (var entry in Node.Edits.Entries)
        {
            if (entry.Purpose == ContextEntryPurpose.Upsert)
            {
                if (string.IsNullOrWhiteSpace(entry.InputPath))
                    throw new SharpOMaticException($"Edit node upsert path cannot be empty.");

                var entryValue = await EvaluateContextEntryValue(entry);

                if (!ThreadContext.NodeContext.TrySet(entry.InputPath, entryValue))
                    throw new SharpOMaticException($"Edit node entry '{entry.InputPath}' could not be assigned the value.");
            }
        }

        foreach (var entry in Node.Edits.Entries)
        {
            if (entry.Purpose == ContextEntryPurpose.Delete)
            {
                if (string.IsNullOrWhiteSpace(entry.InputPath))
                    throw new SharpOMaticException($"Edit node delete path cannot be empty.");

                ThreadContext.NodeContext.RemovePath(entry.InputPath);
            }
        }

        var numUpserts = Node.Edits.Entries.Where(e => e.Purpose == ContextEntryPurpose.Upsert).Count();
        var numMoves = Node.Edits.Entries.Where(e => e.Purpose == ContextEntryPurpose.Move).Count();
        var numDuplicates = Node.Edits.Entries.Where(e => e.Purpose == ContextEntryPurpose.Duplicate).Count();
        var numDeletes = Node.Edits.Entries.Where(e => e.Purpose == ContextEntryPurpose.Delete).Count();

        var message = $"{numMoves} moved, {numDuplicates} duplicated, {numUpserts} upserted, {numDeletes} deleted";
        return (message, ResolveOptionalSingleOutput(ThreadContext));
    }
}
