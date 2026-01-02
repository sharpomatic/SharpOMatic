namespace SharpOMatic.Engine.Services;

public class SamplesService(IRepositoryService repositoryService) : ISamplesService
{
    private const string WorkflowResourceFilter = "Samples.Workflows";

    private static readonly JsonSerializerOptions _workflowOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new NodeEntityConverter() }
    };

    private static readonly Lazy<Task<SampleCatalog>> _catalog = new(BuildCatalogAsync);

    public async Task<IReadOnlyList<string>> GetWorkflowNames()
    {
        var catalog = await _catalog.Value;
        return catalog.Names;
    }

    public async Task<Guid> CreateWorkflow(string sampleName)
    {
        if (string.IsNullOrWhiteSpace(sampleName))
            throw new SharpOMaticException("Sample name cannot be empty or whitespace.");

        var catalog = await _catalog.Value;
        if (!catalog.ResourceMap.TryGetValue(sampleName, out var resourceName))
            throw new SharpOMaticException($"Sample '{sampleName}' cannot be found.");

        var assembly = Assembly.GetExecutingAssembly();
        var workflow = await LoadWorkflowSample(assembly, resourceName);
        workflow.Id = Guid.NewGuid();

        await repositoryService.UpsertWorkflow(workflow);
        return workflow.Id;
    }

    private static IEnumerable<string> GetWorkflowResourceNames(Assembly assembly)
    {
        return assembly.GetManifestResourceNames()
            .Where(name => name.Contains(WorkflowResourceFilter, StringComparison.OrdinalIgnoreCase) && 
                           name.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<SampleCatalog> BuildCatalogAsync()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = GetWorkflowResourceNames(assembly).ToList();
        var names = new List<string>();
        var resourceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resourceName in resourceNames)
        {
            string? displayName = null;

            try
            {
                displayName = await TryReadWorkflowName(assembly, resourceName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read workflow sample '{resourceName}': {ex.Message}");
            }

            var fileName = GetResourceFileName(resourceName);
            var name = displayName ?? fileName ?? resourceName;
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);

            TryAddAlias(resourceMap, resourceName, resourceName);
            TryAddAlias(resourceMap, fileName, resourceName);
            TryAddAlias(resourceMap, displayName, resourceName);
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return new SampleCatalog(names.AsReadOnly(), new ReadOnlyDictionary<string, string>(resourceMap));
    }

    private static async Task<string?> TryReadWorkflowName(Assembly assembly, string resourceName)
    {
        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var document = await JsonDocument.ParseAsync(stream);
        if (document.RootElement.TryGetProperty("name", out var nameElement))
            return nameElement.GetString();

        if (document.RootElement.TryGetProperty("Name", out var namePascal))
            return namePascal.GetString();

        return null;
    }

    private static async Task<WorkflowEntity> LoadWorkflowSample(Assembly assembly, string resourceName)
    {
        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new SharpOMaticException($"Sample '{resourceName}' could not be loaded.");

        var workflow = await JsonSerializer.DeserializeAsync<WorkflowEntity>(stream, _workflowOptions);
        if (workflow is null)
            throw new SharpOMaticException($"Sample '{resourceName}' could not be parsed.");

        return workflow;
    }

    private static string? GetResourceFileName(string resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
            return null;

        var lastDot = resourceName.LastIndexOf('.');
        if (lastDot <= 0)
            return resourceName;

        var withoutExtension = resourceName[..lastDot];
        var lastSeparator = withoutExtension.LastIndexOf('.');
        
        return lastSeparator >= 0 ? withoutExtension.Substring(lastSeparator + 1) : withoutExtension;
    }

    private static void TryAddAlias(IDictionary<string, string> resourceMap, string? alias, string resourceName)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return;

        if (!resourceMap.ContainsKey(alias))
            resourceMap[alias] = resourceName;
    }

    private record SampleCatalog(IReadOnlyList<string> Names, IReadOnlyDictionary<string, string> ResourceMap);
}
