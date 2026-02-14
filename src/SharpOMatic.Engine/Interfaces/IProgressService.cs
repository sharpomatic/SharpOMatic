namespace SharpOMatic.Engine.Interfaces;

public interface IProgressService
{
    Task RunProgress(Run run);
    Task TraceProgress(Trace trace);
    Task EvalRunProgress(EvalRun evalRun);
}
