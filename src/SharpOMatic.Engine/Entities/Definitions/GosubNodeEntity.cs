namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.Gosub)]
public class GosubNodeEntity : NodeEntity
{
    public required Guid? WorkflowId { get; set; }
    public required bool ApplyInputMappings { get; set; }
    public required ContextEntryListEntity InputMappings { get; set; }
    public required bool ApplyOutputMappings { get; set; }
    public required ContextEntryListEntity OutputMappings { get; set; }
}
