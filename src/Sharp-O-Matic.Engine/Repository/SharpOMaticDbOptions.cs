namespace SharpOMatic.Engine.Repository;

public class SharpOMaticDbOptions
{
    public string? DefaultSchema { get; set; }
    public string? TablePrefix { get; set; }
    public int? CommandTimeout { get; set; }
}
