namespace SharpOMatic.Tests.Services;

public sealed class AssetsControllerUnitTests
{
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
