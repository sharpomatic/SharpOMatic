namespace SharpOMatic.Engine.DTO;

/// <summary>
/// Describes a registered tool method. <see cref="ClassName"/> is the name of the type declaring the method and is
/// empty when the method has no useful declaring type, such as a lambda compiled onto a closure class.
/// <see cref="ToolName"/> is the name presented to the model. <see cref="QualifiedName"/> combines the two and is the
/// value stored in the model call node <c>selected_tools</c> configuration so tools sharing a name can be told apart.
/// <see cref="IsUnqualifiedMatch"/> is true when an unqualified selection of <see cref="ToolName"/> resolves to this
/// method, which is how workflows saved before class names existed keep running.
/// </summary>
public sealed record ToolMethodDescriptor(string ClassName, string ToolName, bool IsUnqualifiedMatch = true)
{
    public string QualifiedName => string.IsNullOrWhiteSpace(ClassName) ? ToolName : $"{ClassName}.{ToolName}";
}
