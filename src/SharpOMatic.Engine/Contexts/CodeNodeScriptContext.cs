namespace SharpOMatic.Engine.Contexts;

public class CodeNodeScriptContext : ScriptCodeContext
{
    public required DebugInformationHelper Debug { get; set; }
    public required StreamEventHelper Events { get; set; }
}
