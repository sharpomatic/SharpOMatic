namespace SharpOMatic.Engine.DTO;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ContinueResumeInput), "continue")]
[JsonDerivedType(typeof(AgUiAgentResumeInput), "agui-agent")]
[JsonDerivedType(typeof(ContextMergeResumeInput), "context-merge")]
public abstract class NodeResumeInput { }
