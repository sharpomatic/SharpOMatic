namespace SharpOMatic.Engine.Contexts;

internal static class ContextPathResolver
{
    private enum PathPartKind { Property, Index }

    private readonly record struct PathPart(PathPartKind Kind, string? Name, int Index)
    {
        public static PathPart Prop(string name) => new(PathPartKind.Property, name, -1);
        public static PathPart At(int index) => new(PathPartKind.Index, null, index);
    }

    public static bool TryGetValue(object? root, string path, bool requireLeadingIndex, bool throwOnError, out object? value)
    {
        value = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            if (throwOnError) 
                throw new SharpOMaticException("Path cannot be null or empty.");
             
            return false;
        }

        if (!TryParsePath(path, requireLeadingIndex, throwOnError, out var parts))
            return false;

        object? current = root;
        for (int i = 0; i < parts!.Count; i++)
        {
            var part = parts[i];

            switch (part.Kind)
            {
                case PathPartKind.Property:
                    if (current is not ContextObject co)
                    {
                        if (throwOnError) 
                            throw new SharpOMaticException($"'{path}' segment '{part.Name}' expects a ContextObject, found '{TypeNameOf(current)}'.");

                        return false;
                    }

                    if (!co.TryGetValue(part.Name!, out current))
                    {
                        if (throwOnError) 
                            throw new SharpOMaticException($"Missing '{part.Name}' in path '{path}'.");

                        return false;
                    }
                    break;

                case PathPartKind.Index:
                    if (current is not ContextList list)
                    {
                        if (throwOnError) 
                            throw new SharpOMaticException($"'{path}' index [{part.Index}] expects a ContextList, found '{TypeNameOf(current)}'.");
                        
                        return false;
                    }
                    if (part.Index < 0 || part.Index >= list.Count)
                    {
                        if (throwOnError) 
                            throw new SharpOMaticException($"Index {part.Index} out of range for list (size {list.Count}) in path '{path}'.");
                        
                        return false;
                    }
                    current = list[part.Index];
                    break;
            }
        }

        value = current;
        return true;
    }

    public static bool TryStrictCast<T>(object? value, [MaybeNullWhen(false)] out T result)
    {
        if (value is null)
        {
            if (default(T) is null)
            {
                result = default!;
                return true;
            }

            result = default!;
            return false;
        }

        if (value is T ok)
        {
            result = ok;
            return true;
        }

        var t = typeof(T);
        var u = Nullable.GetUnderlyingType(t);
        if (u is not null && value.GetType() == u)
        {
            var boxedNullable = Activator.CreateInstance(t, value);
            result = (T)boxedNullable!;
            return true;
        }

        result = default!;
        return false;
    }

    public static bool TrySetValue(object? root, string path, object? newValue, bool requireLeadingIndex, bool throwOnError)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (throwOnError)
                throw new SharpOMaticException("Path cannot be null or empty.");

            return false;
        }

        if (!TryParsePath(path, requireLeadingIndex, throwOnError, out var parts) || parts is null)
            return false;

        if (parts.Count == 0)
        {
            if (throwOnError)
                throw new SharpOMaticException("Path contains no segments.");

            return false;
        }

        object? current = root;

        // Traverse to parent of final part, creating intermediate ContextObjects when missing
        for (int i = 0; i < parts.Count - 1; i++)
        {
            var part = parts[i];
            var next = parts[i + 1];

            if (part.Kind == PathPartKind.Property)
            {
                if (current is not ContextObject co)
                {
                    if (throwOnError)
                        throw new SharpOMaticException($"'{path}' segment '{part.Name}' expects a ContextObject, found '{TypeNameOf(current)}'.");
                    
                    return false;
                }

                if (!co.TryGetValue(part.Name!, out var child))
                {
                    // Missing property: auto-create only if the next segment is a property
                    if (next.Kind == PathPartKind.Property)
                    {
                        var created = new ContextObject();
                        co.Add(part.Name!, created);
                        current = created;
                        continue;
                    }
                    else
                    {
                        if (throwOnError)
                            throw new SharpOMaticException($"Missing list '{part.Name}' in path '{path}'. Lists are not auto-created.");
                        
                        return false;
                    }
                }

                if (next.Kind == PathPartKind.Property)
                {
                    if (child is ContextObject nextObj)
                    {
                        current = nextObj;
                    }
                    else
                    {
                        if (throwOnError)
                            throw new SharpOMaticException($"'{part.Name}' in path '{path}' is '{TypeNameOf(child)}', not a ContextObject.");
                        return false;
                    }
                }
                else // next is Index
                {
                    if (child is ContextList nextList)
                    {
                        current = nextList;
                    }
                    else
                    {
                        if (throwOnError)
                            throw new SharpOMaticException($"'{part.Name}' in path '{path}' is '{TypeNameOf(child)}', not a ContextList.");
                        return false;
                    }
                }
            }
            else // Index
            {
                if (current is not ContextList list)
                {
                    if (throwOnError)
                        throw new SharpOMaticException($"'{path}' index [{part.Index}] expects a ContextList, found '{TypeNameOf(current)}'.");
                    
                    return false;
                }

                if (part.Index < 0 || part.Index >= list.Count)
                {
                    if (throwOnError)
                        throw new SharpOMaticException($"Index {part.Index} out of range for list (size {list.Count}) in path '{path}'.");
                    
                    return false;
                }

                var elem = list[part.Index];
                if (next.Kind == PathPartKind.Property)
                {
                    if (elem is ContextObject elemObj)
                    {
                        current = elemObj;
                    }
                    else
                    {
                        if (throwOnError)
                            throw new SharpOMaticException($"Element at index {part.Index} in path '{path}' is '{TypeNameOf(elem)}', not a ContextObject.");
                        return false;
                    }
                }
                else // next is Index
                {
                    if (elem is ContextList elemList)
                    {
                        current = elemList;
                    }
                    else
                    {
                        if (throwOnError)
                            throw new SharpOMaticException($"Element at index {part.Index} in path '{path}' is '{TypeNameOf(elem)}', not a ContextList.");
                        return false;
                    }
                }
            }
        }

        // Apply final segment on current
        var last = parts[^1];
        if (last.Kind == PathPartKind.Property)
        {
            if (current is not ContextObject co)
            {
                if (throwOnError)
                    throw new SharpOMaticException($"Final segment '{last.Name}' expects a ContextObject in path '{path}'.");
                
                return false;
            }

            if (co.ContainsKey(last.Name!))
                co[last.Name!] = newValue;
            else
                co.Add(last.Name!, newValue);
        }
        else // final is Index
        {
            if (current is not ContextList list)
            {
                if (throwOnError)
                    throw new SharpOMaticException($"Final index [{last.Index}] expects a ContextList in path '{path}'.");
                
                return false;
            }

            if (last.Index < 0 || last.Index >= list.Count)
            {
                if (throwOnError)
                    throw new SharpOMaticException($"Index {last.Index} out of range for list (size {list.Count}) in path '{path}'.");
                
                return false;
            }

            list[last.Index] = newValue;
        }

        return true;
    }

    private static bool TryParsePath(string path, bool requireLeadingIndex, bool throwOnError, out List<PathPart>? parts)
    {
        parts = null;
        var list = new List<PathPart>();

        int i = 0;
        int n = path.Length;
        bool sawAny = false;
        bool leadingIsIndex = false;

        while (i < n)
        {
            int start = i;
            while (i < n && path[i] != '.' && path[i] != '[')
                i++;

            if (i > start)
            {
                var name = path[start..i];

                try
                {
                    IdentifierValidator.ValidateIdentifier(name);
                }
                catch (Exception)
                {
                    if (throwOnError) 
                        throw;

                    return false;
                }

                list.Add(PathPart.Prop(name));

                if (!sawAny) 
                    leadingIsIndex = false;

                sawAny = true;
            }

            while (i < n && path[i] == '[')
            {
                if (!sawAny) 
                    leadingIsIndex = true;

                i++; // '['
                int idxStart = i;
                while (i < n && char.IsDigit(path[i])) 
                    i++;

                if (i == idxStart)
                {
                    if (throwOnError) 
                        throw new SharpOMaticException($"Empty index in path '{path}'.");

                    return false;
                }
                if (i >= n || path[i] != ']')
                {
                    if (throwOnError) 
                        throw new SharpOMaticException($"Unclosed indexer in path '{path}'.");

                    return false;
                }

                var span = path.AsSpan(idxStart, i - idxStart);
                if (!int.TryParse(span, out var index))
                {
                    if (throwOnError) 
                        throw new SharpOMaticException($"Invalid index '{span.ToString()}' in path '{path}'.");

                    return false;
                }

                list.Add(PathPart.At(index));
                i++; // ']'
                sawAny = true;
            }

            if (i < n)
            {
                if (path[i] == '.')
                {
                    i++;
                    if (i >= n)
                    {
                        if (throwOnError) 
                            throw new SharpOMaticException($"Path '{path}' cannot end with '.'.");

                        return false;
                    }
                }
                else if (path[i] != '[')
                {
                    if (throwOnError) 
                        throw new SharpOMaticException($"Unexpected character '{path[i]}' in path '{path}'.");

                    return false;
                }
            }
        }

        if (list.Count == 0)
        {
            if (throwOnError) 
                throw new SharpOMaticException("Path contains no segments.");

            return false;
        }

        if (requireLeadingIndex && !leadingIsIndex)
        {
            if (throwOnError) 
                throw new SharpOMaticException($"Path '{path}' must start with an index when navigating a ContextList instance.");

            return false;
        }

        parts = list;
        return true;
    }

    private static string TypeNameOf(object? o) => o is null ? "null" : o.GetType().FullName ?? o.GetType().Name;

    public static bool TryRemove(object? root, string path, bool requireLeadingIndex, bool throwOnError)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (throwOnError)
                throw new SharpOMaticException("Path cannot be null or empty.");
            
            return false;
        }

        if (!TryParsePath(path, requireLeadingIndex, throwOnError, out var parts) || parts is null || parts.Count == 0)
        {
            if (throwOnError)
                throw new SharpOMaticException("Path contains no segments.");
            
            return false;
        }

        object? current = root;

        // Traverse to parent of final segment without creating anything
        for (int i = 0; i < parts.Count - 1; i++)
        {
            var part = parts[i];
            var next = parts[i + 1];

            if (part.Kind == PathPartKind.Property)
            {
                if (current is not ContextObject co)
                {
                    if (throwOnError)
                        throw new SharpOMaticException($"'{path}' segment '{part.Name}' expects a ContextObject, found '{TypeNameOf(current)}'.");
                    
                    return false;
                }

                if (!co.TryGetValue(part.Name!, out var child))
                {
                    // Missing parent property → nothing to remove
                    return false;
                }

                if (next.Kind == PathPartKind.Property)
                {
                    if (child is ContextObject nextObj)
                        current = nextObj;
                    else
                    {
                        if (throwOnError)
                            throw new SharpOMaticException($"'{part.Name}' in path '{path}' is '{TypeNameOf(child)}', not a ContextObject.");
                        
                        return false;
                    }
                }
                else // next is Index
                {
                    if (child is ContextList nextList)
                        current = nextList;
                    else
                    {
                        if (throwOnError)
                            throw new SharpOMaticException($"'{part.Name}' in path '{path}' is '{TypeNameOf(child)}', not a ContextList.");
                        
                        return false;
                    }
                }
            }
            else // Index
            {
                if (current is not ContextList list)
                {
                    if (throwOnError)
                        throw new SharpOMaticException($"'{path}' index [{part.Index}] expects a ContextList, found '{TypeNameOf(current)}'.");
                    
                    return false;
                }

                if (part.Index < 0 || part.Index >= list.Count)
                {
                    // Missing parent index → nothing to remove
                    return false;
                }

                var elem = list[part.Index];
                if (next.Kind == PathPartKind.Property)
                {
                    if (elem is ContextObject elemObj)
                        current = elemObj;
                    else
                    {
                        if (throwOnError)
                            throw new SharpOMaticException($"Element at index {part.Index} in path '{path}' is '{TypeNameOf(elem)}', not a ContextObject.");
                        
                        return false;
                    }
                }
                else // next is Index
                {
                    if (elem is ContextList elemList)
                        current = elemList;
                    else
                    {
                        if (throwOnError)
                            throw new SharpOMaticException($"Element at index {part.Index} in path '{path}' is '{TypeNameOf(elem)}', not a ContextList.");
                        
                        return false;
                    }
                }
            }
        }

        // Remove final
        var last = parts[^1];
        if (last.Kind == PathPartKind.Property)
        {
            if (current is not ContextObject co)
            {
                if (throwOnError)
                    throw new SharpOMaticException($"Final segment '{last.Name}' expects a ContextObject in path '{path}'.");
                
                return false;
            }
            return co.Remove(last.Name!);
        }
        else
        {
            if (current is not ContextList list)
            {
                if (throwOnError)
                    throw new SharpOMaticException($"Final index [{last.Index}] expects a ContextList in path '{path}'.");
                
                return false;
            }

            if (last.Index < 0 || last.Index >= list.Count)
                return false;

            list.RemoveAt(last.Index);
            return true;
        }
    }
}
