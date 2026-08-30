namespace SharpOMatic.Tests.Services;

public sealed class SqliteMigrationUnitTests
{
    [Fact]
    public async Task Migrations_create_current_schema_from_empty_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>()
            .UseSqlite(connection, sqliteOptions => sqliteOptions.MigrationsAssembly(typeof(SqliteSharpOMaticBuilderExtensions).Assembly.FullName))
            .Options;

        await using var dbContext = new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions()));
        await dbContext.Database.MigrateAsync();

        Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());

        var tableNames = await GetNames(connection, "SELECT name FROM sqlite_master WHERE type = 'table'");
        Assert.Contains("ModelCallMetrics", tableNames);
        Assert.Contains("WorkflowRunMetrics", tableNames);
        Assert.Contains("WorkflowFolders", tableNames);

        var conversationColumns = await GetNames(connection, "SELECT name FROM pragma_table_info('Conversations')");
        Assert.DoesNotContain("LeaseExpires", conversationColumns);
        Assert.DoesNotContain("LeaseOwner", conversationColumns);
        Assert.Contains("ModelCallCount", conversationColumns);
        Assert.Contains("TotalModelCost", conversationColumns);

        var runColumns = await GetNames(connection, "SELECT name FROM pragma_table_info('Runs')");
        Assert.Contains("ModelCallCount", runColumns);
        Assert.Contains("TotalModelCost", runColumns);

        var modelCallMetricIndexes = await GetNames(connection, "SELECT name FROM pragma_index_list('ModelCallMetrics')");
        Assert.Contains("IX_ModelCallMetrics_RunId", modelCallMetricIndexes);
    }

    private static async Task<List<string>> GetNames(SqliteConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));

        return names;
    }
}
