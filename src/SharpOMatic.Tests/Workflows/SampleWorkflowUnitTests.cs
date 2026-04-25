namespace SharpOMatic.Tests.Workflows;

public sealed class SampleWorkflowUnitTests
{
    [Fact]
    public void Sample_model_call_prompts_use_latest_user_message_content()
    {
        var sampleDirectory = FindSampleWorkflowDirectory();

        foreach (var file in Directory.EnumerateFiles(sampleDirectory, "*.json"))
        {
            var json = File.ReadAllText(file);
            Assert.DoesNotContain("agent.lastUserMessage", json);

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("nodes", out var nodes))
                continue;

            foreach (var node in nodes.EnumerateArray())
            {
                if (!node.TryGetProperty("nodeType", out var nodeType) || nodeType.GetInt32() != (int)NodeType.ModelCall)
                    continue;

                if (!node.TryGetProperty("prompt", out var promptProperty) || promptProperty.ValueKind != JsonValueKind.String)
                    continue;

                var prompt = promptProperty.GetString() ?? string.Empty;
                Assert.DoesNotContain("{{$agent.latestUserMessage}}", prompt);
            }
        }
    }

    private static string FindSampleWorkflowDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "SharpOMatic.Engine", "Samples", "Workflows");
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find sample workflow directory.");
    }
}
