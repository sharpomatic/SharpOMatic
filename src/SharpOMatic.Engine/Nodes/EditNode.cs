namespace SharpOMatic.Engine.Nodes;

[RunNode(NodeType.Edit)]
public class EditNode(ThreadContext threadContext, EditNodeEntity node)
    : RunNode<EditNodeEntity>(threadContext, node)
{
    protected override async Task<(string, List<NextNodeData>)> RunInternal()
    {
        foreach (var entry in Node.Edits.Entries)
        {
            if (entry.Purpose == ContextEntryPurpose.Upsert)
            {
                if (string.IsNullOrWhiteSpace(entry.InputPath))
                    throw new SharpOMaticException($"Edit node upsert path cannot be empty.");

                var entryValue = await EvaluateContextEntryValue(entry);

                if (!ThreadContext.NodeContext.TrySet(entry.InputPath, entryValue))
                    throw new SharpOMaticException(
                        $"Edit node entry '{entry.InputPath}' could not be assigned the value."
                    );
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

        var numUpserts = Node
            .Edits.Entries.Where(e => e.Purpose == ContextEntryPurpose.Upsert)
            .Count();
        var numDeletes = Node
            .Edits.Entries.Where(e => e.Purpose == ContextEntryPurpose.Delete)
            .Count();

        StringBuilder message = new();
        if (numUpserts == 0)
            message.Append("No upserts");
        else if (numUpserts == 1)
            message.Append("1 upsert");
        else
            message.Append($"{numUpserts} upserts");

        message.Append($", {numDeletes} deleted");

        return (message.ToString(), ResolveOptionalSingleOutput(ThreadContext));
    }
}
