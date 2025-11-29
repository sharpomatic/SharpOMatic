namespace SharpOMatic.Engine.Nodes;

public class EditNode(RunContext runContext, EditNodeEntity node)
    : RunNode<EditNodeEntity>(runContext, node)
{
    public override async Task<NodeEntity?> Run()
    {
        await base.Run();

        try
        {
            foreach (var entry in Node.Edits.Entries)
            {
                if (entry.Purpose == ContextEntryPurpose.Upsert)
                {
                    if (string.IsNullOrWhiteSpace(entry.InputPath))
                        throw new SharpOMaticException($"Edit node upsert path cannot be empty.");

                    var entryValue = await EvaluateContextEntryValue(entry);

                    if (!RunContext.NodeContext.TrySet(entry.InputPath, entryValue))
                        throw new SharpOMaticException($"Edit node entry '{entry.InputPath}' could not be assigned the value.");
                }
            }

            foreach (var entry in Node.Edits.Entries)
            {
                if (entry.Purpose == ContextEntryPurpose.Delete)
                {
                    if (string.IsNullOrWhiteSpace(entry.InputPath))
                        throw new SharpOMaticException($"Edit node delete path cannot be empty.");

                    RunContext.NodeContext.RemovePath(entry.InputPath);
                }
            }

            var numUpserts = Node.Edits.Entries.Where(e => e.Purpose == ContextEntryPurpose.Upsert).Count();
            var numDeletes = Node.Edits.Entries.Where(e => e.Purpose == ContextEntryPurpose.Delete).Count();

            StringBuilder message = new();
            if (numUpserts == 0)
                message.Append("No upserts");
            else if (numUpserts == 1)
                message.Append("1 upsert");
            else
                message.Append($"{numUpserts} upserts");

            message.Append($", {numDeletes} deleted");

            var nextNode = RunContext.ResolveSingleOutput(Node);

            Trace.NodeStatus = NodeStatus.Success;
            Trace.Finished = DateTime.Now;
            Trace.Message = message.ToString();
            Trace.OutputContext = RunContext.TypedSerialization(RunContext.NodeContext);
            await NodeUpdated();

            return nextNode;
        }
        catch (Exception ex)
        {
            Trace.NodeStatus = NodeStatus.Failed;
            Trace.Finished = DateTime.Now;
            Trace.Message = "Failed";
            Trace.Error = ex.Message;
            Trace.OutputContext = RunContext.TypedSerialization(RunContext.NodeContext);
            await NodeUpdated();

            throw;
        }
    }
}
