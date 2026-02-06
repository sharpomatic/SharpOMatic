namespace SharpOMatic.Engine.Services;

public class HostedNodeExecutionService(IServiceScopeFactory scopeFactory, INodeExecutionService executionService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ApplyMigrations();
        await CheckSettings();
        await LoadMetadata();

        await executionService.RunQueueAsync(stoppingToken);
    }

    private async Task ApplyMigrations()
    {
        using var scope = scopeFactory.CreateScope();
        var dbOptions = scope.ServiceProvider.GetRequiredService<IOptions<SharpOMaticDbOptions>>();

        if ((dbOptions.Value.ApplyMigrationsOnStartup is null) || dbOptions.Value.ApplyMigrationsOnStartup.Value)
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SharpOMaticDbContext>>();
            await using var dbContext = await contextFactory.CreateDbContextAsync();
            await dbContext.Database.MigrateAsync();
        }
    }

    private async Task CheckSettings()
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositoryService>();
        var versionSetting = await repository.GetSetting("Version");

        if (versionSetting is null)
        {
            await repository.UpsertSetting(
                new Setting()
                {
                    SettingId = Guid.NewGuid(),
                    Name = "Version",
                    DisplayName = "Version",
                    UserEditable = false,
                    SettingType = SettingType.Integer,
                    ValueInteger = 1,
                }
            );

            await repository.UpsertSetting(
                new Setting()
                {
                    SettingId = Guid.NewGuid(),
                    Name = "RunHistoryLimit",
                    DisplayName = "Run History Limit",
                    UserEditable = true,
                    SettingType = SettingType.Integer,
                    ValueInteger = NodeExecutionService.DEFAULT_RUN_HISTORY_LIMIT,
                }
            );

            await repository.UpsertSetting(
                new Setting()
                {
                    SettingId = Guid.NewGuid(),
                    Name = "RunNodeLimit",
                    UserEditable = true,
                    DisplayName = "Run Node Limit",
                    SettingType = SettingType.Integer,
                    ValueInteger = NodeExecutionService.DEFAULT_NODE_RUN_LIMIT,
                }
            );
        }
    }

    private async Task LoadMetadata()
    {
        await LoadMetadata<ConnectorConfig>("Metadata.Resources.ConnectorConfig", (repo, config) => repo.UpsertConnectorConfig(config));
        await LoadMetadata<ModelConfig>("Metadata.Resources.ModelConfig", (repo, config) => repo.UpsertModelConfig(config));
    }

    private async Task LoadMetadata<T>(string resourceFilter, Func<IRepositoryService, T, Task> upsertAction)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositoryService>();
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames().Where(name => name.Contains(resourceFilter) && name.EndsWith(".json"));

        foreach (var resourceName in resourceNames)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    var config = await JsonSerializer.DeserializeAsync<T>(stream);
                    if (config != null)
                        await upsertAction(repository, config);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load metadata from {resourceName}: {ex.Message}");
            }
        }
    }
}
