namespace SharpOMatic.Engine.Interfaces;

public interface ISharpOMaticNotification
{
    Task RunProgress(Run run);
    Task TraceProgress(Trace trace);
}
