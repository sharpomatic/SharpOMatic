namespace SharpOMatic.Engine.Entities.Definitions;

[NodeEntity(NodeType.ModelCall)]
public class ModelCallNodeEntity : NodeEntity
{
    public required Guid? ModelId { get; set; }
    public bool BatchOutput { get; set; }
    public bool DropToolCalls { get; set; }
    public bool DisableStreamUser { get; set; }
    public bool DisableStreamTool { get; set; }
    public bool DisableStreamReasoning { get; set; }
    public bool DisableStreamAssistantText { get; set; }
    public Dictionary<string, ModelCallToolAgUiOutputMode> ToolAgUiOutputModes { get; set; } = [];
    public required string Instructions { get; set; }
    public required string Prompt { get; set; }
    public required string ChatInputPath { get; set; }
    public required string ChatOutputPath { get; set; }
    public required string TextOutputPath { get; set; }
    public required string ImageInputPath { get; set; }
    public required string ImageOutputPath { get; set; }
    public required Dictionary<string, string?> ParameterValues { get; set; }

    /// <summary>
    /// True when the model response will not produce any incremental AG-UI stream events: assistant text,
    /// reasoning and tool events are all disabled at the node level AND no per-tool override re-enables them
    /// with <see cref="ModelCallToolAgUiOutputMode.Always"/>. Note this deliberately ignores
    /// <see cref="DisableStreamUser"/> because the user prompt events are emitted before the model call
    /// (independently of streaming vs batch) and so do not affect whether the response needs to be streamed.
    /// Expressed as a method rather than a property so it is never serialized into the workflow snapshot.
    /// </summary>
    public bool IsAgUiResponseStreamSuppressed()
    {
        return DisableStreamAssistantText
            && DisableStreamReasoning
            && DisableStreamTool
            && !ToolAgUiOutputModes.Values.Any(mode => mode == ModelCallToolAgUiOutputMode.Always);
    }
}
