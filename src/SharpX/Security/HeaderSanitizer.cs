namespace SharpX.Security;

/// <summary>
/// Utility for sanitizing header values to prevent CRLF/header injection attacks.
/// </summary>
public static class HeaderSanitizer
{
    private const int MaxHeaderLength = 8192;

    /// <summary>
    /// Validates that a header name does not contain illegal characters.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the name is null, empty, or contains illegal characters.</exception>
    public static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Header name cannot be null or whitespace.", nameof(name));
        }

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c <= 0x20 || c == 0x7F || c == ':' || c == '\r' || c == '\n')
            {
                throw new ArgumentException($"Header name '{name}' contains illegal character at position {i}.", nameof(name));
            }
        }

        return name;
    }

    /// <summary>
    /// Sanitizes a header value by rejecting CR/LF and NUL characters and enforcing a maximum length.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value contains forbidden characters or exceeds the limit.</exception>
    public static string SanitizeValue(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value.Length > MaxHeaderLength)
        {
            throw new ArgumentException($"Header value exceeds maximum length of {MaxHeaderLength} characters.", nameof(value));
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '\r' || c == '\n' || c == '\0')
            {
                throw new ArgumentException("Header value contains forbidden control characters (CR/LF/NUL).", nameof(value));
            }
        }

        return value;
    }
}
