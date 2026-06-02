namespace SharpOMatic.Tests.Services;

public sealed class RepositoryServiceMetadataUnitTests
{
    [Fact]
    public async Task UpsertConnectorConfigs_skips_unchanged_rows_and_updates_changed_rows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var repository = await CreateRepository(connection);
        var config = CreateConnectorConfig(version: 1, description: "Initial");

        await repository.UpsertConnectorConfigs([config]);
        var changesAfterInsert = await GetTotalChanges(connection);

        await repository.UpsertConnectorConfigs([config]);
        var changesAfterUnchangedUpsert = await GetTotalChanges(connection);

        await repository.UpsertConnectorConfigs([CreateConnectorConfig(version: 2, description: "Updated")]);
        var updated = await repository.GetConnectorConfig("test_connector");

        Assert.Equal(changesAfterInsert, changesAfterUnchangedUpsert);
        Assert.NotEqual(changesAfterUnchangedUpsert, await GetTotalChanges(connection));
        Assert.NotNull(updated);
        Assert.Equal(2, updated.Version);
        Assert.Equal("Updated", updated.Description);
    }

    [Fact]
    public async Task UpsertModelConfigs_skips_unchanged_rows_and_updates_changed_rows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var repository = await CreateRepository(connection);
        var config = CreateModelConfig(version: 1, description: "Initial");

        await repository.UpsertModelConfigs([config]);
        var changesAfterInsert = await GetTotalChanges(connection);

        await repository.UpsertModelConfigs([config]);
        var changesAfterUnchangedUpsert = await GetTotalChanges(connection);

        await repository.UpsertModelConfigs([CreateModelConfig(version: 2, description: "Updated")]);
        var updated = await repository.GetModelConfig("test_model");

        Assert.Equal(changesAfterInsert, changesAfterUnchangedUpsert);
        Assert.NotEqual(changesAfterUnchangedUpsert, await GetTotalChanges(connection));
        Assert.NotNull(updated);
        Assert.Equal(2, updated.Version);
        Assert.Equal("Updated", updated.Description);
    }

    private static async Task<RepositoryService> CreateRepository(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<SharpOMaticDbContext>().UseSqlite(connection).Options;

        await using var dbContext = new SharpOMaticDbContext(options, Options.Create(new SharpOMaticDbOptions()));
        await dbContext.Database.EnsureCreatedAsync();

        return new RepositoryService(new TestDbContextFactory(options));
    }

    private static async Task<int> GetTotalChanges(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT total_changes()";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static ConnectorConfig CreateConnectorConfig(int version, string description)
    {
        return new ConnectorConfig
        {
            Version = version,
            ConfigId = "test_connector",
            DisplayName = "Test Connector",
            Description = description,
            AuthModes =
            [
                new AuthenticationModeConfig
                {
                    Id = "api_key",
                    DisplayName = "API Key",
                    Kind = AuthenticationModeKind.ApiKey,
                    IsDefault = true,
                    Fields =
                    [
                        new FieldDescriptor
                        {
                            Name = "api_key",
                            Label = "API key",
                            Description = "",
                            CallDefined = false,
                            Type = FieldDescriptorType.Secret,
                            IsRequired = true,
                        },
                    ],
                },
            ],
        };
    }

    private static ModelConfig CreateModelConfig(int version, string description)
    {
        return new ModelConfig
        {
            Version = version,
            ConfigId = "test_model",
            DisplayName = "Test Model",
            Description = description,
            ConnectorConfigId = "test_connector",
            IsCustom = false,
            Information = null,
            Capabilities =
            [
                new ModelCapability { Name = "text", DisplayName = "Text" },
            ],
            ParameterFields =
            [
                new FieldDescriptor
                {
                    Name = "deployment",
                    Label = "Deployment",
                    Description = "",
                    CallDefined = false,
                    Type = FieldDescriptorType.String,
                    IsRequired = true,
                },
            ],
        };
    }

    private sealed class TestDbContextFactory(DbContextOptions<SharpOMaticDbContext> options) : IDbContextFactory<SharpOMaticDbContext>
    {
        public SharpOMaticDbContext CreateDbContext() => new(options, Options.Create(new SharpOMaticDbOptions()));
    }
}
