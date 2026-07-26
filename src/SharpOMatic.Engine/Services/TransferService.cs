namespace SharpOMatic.Engine.Services;

public class TransferService(IRepositoryService repositoryService, IAssetStore assetStore) : ITransferService
{
    private const string WorkflowDirectory = "workflows";
    private const string ConnectorDirectory = "connectors";
    private const string ModelDirectory = "models";
    private const string EvaluationDirectory = "evaluations";
    private const string AssetDirectory = "assets";
    private const string JsonExtension = ".json";

    private const string WorkflowType = "workflow";
    private const string ConnectorType = "connector";
    private const string ModelType = "model";
    private const string EvaluationType = "evaluation";
    private const string AssetType = "asset";
    private const string RepeatCsvColumnName = "Repeat";

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

        var evaluationIds = await ResolveSelectionAsync(
            request.Evaluations,
            async () =>
            {
                var summaries = await repositoryService.GetEvalConfigSummaries(search: null, sortBy: EvalConfigSortField.Name, sortDirection: SortDirection.Ascending, skip: 0, take: 0);
                return summaries.Select(summary => summary.EvalConfigId);
            }
        );

        var evaluationPackages = new List<TransferEvaluationPackage>();
        foreach (var evaluationId in evaluationIds)
            evaluationPackages.Add(await repositoryService.GetEvalTransferPackage(evaluationId));

        var assets = await ResolveAssetsAsync(request.Assets);
        var exportedUtc = DateTime.UtcNow;

        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var workflowId in workflowIds)
        {
            var workflow = await repositoryService.GetWorkflow(workflowId);
            workflow.FolderName = workflow.WorkflowFolderName;
            await WriteEnvelopeEntryAsync(archive, BuildJsonEntryName(WorkflowDirectory, workflow.Name, workflow.Id), WorkflowType, workflow, exportedUtc, cancellationToken);
        }

        foreach (var connectorId in connectorIds)
        {
            var connector = await repositoryService.GetConnector(connectorId, hideSecrets: !request.IncludeSecrets);
            if (!request.IncludeSecrets)
                await StripConnectorSecrets(connector);

            await WriteEnvelopeEntryAsync(archive, BuildJsonEntryName(ConnectorDirectory, connector.Name, connector.ConnectorId), ConnectorType, connector, exportedUtc, cancellationToken);
        }

        foreach (var modelId in modelIds)
        {
            var model = await repositoryService.GetModel(modelId, hideSecrets: !request.IncludeSecrets);
            if (!request.IncludeSecrets)
                await StripModelSecrets(model);

            await WriteEnvelopeEntryAsync(archive, BuildJsonEntryName(ModelDirectory, model.Name, model.ModelId), ModelType, model, exportedUtc, cancellationToken);
        }

        foreach (var evaluationPackage in evaluationPackages)
        {
            await WriteEnvelopeEntryAsync(
                archive,
                BuildJsonEntryName(EvaluationDirectory, evaluationPackage.EvalConfig.Name, evaluationPackage.EvalConfig.EvalConfigId),
                EvaluationType,
                evaluationPackage,
                exportedUtc,
                cancellationToken
            );
        }

        foreach (var asset in assets)
        {
            var folderName = asset.FolderId.HasValue ? (await repositoryService.GetAssetFolder(asset.FolderId.Value)).Name : null;

            await using var assetStream = await assetStore.OpenReadAsync(asset.StorageKey, cancellationToken);
            using var memory = new MemoryStream();
            await assetStream.CopyToAsync(memory, cancellationToken);

            var payload = new TransferAssetPayload
            {
                AssetId = asset.AssetId,
                FolderName = folderName,
                Name = asset.Name,
                MediaType = asset.MediaType,
                Created = asset.Created,
                SizeBytes = asset.SizeBytes,
                ContentBase64 = Convert.ToBase64String(memory.ToArray()),
            };

            await WriteEnvelopeEntryAsync(archive, BuildJsonEntryName(AssetDirectory, asset.Name, asset.AssetId), AssetType, payload, exportedUtc, cancellationToken);
        }
    }

    public async Task<TransferImportBatchResult> ImportZipAsync(Stream input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var batchResult = new TransferImportBatchResult();
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);

        foreach (var entry in archive.Entries)
        {
            var entryName = entry.FullName.Replace('\\', '/');
            if (!IsJsonEntry(entryName))
                continue;

            batchResult.FilesProcessed++;
            await using var entryStream = entry.Open();
            await TryImportFileAsync(entryName, entryStream, batchResult, cancellationToken);
        }

        return batchResult;
    }

    public async Task<TransferImportBatchResult> ImportFilesAsync(IEnumerable<TransferImportFile> files, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        var batchResult = new TransferImportBatchResult();
        foreach (var file in files)
        {
            batchResult.FilesProcessed++;
            await TryImportFileAsync(file.Name, file.Stream, batchResult, cancellationToken);
        }

        return batchResult;
    }

    public async Task<TransferImportResult> ImportJsonAsync(Stream input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return await ImportEnvelopeAsync(input, "request body", cancellationToken);
    }

    private async Task TryImportFileAsync(string fileName, Stream input, TransferImportBatchResult batchResult, CancellationToken cancellationToken)
    {
        try
        {
            var result = await ImportEnvelopeAsync(input, fileName, cancellationToken);
            AddCounts(batchResult.Result, result);
            batchResult.FilesImported++;
        }
        catch (Exception exception) when (IsImportFileFailure(exception))
        {
            batchResult.FilesFailed++;
        }
    }

    private async Task<TransferImportResult> ImportEnvelopeAsync(Stream input, string sourceName, CancellationToken cancellationToken)
    {
        var envelope = await JsonSerializer.DeserializeAsync<TransferEnvelope>(input, JsonOptions, cancellationToken);
        if (envelope is null)
            throw new SharpOMaticException($"Transfer file '{sourceName}' is invalid.");

        if (envelope.SchemaVersion != TransferEnvelope.CurrentSchemaVersion)
            throw new SharpOMaticException($"Transfer file '{sourceName}' uses unsupported schema version '{envelope.SchemaVersion}'.");

        if (string.IsNullOrWhiteSpace(envelope.Type))
            throw new SharpOMaticException($"Transfer file '{sourceName}' is missing a type.");

        if (envelope.Payload.ValueKind != JsonValueKind.Object)
            throw new SharpOMaticException($"Transfer file '{sourceName}' is missing a payload.");

        return envelope.Type.Trim().ToLowerInvariant() switch
        {
            WorkflowType => await ImportWorkflowAsync(DeserializePayload<WorkflowEntity>(envelope, sourceName)),
            ConnectorType => await ImportConnectorAsync(DeserializePayload<Connector>(envelope, sourceName)),
            ModelType => await ImportModelAsync(DeserializePayload<Model>(envelope, sourceName)),
            EvaluationType => await ImportEvaluationAsync(DeserializePayload<TransferEvaluationPackage>(envelope, sourceName), sourceName),
            AssetType => await ImportAssetAsync(DeserializePayload<TransferAssetPayload>(envelope, sourceName), sourceName, cancellationToken),
            _ => throw new SharpOMaticException($"Transfer file '{sourceName}' contains unsupported type '{envelope.Type}'."),
        };
    }

    private static T DeserializePayload<T>(TransferEnvelope envelope, string sourceName)
    {
        var payload = JsonSerializer.Deserialize<T>(envelope.Payload.GetRawText(), JsonOptions);
        return payload is null ? throw new SharpOMaticException($"Transfer file '{sourceName}' has an invalid payload.") : payload;
    }

    private async Task<TransferImportResult> ImportWorkflowAsync(WorkflowEntity workflow)
    {
        var folderName = (workflow.FolderName ?? workflow.WorkflowFolderName)?.Trim();
        if (string.IsNullOrWhiteSpace(folderName))
            folderName = null;

        if (folderName?.Contains('/', StringComparison.Ordinal) == true || folderName?.Contains('\\', StringComparison.Ordinal) == true)
            throw new SharpOMaticException($"Workflow '{workflow.Name}' has an invalid workflow folder name.");

        workflow.WorkflowFolderId = null;
        workflow.WorkflowFolderName = null;
        workflow.FolderName = null;

        if (folderName is not null)
        {
            var folder = await repositoryService.GetWorkflowFolderByName(folderName);
            if (folder is null)
            {
                folder = new WorkflowFolder
                {
                    WorkflowFolderId = Guid.NewGuid(),
                    Name = folderName,
                    Created = DateTime.Now,
                };
                await repositoryService.UpsertWorkflowFolder(folder);
            }

            workflow.WorkflowFolderId = folder.WorkflowFolderId;
        }

        await repositoryService.UpsertWorkflow(workflow);
        return new TransferImportResult { WorkflowsImported = 1 };
    }

    private async Task<TransferImportResult> ImportConnectorAsync(Connector connector)
    {
        await MergeConnectorSecrets(connector);
        await repositoryService.UpsertConnector(connector, hideSecrets: false);
        return new TransferImportResult { ConnectorsImported = 1 };
    }

    private async Task<TransferImportResult> ImportModelAsync(Model model)
    {
        await MergeModelSecrets(model);
        await repositoryService.UpsertModel(model);
        return new TransferImportResult { ModelsImported = 1 };
    }

    private async Task<TransferImportResult> ImportEvaluationAsync(TransferEvaluationPackage package, string sourceName)
    {
        ValidateEvalPackage(package, sourceName);

        var remapped = RemapEvalPackage(package);
        await repositoryService.UpsertEvalConfig(remapped.EvalConfig);
        await repositoryService.UpsertEvalGraders(remapped.Graders);
        await repositoryService.UpsertEvalColumns(remapped.Columns);
        await repositoryService.UpsertEvalRows(remapped.Rows);
        await repositoryService.UpsertEvalData(remapped.Data);
        foreach (var run in remapped.Runs)
            await repositoryService.UpsertEvalRun(run);

        await repositoryService.UpsertEvalRunRows(remapped.RunRows);
        await repositoryService.UpsertEvalRunRowGraders(remapped.RunRowGraders);
        await repositoryService.UpsertEvalRunGraderSummaries(remapped.RunGraderSummaries);

        return new TransferImportResult { EvaluationsImported = 1, EvaluationRunsImported = remapped.Runs.Count };
    }

    private async Task<TransferImportResult> ImportAssetAsync(TransferAssetPayload payload, string sourceName, CancellationToken cancellationToken)
    {
        if (payload.AssetId == Guid.Empty)
            throw new SharpOMaticException($"Transfer file '{sourceName}' has an invalid asset id.");

        if (!AssetNameParser.IsValidAssetName(payload.Name))
            throw new SharpOMaticException($"Transfer file '{sourceName}' has an invalid asset name.");

        var folderName = payload.FolderName?.Trim();
        if (string.IsNullOrWhiteSpace(folderName))
            folderName = null;

        if (folderName is not null && !AssetNameParser.IsValidFolderName(folderName))
            throw new SharpOMaticException($"Transfer file '{sourceName}' has an invalid asset folder name.");

        Guid? folderId = null;
        if (folderName is not null)
        {
            var folder = await repositoryService.GetAssetFolderByName(folderName);
            if (folder is null)
            {
                folder = new AssetFolder
                {
                    FolderId = Guid.NewGuid(),
                    Name = folderName,
                    Created = DateTime.UtcNow,
                };
                await repositoryService.UpsertAssetFolder(folder);
            }

            folderId = folder.FolderId;
        }

        byte[] content;
        try
        {
            content = Convert.FromBase64String(payload.ContentBase64);
        }
        catch (FormatException exception)
        {
            throw new SharpOMaticException($"Transfer file '{sourceName}' has invalid asset content: {exception.Message}");
        }

        var storageKey = AssetStorageKey.ForLibrary(payload.AssetId, folderId);
        await using (var stream = new MemoryStream(content, writable: false))
        {
            await assetStore.SaveAsync(storageKey, stream, cancellationToken);
        }

        var asset = new Asset
        {
            AssetId = payload.AssetId,
            RunId = null,
            ConversationId = null,
            FolderId = folderId,
            Name = payload.Name.Trim(),
            Scope = AssetScope.Library,
            Created = payload.Created == default ? DateTime.UtcNow : payload.Created,
            MediaType = payload.MediaType,
            SizeBytes = content.LongLength,
            StorageKey = storageKey,
        };

        await repositoryService.UpsertAsset(asset);
        return new TransferImportResult { AssetsImported = 1 };
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
            return [.. (await resolveAllIds()).Distinct()];

        return [.. (selection.Ids ?? []).Distinct()];
    }

    private static async Task WriteEnvelopeEntryAsync<T>(ZipArchive archive, string entryName, string type, T payload, DateTime exportedUtc, CancellationToken cancellationToken)
    {
        var envelope = new TransferEnvelope<T>
        {
            Type = type,
            ExportedUtc = exportedUtc,
            Payload = payload,
        };

        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, envelope, JsonOptions, cancellationToken);
    }

    private static string BuildJsonEntryName(string directory, string name, Guid id)
    {
        var safeName = SanitizeFileName(name);
        return $"{directory}/{safeName}_{id:D}{JsonExtension}";
    }

    private static string SanitizeFileName(string name)
    {
        var builder = new StringBuilder();
        var previousUnderscore = false;
        foreach (var character in name.Trim())
        {
            var replacement = IsUnsafeFileNameCharacter(character) ? '_' : character;
            if (replacement == '_')
            {
                if (previousUnderscore)
                    continue;

                previousUnderscore = true;
            }
            else
            {
                previousUnderscore = false;
            }

            builder.Append(replacement);
            if (builder.Length >= 80)
                break;
        }

        var safeName = builder.ToString().Trim(' ', '.', '_');
        if (string.IsNullOrWhiteSpace(safeName) || IsReservedWindowsFileName(safeName))
            return "item";

        return safeName;
    }

    private static bool IsUnsafeFileNameCharacter(char character)
    {
        return char.IsControl(character) || character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*';
    }

    private static bool IsReservedWindowsFileName(string fileName)
    {
        var name = fileName.Split('.')[0];
        if (
            name.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || name.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || name.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || name.Equals("NUL", StringComparison.OrdinalIgnoreCase)
        )
            return true;

        if (name.Length == 4 && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)))
            return name[3] is >= '1' and <= '9';

        return false;
    }

    private static bool IsJsonEntry(string fullName)
    {
        if (!IsSafeEntryName(fullName) || fullName.EndsWith('/'))
            return false;

        return fullName.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeEntryName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return false;

        if (fullName.StartsWith('/') || fullName.StartsWith('\\'))
            return false;

        if (fullName.Contains("..", StringComparison.Ordinal))
            return false;

        return true;
    }

    private static bool IsImportFileFailure(Exception exception)
    {
        return exception is SharpOMaticException or JsonException or FormatException or NotSupportedException or InvalidOperationException;
    }

    private static void AddCounts(TransferImportResult target, TransferImportResult source)
    {
        target.WorkflowsImported += source.WorkflowsImported;
        target.ConnectorsImported += source.ConnectorsImported;
        target.ModelsImported += source.ModelsImported;
        target.EvaluationsImported += source.EvaluationsImported;
        target.EvaluationRunsImported += source.EvaluationRunsImported;
        target.AssetsImported += source.AssetsImported;
    }

    private static void ValidateEvalConfigDetail(EvalConfigDetail detail, string entryName)
    {
        var evalConfigId = detail.EvalConfig.EvalConfigId;
        var graderIds = new HashSet<Guid>();
        var columnIds = new HashSet<Guid>();
        var rowIds = new HashSet<Guid>();
        var dataIds = new HashSet<Guid>();

        foreach (var grader in detail.Graders)
        {
            if (grader.EvalConfigId != evalConfigId)
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains grader '{grader.EvalGraderId}' with mismatched EvalConfigId.");

            if (!graderIds.Add(grader.EvalGraderId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains duplicate grader id '{grader.EvalGraderId}'.");
        }

        foreach (var column in detail.Columns)
        {
            if (column.EvalConfigId != evalConfigId)
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains column '{column.EvalColumnId}' with mismatched EvalConfigId.");

            if (!columnIds.Add(column.EvalColumnId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains duplicate column id '{column.EvalColumnId}'.");
        }

        foreach (var row in detail.Rows)
        {
            if (row.EvalConfigId != evalConfigId)
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains row '{row.EvalRowId}' with mismatched EvalConfigId.");

            if (!rowIds.Add(row.EvalRowId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains duplicate row id '{row.EvalRowId}'.");
        }

        foreach (var data in detail.Data)
        {
            if (!dataIds.Add(data.EvalDataId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains duplicate data id '{data.EvalDataId}'.");

            if (!rowIds.Contains(data.EvalRowId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains data '{data.EvalDataId}' referencing missing row '{data.EvalRowId}'.");

            if (!columnIds.Contains(data.EvalColumnId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains data '{data.EvalDataId}' referencing missing column '{data.EvalColumnId}'.");
        }
    }

    private static void ValidateEvalPackage(TransferEvaluationPackage package, string entryName)
    {
        var detail = new EvalConfigDetail
        {
            EvalConfig = package.EvalConfig,
            Graders = package.Graders,
            Columns = package.Columns,
            Rows = package.Rows,
            Data = package.Data,
        };
        ValidateEvalConfigDetail(detail, entryName);

        var evalConfigId = package.EvalConfig.EvalConfigId;
        var graderIds = package.Graders.Select(grader => grader.EvalGraderId).ToHashSet();
        var rowIds = package.Rows.Select(row => row.EvalRowId).ToHashSet();
        var runIds = new HashSet<Guid>();
        var runRowIds = new HashSet<Guid>();
        var runRowGraderIds = new HashSet<Guid>();
        var runGraderSummaryIds = new HashSet<Guid>();

        foreach (var run in package.Runs)
        {
            if (run.EvalConfigId != evalConfigId)
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains run '{run.EvalRunId}' with mismatched EvalConfigId.");

            if (run.Status == EvalRunStatus.Running)
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains running run '{run.EvalRunId}'.");

            if (!runIds.Add(run.EvalRunId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains duplicate run id '{run.EvalRunId}'.");
        }

        foreach (var runRow in package.RunRows)
        {
            if (!runRowIds.Add(runRow.EvalRunRowId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains duplicate run row id '{runRow.EvalRunRowId}'.");

            if (!runIds.Contains(runRow.EvalRunId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains run row '{runRow.EvalRunRowId}' referencing missing run '{runRow.EvalRunId}'.");

            if (!rowIds.Contains(runRow.EvalRowId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains run row '{runRow.EvalRunRowId}' referencing missing row '{runRow.EvalRowId}'.");
        }

        foreach (var runRowGrader in package.RunRowGraders)
        {
            if (!runRowGraderIds.Add(runRowGrader.EvalRunRowGraderId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains duplicate run row grader id '{runRowGrader.EvalRunRowGraderId}'.");

            if (!runIds.Contains(runRowGrader.EvalRunId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains run row grader '{runRowGrader.EvalRunRowGraderId}' referencing missing run '{runRowGrader.EvalRunId}'.");

            if (!runRowIds.Contains(runRowGrader.EvalRunRowId))
                throw new SharpOMaticException(
                    $"Evaluation entry '{entryName}' contains run row grader '{runRowGrader.EvalRunRowGraderId}' referencing missing run row '{runRowGrader.EvalRunRowId}'."
                );

            if (!graderIds.Contains(runRowGrader.EvalGraderId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains run row grader '{runRowGrader.EvalRunRowGraderId}' referencing missing grader '{runRowGrader.EvalGraderId}'.");
        }

        foreach (var summary in package.RunGraderSummaries)
        {
            if (!runGraderSummaryIds.Add(summary.EvalRunGraderSummaryId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains duplicate run grader summary id '{summary.EvalRunGraderSummaryId}'.");

            if (!runIds.Contains(summary.EvalRunId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains run grader summary '{summary.EvalRunGraderSummaryId}' referencing missing run '{summary.EvalRunId}'.");

            if (!graderIds.Contains(summary.EvalGraderId))
                throw new SharpOMaticException($"Evaluation entry '{entryName}' contains run grader summary '{summary.EvalRunGraderSummaryId}' referencing missing grader '{summary.EvalGraderId}'.");
        }
    }

    private static TransferEvaluationPackage RemapEvalPackage(TransferEvaluationPackage source)
    {
        var targetEvalConfigId = Guid.NewGuid();

        var graderIdMap = source.Graders.ToDictionary(grader => grader.EvalGraderId, _ => Guid.NewGuid());
        var columnIdMap = source.Columns.ToDictionary(column => column.EvalColumnId, _ => Guid.NewGuid());
        var rowIdMap = source.Rows.ToDictionary(row => row.EvalRowId, _ => Guid.NewGuid());
        var runIdMap = source.Runs.ToDictionary(run => run.EvalRunId, _ => Guid.NewGuid());
        var runRowIdMap = source.RunRows.ToDictionary(row => row.EvalRunRowId, _ => Guid.NewGuid());

        var evalConfig = new EvalConfig
        {
            EvalConfigId = targetEvalConfigId,
            WorkflowId = source.EvalConfig.WorkflowId,
            Name = source.EvalConfig.Name,
            Description = source.EvalConfig.Description,
            MaxParallel = source.EvalConfig.MaxParallel,
            RowScoreMode = source.EvalConfig.RowScoreMode,
            RunScoreMode = source.EvalConfig.RunScoreMode,
        };

        var graders = source
            .Graders.Select(grader => new EvalGrader
            {
                EvalGraderId = graderIdMap[grader.EvalGraderId],
                EvalConfigId = targetEvalConfigId,
                WorkflowId = grader.WorkflowId,
                Order = grader.Order,
                Label = grader.Label,
                PassThreshold = grader.PassThreshold,
                IncludeInScore = grader.IncludeInScore,
            })
            .ToList();

        var columns = source
            .Columns.Select(column => new EvalColumn
            {
                EvalColumnId = columnIdMap[column.EvalColumnId],
                EvalConfigId = targetEvalConfigId,
                Name = column.Name,
                Order = column.Order,
                EntryType = column.EntryType,
                Optional = column.Optional,
                InputPath = column.InputPath,
            })
            .ToList();

        var rows = source
            .Rows.Select(row => new EvalRow
            {
                EvalRowId = rowIdMap[row.EvalRowId],
                EvalConfigId = targetEvalConfigId,
                Order = row.Order,
                Repeat = row.Repeat ?? EvalRow.DefaultRepeat,
            })
            .ToList();

        var data = source
            .Data.Select(item => new EvalData
            {
                EvalDataId = Guid.NewGuid(),
                EvalRowId = rowIdMap[item.EvalRowId],
                EvalColumnId = columnIdMap[item.EvalColumnId],
                StringValue = item.StringValue,
                IntValue = item.IntValue,
                DoubleValue = item.DoubleValue,
                BoolValue = item.BoolValue,
            })
            .ToList();

        var runs = source
            .Runs.Select(run => new EvalRun
            {
                EvalRunId = runIdMap[run.EvalRunId],
                EvalConfigId = targetEvalConfigId,
                Name = run.Name,
                Order = run.Order,
                Started = run.Started,
                Finished = run.Finished,
                Status = run.Status,
                Message = run.Message,
                Error = run.Error,
                CancelRequested = run.CancelRequested,
                TotalRows = run.TotalRows,
                CompletedRows = run.CompletedRows,
                FailedRows = run.FailedRows,
                AveragePassRate = run.AveragePassRate,
                RunScoreMode = run.RunScoreMode,
                Score = run.Score,
            })
            .ToList();

        var runRows = source
            .RunRows.Select(row => new EvalRunRow
            {
                EvalRunRowId = runRowIdMap[row.EvalRunRowId],
                EvalRunId = runIdMap[row.EvalRunId],
                EvalRowId = rowIdMap[row.EvalRowId],
                Order = row.Order,
                Started = row.Started,
                Finished = row.Finished,
                Status = row.Status,
                Score = row.Score,
                InputContext = row.InputContext,
                OutputContext = row.OutputContext,
                Error = row.Error,
            })
            .ToList();

        var runRowGraders = source
            .RunRowGraders.Select(grader => new EvalRunRowGrader
            {
                EvalRunRowGraderId = Guid.NewGuid(),
                EvalRunRowId = runRowIdMap[grader.EvalRunRowId],
                EvalGraderId = graderIdMap[grader.EvalGraderId],
                EvalRunId = runIdMap[grader.EvalRunId],
                Started = grader.Started,
                Finished = grader.Finished,
                Status = grader.Status,
                Score = grader.Score,
                InputContext = grader.InputContext,
                OutputContext = grader.OutputContext,
                Error = grader.Error,
            })
            .ToList();

        var runGraderSummaries = source
            .RunGraderSummaries.Select(summary => new EvalRunGraderSummary
            {
                EvalRunGraderSummaryId = Guid.NewGuid(),
                EvalRunId = runIdMap[summary.EvalRunId],
                EvalGraderId = graderIdMap[summary.EvalGraderId],
                TotalCount = summary.TotalCount,
                CompletedCount = summary.CompletedCount,
                FailedCount = summary.FailedCount,
                MinScore = summary.MinScore,
                MaxScore = summary.MaxScore,
                AverageScore = summary.AverageScore,
                MedianScore = summary.MedianScore,
                StandardDeviation = summary.StandardDeviation,
                PassRate = summary.PassRate,
            })
            .ToList();

        return new TransferEvaluationPackage
        {
            EvalConfig = evalConfig,
            Graders = graders,
            Columns = columns,
            Rows = rows,
            Data = data,
            Runs = runs,
            RunRows = runRows,
            RunRowGraders = runRowGraders,
            RunGraderSummaries = runGraderSummaries,
        };
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

    public async Task<EvalRowsCsvImportResult> ImportEvalRowsCsvAsync(Guid evalConfigId, Stream input, string fileName, CancellationToken cancellationToken = default)
    {
        if (input is null)
            throw new SharpOMaticException("CSV file is required.");

        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            throw new SharpOMaticException("Import file must have a .csv extension.");

        using var reader = new StreamReader(input, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        var csvText = await reader.ReadToEndAsync(cancellationToken);
        var records = ParseCsvRows(csvText);
        if (records.Count == 0 || records[0].Fields.All(field => string.IsNullOrWhiteSpace(field)))
            throw new SharpOMaticException("CSV file must include a header row.");

        var detail = await repositoryService.GetEvalConfigDetail(evalConfigId);
        var columns = detail.Columns.OrderBy(column => column.Order).ToList();
        if (columns.Count == 0)
            throw new SharpOMaticException("Evaluation has no columns to import.");

        ValidateReservedEvalColumnNames(columns);
        var columnsByName = BuildCsvColumnLookup(columns);
        var repeatHeaderIndex = GetCsvRepeatHeaderIndex(records[0]);
        var matchedHeaderIndexes = MatchCsvHeaders(records[0], columnsByName);
        ValidateCsvRequiredHeaders(columns, matchedHeaderIndexes);

        var importedRows = new List<EvalRow>();
        var importedData = new List<EvalData>();
        var nextOrder = detail.Rows.Count == 0 ? 0 : detail.Rows.Max(row => row.Order) + 1;

        foreach (var record in records.Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (record.Fields.All(field => string.IsNullOrWhiteSpace(field)))
                continue;

            var evalRowId = Guid.NewGuid();
            var repeat = GetCsvRepeatValue(record, repeatHeaderIndex);
            importedRows.Add(new EvalRow
            {
                EvalRowId = evalRowId,
                EvalConfigId = evalConfigId,
                Order = nextOrder++,
                Repeat = repeat,
            });

            foreach (var column in columns)
            {
                var rawValue = GetCsvRecordValue(record, matchedHeaderIndexes, column);
                var value = rawValue?.Trim() ?? string.Empty;
                if (value.Length == 0)
                {
                    if (!column.Optional && column.EntryType != ContextEntryType.String)
                        throw new SharpOMaticException($"CSV row {record.LineNumber} is missing required value for column '{column.Name}'.");

                    if (column.EntryType == ContextEntryType.String)
                        importedData.Add(BuildCsvData(record.LineNumber, evalRowId, column, string.Empty, string.Empty));

                    continue;
                }

                importedData.Add(BuildCsvData(record.LineNumber, evalRowId, column, rawValue ?? string.Empty, value));
            }
        }

        if (importedRows.Count > 0)
            await repositoryService.InsertEvalRowsWithData(importedRows, importedData);

        return new EvalRowsCsvImportResult { RowsImported = importedRows.Count };
    }

    public async Task<Stream> ExportEvalRowsCsvAsync(Guid evalConfigId, CancellationToken cancellationToken = default)
    {
        var detail = await repositoryService.GetEvalConfigDetail(evalConfigId);
        var columns = detail
            .Columns.Where(column =>
                column.EntryType != ContextEntryType.AssetRef
                && column.EntryType != ContextEntryType.AssetRefList
            )
            .OrderBy(column => column.Order)
            .ToList();
        ValidateReservedEvalColumnNames(columns);

        var dataByRow = detail.Data.GroupBy(data => data.EvalRowId).ToDictionary(group => group.Key, group => group.ToDictionary(data => data.EvalColumnId));

        var memoryStream = new MemoryStream();
        var writer = new StreamWriter(memoryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

        await using (writer)
        {
            WriteCsvRow(writer, columns.Select(column => column.Name).Append(RepeatCsvColumnName));

            var rows = detail.Rows.OrderBy(row => row.Order);
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                dataByRow.TryGetValue(row.EvalRowId, out var rowData);
                WriteCsvRow(
                    writer,
                    columns.Select(column =>
                    {
                        var data = rowData is not null && rowData.TryGetValue(column.EvalColumnId, out var cellData) ? cellData : null;
                        return FormatCsvValue(column.EntryType, data);
                    }).Append((row.Repeat ?? EvalRow.DefaultRepeat).ToString(System.Globalization.CultureInfo.InvariantCulture))
                );
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    private static Dictionary<string, EvalColumn> BuildCsvColumnLookup(List<EvalColumn> columns)
    {
        var columnsByName = new Dictionary<string, EvalColumn>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            var name = column.Name.Trim();
            if (!columnsByName.TryAdd(name, column))
                throw new SharpOMaticException($"Evaluation has duplicate column name '{column.Name}'.");
        }

        return columnsByName;
    }

    private static Dictionary<Guid, int> MatchCsvHeaders(CsvRowRecord headerRecord, Dictionary<string, EvalColumn> columnsByName)
    {
        var matchedHeaderIndexes = new Dictionary<Guid, int>();
        foreach (var header in headerRecord.Fields.Select((value, index) => new { value, index }))
        {
            var name = header.value.Trim();
            if (name.Equals(RepeatCsvColumnName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (name.Length == 0 || !columnsByName.TryGetValue(name, out var column))
                continue;

            if (matchedHeaderIndexes.ContainsKey(column.EvalColumnId))
                throw new SharpOMaticException($"CSV header contains duplicate column '{name}'.");

            matchedHeaderIndexes[column.EvalColumnId] = header.index;
        }

        return matchedHeaderIndexes;
    }

    private static int? GetCsvRepeatHeaderIndex(CsvRowRecord headerRecord)
    {
        int? repeatHeaderIndex = null;
        foreach (var header in headerRecord.Fields.Select((value, index) => new { value, index }))
        {
            var name = header.value.Trim();
            if (!name.Equals(RepeatCsvColumnName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (repeatHeaderIndex.HasValue)
                throw new SharpOMaticException($"CSV header contains duplicate column '{RepeatCsvColumnName}'.");

            repeatHeaderIndex = header.index;
        }

        return repeatHeaderIndex;
    }

    private static void ValidateCsvRequiredHeaders(List<EvalColumn> columns, Dictionary<Guid, int> matchedHeaderIndexes)
    {
        foreach (var column in columns.Where(column => !column.Optional))
        {
            if (!matchedHeaderIndexes.ContainsKey(column.EvalColumnId))
                throw new SharpOMaticException($"CSV file is missing required column '{column.Name}'.");
        }
    }

    private static string? GetCsvRecordValue(CsvRowRecord record, Dictionary<Guid, int> matchedHeaderIndexes, EvalColumn column)
    {
        if (!matchedHeaderIndexes.TryGetValue(column.EvalColumnId, out var headerIndex))
            return null;

        return headerIndex < record.Fields.Count ? record.Fields[headerIndex] : string.Empty;
    }

    private static int GetCsvRepeatValue(CsvRowRecord record, int? repeatHeaderIndex)
    {
        if (!repeatHeaderIndex.HasValue || repeatHeaderIndex.Value >= record.Fields.Count)
            return EvalRow.DefaultRepeat;

        var rawValue = record.Fields[repeatHeaderIndex.Value];
        var value = rawValue?.Trim() ?? string.Empty;
        if (value.Length == 0)
            return EvalRow.DefaultRepeat;

        if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var repeat))
            throw new SharpOMaticException($"CSV row {record.LineNumber} column '{RepeatCsvColumnName}' must be an integer.");

        if (repeat < EvalRow.MinRepeat || repeat > EvalRow.MaxRepeat)
            throw new SharpOMaticException($"CSV row {record.LineNumber} column '{RepeatCsvColumnName}' must be between {EvalRow.MinRepeat} and {EvalRow.MaxRepeat}.");

        return repeat;
    }

    private static void ValidateReservedEvalColumnNames(List<EvalColumn> columns)
    {
        foreach (var column in columns)
        {
            var name = column.Name.Trim();
            if (name.Equals(RepeatCsvColumnName, StringComparison.OrdinalIgnoreCase))
                throw new SharpOMaticException($"Evaluation column name '{RepeatCsvColumnName}' is reserved.");

            if (name.Equals("Name", StringComparison.OrdinalIgnoreCase) && column.Order != 0)
                throw new SharpOMaticException("Evaluation column name 'Name' is reserved for the fixed first column.");
        }
    }

    private static EvalData BuildCsvData(int lineNumber, Guid evalRowId, EvalColumn column, string rawValue, string trimmedValue)
    {
        var data = new EvalData
        {
            EvalDataId = Guid.NewGuid(),
            EvalRowId = evalRowId,
            EvalColumnId = column.EvalColumnId,
        };

        switch (column.EntryType)
        {
            case ContextEntryType.Bool:
                if (!bool.TryParse(trimmedValue, out var boolValue))
                    throw new SharpOMaticException($"CSV row {lineNumber} column '{column.Name}' must be true or false.");

                data.BoolValue = boolValue;
                break;

            case ContextEntryType.Int:
                if (!int.TryParse(trimmedValue, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var intValue))
                    throw new SharpOMaticException($"CSV row {lineNumber} column '{column.Name}' must be an integer.");

                data.IntValue = intValue;
                break;

            case ContextEntryType.Double:
                if (!double.TryParse(trimmedValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var doubleValue))
                    throw new SharpOMaticException($"CSV row {lineNumber} column '{column.Name}' must be a number.");

                data.DoubleValue = doubleValue;
                break;

            default:
                data.StringValue = rawValue;
                break;
        }

        return data;
    }

    private static string FormatCsvValue(ContextEntryType entryType, EvalData? data)
    {
        if (data is null)
            return string.Empty;

        return entryType switch
        {
            ContextEntryType.Bool => data.BoolValue.HasValue ? data.BoolValue.Value.ToString().ToLowerInvariant() : string.Empty,
            ContextEntryType.Int => data.IntValue.HasValue ? data.IntValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
            ContextEntryType.Double => data.DoubleValue.HasValue ? data.DoubleValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
            _ => data.StringValue ?? string.Empty,
        };
    }

    private static string QuoteCsvField(string value)
    {
        if (value.Length == 0)
            return value;

        if (value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n'))
            return '"' + value.Replace("\"", "\"\"") + '"';

        return value;
    }

    private static void WriteCsvRow(StreamWriter writer, IEnumerable<string> fields)
    {
        writer.Write(string.Join(",", fields.Select(QuoteCsvField)));
        writer.Write("\r\n");
    }

    private static List<CsvRowRecord> ParseCsvRows(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var records = new List<CsvRowRecord>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var lineNumber = 1;
        var recordLineNumber = 1;

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (inQuotes)
            {
                if (current == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (current == '\n')
                        lineNumber++;
                    else if (current == '\r')
                    {
                        lineNumber++;
                        if (index + 1 < text.Length && text[index + 1] == '\n')
                            index++;
                    }

                    field.Append(current);
                }

                continue;
            }

            if (current == '"')
            {
                if (field.Length != 0)
                    throw new SharpOMaticException($"CSV line {lineNumber} contains an unexpected quote.");

                inQuotes = true;
                continue;
            }

            if (current == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (current == '\n' || current == '\r')
            {
                fields.Add(field.ToString());
                field.Clear();
                records.Add(new CsvRowRecord(recordLineNumber, fields));
                fields = [];

                lineNumber++;
                if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    index++;

                recordLineNumber = lineNumber;
                continue;
            }

            field.Append(current);
        }

        if (inQuotes)
            throw new SharpOMaticException($"CSV line {lineNumber} has an unterminated quoted field.");

        fields.Add(field.ToString());
        if (fields.Count > 1 || fields.Any(fieldValue => fieldValue.Length > 0))
            records.Add(new CsvRowRecord(recordLineNumber, fields));

        return records;
    }

    private sealed record CsvRowRecord(int LineNumber, List<string> Fields);
}
