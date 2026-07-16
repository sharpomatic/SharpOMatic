namespace SharpOMatic.Tests.Workflows;

public sealed class WorkflowSchemaUpgraderUnitTests
{
    [Fact]
    public void Version1_model_call_is_upgraded_to_single_version2_model()
    {
        var modelId = Guid.NewGuid();
        var workflow = CreateVersion1Workflow(modelId, new Dictionary<string, string?> { ["max_output_tokens"] = "1234" });

        var upgraded = WorkflowSchemaUpgrader.Upgrade(workflow);
        var node = Assert.IsType<ModelCallNodeEntity>(Assert.Single(upgraded.Nodes));

        Assert.Equal(WorkflowSchema.CurrentVersion, upgraded.Version);
        var model = Assert.Single(node.Models);
        Assert.Equal(modelId, model.ModelId);
        Assert.False(model.Disabled);
        Assert.Equal("1234", model.ParameterValues["max_output_tokens"]);
    }

    [Fact]
    public void Version1_model_call_without_model_is_upgraded_to_empty_models()
    {
        var workflow = CreateVersion1Workflow(null, new Dictionary<string, string?> { ["unused"] = "value" });

        var upgraded = WorkflowSchemaUpgrader.Upgrade(workflow);
        var node = Assert.IsType<ModelCallNodeEntity>(Assert.Single(upgraded.Nodes));

        Assert.Empty(node.Models);
    }

    [Fact]
    public void Upgrade_is_idempotent_and_serializes_only_version2_model_fields()
    {
        var workflow = CreateVersion1Workflow(Guid.NewGuid(), new Dictionary<string, string?> { ["temperature"] = "0.5" });

        WorkflowSchemaUpgrader.Upgrade(workflow);
        WorkflowSchemaUpgrader.Upgrade(workflow);
        var json = WorkflowSnapshotSerializer.SerializeWorkflow(workflow);
        using var document = JsonDocument.Parse(json);
        var serializedNode = document.RootElement.GetProperty("nodes")[0];

        Assert.Equal(WorkflowSchema.CurrentVersion, workflow.Version);
        Assert.Single(Assert.IsType<ModelCallNodeEntity>(Assert.Single(workflow.Nodes)).Models);
        Assert.True(serializedNode.TryGetProperty("models", out _));
        Assert.False(serializedNode.GetProperty("models")[0].TryGetProperty("disabled", out _));
        Assert.False(serializedNode.TryGetProperty("modelId", out _));
        Assert.False(serializedNode.TryGetProperty("parameterValues", out _));
    }

    [Fact]
    public void Disabled_model_round_trips_while_enabled_default_is_omitted()
    {
        var workflow = CreateVersion1Workflow(Guid.NewGuid(), []);
        WorkflowSchemaUpgrader.Upgrade(workflow);
        var node = Assert.IsType<ModelCallNodeEntity>(Assert.Single(workflow.Nodes));
        node.Models[0].Disabled = true;

        var json = WorkflowSnapshotSerializer.SerializeWorkflow(workflow);
        using var document = JsonDocument.Parse(json);
        var serializedModel = document.RootElement.GetProperty("nodes")[0].GetProperty("models")[0];
        var roundTripped = WorkflowSnapshotSerializer.DeserializeWorkflow(json);
        var roundTrippedModel = Assert.Single(Assert.IsType<ModelCallNodeEntity>(Assert.Single(roundTripped.Nodes)).Models);

        Assert.True(serializedModel.GetProperty("disabled").GetBoolean());
        Assert.True(roundTrippedModel.Disabled);
    }

    [Fact]
    public void Version2_workflow_with_legacy_fields_is_rejected()
    {
        var workflow = CreateVersion1Workflow(Guid.NewGuid(), []);
        workflow.Version = WorkflowSchema.CurrentVersion;

        var exception = Assert.Throws<SharpOMaticException>(() => WorkflowSchemaUpgrader.Upgrade(workflow));

        Assert.Contains("version 1 model fields", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Newer_workflow_version_is_rejected()
    {
        var workflow = CreateVersion1Workflow(null, []);
        workflow.Version = WorkflowSchema.CurrentVersion + 1;

        Assert.Throws<SharpOMaticException>(() => WorkflowSchemaUpgrader.Upgrade(workflow));
    }

    [Fact]
    public void Snapshot_deserializer_upgrades_released_version1_shape()
    {
        var workflowId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var modelId = Guid.NewGuid();
        var json = $$"""
        {
          "version": 1,
          "id": "{{workflowId}}",
          "name": "Legacy",
          "description": "",
          "isConversationEnabled": false,
          "nodes": [
            {
              "version": 1,
              "id": "{{nodeId}}",
              "nodeType": 7,
              "title": "Model",
              "top": 10,
              "left": 20,
              "width": 80,
              "height": 80,
              "inputs": [],
              "outputs": [],
              "modelId": "{{modelId}}",
              "batchOutput": false,
              "dropToolCalls": false,
              "disableStreamUser": false,
              "disableStreamTool": false,
              "disableStreamReasoning": false,
              "disableStreamAssistantText": false,
              "toolAgUiOutputModes": {},
              "instructions": "System",
              "prompt": "User",
              "chatInputPath": "input.chat",
              "chatOutputPath": "output.chat",
              "textOutputPath": "output.text",
              "imageInputPath": "",
              "imageOutputPath": "output.image",
              "parameterValues": { "max_output_tokens": "2048" }
            }
          ],
          "connections": []
        }
        """;

        var workflow = WorkflowSnapshotSerializer.DeserializeWorkflow(json);
        var node = Assert.IsType<ModelCallNodeEntity>(Assert.Single(workflow.Nodes));

        Assert.Equal(WorkflowSchema.CurrentVersion, workflow.Version);
        Assert.Equal(nodeId, node.Id);
        Assert.Equal("System", node.Instructions);
        Assert.Equal("input.chat", node.ChatInputPath);
        var model = Assert.Single(node.Models);
        Assert.Equal(modelId, model.ModelId);
        Assert.False(model.Disabled);
        Assert.Equal("2048", model.ParameterValues["max_output_tokens"]);
    }

    [Fact]
    public async Task Repository_read_upgrades_without_writing_and_next_save_persists_version2_atomically()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        await using (var setupContext = new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions())))
        {
            await setupContext.Database.EnsureCreatedAsync();
            var legacy = CreateVersion1Workflow(Guid.NewGuid(), new Dictionary<string, string?> { ["temperature"] = "0.4" });
            var serializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new NodeEntityConverter() } };
            setupContext.Workflows.Add(
                new Workflow
                {
                    Version = 1,
                    WorkflowId = legacy.Id,
                    Named = legacy.Name,
                    Description = legacy.Description,
                    IsConversationEnabled = false,
                    Nodes = JsonSerializer.Serialize(legacy.Nodes, serializerOptions),
                    Connections = "[]",
                }
            );
            await setupContext.SaveChangesAsync();
        }

        var repository = new RepositoryService(new TestDbContextFactory(options));
        var upgraded = await repository.GetWorkflow((await repository.GetWorkflowSummaries()).Single().Id);
        Assert.Equal(WorkflowSchema.CurrentVersion, upgraded.Version);

        await using (var readCheck = new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions())))
            Assert.Equal(1, await readCheck.Workflows.Select(item => item.Version).SingleAsync());

        await repository.UpsertWorkflow(upgraded);

        await using (var saveCheck = new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions())))
        {
            var stored = await saveCheck.Workflows.SingleAsync();
            Assert.Equal(WorkflowSchema.CurrentVersion, stored.Version);
            using var document = JsonDocument.Parse(stored.Nodes);
            var storedNode = document.RootElement[0];
            Assert.True(storedNode.TryGetProperty("models", out _));
            Assert.False(storedNode.TryGetProperty("modelId", out _));
            Assert.False(storedNode.TryGetProperty("parameterValues", out _));
        }
    }

    private static WorkflowEntity CreateVersion1Workflow(Guid? modelId, Dictionary<string, string?> parameters)
    {
#pragma warning disable CS0618
        return new WorkflowEntity
        {
            Version = 1,
            Id = Guid.NewGuid(),
            Name = "Legacy",
            Description = string.Empty,
            IsConversationEnabled = false,
            Nodes =
            [
                new ModelCallNodeEntity
                {
                    Version = 1,
                    Id = Guid.NewGuid(),
                    NodeType = NodeType.ModelCall,
                    Title = "Model",
                    Top = 0,
                    Left = 0,
                    Width = 80,
                    Height = 80,
                    Inputs = [],
                    Outputs = [],
                    ModelId = modelId,
                    ParameterValues = parameters,
                    BatchOutput = false,
                    DropToolCalls = false,
                    DisableStreamUser = false,
                    DisableStreamTool = false,
                    DisableStreamReasoning = false,
                    DisableStreamAssistantText = false,
                    Instructions = string.Empty,
                    Prompt = string.Empty,
                    ChatInputPath = string.Empty,
                    ChatOutputPath = string.Empty,
                    TextOutputPath = "output.text",
                    ImageInputPath = string.Empty,
                    ImageOutputPath = "output.image",
                },
            ],
            Connections = [],
        };
#pragma warning restore CS0618
    }

    private sealed class TestDbContextFactory(DbContextOptions<SharpOMaticDbContext> options) : IDbContextFactory<SharpOMaticDbContext>
    {
        public SharpOMaticDbContext CreateDbContext() => new(options, Options.Create(new SharpOMaticDbOptions()));
    }
}
