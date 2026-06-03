using System.Text.RegularExpressions;

namespace SharpX.Security;

/// <summary>
/// Redacts sensitive header values and URL credentials before they appear in logs or exception messages.
/// </summary>
public static class SensitiveDataRedactor
{
    /// <summary>The masked replacement value.</summary>
    public const string Mask = "***REDACTED***";

    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Proxy-Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "X-Auth-Token",
        "X-CSRF-Token",
        "X-Access-Token",
    };

    private static readonly Regex UserInfoRegex = new(
        @"(?<scheme>[a-zA-Z][a-zA-Z0-9+\-.]*://)(?<userinfo>[^/@\s]+)@",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] SensitiveTokenPatterns =
    {
        "password",
        "passwd",
        "secret",
        "token",
        "apikey",
        "api_key",
        "access_token",
        "refresh_token",
    };

    /// <summary>
    /// Returns a new dictionary with sensitive header values masked.
    /// </summary>
    public static IReadOnlyDictionary<string, string> RedactHeaders(IEnumerable<KeyValuePair<string, string>>? headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers is null)
        {
            return result;
        }

        foreach (var kv in headers)
        {
            result[kv.Key] = SensitiveHeaders.Contains(kv.Key) ? Mask : kv.Value;
        }

        return result;
    }

    /// <summary>
    /// Strips userinfo (user:pass@) from URLs so credentials are not leaked into logs.
    /// </summary>
    public static string RedactUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return string.Empty;
        }

        return UserInfoRegex.Replace(url!, m => $"{m.Groups["scheme"].Value}{Mask}@");
    }

    /// <summary>
    /// Returns true when the supplied key looks sensitive (used in body redaction).
    /// </summary>
    public static bool IsSensitiveKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var lower = key.ToLowerInvariant();
        foreach (var p in SensitiveTokenPatterns)
        {
            if (lower.Contains(p, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
