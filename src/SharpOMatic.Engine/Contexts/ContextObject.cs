using System.Text.Json.Serialization;

namespace SharpOMatic.Engine.Contexts;

public class ContextObject : IDictionary<string, object?>
{
    private readonly Dictionary<string, object?> _dict = [];

    public object? this[string key] 
    {
        get => _dict[key];
        set => _dict[key] = value;    
    }

    public ICollection<string> Keys => _dict.Keys;

    public ICollection<object?> Values => _dict.Values;

    public void Add(string key, object? value)
    {
        IdentifierValidator.ValidateIdentifier(key);
        _dict.Add(key, value);
    }

    public bool ContainsKey(string key)
    {
        return _dict.ContainsKey(key);
    }

    public bool Remove(string key)
    {
        return _dict.Remove(key);
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object? value)
    {
        return _dict.TryGetValue(key, out value);
    }

    public int Count => _dict.Count;

    public bool IsReadOnly => false;

    public void Add(KeyValuePair<string, object?> item)
    {
        IdentifierValidator.ValidateIdentifier(item.Key);
        ((ICollection<KeyValuePair<string, object?>>)_dict).Add(item);
    }

    public void Clear()
    {
        _dict.Clear();
    }

    public bool Contains(KeyValuePair<string, object?> item)
    {
        return ((ICollection<KeyValuePair<string, object?>>)_dict).Contains(item);
    }

    public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<string, object?>>)_dict).CopyTo(array, arrayIndex);
    }

    public bool Remove(KeyValuePair<string, object?> item)
    {
        return ((ICollection<KeyValuePair<string, object?>>)_dict).Remove(item);
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        return _dict.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _dict.GetEnumerator();
    }

    public T Get<T>(string path)
    {
        if (!ContextPathResolver.TryGetValue(this, path, requireLeadingIndex: false, throwOnError: true, out var value))
            throw new SharpOMaticException($"Path '{path}' not found.");

        if (ContextPathResolver.TryStrictCast(value, out T? result))
            return result!;

        var actual = value is null ? "null" : value.GetType().FullName;
        throw new SharpOMaticException($"Value at '{path}' is of type '{actual}', not '{typeof(T).FullName}'.");
    }

    public bool TryGet<T>(string path, out T? value)
    {
        value = default!;

        if (!ContextPathResolver.TryGetValue(this, path, requireLeadingIndex: false, throwOnError: false, out var resolved))
            return false;

        if (ContextPathResolver.TryStrictCast(resolved, out T? result))
        {
            value = result;
            return true;
        }

        return false;
    }
    public bool TryGetObject(string path, [MaybeNullWhen(false)] out ContextObject obj)
    {
        obj = default!;
        if (!ContextPathResolver.TryGetValue(this, path, requireLeadingIndex: false, throwOnError: false, out var value))
            return false;
        if (value is ContextObject co)
        {
            obj = co;
            return true;
        }
        return false;
    }

    public bool TryGetList(string path, [MaybeNullWhen(false)] out ContextList list)
    {
        list = default!;
        if (!ContextPathResolver.TryGetValue(this, path, requireLeadingIndex: false, throwOnError: false, out var value))
            return false;
        if (value is ContextList cl)
        {
            list = cl;
            return true;
        }
        return false;
    }

    public void Set<T>(string path, T value)
    {
        ContextPathResolver.TrySetValue(this, path, value, requireLeadingIndex: false, throwOnError: true);
    }

    public bool TrySet<T>(string path, T value)
    {
        return ContextPathResolver.TrySetValue(this, path, value, requireLeadingIndex: false, throwOnError: false);
    }

    public bool RemovePath(string path)
    {
        return ContextPathResolver.TryRemove(this, path, requireLeadingIndex: false, throwOnError: false);
    }

    public string Serialize(IServiceProvider serviceProvider)
    {
        var jsonConverterService = serviceProvider.GetRequiredService<IJsonConverterService>();
        return Serialize(jsonConverterService);
    }

    public string Serialize(IJsonConverterService jsonConverterService)
    {
        var jsonConverters = jsonConverterService.GetConverters();
        return Serialize(jsonConverters);
    }

    public string Serialize(IEnumerable<JsonConverter>? jsonConverters = null)
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions().BuildOptions(jsonConverters));
    }

    public static ContextObject Deserialize(string? json, IServiceProvider serviceProvider)
    {
        var jsonConverterService = serviceProvider.GetRequiredService<IJsonConverterService>();
        return Deserialize(json, jsonConverterService);
    }

    public static ContextObject Deserialize(string? json, IJsonConverterService jsonConverterService)
    {
        var jsonConverters = jsonConverterService.GetConverters();
        return Deserialize(json, jsonConverters);
    }

    public static ContextObject Deserialize(string? json, IEnumerable<JsonConverter>? jsonConverters = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<ContextObject>(json, new JsonSerializerOptions().BuildOptions(jsonConverters))!;
    }
}
