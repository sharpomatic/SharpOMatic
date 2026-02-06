using Location = SharpOMatic.Engine.Helpers.Location;

namespace SharpOMatic.Engine.Exceptions;

public class FastSerializationException(Location location, string message) : SharpOMaticException(location, message)
{
    public static FastSerializationException UnexpectedEndOfFile(Location location) => new(location, "Unexpected end of file encountered.");

    public static FastSerializationException UnrecognizedCharacterCode(Location location, char c) => new(location, $"Unrecognized character code '{(int)c}' found.");

    public static FastSerializationException UnrecognizedKeyword(Location location, string keyword) => new(location, $"Unrecognized keyword '{keyword}'.");

    public static FastSerializationException IllegalCharacterCode(Location location, char c) => new(location, $"Illegal character code '{(int)c}' for this location.");

    public static FastSerializationException MinusMustBeFollowedByDigit(Location location) => new(location, "Minus sign must be followed by a digit.");

    public static FastSerializationException PointMustBeFollowedByDigit(Location location) => new(location, "Decimal point must be followed by a digit.");

    public static FastSerializationException ExponentMustHaveDigit(Location location) => new(location, "Exponent must have at least one digit.");

    public static FastSerializationException FloatCannotBeFollowed(Location location, string param) => new(location, $"Floating point value cannot be followed by a {param.ToLower()}.");

    public static FastSerializationException IntCannotBeFollowed(Location location, string param) => new(location, $"Integer value cannot be followed by a {param.ToLower()}.");

    public static FastSerializationException EscapeAtLeast1Hex(Location location) => new(location, "Escaped character must have at least 1 hexadecimal value.");

    public static FastSerializationException EscapeOnlyUsingHex(Location location) => new(location, "Escaped character must be specificed only using hexadecimal values.");

    public static FastSerializationException EscapeCannotBeConverted(Location location, string param) => new(location, $"Cannot escape characters using hexidecimal value '{param}'.");

    public static FastSerializationException EscapeMustBeOneOf(Location location) => new(location, "Escaped character is not one of \\\" \\\\ \\/ \\b \\f \\n \\r \\t.");

    public static FastSerializationException ExpectedTokenNotFound(Location location, string expected, string found) => new(location, $"Expected token '{expected}' but found '{found}' instead.");

    public static FastSerializationException TokenNotAllowedHere(Location location, string found) => new(location, $"Token '{found}' not allowed in this position.");
}
