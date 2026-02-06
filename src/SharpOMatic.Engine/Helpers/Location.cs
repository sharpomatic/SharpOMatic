namespace SharpOMatic.Engine.Helpers;

public class Location
{
    public static readonly Location Empty = new();

    [SetsRequiredMembers]
    protected Location()
    {
        Position = 0;
        Line = 0;
        Column = 0;
    }

    [SetsRequiredMembers]
    public Location(int position, int line, int column)
    {
        Position = position;
        Line = line;
        Column = column;
    }

    public required int Position { get; init; }
    public required int Line { get; init; }
    public required int Column { get; init; }
}
