namespace SharpOMatic.Engine.Interfaces;

public interface IProgressService
{
    Task RunProgress(Run run);
    Task TraceProgress(Run run, Trace trace);
    Task InformationsProgress(Run run, List<Information> informations);
    Task EvalRunProgress(EvalRun evalRun);
}
