namespace SharpOMatic.Engine.Interfaces;

public interface IProgressService
{
    Task RunProgress(Run run);
    Task TraceProgress(Trace trace);
    Task InformationsProgress(List<Information> informations);
    Task EvalRunProgress(EvalRun evalRun);
}
