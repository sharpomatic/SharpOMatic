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
        await EnsureSetting(repository, "Version", "Version", false, SettingType.Integer, 1);
        await EnsureSetting(repository, "RunHistoryLimit", "Run History Limit", true, SettingType.Integer, NodeExecutionService.DEFAULT_RUN_HISTORY_LIMIT);
        await EnsureSetting(repository, "ConversationHistoryLimit", "Conversation History Limit", true, SettingType.Integer, NodeExecutionService.DEFAULT_RUN_HISTORY_LIMIT);
        await EnsureSetting(repository, "RunNodeLimit", "Run Node Limit", true, SettingType.Integer, NodeExecutionService.DEFAULT_NODE_RUN_LIMIT);
    }

    private static async Task EnsureSetting(IRepositoryService repository, string name, string displayName, bool userEditable, SettingType settingType, int valueInteger)
    {
        var existing = await repository.GetSetting(name);
        if (existing is not null)
            return;

        await repository.UpsertSetting(
            new Setting()
            {
                SettingId = Guid.NewGuid(),
                Name = name,
                DisplayName = displayName,
                UserEditable = userEditable,
                SettingType = settingType,
                ValueInteger = valueInteger,
            }
        );
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
