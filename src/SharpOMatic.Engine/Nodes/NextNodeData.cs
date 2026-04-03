namespace SharpOMatic.Engine.Nodes;

public record class NextNodeData(
    ThreadContext ThreadContext,
    NodeEntity Node,
    NodeInvocationKind InvocationKind = NodeInvocationKind.Run,
    NodeResumeInput? ResumeInput = null
);
