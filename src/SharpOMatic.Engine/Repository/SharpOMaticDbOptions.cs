namespace SharpOMatic.Engine.Repository;

public class SharpOMaticDbOptions
{
    public int? CommandTimeout { get; set; }
    public bool? ApplyMigrationsOnStartup { get; set; }
}
