namespace SharpOMatic.Engine.Entities.Definitions;

public class ModelCallModelDefinition
{
    public required Guid ModelId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Disabled { get; set; }
    public Dictionary<string, string?> ParameterValues { get; set; } = [];
}
