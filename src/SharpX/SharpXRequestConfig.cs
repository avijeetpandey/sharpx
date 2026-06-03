using System.Text.Json;

namespace SharpX;

/// <summary>
/// Delegate that transforms request payloads before serialization.
/// </summary>
/// <param name="data">The raw payload.</param>
/// <param name="headers">The mutable header collection.</param>
/// <returns>The transformed payload.</returns>
public delegate object? TransformRequestDelegate(object? data, IDictionary<string, string> headers);

/// <summary>
/// Delegate that transforms response payloads (raw string body) before deserialization.
/// </summary>
/// <param name="rawBody">The raw response body string.</param>
/// <param name="headers">A read-only view of response headers.</param>
/// <returns>The transformed body string.</returns>
public delegate string TransformResponseDelegate(string rawBody, IReadOnlyDictionary<string, string> headers);

/// <summary>
/// Predicate evaluated to decide whether a status code is treated as success.
/// </summary>
/// <param name="status">The HTTP status code.</param>
/// <returns>True if the response should resolve successfully; false to throw a <see cref="SharpXException"/>.</returns>
public delegate bool ValidateStatusDelegate(int status);

/// <summary>
/// Strongly-typed configuration for an outgoing SharpX request. Mirrors the configuration object
/// supplied to axios in JavaScript and is used by both per-call and per-instance defaults.
/// </summary>
public sealed class SharpXRequestConfig
{
    /// <summary>HTTP method to use. Defaults to <see cref="SharpXHttpMethod.Get"/>.</summary>
    public SharpXHttpMethod Method { get; set; } = SharpXHttpMethod.Get;

    /// <summary>Optional base URL prepended to <see cref="Url"/> when the latter is relative.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Target URL. May be absolute or relative to <see cref="BaseUrl"/>.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Headers sent with the request. Header values are sanitized to prevent CRLF injection.</summary>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Query parameters to be appended to the URL. Values are URL-encoded and arrays are expanded
    /// using axios-style indices indicators.
    /// </summary>
    public IDictionary<string, object?> Params { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>The request payload. May be a primitive, collection, anonymous object, or stream.</summary>
    public object? Data { get; set; }

    /// <summary>Per-request timeout. Overrides the client default when set.</summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Predicate evaluated to determine if a status code resolves as success. Defaults to 2xx.
    /// </summary>
    public ValidateStatusDelegate? ValidateStatus { get; set; }

    /// <summary>JSON serializer options used for body serialization/deserialization.</summary>
    public JsonSerializerOptions? JsonOptions { get; set; }

    /// <summary>Optional request transformers executed in order before sending.</summary>
    public IList<TransformRequestDelegate> TransformRequest { get; } = new List<TransformRequestDelegate>();

    /// <summary>Optional response transformers executed in order after receiving the body.</summary>
    public IList<TransformResponseDelegate> TransformResponse { get; } = new List<TransformResponseDelegate>();

    /// <summary>Optional per-request <see cref="System.Net.Http.HttpCompletionOption"/>.</summary>
    public HttpCompletionOption? CompletionOption { get; set; }

    /// <summary>
    /// Creates a deep-enough copy of the configuration so callers can safely mutate per-request copies
    /// without affecting client defaults.
    /// </summary>
    public SharpXRequestConfig Clone()
    {
        var clone = new SharpXRequestConfig
        {
            Method = Method,
            BaseUrl = BaseUrl,
            Url = Url,
            Data = Data,
            Timeout = Timeout,
            ValidateStatus = ValidateStatus,
            JsonOptions = JsonOptions,
            CompletionOption = CompletionOption,
        };

        foreach (var kv in Headers)
        {
            clone.Headers[kv.Key] = kv.Value;
        }

        foreach (var kv in Params)
        {
            clone.Params[kv.Key] = kv.Value;
        }

        foreach (var t in TransformRequest)
        {
            clone.TransformRequest.Add(t);
        }

        foreach (var t in TransformResponse)
        {
            clone.TransformResponse.Add(t);
        }

        return clone;
    }

    /// <summary>
    /// Merges another configuration into this one (other wins). Used to apply per-call overrides on top of defaults.
    /// </summary>
    public SharpXRequestConfig MergeWith(SharpXRequestConfig? other)
    {
        if (other is null)
        {
            return this;
        }

        var merged = Clone();
        merged.Method = other.Method != SharpXHttpMethod.Get || merged.Method == SharpXHttpMethod.Get ? other.Method : merged.Method;
        if (!string.IsNullOrEmpty(other.BaseUrl))
        {
            merged.BaseUrl = other.BaseUrl;
        }
        if (!string.IsNullOrEmpty(other.Url))
        {
            merged.Url = other.Url;
        }
        if (other.Data is not null)
        {
            merged.Data = other.Data;
        }
        if (other.Timeout.HasValue)
        {
            merged.Timeout = other.Timeout;
        }
        if (other.ValidateStatus is not null)
        {
            merged.ValidateStatus = other.ValidateStatus;
        }
        if (other.JsonOptions is not null)
        {
            merged.JsonOptions = other.JsonOptions;
        }
        if (other.CompletionOption.HasValue)
        {
            merged.CompletionOption = other.CompletionOption;
        }

        foreach (var kv in other.Headers)
        {
            merged.Headers[kv.Key] = kv.Value;
        }

        foreach (var kv in other.Params)
        {
            merged.Params[kv.Key] = kv.Value;
        }

        foreach (var t in other.TransformRequest)
        {
            merged.TransformRequest.Add(t);
        }

        foreach (var t in other.TransformResponse)
        {
            merged.TransformResponse.Add(t);
        }

        return merged;
    }
}
