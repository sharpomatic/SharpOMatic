using SharpOMatic.Engine.Exceptions;

namespace SharpOMatic.Engine.Test;

public class ContextObjectUnitTest
{
    [Fact]
    public void Add_ValidIdentifier_ShouldSucceed()
    {
        // Arrange
        var context = new ContextObject
        {
            // Act
            { "validName", 123 },
            { "_anotherValid", "test" },
            { "camelCase", true },
            { "PascalCase", null }
        };

        // Assert
        Assert.Equal(4, context.Count);
        Assert.True(context.ContainsKey("validName"));
    }

    [Theory]
    [InlineData("123number")] // starts with a digit
    [InlineData("user-name")] // contains invalid character
    [InlineData("")]          // empty
    [InlineData(" ")]         // whitespace
    public void Add_InvalidIdentifier_ShouldThrow(string key)
    {
        // Arrange
        var context = new ContextObject();

        // Act & Assert
        var ex = Assert.Throws<SharpOMaticException>(() => context.Add(key, "value"));
    }

    [Fact]
    public void Add_KeywordWithoutAtPrefix_ShouldThrow()
    {
        var context = new ContextObject();
        var ex = Assert.Throws<SharpOMaticException>(() => context.Add("int", 10));
        Assert.Contains("reserved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

}
