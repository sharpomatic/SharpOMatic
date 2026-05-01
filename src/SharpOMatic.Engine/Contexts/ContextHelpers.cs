namespace SharpOMatic.Engine.Contexts;

public static class ContextHelpers
{
    private const int MaxTemplateExpansionDepth = 10;
    private static readonly System.Text.RegularExpressions.Regex ContextTemplateRegex = new(@"\{\{\s*(?:\$\s*)?(.*?)\s*\}\}", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static readonly System.Text.RegularExpressions.Regex AssetTemplateRegex = new(@"<<\s*(.*?)\s*>>", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions ContextJsonOptions = new JsonSerializerOptions().BuildOptions();
    private static readonly JsonSerializerOptions AssetRefJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<object?> ResolveContextEntryValue(
        IServiceProvider serviceProvider,
        ContextObject context,
        ContextEntryEntity entry,
        IScriptOptionsService scriptOptionsService,
        Guid runId,
        string? conversationId = null
    )
    {
        object? entryValue = entry.EntryValue;

        // Type check some entry types
        switch (entry.EntryType)
        {
            case ContextEntryType.Bool:
                if (!bool.TryParse(entry.EntryValue, out var boolValue))
                    throw new SharpOMaticException($"Input entry '{entry.InputPath}' value could not be parsed as boolean.");

                entryValue = boolValue;
                break;

            case ContextEntryType.Int:
                if (!int.TryParse(entry.EntryValue, out var intValue))
                    throw new SharpOMaticException($"Input entry '{entry.InputPath}' value could not be parsed as an int.");

                entryValue = intValue;
                break;

            case ContextEntryType.Double:
                if (!double.TryParse(entry.EntryValue, out var doubleValue))
                    throw new SharpOMaticException($"Input entry '{entry.InputPath}' value could not be parsed as a double.");

                entryValue = doubleValue;
                break;

            case ContextEntryType.String:
                // No parsing needed
                break;

            case ContextEntryType.JSON:
                try
                {
                    entryValue = FastDeserializeString(entry.EntryValue);
                }
                catch
                {
                    throw new SharpOMaticException($"Input entry '{entry.InputPath}' value could not be parsed as json.");
                }
                break;

            case ContextEntryType.Expression:
                if (!string.IsNullOrWhiteSpace(entry.EntryValue))
                {
                    var options = scriptOptionsService.GetScriptOptions();
                    var repositoryService = serviceProvider.GetRequiredService<IRepositoryService>();
                    var assetStore = serviceProvider.GetRequiredService<IAssetStore>();
                    var globals = new ScriptCodeContext()
                    {
                        Context = context,
                        ServiceProvider = serviceProvider,
                        Assets = new AssetHelper(repositoryService, assetStore, runId, conversationId),
                        Templates = new TemplateHelper(context, repositoryService, assetStore, runId, conversationId),
                    };

                    try
                    {
                        entryValue = await CSharpScript.EvaluateAsync(entry.EntryValue, options, globals, typeof(ScriptCodeContext));
                    }
                    catch (CompilationErrorException e1)
                    {
                        // Return the first 3 errors only
                        StringBuilder sb = new();
                        sb.AppendLine($"Input entry '{entry.InputPath}' expression failed compilation.\n");
                        foreach (var diagnostic in e1.Diagnostics.Take(3))
                            sb.AppendLine(diagnostic.ToString());

                        throw new SharpOMaticException(sb.ToString());
                    }
                    catch (InvalidOperationException e2)
                    {
                        StringBuilder sb = new();
                        sb.AppendLine($"Input entry '{entry.InputPath}' expression failed during execution.\n");
                        sb.Append(e2.Message);
                        throw new SharpOMaticException(sb.ToString());
                    }
                    catch (Exception e3)
                    {
                        StringBuilder sb = new();
                        sb.AppendLine($"Input entry '{entry.InputPath}' expression failed during execution.\n");
                        sb.Append(e3.Message);
                        throw new SharpOMaticException(sb.ToString());
                    }
                }
                break;
            case ContextEntryType.AssetRef:
                entryValue = ParseAssetRef(entry.EntryValue, entry.InputPath);
                break;
            case ContextEntryType.AssetRefList:
                entryValue = ParseAssetRefList(entry.EntryValue, entry.InputPath);
                break;
        }

        return entryValue;
    }

    public static object? FastDeserializeString(string json)
    {
        var deserializer = new FastJsonDeserializer(json);
        return deserializer.Deserialize();
    }

    public static AssetRef ParseAssetRef(string rawValue, string inputPath)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            throw new SharpOMaticException($"Input entry '{inputPath}' asset reference cannot be empty.");

        try
        {
            var asset = JsonSerializer.Deserialize<AssetRef>(rawValue, AssetRefJsonOptions);
            if (asset is null)
                throw new SharpOMaticException($"Input entry '{inputPath}' asset reference cannot be null.");

            ValidateAssetRef(asset, inputPath);
            return asset;
        }
        catch (JsonException)
        {
            throw new SharpOMaticException($"Input entry '{inputPath}' value could not be parsed as an asset reference.");
        }
    }

    public static ContextList ParseAssetRefList(string rawValue, string inputPath)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            throw new SharpOMaticException($"Input entry '{inputPath}' asset list cannot be empty.");

        try
        {
            var assets = JsonSerializer.Deserialize<List<AssetRef>>(rawValue, AssetRefJsonOptions);
            if (assets is null)
                throw new SharpOMaticException($"Input entry '{inputPath}' asset list cannot be null.");

            var list = new ContextList();
            for (var i = 0; i < assets.Count; i += 1)
            {
                var asset = assets[i];
                ValidateAssetRef(asset, inputPath, i);
                list.Add(asset);
            }

            return list;
        }
        catch (JsonException)
        {
            throw new SharpOMaticException($"Input entry '{inputPath}' value could not be parsed as an asset list.");
        }
    }

    private static void ValidateAssetRef(AssetRef asset, string inputPath, int? index = null)
    {
        var label = index.HasValue ? $"Input entry '{inputPath}' asset at index {index.Value}" : $"Input entry '{inputPath}' asset";

        if (asset.AssetId == Guid.Empty)
            throw new SharpOMaticException($"{label} must include a valid assetId.");

        if (string.IsNullOrWhiteSpace(asset.Name))
            throw new SharpOMaticException($"{label} must include a name.");

        if (string.IsNullOrWhiteSpace(asset.MediaType))
            throw new SharpOMaticException($"{label} must include a mediaType.");

        if (asset.SizeBytes < 0)
            throw new SharpOMaticException($"{label} must include a non-negative size.");
    }

    public static string SubstituteValues(string input, ContextObject context)
    {
        return SubstituteValues(input, context, new TemplateExpansionState(), 0);
    }

    public static async Task<string> SubstituteValuesAsync(string input, ContextObject context, IRepositoryService repositoryService, IAssetStore assetStore, Guid? runId, string? conversationId = null)
    {
        return await SubstituteValuesAsync(input, context, repositoryService, assetStore, runId, conversationId, new TemplateExpansionState(), 0);
    }

    private static string SubstituteValues(string input, ContextObject context, TemplateExpansionState state, int depth)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        EnsureTemplateExpansionDepth(depth);

        return ContextTemplateRegex.Replace(
            input,
            match =>
            {
                var path = match.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(path))
                    return string.Empty;

                if (!ContextPathResolver.TryGetValue(context, path, false, false, out var value))
                    return string.Empty;

                if (value is string stringValue)
                {
                    if (!ContextTemplateRegex.IsMatch(stringValue))
                        return stringValue;

                    var key = BuildContextExpansionKey(path);
                    state.Enter(key, $"Recursive context template expansion detected for '{path}'.");
                    try
                    {
                        return SubstituteValues(stringValue, context, state, depth + 1);
                    }
                    finally
                    {
                        state.Exit(key);
                    }
                }

                return value switch
                {
                    null => string.Empty,
                    ContextObject => JsonSerializer.Serialize(value, ContextJsonOptions),
                    ContextList => JsonSerializer.Serialize(value, ContextJsonOptions),
                    _ => value.ToString() ?? string.Empty,
                };
            }
        );
    }

    private static async Task<string> SubstituteValuesAsync(
        string input,
        ContextObject context,
        IRepositoryService repositoryService,
        IAssetStore assetStore,
        Guid? runId,
        string? conversationId,
        TemplateExpansionState state,
        int depth
    )
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        EnsureTemplateExpansionDepth(depth);

        var substituted = await SubstituteContextValuesAsync(input, context, repositoryService, assetStore, runId, conversationId, state, depth);
        return await SubstituteAssetValuesAsync(substituted, context, repositoryService, assetStore, runId, conversationId, state, depth);
    }

    private static async Task<string> SubstituteContextValuesAsync(
        string input,
        ContextObject context,
        IRepositoryService repositoryService,
        IAssetStore assetStore,
        Guid? runId,
        string? conversationId,
        TemplateExpansionState state,
        int depth
    )
    {
        var matches = ContextTemplateRegex.Matches(input);
        if (matches.Count == 0)
            return input;

        var sb = new StringBuilder(input.Length);
        var lastIndex = 0;

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            sb.Append(input, lastIndex, match.Index - lastIndex);

            var path = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(path) && ContextPathResolver.TryGetValue(context, path, false, false, out var value))
                sb.Append(await ResolveContextReplacementAsync(path, value, context, repositoryService, assetStore, runId, conversationId, state, depth));

            lastIndex = match.Index + match.Length;
        }

        sb.Append(input, lastIndex, input.Length - lastIndex);
        return sb.ToString();
    }

    private static async Task<string> ResolveContextReplacementAsync(
        string path,
        object? value,
        ContextObject context,
        IRepositoryService repositoryService,
        IAssetStore assetStore,
        Guid? runId,
        string? conversationId,
        TemplateExpansionState state,
        int depth
    )
    {
        if (value is string stringValue)
        {
            if (!HasAnyTemplateMarkers(stringValue))
                return stringValue;

            var key = BuildContextExpansionKey(path);
            state.Enter(key, $"Recursive context template expansion detected for '{path}'.");
            try
            {
                return await SubstituteValuesAsync(stringValue, context, repositoryService, assetStore, runId, conversationId, state, depth + 1);
            }
            finally
            {
                state.Exit(key);
            }
        }

        return value switch
        {
            null => string.Empty,
            ContextObject => JsonSerializer.Serialize(value, ContextJsonOptions),
            ContextList => JsonSerializer.Serialize(value, ContextJsonOptions),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static async Task<string> SubstituteAssetValuesAsync(
        string input,
        ContextObject context,
        IRepositoryService repositoryService,
        IAssetStore assetStore,
        Guid? runId,
        string? conversationId,
        TemplateExpansionState state,
        int depth
    )
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var matches = AssetTemplateRegex.Matches(input);
        if (matches.Count == 0)
            return input;

        var sb = new StringBuilder(input.Length);
        var lastIndex = 0;

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            sb.Append(input, lastIndex, match.Index - lastIndex);

            var assetName = match.Groups[1].Value.Trim();
            var replacement = await ResolveAssetTextAsync(assetName, context, repositoryService, assetStore, runId, conversationId, state, depth);
            sb.Append(replacement);

            lastIndex = match.Index + match.Length;
        }

        sb.Append(input, lastIndex, input.Length - lastIndex);
        return sb.ToString();
    }

    private static async Task<string> ResolveAssetTextAsync(
        string assetName,
        ContextObject context,
        IRepositoryService repositoryService,
        IAssetStore assetStore,
        Guid? runId,
        string? conversationId,
        TemplateExpansionState state,
        int depth
    )
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return string.Empty;

        var asset = await ResolveAssetAsync(assetName, repositoryService, runId, conversationId);

        if (asset is null || !AssetMediaTypePolicy.IsTextLike(asset.MediaType))
            return string.Empty;

        var key = BuildAssetExpansionKey(asset.AssetId);
        state.Enter(key, $"Recursive asset template expansion detected for '{asset.Name}'.");
        try
        {
            await using var stream = await assetStore.OpenReadAsync(asset.StorageKey);
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            if (!HasAnyTemplateMarkers(content))
                return content;

            return await SubstituteValuesAsync(content, context, repositoryService, assetStore, runId, conversationId, state, depth + 1);
        }
        catch (SharpOMaticException)
        {
            throw;
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            state.Exit(key);
        }
    }

    private static async Task<Asset?> ResolveAssetAsync(string assetName, IRepositoryService repositoryService, Guid? runId, string? conversationId)
    {
        var normalizedAssetName = assetName.Trim();
        Asset? asset = null;

        if (runId.HasValue)
            asset = await repositoryService.GetRunAssetByName(runId.Value, normalizedAssetName);

        if (asset is null && !string.IsNullOrWhiteSpace(conversationId))
            asset = await repositoryService.GetConversationAssetByName(conversationId, normalizedAssetName);

        if (asset is null && AssetNameParser.TryParseFolderQualifiedName(normalizedAssetName, out var folderName, out var folderAssetName))
            asset = await repositoryService.GetLibraryAssetByFolderAndName(folderName, folderAssetName);

        if (asset is null)
            asset = await repositoryService.GetLibraryAssetByName(normalizedAssetName);

        return asset;
    }

    private static void EnsureTemplateExpansionDepth(int depth)
    {
        if (depth > MaxTemplateExpansionDepth)
            throw new SharpOMaticException($"Template expansion exceeded the maximum depth of {MaxTemplateExpansionDepth}.");
    }

    private static bool HasAnyTemplateMarkers(string input)
    {
        return ContextTemplateRegex.IsMatch(input) || AssetTemplateRegex.IsMatch(input);
    }

    private static string BuildContextExpansionKey(string path)
    {
        return $"context:{path}";
    }

    private static string BuildAssetExpansionKey(Guid assetId)
    {
        return $"asset:{assetId:N}";
    }

    private sealed class TemplateExpansionState
    {
        private readonly HashSet<string> _activeKeys = new(StringComparer.Ordinal);

        public void Enter(string key, string message)
        {
            if (!_activeKeys.Add(key))
                throw new SharpOMaticException(message);
        }

        public void Exit(string key)
        {
            _activeKeys.Remove(key);
        }
    }

    public static void OverwriteContexts(ContextObject target, ContextObject source)
    {
        foreach (var key in source.Keys)
        {
            if (!target.TryGetValue(key, out var targetValue))
            {
                target[key] = source[key];
                continue;
            }

            var sourceValue = source[key];

            if (targetValue is ContextObject targetObject && sourceValue is ContextObject sourceObject)
                OverwriteContexts(targetObject, sourceObject);
            else
                target[key] = sourceValue;
        }
    }
}
