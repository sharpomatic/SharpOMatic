namespace SharpOMatic.Engine.Contexts;

public class ScriptCodeContext
{
    public required IServiceProvider ServiceProvider { get; set; }
    public required ContextObject Context { get; set; }
    public required AssetHelper Assets { get; set; }
}
