using System.Globalization;
using System.Text;

namespace SharpX.Serialization;

/// <summary>
/// Builds query strings from objects, dictionaries, or enumerables in an axios-compatible way.
/// </summary>
public static class QueryStringBuilder
{
    /// <summary>
    /// Appends the supplied parameters to <paramref name="url"/> as a properly-encoded query string.
    /// Existing query parameters on the URL are preserved.
    /// </summary>
    public static string AppendQuery(string url, IEnumerable<KeyValuePair<string, object?>>? parameters)
    {
        if (parameters is null)
        {
            return url;
        }

        var encoded = Build(parameters);
        if (string.IsNullOrEmpty(encoded))
        {
            return url;
        }

        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return string.Concat(url, separator.ToString(), encoded);
    }

    /// <summary>
    /// Builds a query string (without leading '?') from the supplied parameters.
    /// </summary>
    public static string Build(IEnumerable<KeyValuePair<string, object?>>? parameters)
    {
        if (parameters is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var kv in parameters)
        {
            AppendValue(sb, kv.Key, kv.Value);
        }

        return sb.ToString();
    }

    private static void AppendValue(StringBuilder sb, string key, object? value)
    {
        if (value is null)
        {
            return;
        }

        if (value is string s)
        {
            AppendPair(sb, key, s);
            return;
        }

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                if (item is null)
                {
                    continue;
                }

                AppendPair(sb, key + "[]", FormatScalar(item));
            }

            return;
        }

        AppendPair(sb, key, FormatScalar(value));
    }

    private static void AppendPair(StringBuilder sb, string key, string value)
    {
        if (sb.Length > 0)
        {
            sb.Append('&');
        }

        sb.Append(Uri.EscapeDataString(key));
        sb.Append('=');
        sb.Append(Uri.EscapeDataString(value));
    }

    private static string FormatScalar(object value)
    {
        return value switch
        {
            bool b => b ? "true" : "false",
            DateTime dt => dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
