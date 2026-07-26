namespace SharpOMatic.Tests.Services;

public sealed class AssetsControllerUnitTests
{
    [Fact]
    public async Task Upload_asset_overwrites_existing_name_in_same_folder()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repository = provider.GetRequiredService<IRepositoryService>();
        var assetStore = provider.GetRequiredService<IAssetStore>();
        var existing = CreateAsset("existing.txt");
        await repository.UpsertAsset(existing);

        var content = Encoding.UTF8.GetBytes("replacement content");
        await using var uploadStream = new MemoryStream(content);
        var file = new FormFile(uploadStream, 0, content.Length, "File", "existing.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/markdown",
        };
        var controller = new AssetsController(repository, assetStore)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.UploadAsset(
            new AssetUploadRequest
            {
                File = file,
                Name = "EXISTING.txt",
                Scope = AssetScope.Library,
            }
        );

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var summary = Assert.IsType<AssetSummary>(response.Value);
        Assert.Equal(existing.AssetId, summary.AssetId);
        Assert.Equal("EXISTING.txt", summary.Name);
        Assert.Equal("text/markdown", summary.MediaType);
        Assert.Equal(1, await repository.GetAssetCount(AssetScope.Library, null));

        await using var storedContent = await assetStore.OpenReadAsync(existing.StorageKey);
        using var reader = new StreamReader(storedContent);
        Assert.Equal("replacement content", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Rename_asset_updates_name_and_preserves_storage_metadata()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repository = provider.GetRequiredService<IRepositoryService>();
        var asset = CreateAsset("original.txt");
        await repository.UpsertAsset(asset);

        var controller = new AssetsController(repository, provider.GetRequiredService<IAssetStore>());
        var result = await controller.RenameAsset(asset.AssetId, new AssetNameRequest { Name = "renamed.md" });

        Assert.Equal("renamed.md", result.Value?.Name);
        var updated = await repository.GetAsset(asset.AssetId);
        Assert.Equal("renamed.md", updated.Name);
        Assert.Equal(asset.StorageKey, updated.StorageKey);
        Assert.Equal(asset.MediaType, updated.MediaType);
    }

    [Fact]
    public async Task Rename_asset_rejects_duplicate_name_in_same_folder()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repository = provider.GetRequiredService<IRepositoryService>();
        var folderId = Guid.NewGuid();
        await repository.UpsertAssetFolder(
            new AssetFolder
            {
                FolderId = folderId,
                Name = "Prompts",
                Created = DateTime.UtcNow,
            }
        );
        var source = CreateAsset("source.txt", folderId);
        await repository.UpsertAsset(source);
        await repository.UpsertAsset(CreateAsset("existing.txt", folderId));

        var controller = new AssetsController(repository, provider.GetRequiredService<IAssetStore>());
        var result = await controller.RenameAsset(source.AssetId, new AssetNameRequest { Name = "EXISTING.txt" });

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Contains("already exists", conflict.Value?.ToString());
        Assert.Equal("source.txt", (await repository.GetAsset(source.AssetId)).Name);
    }

    [Fact]
    public async Task Rename_asset_with_unchanged_name_is_noop()
    {
        using var provider = WorkflowRunner.BuildProvider();
        var repository = provider.GetRequiredService<IRepositoryService>();
        var asset = CreateAsset("unchanged.txt");
        await repository.UpsertAsset(asset);

        var controller = new AssetsController(repository, provider.GetRequiredService<IAssetStore>());
        var result = await controller.RenameAsset(asset.AssetId, new AssetNameRequest { Name = " unchanged.txt " });

        Assert.Equal("unchanged.txt", result.Value?.Name);
        Assert.Same(asset, await repository.GetAsset(asset.AssetId));
    }

    private static Asset CreateAsset(string name, Guid? folderId = null)
    {
        return new Asset
        {
            AssetId = Guid.NewGuid(),
            RunId = null,
            ConversationId = null,
            FolderId = folderId,
            Name = name,
            Scope = AssetScope.Library,
            Created = DateTime.UtcNow,
            MediaType = "text/plain",
            SizeBytes = 10,
            StorageKey = $"library/{Guid.NewGuid():N}",
        };
    }
}
