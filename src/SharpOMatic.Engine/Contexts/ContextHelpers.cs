namespace SharpOMatic.Engine.Contexts;

public static class ContextHelpers
{
    private static readonly JsonSerializerOptions ContextJsonOptions = new JsonSerializerOptions().BuildOptions();
    private static readonly JsonSerializerOptions AssetRefJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<object?> ResolveContextEntryValue(
        IServiceProvider serviceProvider,
        ContextObject context,
        ContextEntryEntity entry,
        IScriptOptionsService scriptOptionsService,
        Guid runId
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
                        Assets = new AssetHelper(repositoryService, assetStore, runId),
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

    private static object? FastDeserializeString(string json)
    {
        var deserializer = new FastJsonDeserializer(json);
        return deserializer.Deserialize();
    }

    private static AssetRef ParseAssetRef(string rawValue, string inputPath)
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

    private static ContextList ParseAssetRefList(string rawValue, string inputPath)
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
        if (string.IsNullOrWhiteSpace(input))
            return input;

        return System.Text.RegularExpressions.Regex.Replace(
            input,
            @"\{\{\s*(?:\$\s*)?(.*?)\s*\}\}",
            match =>
            {
                var path = match.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(path))
                    return string.Empty;

                if (ContextPathResolver.TryGetValue(context, path, false, false, out var value))
                {
                    return value switch
                    {
                        null => string.Empty,
                        ContextObject => JsonSerializer.Serialize(value, ContextJsonOptions),
                        ContextList => JsonSerializer.Serialize(value, ContextJsonOptions),
                        _ => value.ToString() ?? string.Empty,
                    };
                }

                return string.Empty;
            }
        );
    }

    public static async Task<string> SubstituteValuesAsync(string input, ContextObject context, IRepositoryService repositoryService, IAssetStore assetStore, Guid? runId)
    {
        var substituted = SubstituteValues(input, context);
        return await SubstituteAssetValuesAsync(substituted, repositoryService, assetStore, runId);
    }

    private static async Task<string> SubstituteAssetValuesAsync(string input, IRepositoryService repositoryService, IAssetStore assetStore, Guid? runId)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var matches = System.Text.RegularExpressions.Regex.Matches(input, @"<<\s*(.*?)\s*>>");
        if (matches.Count == 0)
            return input;

        var sb = new StringBuilder(input.Length);
        var lastIndex = 0;

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            sb.Append(input, lastIndex, match.Index - lastIndex);

            var assetName = match.Groups[1].Value.Trim();
            var replacement = await ResolveAssetTextAsync(assetName, repositoryService, assetStore, runId);
            sb.Append(replacement);

            lastIndex = match.Index + match.Length;
        }

        sb.Append(input, lastIndex, input.Length - lastIndex);
        return sb.ToString();
    }

    private static async Task<string> ResolveAssetTextAsync(string assetName, IRepositoryService repositoryService, IAssetStore assetStore, Guid? runId)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return string.Empty;

        Asset? asset = null;

        if (runId.HasValue)
            asset = await repositoryService.GetRunAssetByName(runId.Value, assetName);

        if (asset is null)
            asset = await repositoryService.GetLibraryAssetByName(assetName);

        if (asset is null || !IsTextMediaType(asset.MediaType))
            return string.Empty;

        try
        {
            await using var stream = await assetStore.OpenReadAsync(asset.StorageKey);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsTextMediaType(string mediaType) => mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
}
