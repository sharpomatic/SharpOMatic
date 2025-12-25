namespace SharpOMatic.Engine.Repository;

public class Setting
{
    [Key]
    public required Guid SettingId { get; set; }
    public required string Name { get; set; }
    public required SettingType SettingType { get; set; }
    public string? ValueString { get; set; }
    public bool? ValueBoolean { get; set; }
    public int? ValueInteger { get; set; }
    public double? ValueDouble { get; set; }
}
