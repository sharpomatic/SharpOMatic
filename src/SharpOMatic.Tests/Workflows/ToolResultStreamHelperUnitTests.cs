namespace SharpOMatic.Tests.Workflows;

public sealed class ToolResultStreamHelperUnitTests
{
    private static readonly byte[] ImageBytes = new byte[300 * 1024];

    private static AIContent[] ImageResult() => [new TextContent("Image 7043 attached (2048x1583)."), new DataContent(ImageBytes, "image/webp")];

    public enum ResultShape
    {
        ContentArray,
        ClonedObjectArray,
        MarshalledJsonElement,
        WebDefaultsJsonElement,
    }

    [Theory]
    [InlineData(ResultShape.ContentArray)]
    [InlineData(ResultShape.ClonedObjectArray)]
    [InlineData(ResultShape.MarshalledJsonElement)]
    [InlineData(ResultShape.WebDefaultsJsonElement)]
    public void Binary_parts_are_replaced_by_a_note_whatever_shape_the_result_arrives_in(ResultShape shape)
    {
        // A tool's AIContent[] reaches the stream as itself, as the object[] ChatHistoryReplayHelper clones it into,
        // or as a JsonElement the function invoker has already marshalled - with either property casing.
        object result = shape switch
        {
            ResultShape.ClonedObjectArray => ImageResult().Cast<object?>().ToArray(),
            ResultShape.MarshalledJsonElement => JsonSerializer.SerializeToElement(ImageResult()),
            ResultShape.WebDefaultsJsonElement => JsonSerializer.SerializeToElement(ImageResult(), AIJsonUtilities.DefaultOptions),
            _ => ImageResult(),
        };

        var streamed = ToolResultStreamHelper.Serialize(new FunctionResultContent("call-1", result));

        Assert.DoesNotContain("base64", streamed);
        Assert.Contains("Image 7043 attached (2048x1583).", streamed);
        Assert.Contains("[image/webp, 300 KB, omitted from the stream event.]", streamed);
        Assert.True(streamed.Length < 1000, $"The streamed result was {streamed.Length} characters.");
    }

    [Fact]
    public void Binary_part_nested_inside_an_object_is_replaced()
    {
        var streamed = ToolResultStreamHelper.Serialize(new FunctionResultContent("call-1", new { status = "ok", attachment = new DataContent(ImageBytes, "image/png") }));

        var root = JsonNode.Parse(streamed)!;
        Assert.Equal("ok", root["status"]!.GetValue<string>());
        Assert.Equal("[image/png, 300 KB, omitted from the stream event.]", root["attachment"]!["Text"]!.GetValue<string>());
    }

    [Fact]
    public void A_result_that_is_only_binary_becomes_a_single_note()
    {
        var streamed = ToolResultStreamHelper.Serialize(new FunctionResultContent("call-1", new DataContent(ImageBytes, "image/jpeg")));

        Assert.Equal("[image/jpeg, 300 KB, omitted from the stream event.]", JsonNode.Parse(streamed)!["Text"]!.GetValue<string>());
    }

    [Fact]
    public void Results_without_binary_content_are_serialized_exactly_as_before()
    {
        object[] results = [new { city = "Sydney", forecast = "Sunny" }, JsonSerializer.SerializeToElement(new[] { 1, 2, 3 }), new AIContent[] { new TextContent("plain") }];

        foreach (var result in results)
            Assert.Equal(JsonSerializer.Serialize(result), ToolResultStreamHelper.Serialize(new FunctionResultContent("call-1", result)));
    }

    [Theory]
    [InlineData("Sunny")]
    [InlineData("A note about \"$type\":\"data\" that is not JSON {")]
    public void String_results_pass_through_untouched(string text)
    {
        Assert.Equal(text, ToolResultStreamHelper.Serialize(new FunctionResultContent("call-1", text)));
    }

    [Fact]
    public void Null_result_streams_as_empty()
    {
        Assert.Equal(string.Empty, ToolResultStreamHelper.Serialize(new FunctionResultContent("call-1", null)));
    }
}
