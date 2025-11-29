using Microsoft.CodeAnalysis.CSharp;

namespace SharpOMatic.Engine.Helpers;

public static class IdentifierValidator
{
    public static void ValidateIdentifier(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new SharpOMaticException("Identifier cannot be null, empty, or whitespace.");

        // Check if it's a valid C# identifier
        if (!SyntaxFacts.IsValidIdentifier(key))
            throw new SharpOMaticException($"'{key}' is not a valid C# identifier format.");

        // Check if it's a reserved keyword (like class, int, etc.)
        if (SyntaxFacts.GetKeywordKind(key) != SyntaxKind.None)
            throw new SharpOMaticException($"'{key}' is a reserved C# keyword and cannot be used as an identifier without '@'.");
    }
}
