namespace SharpOMatic.Engine.Interfaces;

public interface ISharpOMaticCode
{
    Task<List<CodeCheckResultModel>> CodeCheck(CodeCheckRequestModel request);
}
