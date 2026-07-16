
namespace SharpOMatic.Engine.Exceptions;

public class SharpOMaticException : Exception
{
    public SharpOMaticException(Location location, string message)
        : base(message)
    {
        Location = location;
    }

    [SetsRequiredMembers]
    public SharpOMaticException(string message)
        : this(Location.Empty, message) { }

    [SetsRequiredMembers]
    public SharpOMaticException(string message, Exception innerException)
        : base(message, innerException)
    {
        Location = Location.Empty;
    }

    public Location Location { get; init; }
}

public class SharpOMaticExceptions(IEnumerable<SharpOMaticException> innerExceptions) : Exception("Aggregate SharpOMaticExceptions")
{
    private readonly List<SharpOMaticException> _innerExceptions = new(innerExceptions);
    private ReadOnlyCollection<SharpOMaticException>? _readOnlyExceptions;

    public SharpOMaticExceptions(SharpOMaticException innerException)
        : this([innerException]) { }

    public ReadOnlyCollection<SharpOMaticException> InnerExceptions => _readOnlyExceptions ??= new ReadOnlyCollection<SharpOMaticException>(_innerExceptions);
}
