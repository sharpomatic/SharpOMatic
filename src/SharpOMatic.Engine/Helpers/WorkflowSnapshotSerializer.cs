namespace SharpOMatic.Engine.Helpers;

public static class WorkflowSnapshotSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new NodeEntityConverter() },
    };

    public static string SerializeWorkflow(WorkflowEntity workflow)
    {
        return JsonSerializer.Serialize(workflow, _options);
    }

    public static WorkflowEntity DeserializeWorkflow(string json)
    {
        var workflow = JsonSerializer.Deserialize<WorkflowEntity>(json, _options)
            ?? throw new SharpOMaticException("Workflow snapshot could not be deserialized.");
        return WorkflowSchemaUpgrader.Upgrade(workflow);
    }
}
