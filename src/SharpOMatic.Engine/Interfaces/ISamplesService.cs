namespace SharpOMatic.Engine.Interfaces;

public interface ISamplesService
{
    Task<IReadOnlyList<string>> GetWorkflowNames();
    Task<Guid> CreateWorkflow(string sampleName);
}
