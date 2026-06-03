using System.Net;

namespace SharpX;

/// <summary>
/// Exception type thrown by SharpX when a request fails (network error, timeout, or unsuccessful status code).
/// Sensitive headers (Authorization, Cookie, etc.) are redacted before being attached to the exception.
/// </summary>
[Serializable]
public sealed class SharpXException : Exception
{
    /// <summary>The request configuration used when the failure occurred. Headers are redacted.</summary>
    public SharpXRequestConfig? RequestConfig { get; }

    /// <summary>The HTTP status code, when available.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>The HTTP status code as an integer, when available.</summary>
    public int? Status => StatusCode.HasValue ? (int)StatusCode.Value : null;

    /// <summary>Redacted response headers, when available.</summary>
    public IReadOnlyDictionary<string, string> ResponseHeaders { get; }

    /// <summary>The raw response body, when available.</summary>
    public string? ResponseBody { get; }

    /// <summary>True when the failure was caused by a timeout.</summary>
    public bool IsTimeout { get; }

    /// <summary>True when the failure was caused by request cancellation.</summary>
    public bool IsCancelled { get; }

    /// <summary>True when the failure originated from the network layer (DNS, TCP, TLS).</summary>
    public bool IsNetworkError { get; }

    /// <summary>The category describing the failure cause.</summary>
    public SharpXErrorCategory Category { get; }

    /// <summary>Creates a new <see cref="SharpXException"/>.</summary>
    public SharpXException(
        string message,
        SharpXRequestConfig? requestConfig = null,
        HttpStatusCode? statusCode = null,
        IReadOnlyDictionary<string, string>? responseHeaders = null,
        string? responseBody = null,
        SharpXErrorCategory category = SharpXErrorCategory.Unknown,
        Exception? innerException = null)
        : base(message, innerException)
    {
        RequestConfig = requestConfig is null ? null : RedactConfig(requestConfig);
        StatusCode = statusCode;
        ResponseHeaders = responseHeaders ?? new Dictionary<string, string>();
        ResponseBody = responseBody;
        Category = category;
        IsTimeout = category == SharpXErrorCategory.Timeout;
        IsCancelled = category == SharpXErrorCategory.Cancelled;
        IsNetworkError = category == SharpXErrorCategory.Network;
    }

    private static SharpXRequestConfig RedactConfig(SharpXRequestConfig source)
    {
        var clone = source.Clone();
        var redacted = Security.SensitiveDataRedactor.RedactHeaders(clone.Headers);

        clone.Headers.Clear();
        foreach (var kv in redacted)
        {
            clone.Headers[kv.Key] = kv.Value;
        }

        clone.Url = Security.SensitiveDataRedactor.RedactUrl(clone.Url);
        if (!string.IsNullOrEmpty(clone.BaseUrl))
        {
            clone.BaseUrl = Security.SensitiveDataRedactor.RedactUrl(clone.BaseUrl);
        }

        return clone;
    }
}

/// <summary>Classifies the cause of a <see cref="SharpXException"/>.</summary>
public enum SharpXErrorCategory
{
    /// <summary>Cause could not be classified.</summary>
    Unknown,
    /// <summary>The server returned a non-success status code.</summary>
    HttpStatus,
    /// <summary>The request exceeded its configured timeout.</summary>
    Timeout,
    /// <summary>The caller cancelled the request via a <see cref="CancellationToken"/>.</summary>
    Cancelled,
    /// <summary>A network-layer failure (DNS, TCP, TLS).</summary>
    Network,
    /// <summary>The response body could not be deserialized.</summary>
    Deserialization,
    /// <summary>An interceptor or transformer threw.</summary>
    Interceptor,
    /// <summary>The request configuration was invalid.</summary>
    InvalidConfiguration,
}
