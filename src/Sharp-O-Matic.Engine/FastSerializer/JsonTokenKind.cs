namespace SharpOMatic.Engine.FastSerializer;

public enum FastJsonTokenKind : byte
{
    Colon = 4,
    LeftSquareBracket = 5,
    RightSquareBracket = 6,
    LeftCurlyBracket = 7,
    RightCurlyBracket = 8,
    Comma = 9,
    StartOfText = 17,
    EndOfText = 18,
    IntValue = 19,
    FloatValue = 20,
    StringValue = 21,
    TrueValue = 22,
    FalseValue = 23,
    NullValue = 24,
}
