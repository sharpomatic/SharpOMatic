namespace SharpOMatic.Engine.Helpers;

public class TemplateHelper(
    ContextObject context,
    IRepositoryService repositoryService,
    IAssetStore assetStore,
    Guid? runId,
    string? conversationId = null
)
{
    private readonly ContextObject _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly IRepositoryService _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
    private readonly IAssetStore _assetStore = assetStore ?? throw new ArgumentNullException(nameof(assetStore));
    private readonly Guid? _runId = runId;
    private readonly string? _conversationId = conversationId;

    public Task<string> ExpandAsync(string template)
    {
        return ContextHelpers.SubstituteValuesAsync(template, _context, _repositoryService, _assetStore, _runId, _conversationId);
    }
}
