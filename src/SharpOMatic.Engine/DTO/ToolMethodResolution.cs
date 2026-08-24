namespace SharpOMatic.Engine.DTO;

/// <summary>
/// The outcome of resolving a stored <c>selected_tools</c> entry against the tool method registry. The descriptor
/// carries the tool name that must be presented to the model, which is never the qualified name because providers
/// restrict function names to letters, digits, underscores and hyphens.
/// </summary>
public sealed record ToolMethodResolution(ToolMethodDescriptor Descriptor, Delegate Method);
