namespace SharpOMatic.Tests.Services;

public sealed class RepositoryTimestampUnitTests
{
    [Fact]
    public async Task Upserts_manage_created_and_modified_timestamps()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = await CreateRepository(connection);

        var workflow = CreateWorkflow();
        var connector = CreateConnector();
        var model = CreateModel(connector.ConnectorId);
        var evalConfig = CreateEvalConfig(workflow.Id);
        var beforeCreate = DateTime.UtcNow;

        await repository.UpsertWorkflow(workflow);
        await repository.UpsertConnector(connector, hideSecrets: false);
        await repository.UpsertModel(model);
        await repository.UpsertEvalConfig(evalConfig);

        var afterCreate = DateTime.UtcNow;
        var createdWorkflow = (await repository.GetWorkflowSummaries()).Single();
        var createdConnector = await repository.GetConnector(connector.ConnectorId, hideSecrets: false);
        var createdModel = await repository.GetModel(model.ModelId, hideSecrets: false);
        var createdEvalConfig = await repository.GetEvalConfig(evalConfig.EvalConfigId);

        AssertCreated(createdWorkflow.Created, createdWorkflow.Modified, beforeCreate, afterCreate);
        AssertCreated(createdConnector.Created, createdConnector.Modified, beforeCreate, afterCreate);
        AssertCreated(createdModel.Created, createdModel.Modified, beforeCreate, afterCreate);
        AssertCreated(createdEvalConfig.Created, createdEvalConfig.Modified, beforeCreate, afterCreate);

        workflow.Name = "Updated workflow";
        connector.Name = "Updated connector";
        model.Name = "Updated model";
        evalConfig.Name = "Updated evaluation";

        await repository.UpsertWorkflow(workflow);
        await repository.UpsertConnector(connector, hideSecrets: false);
        await repository.UpsertModel(model);
        await repository.UpsertEvalConfig(evalConfig);

        var updatedWorkflow = (await repository.GetWorkflowSummaries()).Single();
        var updatedConnector = await repository.GetConnector(connector.ConnectorId, hideSecrets: false);
        var updatedModel = await repository.GetModel(model.ModelId, hideSecrets: false);
        var updatedEvalConfig = await repository.GetEvalConfig(evalConfig.EvalConfigId);

        AssertUpdated(createdWorkflow.Created, createdWorkflow.Modified, updatedWorkflow.Created, updatedWorkflow.Modified);
        AssertUpdated(createdConnector.Created, createdConnector.Modified, updatedConnector.Created, updatedConnector.Modified);
        AssertUpdated(createdModel.Created, createdModel.Modified, updatedModel.Created, updatedModel.Modified);
        AssertUpdated(createdEvalConfig.Created, createdEvalConfig.Modified, updatedEvalConfig.Created, updatedEvalConfig.Modified);

        Assert.Equal(updatedWorkflow.Created, (await repository.GetWorkflowSummaries()).Single().Created);
        Assert.Equal(updatedConnector.Modified, (await repository.GetConnectorSummaries()).Single().Modified);
        Assert.Equal(updatedModel.Modified, (await repository.GetModelSummaries()).Single().Modified);
        Assert.Equal(updatedEvalConfig.Modified, (await repository.GetEvalConfigSummaries()).Single().Modified);
    }

    [Fact]
    public async Task Updating_legacy_record_preserves_null_created_timestamp()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = await CreateRepository(connection);
        var evalConfig = CreateEvalConfig(null);

        await using (var dbContext = CreateContext(connection))
        {
            dbContext.EvalConfigs.Add(evalConfig);
            await dbContext.SaveChangesAsync();
        }

        var beforeUpdate = DateTime.UtcNow;
        evalConfig.Name = "Updated legacy evaluation";
        await repository.UpsertEvalConfig(evalConfig);
        var afterUpdate = DateTime.UtcNow;

        var updated = await repository.GetEvalConfig(evalConfig.EvalConfigId);
        Assert.Null(updated.Created);
        Assert.NotNull(updated.Modified);
        Assert.InRange(updated.Modified.Value, beforeUpdate, afterUpdate);
    }

    [Fact]
    public async Task Asset_upsert_initializes_modified_and_supports_modified_sorting()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = await CreateRepository(connection);
        var olderAsset = CreateAsset("older.txt", DateTime.UtcNow.AddDays(-2));
        var newerAsset = CreateAsset("newer.txt", DateTime.UtcNow.AddDays(-1));

        await repository.UpsertAsset(olderAsset);
        await repository.UpsertAsset(newerAsset);

        Assert.Equal(olderAsset.Created, olderAsset.Modified);
        Assert.Equal(newerAsset.Created, newerAsset.Modified);

        olderAsset.Name = "updated.txt";
        var originalCreated = olderAsset.Created;
        await repository.UpsertAsset(olderAsset);

        var updated = await repository.GetAsset(olderAsset.AssetId);
        Assert.Equal(originalCreated, updated.Created);
        Assert.NotNull(updated.Modified);
        Assert.True(updated.Modified > newerAsset.Modified);

        var descending = await repository.GetAssetsByScope(AssetScope.Library, null, AssetSortField.Modified, SortDirection.Descending, 0, 0);
        var ascending = await repository.GetAssetsByScope(AssetScope.Library, null, AssetSortField.Modified, SortDirection.Ascending, 0, 0);
        Assert.Equal([olderAsset.AssetId, newerAsset.AssetId], descending.Select(asset => asset.AssetId));
        Assert.Equal([newerAsset.AssetId, olderAsset.AssetId], ascending.Select(asset => asset.AssetId));
    }

    [Fact]
    public async Task Workflow_summaries_support_created_and_modified_sorting()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = await CreateRepository(connection);
        var olderWorkflow = CreateWorkflow();
        olderWorkflow.Name = "Older";
        var newerWorkflow = CreateWorkflow();
        newerWorkflow.Name = "Newer";
        await repository.UpsertWorkflow(olderWorkflow);
        await repository.UpsertWorkflow(newerWorkflow);

        await using (var dbContext = CreateContext(connection))
        {
            var olderEntity = await dbContext.Workflows.SingleAsync(workflow => workflow.WorkflowId == olderWorkflow.Id);
            var newerEntity = await dbContext.Workflows.SingleAsync(workflow => workflow.WorkflowId == newerWorkflow.Id);
            olderEntity.Created = DateTime.UtcNow.AddDays(-1);
            newerEntity.Created = DateTime.UtcNow.AddDays(-2);
            olderEntity.Modified = DateTime.UtcNow.AddDays(-2);
            newerEntity.Modified = DateTime.UtcNow.AddDays(-1);
            await dbContext.SaveChangesAsync();
        }

        var descending = await repository.GetWorkflowSummaries(null, WorkflowSortField.Modified, SortDirection.Descending, 0, 0);
        var ascending = await repository.GetWorkflowSummaries(null, WorkflowSortField.Modified, SortDirection.Ascending, 0, 0);
        Assert.Equal([newerWorkflow.Id, olderWorkflow.Id], descending.Select(workflow => workflow.Id));
        Assert.Equal([olderWorkflow.Id, newerWorkflow.Id], ascending.Select(workflow => workflow.Id));

        descending = await repository.GetWorkflowSummaries(null, WorkflowSortField.Created, SortDirection.Descending, 0, 0);
        ascending = await repository.GetWorkflowSummaries(null, WorkflowSortField.Created, SortDirection.Ascending, 0, 0);
        Assert.Equal([olderWorkflow.Id, newerWorkflow.Id], descending.Select(workflow => workflow.Id));
        Assert.Equal([newerWorkflow.Id, olderWorkflow.Id], ascending.Select(workflow => workflow.Id));
    }

    [Fact]
    public async Task Connector_model_and_evaluation_summaries_support_timestamp_sorting()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var repository = await CreateRepository(connection);

        var firstConnector = CreateConnector();
        firstConnector.Name = "First connector";
        var secondConnector = CreateConnector();
        secondConnector.Name = "Second connector";
        var firstModel = CreateModel(firstConnector.ConnectorId);
        firstModel.Name = "First model";
        var secondModel = CreateModel(secondConnector.ConnectorId);
        secondModel.Name = "Second model";
        var firstEvaluation = CreateEvalConfig(null);
        firstEvaluation.Name = "First evaluation";
        var secondEvaluation = CreateEvalConfig(null);
        secondEvaluation.Name = "Second evaluation";

        await repository.UpsertConnector(firstConnector, hideSecrets: false);
        await repository.UpsertConnector(secondConnector, hideSecrets: false);
        await repository.UpsertModel(firstModel);
        await repository.UpsertModel(secondModel);
        await repository.UpsertEvalConfig(firstEvaluation);
        await repository.UpsertEvalConfig(secondEvaluation);

        var firstCreated = DateTime.UtcNow.AddDays(-1);
        var secondCreated = DateTime.UtcNow.AddDays(-2);
        var firstModified = DateTime.UtcNow.AddDays(-2);
        var secondModified = DateTime.UtcNow.AddDays(-1);

        await using (var dbContext = CreateContext(connection))
        {
            var connectorMetadata = await dbContext.ConnectorMetadata.ToDictionaryAsync(item => item.ConnectorId);
            SetTimestamps(connectorMetadata[firstConnector.ConnectorId], firstCreated, firstModified);
            SetTimestamps(connectorMetadata[secondConnector.ConnectorId], secondCreated, secondModified);

            var modelMetadata = await dbContext.ModelMetadata.ToDictionaryAsync(item => item.ModelId);
            SetTimestamps(modelMetadata[firstModel.ModelId], firstCreated, firstModified);
            SetTimestamps(modelMetadata[secondModel.ModelId], secondCreated, secondModified);

            var evaluations = await dbContext.EvalConfigs.ToDictionaryAsync(item => item.EvalConfigId);
            SetTimestamps(evaluations[firstEvaluation.EvalConfigId], firstCreated, firstModified);
            SetTimestamps(evaluations[secondEvaluation.EvalConfigId], secondCreated, secondModified);
            await dbContext.SaveChangesAsync();
        }

        var connectorsByCreated = await repository.GetConnectorSummaries(null, ConnectorSortField.Created, SortDirection.Descending, 0, 0);
        var connectorsByModified = await repository.GetConnectorSummaries(null, ConnectorSortField.Modified, SortDirection.Descending, 0, 0);
        Assert.Equal([firstConnector.ConnectorId, secondConnector.ConnectorId], connectorsByCreated.Select(item => item.ConnectorId));
        Assert.Equal([secondConnector.ConnectorId, firstConnector.ConnectorId], connectorsByModified.Select(item => item.ConnectorId));

        var modelsByCreated = await repository.GetModelSummaries(null, ModelSortField.Created, SortDirection.Descending, 0, 0);
        var modelsByModified = await repository.GetModelSummaries(null, ModelSortField.Modified, SortDirection.Descending, 0, 0);
        Assert.Equal([firstModel.ModelId, secondModel.ModelId], modelsByCreated.Select(item => item.ModelId));
        Assert.Equal([secondModel.ModelId, firstModel.ModelId], modelsByModified.Select(item => item.ModelId));

        var evaluationsByCreated = await repository.GetEvalConfigSummaries(null, EvalConfigSortField.Created, SortDirection.Descending, 0, 0);
        var evaluationsByModified = await repository.GetEvalConfigSummaries(null, EvalConfigSortField.Modified, SortDirection.Descending, 0, 0);
        Assert.Equal([firstEvaluation.EvalConfigId, secondEvaluation.EvalConfigId], evaluationsByCreated.Select(item => item.EvalConfigId));
        Assert.Equal([secondEvaluation.EvalConfigId, firstEvaluation.EvalConfigId], evaluationsByModified.Select(item => item.EvalConfigId));
    }

    private static async Task<RepositoryService> CreateRepository(SqliteConnection connection)
    {
        var options = CreateOptions(connection);
        await using var dbContext = new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions()));
        await dbContext.Database.EnsureCreatedAsync();
        return new RepositoryService(new TestDbContextFactory(options));
    }

    private static SharpOMaticDbContext CreateContext(SqliteConnection connection)
    {
        return new SharpOMaticDbContext(CreateOptions(connection), Options.Create(new SharpOMaticDbOptions()));
    }

    private static DbContextOptions<SharpOMaticDbContext> CreateOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;

    private static void AssertCreated(DateTime? created, DateTime? modified, DateTime before, DateTime after)
    {
        Assert.NotNull(created);
        Assert.Equal(created, modified);
        Assert.InRange(created.Value, before, after);
    }

    private static void AssertUpdated(DateTime? originalCreated, DateTime? originalModified, DateTime? updatedCreated, DateTime? updatedModified)
    {
        Assert.Equal(originalCreated, updatedCreated);
        Assert.NotNull(originalModified);
        Assert.NotNull(updatedModified);
        Assert.True(updatedModified > originalModified);
    }

    private static void SetTimestamps(ConnectorMetadata entity, DateTime created, DateTime modified)
    {
        entity.Created = created;
        entity.Modified = modified;
    }

    private static void SetTimestamps(ModelMetadata entity, DateTime created, DateTime modified)
    {
        entity.Created = created;
        entity.Modified = modified;
    }

    private static void SetTimestamps(EvalConfig entity, DateTime created, DateTime modified)
    {
        entity.Created = created;
        entity.Modified = modified;
    }

    private static WorkflowEntity CreateWorkflow() =>
        new()
        {
            Id = Guid.NewGuid(),
            Version = 2,
            Name = "Workflow",
            Description = "",
            Nodes = [],
            Connections = [],
        };

    private static Connector CreateConnector() =>
        new()
        {
            ConnectorId = Guid.NewGuid(),
            Version = 1,
            Name = "Connector",
            Description = "",
            ConfigId = "test-connector",
            AuthenticationModeId = "none",
            FieldValues = [],
        };

    private static Model CreateModel(Guid connectorId) =>
        new()
        {
            ModelId = Guid.NewGuid(),
            Version = 1,
            Name = "Model",
            Description = "",
            ConfigId = "test-model",
            ConnectorId = connectorId,
            CustomCapabilities = [],
            ParameterValues = [],
        };

    private static EvalConfig CreateEvalConfig(Guid? workflowId) =>
        new()
        {
            EvalConfigId = Guid.NewGuid(),
            WorkflowId = workflowId,
            Name = "Evaluation",
            Description = "",
            MaxParallel = 1,
        };

    private static Asset CreateAsset(string name, DateTime created) =>
        new()
        {
            AssetId = Guid.NewGuid(),
            RunId = null,
            ConversationId = null,
            FolderId = null,
            Name = name,
            Scope = AssetScope.Library,
            Created = created,
            MediaType = "text/plain",
            SizeBytes = 1,
            StorageKey = $"library/{Guid.NewGuid():N}",
        };

    private sealed class TestDbContextFactory(DbContextOptions<SharpOMaticDbContext> options) : IDbContextFactory<SharpOMaticDbContext>
    {
        public SharpOMaticDbContext CreateDbContext() => new(options, Options.Create(new SharpOMaticDbOptions()));
    }
}
