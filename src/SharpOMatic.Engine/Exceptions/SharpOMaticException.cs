using Location = SharpOMatic.Engine.Helpers.Location;

namespace SharpOMatic.Engine.Exceptions;

public class SharpOMaticException(Location location, string message) : Exception(message)
{
    [SetsRequiredMembers]
    public SharpOMaticException(string message)
        : this(Location.Empty, message) { }

    public Location Location { get; init; } = location;
}

public class SharpOMaticExceptions(IEnumerable<SharpOMaticException> innerExceptions)
    : Exception("Aggregate SharpOMaticExceptions")
{
    private readonly List<SharpOMaticException> _innerExceptions = new(innerExceptions);
    private ReadOnlyCollection<SharpOMaticException>? _readOnlyExceptions;

    public SharpOMaticExceptions(SharpOMaticException innerException)
        : this([innerException]) { }

    public ReadOnlyCollection<SharpOMaticException> InnerExceptions =>
        _readOnlyExceptions ??= new ReadOnlyCollection<SharpOMaticException>(_innerExceptions);
}
