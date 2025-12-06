namespace SharpOMatic.Engine.Interfaces;

public interface ICodeCheck
{
    Task<List<CodeCheckResult>> CodeCheck(CodeCheckRequest request);
}
