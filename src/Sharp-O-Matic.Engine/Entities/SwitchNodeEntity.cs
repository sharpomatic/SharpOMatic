namespace SharpOMatic.Engine.Entities;

public class SwitchNodeEntity : NodeEntity
{
    public required SwitchEntryEntity[] Switches { get; set; }
}

