namespace SharpOMatic.Engine.Contexts;

public class ProcessContext : ExecutionContext
{
    private readonly ConcurrentDictionary<ExecutionContext, byte> _activeContexts = new();
    private readonly ConcurrentDictionary<int, ThreadContext> _threads = new();
    private TaskCompletionSource<Run>? _completionSource;

    private int _threadId = 1;
    private int _threadCount = 1;
    private int _nodesRun = 0;
    private int _runNodeLimit = 0;

    public IServiceScope ServiceScope { get; }
    public IRepositoryService RepositoryService { get; }
    public IAssetStore AssetStore { get; }
    public IEnumerable<IProgressService> ProgressServices { get; }
    public IToolMethodRegistry ToolMethodRegistry { get; }
    public ISchemaTypeRegistry SchemaTypeRegistry { get; }
    public IScriptOptionsService ScriptOptionsService { get; }
    public IEnumerable<JsonConverter> JsonConverters { get; }
    public Run Run { get; }
    public TaskCompletionSource<Run>? CompletionSource { get; }
    public int ActiveThreadCount => _threadCount;
    public int RunNodeLimit => _runNodeLimit;
    public int NodesRun => Volatile.Read(ref _nodesRun);
    public IReadOnlyCollection<ExecutionContext> ActiveContexts => _activeContexts.Keys.ToList();

    public ProcessContext(IServiceScope serviceScope, Run run, int runNodeLimit, TaskCompletionSource<Run>? completionSource)
        : base(null)
    {
        ServiceScope = serviceScope;
        Run = run;
        RepositoryService = serviceScope.ServiceProvider.GetRequiredService<IRepositoryService>();
        AssetStore = serviceScope.ServiceProvider.GetRequiredService<IAssetStore>();
        ProgressServices = serviceScope.ServiceProvider.GetRequiredService<IEnumerable<IProgressService>>();
        ToolMethodRegistry = serviceScope.ServiceProvider.GetRequiredService<IToolMethodRegistry>();
        SchemaTypeRegistry = serviceScope.ServiceProvider.GetRequiredService<ISchemaTypeRegistry>();
        ScriptOptionsService = serviceScope.ServiceProvider.GetRequiredService<IScriptOptionsService>();
        JsonConverters = serviceScope.ServiceProvider.GetRequiredService<IJsonConverterService>().GetConverters();
        _runNodeLimit = runNodeLimit;
        CompletionSource = completionSource;
        _completionSource = completionSource;

        TrackContext(this);
    }

    public ThreadContext CreateThread(ContextObject nodeContext, ExecutionContext currentContext)
    {
        var threadContext = new ThreadContext(this, currentContext, nodeContext);
        _threads.TryAdd(threadContext.ThreadId, threadContext);
        var gosubContext = GosubContext.Find(currentContext);
        gosubContext?.IncrementThreads();
        return threadContext;
    }

    public void RemoveThread(ThreadContext threadContext)
    {
        _threads.TryRemove(threadContext.ThreadId, out _);
    }

    public int UpdateThreadCount(int delta)
    {
        return Interlocked.Add(ref _threadCount, delta);
    }

    public int GetNextThreadId()
    {
        return Interlocked.Increment(ref _threadId);
    }

    public bool TryIncrementNodesRun(out int newCount)
    {
        if (_runNodeLimit <= 0)
        {
            newCount = Interlocked.Increment(ref _nodesRun);
            return true;
        }

        while (true)
        {
            var current = Volatile.Read(ref _nodesRun);
            if (current >= _runNodeLimit)
            {
                newCount = current;
                return false;
            }

            var next = current + 1;
            if (Interlocked.CompareExchange(ref _nodesRun, next, current) == current)
            {
                newCount = next;
                return true;
            }
        }
    }

    public async Task RunUpdated()
    {
        await RepositoryService.UpsertRun(Run);
        foreach (var progressService in ProgressServices)
            await progressService.RunProgress(Run);
        if (Run.RunStatus is RunStatus.Success or RunStatus.Failed)
            CompleteRun();
    }

    public void MergeContexts(ContextObject target, ContextObject source)
    {
        foreach (var key in source.Keys)
        {
            if (!target.TryGetValue(key, out var targetValue))
            {
                target[key] = source[key];
            }
            else
            {
                var sourceValue = source[key];

                if (targetValue is ContextList targetList1 && sourceValue is ContextList sourceList)
                {
                    targetList1.AddRange(sourceList);
                }
                else if (targetValue is ContextList targetList2 && sourceValue is not ContextList)
                {
                    targetList2.Add(sourceValue);
                }
                else if (targetValue is not ContextList && sourceValue is ContextList targetList3)
                {
                    var newList = new ContextList { targetValue };

                    newList.AddRange(targetList3);
                    target[key] = newList;
                }
                else
                {
                    var newList = new ContextList { targetValue, sourceValue };
                    target[key] = newList;
                }
            }
        }
    }

    internal void TrackContext(ExecutionContext context)
    {
        _activeContexts.TryAdd(context, 0);
    }

    internal void UntrackContext(ExecutionContext context)
    {
        _activeContexts.TryRemove(context, out _);
    }

    private void CompleteRun()
    {
        var completionSource = Interlocked.Exchange(ref _completionSource, null);
        completionSource?.TrySetResult(Run);
    }
}
