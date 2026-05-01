namespace SharpOMatic.Tests.Workflows;

public sealed class ContextHelpersTemplateExpansionUnitTests
{
    [Fact]
    public void Substitute_values_recursively_expands_context_strings()
    {
        var context = new ContextObject();
        context.Set("template", "Hello {{$name}}");
        context.Set("name", "Ada");

        var result = ContextHelpers.SubstituteValues("Prompt {{$template}}", context);

        Assert.Equal("Prompt Hello Ada", result);
    }

    [Fact]
    public async Task Substitute_values_async_recursively_expands_text_assets()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await CreateTextAsset(provider, "template.txt", "Hello {{$name}}");

        var context = new ContextObject();
        context.Set("name", "Ada");

        var result = await ContextHelpers.SubstituteValuesAsync(
            "Prompt <<template.txt>>",
            context,
            repositoryService,
            provider.GetRequiredService<IAssetStore>(),
            runId: null
        );

        Assert.Equal("Prompt Hello Ada", result);
    }

    [Fact]
    public async Task Substitute_values_async_recursively_expands_mixed_context_and_asset_markers()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await CreateTextAsset(provider, "template.txt", "Hello {{$name}}");

        var context = new ContextObject();
        context.Set("templateMarker", "<<template.txt>>");
        context.Set("name", "Ada");

        var result = await ContextHelpers.SubstituteValuesAsync(
            "Prompt {{$templateMarker}}",
            context,
            repositoryService,
            provider.GetRequiredService<IAssetStore>(),
            runId: null
        );

        Assert.Equal("Prompt Hello Ada", result);
    }

    [Fact]
    public async Task Substitute_values_async_preserves_missing_value_behavior()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var context = new ContextObject();

        var result = await ContextHelpers.SubstituteValuesAsync(
            "Missing {{$missing}} and <<missing.txt>>.",
            context,
            repositoryService,
            provider.GetRequiredService<IAssetStore>(),
            runId: null
        );

        Assert.Equal("Missing  and .", result);
    }

    [Fact]
    public void Substitute_values_does_not_recursively_expand_non_string_values()
    {
        var context = new ContextObject();
        context.Set("name", "Ada");

        var value = new ContextObject();
        value.Set("text", "Hello {{$name}}");
        context.Set("value", value);

        var result = ContextHelpers.SubstituteValues("Prompt {{$value}}", context);

        Assert.Contains("Hello {{$name}}", result);
        Assert.DoesNotContain("Hello Ada", result);
    }

    [Fact]
    public void Substitute_values_fails_context_self_cycle()
    {
        var context = new ContextObject();
        context.Set("template", "Again {{$template}}");

        var exception = Assert.Throws<SharpOMaticException>(() => ContextHelpers.SubstituteValues("Prompt {{$template}}", context));
        Assert.Contains("Recursive context template expansion detected", exception.Message);
    }

    [Fact]
    public async Task Substitute_values_async_fails_asset_self_cycle()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await CreateTextAsset(provider, "template.txt", "Again <<template.txt>>");

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(
            () =>
                ContextHelpers.SubstituteValuesAsync(
                    "Prompt <<template.txt>>",
                    new ContextObject(),
                    repositoryService,
                    provider.GetRequiredService<IAssetStore>(),
                    runId: null
                )
        );
        Assert.Contains("Recursive asset template expansion detected", exception.Message);
    }

    [Fact]
    public async Task Substitute_values_async_fails_mixed_context_asset_cycle()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await CreateTextAsset(provider, "template.txt", "Again {{$template}}");

        var context = new ContextObject();
        context.Set("template", "<<template.txt>>");

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(
            () =>
                ContextHelpers.SubstituteValuesAsync(
                    "Prompt {{$template}}",
                    context,
                    repositoryService,
                    provider.GetRequiredService<IAssetStore>(),
                    runId: null
                )
        );
        Assert.Contains("Recursive context template expansion detected", exception.Message);
    }

    [Fact]
    public void Substitute_values_fails_when_recursion_exceeds_maximum_depth()
    {
        var context = new ContextObject();
        for (var i = 0; i < 11; i += 1)
            context.Set($"level{i}", "{{$level" + (i + 1) + "}}");

        context.Set("level11", "done");

        var exception = Assert.Throws<SharpOMaticException>(() => ContextHelpers.SubstituteValues("{{$level0}}", context));
        Assert.Contains("maximum depth of 10", exception.Message);
    }

    private static async Task CreateTextAsset(ServiceProvider provider, string name, string content)
    {
        var assetService = new AssetService(provider.GetRequiredService<IRepositoryService>(), provider.GetRequiredService<IAssetStore>());
        await assetService.CreateFromBytesAsync(Encoding.UTF8.GetBytes(content), name, "text/plain", AssetScope.Library);
    }
}
