using Location = SharpOMatic.Engine.Helpers.Location;

namespace SharpOMatic.Engine.Exceptions;

public class SyntaxException(Location location, string message) : SharpOMaticException(location, message)
{
    public static SyntaxException UnexpectedEndOfFile(Location location) => new(location, "Unexpected end of file encountered.");
    public static SyntaxException UnrecognizedCharacterCode(Location location, char c) => new(location, $"Unrecognized character code '{(int)c}' found.");
    public static SyntaxException UnrecognizedToken(Location location, string tokenKind) => new(location, $"Unrecognized token '{tokenKind}'.");
    public static SyntaxException UnrecognizedKeyword(Location location, string keyword) => new(location, $"Unrecognized keyword '{keyword}'.");
    public static SyntaxException IllegalCharacterCode(Location location, char c) => new(location, $"Illegal character code '{(int)c}' for this location.");
    public static SyntaxException MinusMustBeFollowedByDigit(Location location) => new(location, "Minus sign must be followed by a digit.");
    public static SyntaxException PointMustBeFollowedByDigit(Location location) => new(location, "Decimal point must be followed by a digit.");
    public static SyntaxException ExponentMustHaveDigit(Location location) => new(location, "Exponent must have at least one digit.");
    public static SyntaxException FloatCannotBeFollowed(Location location, string param) => new(location, $"Floating point value cannot be followed by a {param.ToLower()}.");
    public static SyntaxException IntCannotBeFollowed(Location location, string param) => new(location, $"Integer value cannot be followed by a {param.ToLower()}.");
    public static SyntaxException EscapeAtLeast1Hex(Location location) => new(location, "Escaped character must have at least 1 hexadecimal value.");
    public static SyntaxException EscapeOnlyUsingHex(Location location) => new(location, "Escaped character must be specificed only using hexadecimal values.");
    public static SyntaxException EscapeCannotBeConverted(Location location, string param) => new(location, $"Cannot escape characters using hexidecimal value '{param}'.");
    public static SyntaxException EscapeMustBeOneOf(Location location) => new(location, "Escaped character is not one of \\\" \\\\ \\/ \\b \\f \\n \\r \\t.");
    public static SyntaxException ExpectedTokenNotFound(Location location, string expected, string found) => new(location, $"Expected token '{expected}' but found '{found}' instead.");
    public static SyntaxException TokenNotAllowedHere(Location location, string found) => new(location, $"Token '{found}' not allowed in this position.");
}
