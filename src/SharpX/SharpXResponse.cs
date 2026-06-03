using System.Net;

namespace SharpX;

/// <summary>
/// Strongly-typed response returned by the SharpX client.
/// </summary>
/// <typeparam name="T">The deserialized data type.</typeparam>
public sealed class SharpXResponse<T>
{
    /// <summary>The deserialized response body.</summary>
    public T? Data { get; init; }

    /// <summary>The raw response body string (post-transformers).</summary>
    public string RawBody { get; init; } = string.Empty;

    /// <summary>The HTTP status code as an integer.</summary>
    public int Status { get; init; }

    /// <summary>The HTTP status code.</summary>
    public HttpStatusCode StatusCode { get; init; }

    /// <summary>The HTTP reason phrase (e.g., "OK", "Not Found").</summary>
    public string? StatusText { get; init; }

    /// <summary>The aggregated response headers (response + content).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>The original request configuration used to issue this call.</summary>
    public SharpXRequestConfig RequestConfig { get; init; } = new();

    /// <summary>True when the call succeeded according to the configured status validator.</summary>
    public bool IsSuccess { get; init; }
}
