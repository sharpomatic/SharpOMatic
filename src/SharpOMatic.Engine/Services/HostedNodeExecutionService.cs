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
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositoryService>();

        var connectorConfigs = await LoadMetadata<ConnectorConfig>("Metadata.Resources.ConnectorConfig");
        var modelConfigs = await LoadMetadata<ModelConfig>("Metadata.Resources.ModelConfig");

        await repository.UpsertConnectorConfigs(connectorConfigs);
        await repository.UpsertModelConfigs(modelConfigs);
    }

    private static async Task<List<T>> LoadMetadata<T>(string resourceFilter)
    {
        var configs = new List<T>();
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
                        configs.Add(config);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load metadata from {resourceName}: {ex.Message}");
            }
        }

        return configs;
    }
}
