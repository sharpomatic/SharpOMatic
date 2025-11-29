namespace SharpOMatic.Engine.Contexts;

public class ContextList : IList<object?>
{
    private readonly List<object?> _list = [];

    public ContextList()
    {
    }

    public ContextList(IEnumerable<object?> items)
    {
        if (items is null)
            return;

        _list.AddRange(items);
    }

    public void AddRange(IEnumerable<object?> items)
    {
        if (items is null) 
            return;

        foreach (var it in items) _list.Add(it);
    }

    public void InsertRange(int index, IEnumerable<object?> items)
    {
        if (items is null) 
            return;

        _list.InsertRange(index, items);
    }

    public object? this[int index] 
    { 
        get => _list[index];
        set => _list[index] = value;
    }

    public int Count => _list.Count;

    public bool IsReadOnly => false;

    public void Add(object? item)
    {
        _list.Add(item);
    }

    public void Clear()
    {
        _list.Clear();
    }

    public bool Contains(object? item)
    {
        return _list.Contains(item);
    }

    public void CopyTo(object?[] array, int arrayIndex)
    {
        _list.CopyTo(array, arrayIndex);
    }

    public IEnumerator<object?> GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    public int IndexOf(object? item)
    {
        return _list.IndexOf(item);
    }

    public void Insert(int index, object? item)
    {
        _list.Insert(index, item);
    }

    public bool Remove(object? item)
    {
        return _list.Remove(item);
    }

    public void RemoveAt(int index)
    {
        _list.RemoveAt(index);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public T Get<T>(string path)
    {
        if (!ContextPathResolver.TryGetValue(this, path, requireLeadingIndex: true, throwOnError: true, out var value))
            throw new SharpOMaticException($"Path '{path}' not found.");

        if (ContextPathResolver.TryStrictCast(value, out T? result))
            return result!;

        var actual = value is null ? "null" : value.GetType().FullName;
        throw new SharpOMaticException($"Value at '{path}' is of type '{actual}', not '{typeof(T).FullName}'.");
    }

    public bool TryGet<T>(string path, [MaybeNullWhen(false)] out T value)
    {
        value = default!;

        if (!ContextPathResolver.TryGetValue(this, path, requireLeadingIndex: true, throwOnError: false, out var resolved))
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
        if (!ContextPathResolver.TryGetValue(this, path, requireLeadingIndex: true, throwOnError: false, out var value))
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
        if (!ContextPathResolver.TryGetValue(this, path, requireLeadingIndex: true, throwOnError: false, out var value))
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
        ContextPathResolver.TrySetValue(this, path, value, requireLeadingIndex: true, throwOnError: true);
    }

    public bool TrySet<T>(string path, T value)
    {
        return ContextPathResolver.TrySetValue(this, path, value, requireLeadingIndex: true, throwOnError: false);
    }

    public bool RemovePath(string path)
    {
        return ContextPathResolver.TryRemove(this, path, requireLeadingIndex: true, throwOnError: false);
    }
}
