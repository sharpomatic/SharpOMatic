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
    public void Substitute_values_uses_first_available_context_path_in_fallback_list()
    {
        var context = new ContextObject();
        context.Set("fallbackValue", "Fallback value");

        const string template = "{{$tenantValue, $fallbackValue}}";
        var fallbackResult = ContextHelpers.SubstituteValues(template, context);

        context.Set("tenantValue", "Tenant value");
        var overrideResult = ContextHelpers.SubstituteValues(template, context);

        Assert.Equal("Fallback value", fallbackResult);
        Assert.Equal("Tenant value", overrideResult);
    }

    [Fact]
    public async Task Substitute_values_async_uses_first_available_context_path_in_fallback_list()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var context = new ContextObject();
        context.Set("fallbackValue", "Fallback value");

        var result = await ContextHelpers.SubstituteValuesAsync(
            "{{$missing, $fallbackValue}}",
            context,
            provider.GetRequiredService<IRepositoryService>(),
            provider.GetRequiredService<IAssetStore>(),
            runId: null
        );

        Assert.Equal("Fallback value", result);
    }

    [Fact]
    public async Task Substitute_values_async_recursively_expands_text_assets()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await CreateTextAsset(provider, "template.txt", "Hello {{$name}}");

        var context = new ContextObject();
        context.Set("name", "Ada");

        var result = await ContextHelpers.SubstituteValuesAsync("Prompt <<template.txt>>", context, repositoryService, provider.GetRequiredService<IAssetStore>(), runId: null);

        Assert.Equal("Prompt Hello Ada", result);
    }

    [Fact]
    public async Task Substitute_values_async_allows_dollar_prefix_for_folder_qualified_assets()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        var folder = new AssetFolder
        {
            FolderId = Guid.NewGuid(),
            Name = "prompts_buildxact_assistant",
            Created = DateTime.UtcNow,
        };
        await repositoryService.UpsertAssetFolder(folder);

        var assetService = new AssetService(repositoryService, provider.GetRequiredService<IAssetStore>());
        await assetService.CreateFromBytesAsync(
            Encoding.UTF8.GetBytes("You are the Buildxact assistant."),
            "buildxact_assistant_instructions.txt",
            "text/plain",
            AssetScope.Library,
            folderId: folder.FolderId
        );

        var result = await ContextHelpers.SubstituteValuesAsync(
            "<<$prompts_buildxact_assistant/buildxact_assistant_instructions.txt>>",
            new ContextObject(),
            repositoryService,
            provider.GetRequiredService<IAssetStore>(),
            runId: null
        );

        Assert.Equal("You are the Buildxact assistant.", result);
    }

    [Fact]
    public async Task Substitute_values_async_uses_first_available_asset_in_fallback_list()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();
        await CreateTextAsset(provider, "default.txt", "Default instructions");

        const string template = "<<$override.txt, $default.txt>>";
        var fallbackResult = await ContextHelpers.SubstituteValuesAsync(template, new ContextObject(), repositoryService, provider.GetRequiredService<IAssetStore>(), runId: null);

        await CreateTextAsset(provider, "override.txt", "Override instructions");
        var overrideResult = await ContextHelpers.SubstituteValuesAsync(template, new ContextObject(), repositoryService, provider.GetRequiredService<IAssetStore>(), runId: null);

        Assert.Equal("Default instructions", fallbackResult);
        Assert.Equal("Override instructions", overrideResult);
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

        var result = await ContextHelpers.SubstituteValuesAsync("Prompt {{$templateMarker}}", context, repositoryService, provider.GetRequiredService<IAssetStore>(), runId: null);

        Assert.Equal("Prompt Hello Ada", result);
    }

    [Fact]
    public async Task Substitute_values_async_fails_when_no_context_path_can_be_resolved()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            ContextHelpers.SubstituteValuesAsync("{{$primaryValue, $fallbackValue}}", new ContextObject(), repositoryService, provider.GetRequiredService<IAssetStore>(), runId: null)
        );

        Assert.Contains("None of the context paths", exception.Message);
        Assert.Contains("primaryValue, $fallbackValue", exception.Message);
    }

    [Fact]
    public async Task Substitute_values_async_fails_when_no_asset_fallback_can_be_resolved()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repositoryService = provider.GetRequiredService<IRepositoryService>();

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            ContextHelpers.SubstituteValuesAsync("<<$override.txt, $default.txt>>", new ContextObject(), repositoryService, provider.GetRequiredService<IAssetStore>(), runId: null)
        );

        Assert.Contains("None of the assets", exception.Message);
        Assert.Contains("override.txt, $default.txt", exception.Message);
    }

    [Fact]
    public async Task Asset_and_folder_names_cannot_contain_commas()
    {
        Assert.False(AssetNameParser.IsValidAssetName("invalid,name.txt"));
        Assert.False(AssetNameParser.IsValidFolderName("invalid,folder"));
        Assert.False(AssetNameParser.TryParseFolderQualifiedName("invalid,folder/name.txt", out _, out _));

        using var provider = WorkflowRunner.BuildProvider();
        var assetService = new AssetService(provider.GetRequiredService<IRepositoryService>(), provider.GetRequiredService<IAssetStore>());
        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            assetService.CreateFromBytesAsync(Encoding.UTF8.GetBytes("content"), "invalid,name.txt", "text/plain", AssetScope.Library)
        );

        Assert.Contains("cannot contain ','", exception.Message);
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

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            ContextHelpers.SubstituteValuesAsync("Prompt <<template.txt>>", new ContextObject(), repositoryService, provider.GetRequiredService<IAssetStore>(), runId: null)
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

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            ContextHelpers.SubstituteValuesAsync("Prompt {{$template}}", context, repositoryService, provider.GetRequiredService<IAssetStore>(), runId: null)
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
