namespace SharpOMatic.Engine.Interfaces;

public interface IProgressService
{
    Task RunProgress(Run run);
    Task TraceProgress(Run run, Trace trace);
    Task InformationsProgress(Run run, List<Information> informations);
    Task StreamEventProgress(Run run, List<StreamEventProgressItem> events);
    Task EvalRunProgress(EvalRun evalRun);
}
