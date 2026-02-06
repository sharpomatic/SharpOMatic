using System.IO.Compression;

namespace SharpOMatic.Engine.Services;

public class TransferService(IRepositoryService repositoryService, IAssetStore assetStore) : ITransferService
{
    private const string ManifestFileName = "manifest.json";
    private const string WorkflowDirectory = "workflows";
    private const string ConnectorDirectory = "connectors";
    private const string ModelDirectory = "models";
    private const string AssetDirectory = "assets";
    private const string JsonExtension = ".json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new NodeEntityConverter() } };

    public async Task ExportAsync(TransferExportRequest request, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(output);

        var workflowIds = await ResolveSelectionAsync(
            request.Workflows,
            async () =>
            {
                var summaries = await repositoryService.GetWorkflowSummaries();
                return summaries.Select(summary => summary.Id);
            }
        );

        var connectorIds = await ResolveSelectionAsync(
            request.Connectors,
            async () =>
            {
                var summaries = await repositoryService.GetConnectorSummaries();
                return summaries.Select(summary => summary.ConnectorId);
            }
        );

        var modelIds = await ResolveSelectionAsync(
            request.Models,
            async () =>
            {
                var summaries = await repositoryService.GetModelSummaries();
                return summaries.Select(summary => summary.ModelId);
            }
        );

        var assets = await ResolveAssetsAsync(request.Assets);

        var manifest = new TransferManifest
        {
            CreatedUtc = DateTime.UtcNow,
            IncludeSecrets = request.IncludeSecrets,
            Counts = new TransferCounts
            {
                Workflows = workflowIds.Count,
                Connectors = connectorIds.Count,
                Models = modelIds.Count,
                Assets = assets.Count,
            },
            Assets = assets
                .Select(asset => new TransferAssetEntry
                {
                    AssetId = asset.AssetId,
                    Name = asset.Name,
                    MediaType = asset.MediaType,
                    SizeBytes = asset.SizeBytes,
                    Created = asset.Created,
                })
                .ToList(),
        };

        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        await WriteJsonEntryAsync(archive, ManifestFileName, manifest, cancellationToken);

        foreach (var workflowId in workflowIds)
        {
            var workflow = await repositoryService.GetWorkflow(workflowId);
            await WriteJsonEntryAsync(archive, BuildEntityEntryName(WorkflowDirectory, workflowId), workflow, cancellationToken);
        }

        foreach (var connectorId in connectorIds)
        {
            var connector = await repositoryService.GetConnector(connectorId, hideSecrets: !request.IncludeSecrets);
            if (!request.IncludeSecrets)
                await StripConnectorSecrets(connector);

            await WriteJsonEntryAsync(archive, BuildEntityEntryName(ConnectorDirectory, connectorId), connector, cancellationToken);
        }

        foreach (var modelId in modelIds)
        {
            var model = await repositoryService.GetModel(modelId, hideSecrets: !request.IncludeSecrets);
            if (!request.IncludeSecrets)
                await StripModelSecrets(model);

            await WriteJsonEntryAsync(archive, BuildEntityEntryName(ModelDirectory, modelId), model, cancellationToken);
        }

        foreach (var asset in assets)
        {
            var entry = archive.CreateEntry(BuildAssetEntryName(asset.AssetId), CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await using var assetStream = await assetStore.OpenReadAsync(asset.StorageKey, cancellationToken);
            await assetStream.CopyToAsync(entryStream, cancellationToken);
        }
    }

    public async Task<TransferImportResult> ImportAsync(Stream input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = archive.GetEntry(ManifestFileName) ?? throw new SharpOMaticException("Transfer manifest is missing.");
        var manifest = await ReadJsonEntryAsync<TransferManifest>(manifestEntry, cancellationToken);

        if (manifest.SchemaVersion != TransferManifest.CurrentSchemaVersion)
            throw new SharpOMaticException($"Transfer manifest schema version {manifest.SchemaVersion} is not supported.");

        var connectorEntries = GetEntityEntries(archive, ConnectorDirectory);
        var modelEntries = GetEntityEntries(archive, ModelDirectory);
        var workflowEntries = GetEntityEntries(archive, WorkflowDirectory);

        var result = new TransferImportResult();

        foreach (var entry in connectorEntries.Values)
        {
            var connector = await ReadJsonEntryAsync<Connector>(entry, cancellationToken);
            if (!manifest.IncludeSecrets)
                await MergeConnectorSecrets(connector);

            await repositoryService.UpsertConnector(connector, hideSecrets: false);
            result.ConnectorsImported++;
        }

        foreach (var entry in modelEntries.Values)
        {
            var model = await ReadJsonEntryAsync<Model>(entry, cancellationToken);
            if (!manifest.IncludeSecrets)
                await MergeModelSecrets(model);

            await repositoryService.UpsertModel(model);
            result.ModelsImported++;
        }

        foreach (var entry in workflowEntries.Values)
        {
            var workflow = await ReadJsonEntryAsync<WorkflowEntity>(entry, cancellationToken);
            await repositoryService.UpsertWorkflow(workflow);
            result.WorkflowsImported++;
        }

        foreach (var assetEntry in manifest.Assets ?? [])
        {
            var zipEntry = archive.GetEntry(BuildAssetEntryName(assetEntry.AssetId)) ?? throw new SharpOMaticException($"Asset '{assetEntry.AssetId}' is missing from the transfer package.");

            var storageKey = AssetStorageKey.ForLibrary(assetEntry.AssetId);
            await using (var assetStream = zipEntry.Open())
            {
                await assetStore.SaveAsync(storageKey, assetStream, cancellationToken);
            }

            var sizeBytes = zipEntry.Length > 0 ? zipEntry.Length : assetEntry.SizeBytes;
            var asset = new Asset
            {
                AssetId = assetEntry.AssetId,
                RunId = null,
                Name = assetEntry.Name,
                Scope = AssetScope.Library,
                Created = assetEntry.Created,
                MediaType = assetEntry.MediaType,
                SizeBytes = sizeBytes,
                StorageKey = storageKey,
            };

            await repositoryService.UpsertAsset(asset);
            result.AssetsImported++;
        }

        return result;
    }

    private async Task<List<Asset>> ResolveAssetsAsync(TransferSelection? selection)
    {
        if (selection is null)
            return [];

        if (selection.All)
        {
            return await repositoryService.GetAssetsByScope(AssetScope.Library, null, AssetSortField.Name, SortDirection.Ascending, 0, 0);
        }

        var assets = new List<Asset>();
        foreach (var assetId in (selection.Ids ?? []).Distinct())
        {
            var asset = await repositoryService.GetAsset(assetId);
            if (asset.Scope != AssetScope.Library)
                throw new SharpOMaticException($"Asset '{assetId}' is not a library asset.");

            assets.Add(asset);
        }

        return assets;
    }

    private static async Task<List<Guid>> ResolveSelectionAsync(TransferSelection? selection, Func<Task<IEnumerable<Guid>>> resolveAllIds)
    {
        if (selection is null)
            return [];

        if (selection.All)
            return (await resolveAllIds()).Distinct().ToList();

        return (selection.Ids ?? []).Distinct().ToList();
    }

    private static string BuildEntityEntryName(string directory, Guid id) => $"{directory}/{id:D}{JsonExtension}";

    private static string BuildAssetEntryName(Guid id) => $"{AssetDirectory}/{id:D}";

    private static async Task WriteJsonEntryAsync<T>(ZipArchive archive, string entryName, T payload, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, payload, JsonOptions, cancellationToken);
    }

    private static async Task<T> ReadJsonEntryAsync<T>(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var entryStream = entry.Open();
        var payload = await JsonSerializer.DeserializeAsync<T>(entryStream, JsonOptions, cancellationToken);
        if (payload is null)
            throw new SharpOMaticException($"Entry '{entry.FullName}' is invalid.");

        return payload;
    }

    private static Dictionary<Guid, ZipArchiveEntry> GetEntityEntries(ZipArchive archive, string directory)
    {
        var entries = new Dictionary<Guid, ZipArchiveEntry>();
        var prefix = $"{directory}/";

        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (!IsSafeEntryName(name) || !name.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            if (name.EndsWith("/", StringComparison.Ordinal))
                continue;

            var fileName = name[prefix.Length..];
            if (fileName.Contains('/', StringComparison.Ordinal))
                continue;

            if (!fileName.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
                continue;

            var idText = fileName[..^JsonExtension.Length];
            if (!Guid.TryParse(idText, out var id))
                continue;

            if (!entries.TryAdd(id, entry))
                throw new SharpOMaticException($"Duplicate entry '{name}' was found.");
        }

        return entries;
    }

    private static bool IsSafeEntryName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return false;

        if (fullName.StartsWith("/", StringComparison.Ordinal) || fullName.StartsWith("\\", StringComparison.Ordinal))
            return false;

        if (fullName.Contains("..", StringComparison.Ordinal))
            return false;

        return true;
    }

    private async Task StripConnectorSecrets(Connector connector)
    {
        if (connector.FieldValues.Count == 0 || string.IsNullOrWhiteSpace(connector.ConfigId))
            return;

        var config = await repositoryService.GetConnectorConfig(connector.ConfigId);
        if (config is null)
        {
            connector.FieldValues.Clear();
            return;
        }

        var secretFields = config
            .AuthModes.SelectMany(authMode => authMode.Fields)
            .Where(field => field.Type == FieldDescriptorType.Secret)
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var fieldName in secretFields)
            connector.FieldValues.Remove(fieldName);
    }

    private async Task StripModelSecrets(Model model)
    {
        if (model.ParameterValues.Count == 0 || string.IsNullOrWhiteSpace(model.ConfigId))
            return;

        var config = await repositoryService.GetModelConfig(model.ConfigId);
        if (config is null)
        {
            model.ParameterValues.Clear();
            return;
        }

        foreach (var field in config.ParameterFields.Where(field => field.Type == FieldDescriptorType.Secret))
            model.ParameterValues.Remove(field.Name);
    }

    private async Task MergeConnectorSecrets(Connector connector)
    {
        if (string.IsNullOrWhiteSpace(connector.ConfigId))
            return;

        var config = await repositoryService.GetConnectorConfig(connector.ConfigId);
        if (config is null)
            return;

        var secretFields = config
            .AuthModes.SelectMany(authMode => authMode.Fields)
            .Where(field => field.Type == FieldDescriptorType.Secret)
            .Select(field => field.Name)
            .ToHashSet(StringComparer.Ordinal);

        if (secretFields.Count == 0)
            return;

        Connector? existing = null;
        try
        {
            existing = await repositoryService.GetConnector(connector.ConnectorId, hideSecrets: false);
        }
        catch (SharpOMaticException) { }

        if (existing is null)
            return;

        foreach (var fieldName in secretFields)
        {
            if (!connector.FieldValues.ContainsKey(fieldName) && existing.FieldValues.TryGetValue(fieldName, out var value))
            {
                connector.FieldValues[fieldName] = value;
            }
        }
    }

    private async Task MergeModelSecrets(Model model)
    {
        if (string.IsNullOrWhiteSpace(model.ConfigId))
            return;

        var config = await repositoryService.GetModelConfig(model.ConfigId);
        if (config is null)
            return;

        var secretFields = config.ParameterFields.Where(field => field.Type == FieldDescriptorType.Secret).Select(field => field.Name).ToHashSet(StringComparer.Ordinal);

        if (secretFields.Count == 0)
            return;

        Model? existing = null;
        try
        {
            existing = await repositoryService.GetModel(model.ModelId, hideSecrets: false);
        }
        catch (SharpOMaticException) { }

        if (existing is null)
            return;

        foreach (var fieldName in secretFields)
        {
            if (!model.ParameterValues.ContainsKey(fieldName) && existing.ParameterValues.TryGetValue(fieldName, out var value))
            {
                model.ParameterValues[fieldName] = value;
            }
        }
    }
}
