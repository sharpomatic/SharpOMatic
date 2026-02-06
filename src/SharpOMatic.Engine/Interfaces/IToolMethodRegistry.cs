namespace SharpOMatic.Engine.Interfaces;

public interface IToolMethodRegistry
{
    public IReadOnlyList<Delegate> GetMethods();
    public IReadOnlyList<string> GetToolDisplayNames();
    public Delegate? GetToolFromDisplayName(string displayName);
}
