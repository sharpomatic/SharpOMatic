namespace SharpOMatic.Tests.Services;

public sealed class WorkflowFolderUnitTests
{
    [Fact]
    public async Task Existing_workflows_load_as_top_level()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        await using (var context = CreateContext(options))
        {
            context.Database.EnsureCreated();
            context.Workflows.Add(CreateWorkflow("Top Level", null));
            await context.SaveChangesAsync();
        }

        var repository = new RepositoryService(new TestDbContextFactory(options));
        var workflows = await repository.GetWorkflowSummaries(null, WorkflowSortField.Name, SortDirection.Ascending, 0, 0, topLevelOnly: true);

        Assert.Single(workflows);
        Assert.Equal("Top Level", workflows[0].Name);
        Assert.Null(workflows[0].WorkflowFolderId);
        Assert.Null(workflows[0].WorkflowFolderName);
    }

    [Fact]
    public async Task Folder_crud_prevents_duplicate_names_and_non_empty_delete()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        await using (var context = CreateContext(options))
            context.Database.EnsureCreated();

        var repository = new RepositoryService(new TestDbContextFactory(options));
        var folder = new WorkflowFolder
        {
            WorkflowFolderId = Guid.NewGuid(),
            Name = "Customers",
            Created = DateTime.UtcNow,
        };
        await repository.UpsertWorkflowFolder(folder);

        await Assert.ThrowsAsync<SharpOMaticException>(() =>
            repository.UpsertWorkflowFolder(
                new WorkflowFolder
                {
                    WorkflowFolderId = Guid.NewGuid(),
                    Name = "Customers",
                    Created = DateTime.UtcNow,
                }
            )
        );

        await repository.UpsertWorkflow(CreateWorkflowEntity("Intake", folder.WorkflowFolderId));
        await Assert.ThrowsAsync<SharpOMaticException>(() => repository.DeleteWorkflowFolder(folder.WorkflowFolderId));
    }

    [Fact]
    public async Task Workflow_names_are_unique_per_folder()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;
        await using (var context = CreateContext(options))
            context.Database.EnsureCreated();

        var repository = new RepositoryService(new TestDbContextFactory(options));
        var folderA = new WorkflowFolder
        {
            WorkflowFolderId = Guid.NewGuid(),
            Name = "A",
            Created = DateTime.UtcNow,
        };
        var folderB = new WorkflowFolder
        {
            WorkflowFolderId = Guid.NewGuid(),
            Name = "B",
            Created = DateTime.UtcNow,
        };
        await repository.UpsertWorkflowFolder(folderA);
        await repository.UpsertWorkflowFolder(folderB);

        await repository.UpsertWorkflow(CreateWorkflowEntity("Build", null));
        await repository.UpsertWorkflow(CreateWorkflowEntity("Build", folderA.WorkflowFolderId));
        await repository.UpsertWorkflow(CreateWorkflowEntity("Build", folderB.WorkflowFolderId));

        await Assert.ThrowsAsync<SharpOMaticException>(() => repository.UpsertWorkflow(CreateWorkflowEntity("Build", null)));
        await Assert.ThrowsAsync<SharpOMaticException>(() => repository.UpsertWorkflow(CreateWorkflowEntity("Build", folderA.WorkflowFolderId)));
    }

    [Fact]
    public async Task Engine_lookup_resolves_top_level_and_slash_qualified_names()
    {
        var repository = new TestRepositoryService();
        var folder = new WorkflowFolder
        {
            WorkflowFolderId = Guid.NewGuid(),
            Name = "Support",
            Created = DateTime.UtcNow,
        };
        await repository.UpsertWorkflowFolder(folder);
        var topLevel = CreateWorkflowEntity("Chat", null);
        var foldered = CreateWorkflowEntity("Chat", folder.WorkflowFolderId);
        await repository.UpsertWorkflow(topLevel);
        await repository.UpsertWorkflow(foldered);

        var engine = new EngineService(Mock.Of<IServiceScopeFactory>(), Mock.Of<INodeQueueService>(), repository, Mock.Of<IScriptOptionsService>(), Mock.Of<IJsonConverterService>());

        Assert.Equal(topLevel.Id, await engine.GetWorkflowId("Chat"));
        Assert.Equal(foldered.Id, await engine.GetWorkflowId("Support/Chat"));
        await Assert.ThrowsAsync<SharpOMaticException>(() => engine.GetWorkflowId("Support/Nested/Chat"));
    }

    private static SharpOMaticDbContext CreateContext(DbContextOptions<SharpOMaticDbContext> options)
    {
        return new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions()));
    }

    private static Workflow CreateWorkflow(string name, Guid? folderId)
    {
        return new Workflow
        {
            WorkflowId = Guid.NewGuid(),
            Version = 1,
            WorkflowFolderId = folderId,
            Named = name,
            Description = "",
            IsConversationEnabled = false,
            Nodes = "[]",
            Connections = "[]",
        };
    }

    private static WorkflowEntity CreateWorkflowEntity(string name, Guid? folderId)
    {
        return new WorkflowEntity
        {
            Id = Guid.NewGuid(),
            Version = 1,
            WorkflowFolderId = folderId,
            Name = name,
            Description = "",
            IsConversationEnabled = false,
            Nodes = [],
            Connections = [],
        };
    }

    private sealed class TestDbContextFactory(DbContextOptions<SharpOMaticDbContext> options) : IDbContextFactory<SharpOMaticDbContext>
    {
        public SharpOMaticDbContext CreateDbContext() => CreateContext(options);
    }
}
