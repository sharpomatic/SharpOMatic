namespace SharpOMatic.Engine.Helpers;

public static class ContextHelpers
{
    public static async Task<object?> EvaluateContextEntryValue(ContextObject context, ContextEntryEntity entry)
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
                    var deserializer = new FastJsonDeserializer(entry.EntryValue);
                    entryValue = deserializer.Deserialize();
                }
                catch
                {
                    throw new SharpOMaticException($"Input entry '{entry.InputPath}' value could not be parsed as json.");
                }
                break;

            case ContextEntryType.Expression:
                if (!string.IsNullOrWhiteSpace(entry.EntryValue))
                {
                    var options = ScriptOptions.Default
                                      .WithImports("System", "System.Threading.Tasks", "SharpOMatic.Engine.Contexts")
                                      .WithReferences(typeof(Task).Assembly, typeof(ContextObject).Assembly);

                    try
                    {
                        entryValue = await CSharpScript.EvaluateAsync(entry.EntryValue, options, new ScriptCodeContext() { Context = context }, typeof(ScriptCodeContext));
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
        }

        return entryValue;
    }
}
