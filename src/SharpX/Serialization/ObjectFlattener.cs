using System.Reflection;

namespace SharpX.Serialization;

/// <summary>
/// Internal helper that flattens objects/dictionaries to string key-value pairs for query strings or form bodies.
/// </summary>
internal static class ObjectFlattener
{
    public static IEnumerable<KeyValuePair<string, object?>> Flatten(object? source)
    {
        if (source is null)
        {
            yield break;
        }

        if (source is IEnumerable<KeyValuePair<string, object?>> kvs)
        {
            foreach (var kv in kvs)
            {
                yield return kv;
            }
            yield break;
        }

        if (source is System.Collections.IDictionary dict)
        {
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                if (entry.Key is null)
                {
                    continue;
                }

                yield return new KeyValuePair<string, object?>(entry.Key.ToString() ?? string.Empty, entry.Value);
            }
            yield break;
        }

        foreach (var prop in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var value = prop.GetValue(source);
            yield return new KeyValuePair<string, object?>(prop.Name, value);
        }
    }
}
