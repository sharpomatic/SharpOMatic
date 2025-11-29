using SharpOMatic.Engine.Contexts;

namespace SharpOMatic.Engine.Interfaces;

public interface ISharpOMaticEngine
{
    Task<Guid> RunWorkflow(Guid workflowId, ContextObject? context = null, ContextEntryListEntity? inputEntries = null);
}
