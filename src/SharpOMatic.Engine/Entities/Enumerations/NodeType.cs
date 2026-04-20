namespace SharpOMatic.Engine.Entities.Enumerations;

public enum NodeType
{
    Start = 0,
    End = 1,
    Code = 2,
    Edit = 3,
    Switch = 4,
    FanIn = 5,
    FanOut = 6,
    ModelCall = 7,
    Batch = 8,
    Gosub = 9,
    Suspend = 10,
    FrontendToolCall = 11,
    BackendToolCall = 12,
    StepStart = 13,
    StepEnd = 14,
}
