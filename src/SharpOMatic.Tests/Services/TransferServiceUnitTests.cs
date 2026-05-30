namespace SharpOMatic.Tests.Services;

public sealed class TransferServiceUnitTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Export_includes_workflows_connectors_models_folders_assets_and_strips_secrets()
    {
        var workflow = CreateWorkflow();
        var connector = CreateConnector();
        var model = CreateModel(connector.ConnectorId);
        var folder = CreateAssetFolder();
        var asset = CreateAsset(folder.FolderId);
        var assetBytes = Encoding.UTF8.GetBytes("asset-content");

        var repository = new Mock<IRepositoryService>();
        repository.Setup(service => service.GetWorkflow(workflow.Id)).ReturnsAsync(workflow);
        repository.Setup(service => service.GetConnector(connector.ConnectorId, true)).ReturnsAsync(connector);
        repository.Setup(service => service.GetConnectorConfig(connector.ConfigId)).ReturnsAsync(CreateConnectorConfig(connector.ConfigId));
        repository.Setup(service => service.GetModel(model.ModelId, true)).ReturnsAsync(model);
        repository.Setup(service => service.GetModelConfig(model.ConfigId)).ReturnsAsync(CreateModelConfig(model.ConfigId));
        repository.Setup(service => service.GetAsset(asset.AssetId)).ReturnsAsync(asset);
        repository.Setup(service => service.GetAssetFolder(folder.FolderId)).ReturnsAsync(folder);

        var assetStore = new Mock<IAssetStore>();
        assetStore
            .Setup(store => store.OpenReadAsync(asset.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(assetBytes));

        var transferService = new TransferService(repository.Object, assetStore.Object);
        await using var output = new MemoryStream();

        await transferService.ExportAsync(
            new TransferExportRequest
            {
                IncludeSecrets = false,
                Workflows = new TransferSelection { Ids = [workflow.Id] },
                Connectors = new TransferSelection { Ids = [connector.ConnectorId] },
                Models = new TransferSelection { Ids = [model.ModelId] },
                Assets = new TransferSelection { Ids = [asset.AssetId] },
            },
            output
        );

        output.Position = 0;
        using var archive = new ZipArchive(output, ZipArchiveMode.Read, leaveOpen: true);
        var exportedWorkflow = ReadEnvelopePayload<WorkflowEntity>(archive, "workflows/", "workflow");
        var exportedConnector = ReadEnvelopePayload<Connector>(archive, "connectors/", "connector");
        var exportedModel = ReadEnvelopePayload<Model>(archive, "models/", "model");
        var exportedAsset = ReadEnvelopePayload<TransferAssetPayload>(archive, "assets/", "asset");

        Assert.Null(archive.GetEntry("manifest.json"));
        Assert.Equal(workflow.Id, exportedWorkflow.Id);
        Assert.False(exportedConnector.FieldValues.ContainsKey("apiKey"));
        Assert.Equal("east", exportedConnector.FieldValues["region"]);
        Assert.False(exportedModel.ParameterValues.ContainsKey("deploymentKey"));
        Assert.Equal("fast", exportedModel.ParameterValues["mode"]);
        Assert.Equal(asset.AssetId, exportedAsset.AssetId);
        Assert.Equal(folder.Name, exportedAsset.FolderName);
        Assert.Equal(assetBytes, Convert.FromBase64String(exportedAsset.ContentBase64));
    }

    [Fact]
    public async Task Import_updates_workflows_connectors_models_folders_assets_and_merges_existing_secrets()
    {
        var workflow = CreateWorkflow();
        var connector = CreateConnector();
        var model = CreateModel(connector.ConnectorId);
        var folder = CreateAssetFolder();
        var asset = CreateAsset(folder.FolderId);
        var assetBytes = Encoding.UTF8.GetBytes("imported-asset");
        connector.FieldValues.Remove("apiKey");
        model.ParameterValues.Remove("deploymentKey");

        await using var input = CreateTransferArchive(
            archive =>
            {
                WriteEnvelopeEntry(archive, "misc/workflow.json", "workflow", workflow);
                WriteEnvelopeEntry(archive, "connectors/connector.json", "connector", connector);
                WriteEnvelopeEntry(archive, "models/model.json", "model", model);
                WriteEnvelopeEntry(
                    archive,
                    "assets/asset.json",
                    "asset",
                    new TransferAssetPayload
                    {
                        AssetId = asset.AssetId,
                        FolderName = folder.Name,
                        Name = asset.Name,
                        MediaType = asset.MediaType,
                        Created = asset.Created,
                        SizeBytes = asset.SizeBytes,
                        ContentBase64 = Convert.ToBase64String(assetBytes),
                    }
                );
            }
        );

        WorkflowEntity? importedWorkflow = null;
        Connector? importedConnector = null;
        Model? importedModel = null;
        AssetFolder? importedFolder = null;
        Asset? importedAsset = null;
        byte[]? savedAssetBytes = null;

        var repository = new Mock<IRepositoryService>();
        repository.Setup(service => service.GetConnectorConfig(connector.ConfigId)).ReturnsAsync(CreateConnectorConfig(connector.ConfigId));
        repository.Setup(service => service.GetConnector(connector.ConnectorId, false)).ReturnsAsync(CreateConnector(secretValue: "existing-connector-secret"));
        repository.Setup(service => service.GetModelConfig(model.ConfigId)).ReturnsAsync(CreateModelConfig(model.ConfigId));
        repository.Setup(service => service.GetModel(model.ModelId, false)).ReturnsAsync(CreateModel(connector.ConnectorId, secretValue: "existing-model-secret"));
        repository.Setup(service => service.GetAssetFolderByName(folder.Name)).ReturnsAsync((AssetFolder?)null);
        repository.Setup(service => service.UpsertWorkflow(It.IsAny<WorkflowEntity>())).Callback<WorkflowEntity>(item => importedWorkflow = item).Returns(Task.CompletedTask);
        repository.Setup(service => service.UpsertConnector(It.IsAny<Connector>(), false)).Callback<Connector, bool>((item, _) => importedConnector = item).Returns(Task.CompletedTask);
        repository.Setup(service => service.UpsertModel(It.IsAny<Model>())).Callback<Model>(item => importedModel = item).Returns(Task.CompletedTask);
        repository.Setup(service => service.UpsertAssetFolder(It.IsAny<AssetFolder>())).Callback<AssetFolder>(item => importedFolder = item).Returns(Task.CompletedTask);
        repository.Setup(service => service.UpsertAsset(It.IsAny<Asset>())).Callback<Asset>(item => importedAsset = item).Returns(Task.CompletedTask);

        var assetStore = new Mock<IAssetStore>();
        assetStore
            .Setup(store => store.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, CancellationToken>((_, stream, _) =>
            {
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                savedAssetBytes = memory.ToArray();
            })
            .Returns(Task.CompletedTask);

        var transferService = new TransferService(repository.Object, assetStore.Object);
        var batch = await transferService.ImportZipAsync(input);
        var result = batch.Result;

        Assert.Equal(4, batch.FilesImported);
        Assert.Equal(0, batch.FilesFailed);
        Assert.Equal(1, result.WorkflowsImported);
        Assert.Equal(1, result.ConnectorsImported);
        Assert.Equal(1, result.ModelsImported);
        Assert.Equal(1, result.AssetsImported);
        Assert.Equal(workflow.Id, importedWorkflow?.Id);
        Assert.Equal("existing-connector-secret", importedConnector?.FieldValues["apiKey"]);
        Assert.Equal("existing-model-secret", importedModel?.ParameterValues["deploymentKey"]);
        Assert.Equal(folder.Name, importedFolder?.Name);
        Assert.Equal(asset.AssetId, importedAsset?.AssetId);
        Assert.Equal(importedFolder?.FolderId, importedAsset?.FolderId);
        Assert.Equal(AssetStorageKey.ForLibrary(asset.AssetId, importedFolder?.FolderId), importedAsset?.StorageKey);
        Assert.Equal(assetBytes, savedAssetBytes);
    }

    [Fact]
    public async Task Export_includes_terminal_evaluation_runs_and_result_data()
    {
        var package = CreateTransferPackage();
        var repository = new Mock<IRepositoryService>();
        repository.Setup(service => service.GetEvalTransferPackage(package.EvalConfig.EvalConfigId)).ReturnsAsync(package);

        var transferService = new TransferService(repository.Object, Mock.Of<IAssetStore>());
        await using var output = new MemoryStream();

        await transferService.ExportAsync(
            new TransferExportRequest
            {
                Evaluations = new TransferSelection { All = false, Ids = [package.EvalConfig.EvalConfigId] },
            },
            output
        );

        output.Position = 0;
        using var archive = new ZipArchive(output, ZipArchiveMode.Read, leaveOpen: true);
        var envelope = ReadEnvelope<TransferEvaluationPackage>(archive, "evaluations/", "evaluation");
        var exportedPackage = envelope.Payload;

        Assert.Equal(TransferEnvelope.CurrentSchemaVersion, envelope.SchemaVersion);
        Assert.Equal(package.Runs.Single().EvalRunId, exportedPackage.Runs.Single().EvalRunId);
        Assert.Equal(package.RunRows.Single().EvalRunRowId, exportedPackage.RunRows.Single().EvalRunRowId);
        Assert.Equal(package.RunRowGraders.Single().EvalRunRowGraderId, exportedPackage.RunRowGraders.Single().EvalRunRowGraderId);
        Assert.Equal(package.RunGraderSummaries.Single().EvalRunGraderSummaryId, exportedPackage.RunGraderSummaries.Single().EvalRunGraderSummaryId);
    }

    [Fact]
    public async Task Repository_transfer_package_excludes_running_runs_and_result_children()
    {
        var package = CreateTransferPackage();
        var runningRun = new EvalRun
        {
            EvalRunId = Guid.NewGuid(),
            EvalConfigId = package.EvalConfig.EvalConfigId,
            Name = "Running",
            Order = 2,
            Started = DateTime.UtcNow,
            Status = EvalRunStatus.Running,
            Message = "Running",
            CancelRequested = false,
            TotalRows = 1,
            CompletedRows = 0,
            FailedRows = 0,
            RunScoreMode = EvalRunScoreMode.AverageScore,
        };
        var runningRunRow = new EvalRunRow
        {
            EvalRunRowId = Guid.NewGuid(),
            EvalRunId = runningRun.EvalRunId,
            EvalRowId = package.Rows.Single().EvalRowId,
            Order = 1,
            Started = DateTime.UtcNow,
            Status = EvalRunStatus.Running,
        };
        var runningRunRowGrader = new EvalRunRowGrader
        {
            EvalRunRowGraderId = Guid.NewGuid(),
            EvalRunRowId = runningRunRow.EvalRunRowId,
            EvalGraderId = package.Graders.Single().EvalGraderId,
            EvalRunId = runningRun.EvalRunId,
            Started = DateTime.UtcNow,
            Status = EvalRunStatus.Running,
        };
        var runningSummary = new EvalRunGraderSummary
        {
            EvalRunGraderSummaryId = Guid.NewGuid(),
            EvalRunId = runningRun.EvalRunId,
            EvalGraderId = package.Graders.Single().EvalGraderId,
            TotalCount = 1,
            CompletedCount = 0,
            FailedCount = 0,
        };

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        using (var context = new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions())))
        {
            context.Database.EnsureCreated();
            context.EvalConfigs.Add(package.EvalConfig);
            context.EvalGraders.AddRange(package.Graders);
            context.EvalColumns.AddRange(package.Columns);
            context.EvalRows.AddRange(package.Rows);
            context.EvalData.AddRange(package.Data);
            context.EvalRuns.AddRange(package.Runs);
            context.EvalRuns.Add(runningRun);
            context.EvalRunRows.AddRange(package.RunRows);
            context.EvalRunRows.Add(runningRunRow);
            context.EvalRunRowGraders.AddRange(package.RunRowGraders);
            context.EvalRunRowGraders.Add(runningRunRowGrader);
            context.EvalRunGraderSummaries.AddRange(package.RunGraderSummaries);
            context.EvalRunGraderSummaries.Add(runningSummary);
            await context.SaveChangesAsync();
        }

        var repository = new RepositoryService(new TestDbContextFactory(options));
        var exportedPackage = await repository.GetEvalTransferPackage(package.EvalConfig.EvalConfigId);

        Assert.DoesNotContain(exportedPackage.Runs, run => run.Status == EvalRunStatus.Running);
        Assert.Equal(package.Runs.Single().EvalRunId, exportedPackage.Runs.Single().EvalRunId);
        Assert.Equal(package.RunRows.Single().EvalRunRowId, exportedPackage.RunRows.Single().EvalRunRowId);
        Assert.Equal(package.RunRowGraders.Single().EvalRunRowGraderId, exportedPackage.RunRowGraders.Single().EvalRunRowGraderId);
        Assert.Equal(package.RunGraderSummaries.Single().EvalRunGraderSummaryId, exportedPackage.RunGraderSummaries.Single().EvalRunGraderSummaryId);
    }

    [Fact]
    public async Task Import_remaps_evaluation_runs_and_preserves_result_values()
    {
        var package = CreateTransferPackage();
        await using var input = CreateTransferArchive(archive => WriteEnvelopeEntry(archive, "nested/evaluation.json", "evaluation", package));

        EvalConfig? importedConfig = null;
        var importedGraders = new List<EvalGrader>();
        var importedRows = new List<EvalRow>();
        var importedRuns = new List<EvalRun>();
        var importedRunRows = new List<EvalRunRow>();
        var importedRunRowGraders = new List<EvalRunRowGrader>();
        var importedSummaries = new List<EvalRunGraderSummary>();

        var repository = CreateImportRepository(
            config => importedConfig = config,
            graders => importedGraders.AddRange(graders),
            rows => importedRows.AddRange(rows),
            runs => importedRuns.Add(runs),
            runRows => importedRunRows.AddRange(runRows),
            runRowGraders => importedRunRowGraders.AddRange(runRowGraders),
            summaries => importedSummaries.AddRange(summaries)
        );

        var transferService = new TransferService(repository.Object, Mock.Of<IAssetStore>());
        var batch = await transferService.ImportZipAsync(input);
        var result = batch.Result;

        Assert.Equal(1, batch.FilesImported);
        Assert.Equal(0, batch.FilesFailed);
        Assert.Equal(1, result.EvaluationsImported);
        Assert.Equal(1, result.EvaluationRunsImported);
        Assert.NotNull(importedConfig);
        Assert.NotEqual(package.EvalConfig.EvalConfigId, importedConfig.EvalConfigId);
        Assert.Equal(package.EvalConfig.Name, importedConfig.Name);
        Assert.Single(importedRuns);
        Assert.NotEqual(package.Runs.Single().EvalRunId, importedRuns.Single().EvalRunId);
        Assert.Equal(importedConfig.EvalConfigId, importedRuns.Single().EvalConfigId);
        Assert.Equal(package.Runs.Single().Score, importedRuns.Single().Score);
        Assert.Equal(importedRuns.Single().EvalRunId, importedRunRows.Single().EvalRunId);
        Assert.Equal(importedRows.Single().EvalRowId, importedRunRows.Single().EvalRowId);
        Assert.Equal(package.RunRows.Single().OutputContext, importedRunRows.Single().OutputContext);
        Assert.Equal(importedRunRows.Single().EvalRunRowId, importedRunRowGraders.Single().EvalRunRowId);
        Assert.Equal(importedGraders.Single().EvalGraderId, importedRunRowGraders.Single().EvalGraderId);
        Assert.Equal(package.RunRowGraders.Single().Score, importedRunRowGraders.Single().Score);
        Assert.Equal(importedRuns.Single().EvalRunId, importedSummaries.Single().EvalRunId);
        Assert.Equal(importedGraders.Single().EvalGraderId, importedSummaries.Single().EvalGraderId);
        Assert.Equal(package.RunGraderSummaries.Single().PassRate, importedSummaries.Single().PassRate);
    }

    [Fact]
    public async Task Import_json_imports_single_envelope()
    {
        var workflow = CreateWorkflow();
        await using var input = CreateEnvelopeStream("workflow", workflow);

        WorkflowEntity? importedWorkflow = null;
        var repository = new Mock<IRepositoryService>();
        repository.Setup(service => service.UpsertWorkflow(It.IsAny<WorkflowEntity>())).Callback<WorkflowEntity>(item => importedWorkflow = item).Returns(Task.CompletedTask);
        var transferService = new TransferService(repository.Object, Mock.Of<IAssetStore>());
        var result = await transferService.ImportJsonAsync(input);

        Assert.Equal(1, result.WorkflowsImported);
        Assert.Equal(workflow.Id, importedWorkflow?.Id);
    }

    [Fact]
    public async Task Import_files_partially_imports_valid_files_and_counts_failures()
    {
        var workflow = CreateWorkflow();
        await using var valid = CreateEnvelopeStream("workflow", workflow);
        await using var invalid = new MemoryStream(Encoding.UTF8.GetBytes("{\"type\":\"unknown\",\"schemaVersion\":1,\"payload\":{}}"));

        var repository = new Mock<IRepositoryService>();
        repository.Setup(service => service.UpsertWorkflow(It.IsAny<WorkflowEntity>())).Returns(Task.CompletedTask);
        var transferService = new TransferService(repository.Object, Mock.Of<IAssetStore>());

        var result = await transferService.ImportFilesAsync(
            [
                new TransferImportFile { Name = "workflow.json", Stream = valid },
                new TransferImportFile { Name = "bad.json", Stream = invalid },
            ]
        );

        Assert.Equal(2, result.FilesProcessed);
        Assert.Equal(1, result.FilesImported);
        Assert.Equal(1, result.FilesFailed);
        Assert.Equal(1, result.Result.WorkflowsImported);
    }

    [Fact]
    public async Task Import_json_rejects_evaluation_package_with_mismatched_run_relationships()
    {
        var package = CreateTransferPackage();
        package.RunRows.Single().EvalRunId = Guid.NewGuid();
        await using var input = CreateEnvelopeStream("evaluation", package);

        var repository = CreateImportRepository();
        var transferService = new TransferService(repository.Object, Mock.Of<IAssetStore>());

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() => transferService.ImportJsonAsync(input));
        Assert.Contains("referencing missing run", exception.Message);
    }

    private static Mock<IRepositoryService> CreateImportRepository(
        Action<EvalConfig>? config = null,
        Action<List<EvalGrader>>? graders = null,
        Action<List<EvalRow>>? rows = null,
        Action<EvalRun>? runs = null,
        Action<List<EvalRunRow>>? runRows = null,
        Action<List<EvalRunRowGrader>>? runRowGraders = null,
        Action<List<EvalRunGraderSummary>>? summaries = null)
    {
        var repository = new Mock<IRepositoryService>();
        repository.Setup(service => service.UpsertEvalConfig(It.IsAny<EvalConfig>())).Callback<EvalConfig>(item => config?.Invoke(item)).Returns(Task.CompletedTask);
        repository.Setup(service => service.UpsertEvalGraders(It.IsAny<List<EvalGrader>>())).Callback<List<EvalGrader>>(items => graders?.Invoke(items)).Returns(Task.CompletedTask);
        repository.Setup(service => service.UpsertEvalColumns(It.IsAny<List<EvalColumn>>())).Returns(Task.CompletedTask);
        repository.Setup(service => service.UpsertEvalRows(It.IsAny<List<EvalRow>>())).Callback<List<EvalRow>>(items => rows?.Invoke(items)).Returns(Task.CompletedTask);
        repository.Setup(service => service.UpsertEvalData(It.IsAny<List<EvalData>>())).Returns(Task.CompletedTask);
        repository.Setup(service => service.UpsertEvalRun(It.IsAny<EvalRun>(), true)).Callback<EvalRun, bool>((item, _) => runs?.Invoke(item)).ReturnsAsync(true);
        repository.Setup(service => service.UpsertEvalRunRows(It.IsAny<List<EvalRunRow>>(), true)).Callback<List<EvalRunRow>, bool>((items, _) => runRows?.Invoke(items)).ReturnsAsync(true);
        repository.Setup(service => service.UpsertEvalRunRowGraders(It.IsAny<List<EvalRunRowGrader>>(), true)).Callback<List<EvalRunRowGrader>, bool>((items, _) => runRowGraders?.Invoke(items)).ReturnsAsync(true);
        repository.Setup(service => service.UpsertEvalRunGraderSummaries(It.IsAny<List<EvalRunGraderSummary>>())).Callback<List<EvalRunGraderSummary>>(items => summaries?.Invoke(items)).Returns(Task.CompletedTask);
        return repository;
    }

    private static TransferEvaluationPackage CreateTransferPackage()
    {
        var evalConfigId = Guid.NewGuid();
        var graderId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var rowId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var runRowId = Guid.NewGuid();

        return new TransferEvaluationPackage
        {
            EvalConfig = new EvalConfig
            {
                EvalConfigId = evalConfigId,
                WorkflowId = Guid.NewGuid(),
                Name = "Regression suite",
                Description = "Transfer test",
                MaxParallel = 2,
                RowScoreMode = EvalRunRowScoreMode.Average,
                RunScoreMode = EvalRunScoreMode.AverageScore,
            },
            Graders =
            [
                new EvalGrader
                {
                    EvalGraderId = graderId,
                    EvalConfigId = evalConfigId,
                    WorkflowId = Guid.NewGuid(),
                    Order = 1,
                    Label = "Quality",
                    PassThreshold = 0.7,
                    IncludeInScore = true,
                },
            ],
            Columns =
            [
                new EvalColumn
                {
                    EvalColumnId = columnId,
                    EvalConfigId = evalConfigId,
                    Name = "Name",
                    Order = 0,
                    EntryType = ContextEntryType.String,
                    Optional = false,
                    InputPath = "name",
                },
            ],
            Rows =
            [
                new EvalRow
                {
                    EvalRowId = rowId,
                    EvalConfigId = evalConfigId,
                    Order = 1,
                },
            ],
            Data =
            [
                new EvalData
                {
                    EvalDataId = Guid.NewGuid(),
                    EvalRowId = rowId,
                    EvalColumnId = columnId,
                    StringValue = "Case 1",
                },
            ],
            Runs =
            [
                new EvalRun
                {
                    EvalRunId = runId,
                    EvalConfigId = evalConfigId,
                    Name = "Baseline",
                    Order = 1,
                    Started = DateTime.UtcNow.AddMinutes(-5),
                    Finished = DateTime.UtcNow,
                    Status = EvalRunStatus.Completed,
                    Message = "Completed",
                    CancelRequested = false,
                    TotalRows = 1,
                    CompletedRows = 1,
                    FailedRows = 0,
                    AveragePassRate = 1.0,
                    RunScoreMode = EvalRunScoreMode.AverageScore,
                    Score = 0.9,
                },
            ],
            RunRows =
            [
                new EvalRunRow
                {
                    EvalRunRowId = runRowId,
                    EvalRunId = runId,
                    EvalRowId = rowId,
                    Order = 1,
                    Started = DateTime.UtcNow.AddMinutes(-4),
                    Finished = DateTime.UtcNow.AddMinutes(-3),
                    Status = EvalRunStatus.Completed,
                    Score = 0.9,
                    InputContext = "{\"name\":\"Case 1\"}",
                    OutputContext = "{\"answer\":\"ok\"}",
                },
            ],
            RunRowGraders =
            [
                new EvalRunRowGrader
                {
                    EvalRunRowGraderId = Guid.NewGuid(),
                    EvalRunRowId = runRowId,
                    EvalGraderId = graderId,
                    EvalRunId = runId,
                    Started = DateTime.UtcNow.AddMinutes(-3),
                    Finished = DateTime.UtcNow.AddMinutes(-2),
                    Status = EvalRunStatus.Completed,
                    Score = 0.9,
                    InputContext = "{\"answer\":\"ok\"}",
                    OutputContext = "{\"score\":0.9}",
                },
            ],
            RunGraderSummaries =
            [
                new EvalRunGraderSummary
                {
                    EvalRunGraderSummaryId = Guid.NewGuid(),
                    EvalRunId = runId,
                    EvalGraderId = graderId,
                    TotalCount = 1,
                    CompletedCount = 1,
                    FailedCount = 0,
                    MinScore = 0.9,
                    MaxScore = 0.9,
                    AverageScore = 0.9,
                    MedianScore = 0.9,
                    StandardDeviation = 0,
                    PassRate = 1.0,
                },
            ],
        };
    }

    private static MemoryStream CreateEnvelopeStream<T>(string type, T payload)
    {
        var output = new MemoryStream();
        WriteEnvelope(output, type, payload);
        output.Position = 0;
        return output;
    }

    private static MemoryStream CreateTransferArchive(Action<ZipArchive> writeEntries)
    {
        var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            writeEntries(archive);
        }

        output.Position = 0;
        return output;
    }

    private static void WriteEnvelopeEntry<T>(ZipArchive archive, string entryName, string type, T payload)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        WriteEnvelope(stream, type, payload);
    }

    private static void WriteEnvelope<T>(Stream stream, string type, T payload)
    {
        JsonSerializer.Serialize(
            stream,
            new TransferEnvelope<T>
            {
                Type = type,
                ExportedUtc = DateTime.UtcNow,
                Payload = payload,
            },
            JsonOptions
        );
    }

    private static WorkflowEntity CreateWorkflow()
    {
        return new WorkflowEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            Name = "Workflow",
            Description = "Workflow transfer test",
            Nodes = [],
            Connections = [],
        };
    }

    private static Connector CreateConnector(string secretValue = "connector-secret")
    {
        return new Connector
        {
            ConnectorId = Guid.NewGuid(),
            Version = 1,
            ConfigId = "connector-config",
            Name = "Connector",
            Description = "Connector transfer test",
            AuthenticationModeId = "api-key",
            FieldValues = new Dictionary<string, string?>
            {
                ["apiKey"] = secretValue,
                ["region"] = "east",
            },
        };
    }

    private static ConnectorConfig CreateConnectorConfig(string configId)
    {
        return new ConnectorConfig
        {
            Version = 1,
            ConfigId = configId,
            DisplayName = "Connector Config",
            Description = "Connector config transfer test",
            AuthModes =
            [
                new AuthenticationModeConfig
                {
                    Id = "api-key",
                    DisplayName = "API Key",
                    Kind = AuthenticationModeKind.ApiKey,
                    IsDefault = true,
                    Fields =
                    [
                        CreateFieldDescriptor("apiKey", FieldDescriptorType.Secret),
                        CreateFieldDescriptor("region", FieldDescriptorType.String),
                    ],
                },
            ],
        };
    }

    private static Model CreateModel(Guid connectorId, string secretValue = "model-secret")
    {
        return new Model
        {
            ModelId = Guid.NewGuid(),
            Version = 1,
            ConfigId = "model-config",
            ConnectorId = connectorId,
            Name = "Model",
            Description = "Model transfer test",
            CustomCapabilities = ["chat"],
            ParameterValues = new Dictionary<string, string?>
            {
                ["deploymentKey"] = secretValue,
                ["mode"] = "fast",
            },
        };
    }

    private static ModelConfig CreateModelConfig(string configId)
    {
        return new ModelConfig
        {
            Version = 1,
            ConfigId = configId,
            DisplayName = "Model Config",
            Description = "Model config transfer test",
            ConnectorConfigId = "connector-config",
            IsCustom = false,
            Capabilities = [new ModelCapability { Name = "chat", DisplayName = "Chat" }],
            ParameterFields =
            [
                CreateFieldDescriptor("deploymentKey", FieldDescriptorType.Secret),
                CreateFieldDescriptor("mode", FieldDescriptorType.String),
            ],
        };
    }

    private static FieldDescriptor CreateFieldDescriptor(string name, FieldDescriptorType type)
    {
        return new FieldDescriptor
        {
            Name = name,
            Label = name,
            Description = name,
            CallDefined = false,
            Type = type,
            IsRequired = false,
        };
    }

    private static AssetFolder CreateAssetFolder()
    {
        return new AssetFolder
        {
            FolderId = Guid.NewGuid(),
            Name = "Transfer Folder",
            Created = DateTime.UtcNow.AddDays(-1),
        };
    }

    private static Asset CreateAsset(Guid folderId)
    {
        var assetId = Guid.NewGuid();
        return new Asset
        {
            AssetId = assetId,
            RunId = null,
            ConversationId = null,
            FolderId = folderId,
            Name = "input.txt",
            Scope = AssetScope.Library,
            Created = DateTime.UtcNow,
            MediaType = "text/plain",
            SizeBytes = 13,
            StorageKey = AssetStorageKey.ForLibrary(assetId, folderId),
        };
    }

    private static TransferEnvelope<T> ReadEnvelope<T>(ZipArchive archive, string directory, string expectedType)
    {
        var entry = archive.Entries.Single(item => item.FullName.StartsWith(directory, StringComparison.Ordinal) && item.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        using var stream = entry.Open();
        var envelope = JsonSerializer.Deserialize<TransferEnvelope<T>>(stream, JsonOptions) ?? throw new InvalidOperationException($"Entry '{entry.FullName}' is invalid.");
        Assert.Equal(expectedType, envelope.Type);
        Assert.Equal(TransferEnvelope.CurrentSchemaVersion, envelope.SchemaVersion);
        return envelope;
    }

    private static T ReadEnvelopePayload<T>(ZipArchive archive, string directory, string expectedType)
    {
        return ReadEnvelope<T>(archive, directory, expectedType).Payload;
    }

    private sealed class TestDbContextFactory(DbContextOptions<SharpOMaticDbContext> options) : IDbContextFactory<SharpOMaticDbContext>
    {
        public SharpOMaticDbContext CreateDbContext() => new(options, Options.Create(new SharpOMaticDbOptions()));
    }
}
