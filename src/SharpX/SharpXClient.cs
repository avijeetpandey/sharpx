using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SharpX.Interceptors;
using SharpX.Security;
using SharpX.Serialization;

namespace SharpX;

/// <summary>
/// Production-ready, axios-style HTTP client built on top of <see cref="HttpClient"/>.
/// </summary>
public sealed class SharpXClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly SharpXClientOptions _options;

    /// <summary>Default JSON serializer options for this client.</summary>
    public JsonSerializerOptions JsonOptions { get; }

    /// <summary>Default base URL.</summary>
    public string? BaseUrl => _options.BaseUrl;

    /// <summary>Default request headers applied to every request.</summary>
    public IReadOnlyDictionary<string, string> DefaultHeaders { get; }

    /// <summary>Default timeout per request.</summary>
    public TimeSpan Timeout => _options.Timeout;

    /// <summary>Request interceptors registry. See <see cref="InterceptorManager{T}"/>.</summary>
    public InterceptorManager<RequestInterceptorDelegate> RequestInterceptors { get; } = new();

    /// <summary>Response interceptors registry.</summary>
    public InterceptorManager<ResponseInterceptorDelegate> ResponseInterceptors { get; } = new();

    /// <summary>
    /// Creates a new client. Prefer <see cref="Create(SharpXClientOptions)"/> for explicit configuration.
    /// </summary>
    public SharpXClient(SharpXClientOptions? options = null)
    {
        _options = options ?? new SharpXClientOptions();

        if (_options.HttpMessageHandler is not null)
        {
            _httpClient = new HttpClient(_options.HttpMessageHandler, _options.DisposeHandler);
            _ownsHttpClient = true;
        }
        else
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = _options.AllowAutoRedirect,
                MaxAutomaticRedirections = Math.Max(1, _options.MaxAutomaticRedirects),
            };
            _httpClient = new HttpClient(handler, true);
            _ownsHttpClient = true;
        }

        _httpClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        JsonOptions = _options.JsonOptions ?? DefaultJsonOptions.Instance;

        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _options.DefaultHeaders)
        {
            defaults[HeaderSanitizer.ValidateName(kv.Key)] = HeaderSanitizer.SanitizeValue(kv.Value);
        }
        DefaultHeaders = defaults;
    }

    /// <summary>
    /// Creates a new isolated <see cref="SharpXClient"/> instance with its own base URL, headers, and interceptors.
    /// Equivalent to <c>axios.create</c>.
    /// </summary>
    public static SharpXClient Create(SharpXClientOptions options)
    {
        return new SharpXClient(options ?? throw new ArgumentNullException(nameof(options)));
    }

    /// <summary>Convenience overload that builds options inline.</summary>
    public static SharpXClient Create(Action<SharpXClientOptions> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var opt = new SharpXClientOptions();
        configure(opt);
        return new SharpXClient(opt);
    }

    /// <summary>Issues a GET request and deserializes the response.</summary>
    public Task<SharpXResponse<T>> GetAsync<T>(string url, SharpXRequestConfig? config = null, CancellationToken cancellationToken = default)
        => RequestAsync<T>(MergeMethod(SharpXHttpMethod.Get, url, config), cancellationToken);

    /// <summary>Issues a POST request with the supplied data and deserializes the response.</summary>
    public Task<SharpXResponse<T>> PostAsync<T>(string url, object? data = null, SharpXRequestConfig? config = null, CancellationToken cancellationToken = default)
        => RequestAsync<T>(MergeMethod(SharpXHttpMethod.Post, url, config, data), cancellationToken);

    /// <summary>Issues a PUT request with the supplied data and deserializes the response.</summary>
    public Task<SharpXResponse<T>> PutAsync<T>(string url, object? data = null, SharpXRequestConfig? config = null, CancellationToken cancellationToken = default)
        => RequestAsync<T>(MergeMethod(SharpXHttpMethod.Put, url, config, data), cancellationToken);

    /// <summary>Issues a PATCH request with the supplied data and deserializes the response.</summary>
    public Task<SharpXResponse<T>> PatchAsync<T>(string url, object? data = null, SharpXRequestConfig? config = null, CancellationToken cancellationToken = default)
        => RequestAsync<T>(MergeMethod(SharpXHttpMethod.Patch, url, config, data), cancellationToken);

    /// <summary>Issues a DELETE request and deserializes the response.</summary>
    public Task<SharpXResponse<T>> DeleteAsync<T>(string url, SharpXRequestConfig? config = null, CancellationToken cancellationToken = default)
        => RequestAsync<T>(MergeMethod(SharpXHttpMethod.Delete, url, config), cancellationToken);

    /// <summary>Issues a HEAD request.</summary>
    public Task<SharpXResponse<T>> HeadAsync<T>(string url, SharpXRequestConfig? config = null, CancellationToken cancellationToken = default)
        => RequestAsync<T>(MergeMethod(SharpXHttpMethod.Head, url, config), cancellationToken);

    /// <summary>Issues an OPTIONS request.</summary>
    public Task<SharpXResponse<T>> OptionsAsync<T>(string url, SharpXRequestConfig? config = null, CancellationToken cancellationToken = default)
        => RequestAsync<T>(MergeMethod(SharpXHttpMethod.Options, url, config), cancellationToken);

    /// <summary>Lower-level request entry that accepts a fully-formed config.</summary>
    public Task<SharpXResponse<T>> RequestAsync<T>(SharpXRequestConfig config, CancellationToken cancellationToken = default)
        => RequestInternalAsync<T>(config ?? throw new ArgumentNullException(nameof(config)), cancellationToken);

    private SharpXRequestConfig MergeMethod(SharpXHttpMethod method, string url, SharpXRequestConfig? config, object? data = null)
    {
        var merged = (config ?? new SharpXRequestConfig()).Clone();
        merged.Method = method;
        if (!string.IsNullOrEmpty(url))
        {
            merged.Url = url;
        }
        if (data is not null)
        {
            merged.Data = data;
        }
        return merged;
    }

    private async Task<SharpXResponse<T>> RequestInternalAsync<T>(SharpXRequestConfig requestConfig, CancellationToken cancellationToken)
    {
        var config = ApplyClientDefaults(requestConfig);

        try
        {
            foreach (var (fulfilled, _) in RequestInterceptors.Snapshot())
            {
                config = await fulfilled(config, cancellationToken).ConfigureAwait(false) ?? config;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new SharpXException(
                "A request interceptor threw an exception.",
                requestConfig: config,
                category: SharpXErrorCategory.Interceptor,
                innerException: ex);
        }

        ValidateConfig(config);

        var jsonOptions = config.JsonOptions ?? JsonOptions;

        var data = config.Data;
        foreach (var transform in config.TransformRequest)
        {
            data = transform(data, config.Headers);
        }

        var url = BuildUrl(config);
        using var requestMessage = new HttpRequestMessage(config.Method.ToHttpMethod(), url);
        var content = RequestContentFactory.Build(data, jsonOptions, config.Headers);
        if (content is not null)
        {
            requestMessage.Content = content;
        }

        ApplyHeaders(requestMessage, config.Headers);

        var timeout = config.Timeout ?? _options.Timeout;
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        HttpResponseMessage? response = null;
        try
        {
            var completion = config.CompletionOption ?? HttpCompletionOption.ResponseContentRead;
            response = await _httpClient.SendAsync(requestMessage, completion, linkedCts.Token).ConfigureAwait(false);

            var rawBody = response.Content is null
                ? string.Empty
#if NET8_0_OR_GREATER
                : await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
#else
                : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif

            foreach (var transform in config.TransformResponse)
            {
                rawBody = transform(rawBody, BuildResponseHeaders(response));
            }

            var envelope = new SharpXResponseEnvelope(response, rawBody, config);
            try
            {
                foreach (var (fulfilled, _) in ResponseInterceptors.Snapshot())
                {
                    envelope = await fulfilled(envelope, linkedCts.Token).ConfigureAwait(false) ?? envelope;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new SharpXException(
                    "A response interceptor threw an exception.",
                    requestConfig: config,
                    statusCode: response.StatusCode,
                    responseHeaders: BuildResponseHeaders(response),
                    responseBody: rawBody,
                    category: SharpXErrorCategory.Interceptor,
                    innerException: ex);
            }

            var headers = BuildResponseHeaders(envelope.Message);
            var status = (int)envelope.Message.StatusCode;
            var validator = config.ValidateStatus ?? _options.ValidateStatus ?? DefaultStatusValidator;
            var isSuccess = validator(status);

            if (!isSuccess)
            {
                throw new SharpXException(
                    $"Request failed with status code {status}.",
                    requestConfig: config,
                    statusCode: envelope.Message.StatusCode,
                    responseHeaders: headers,
                    responseBody: envelope.RawBody,
                    category: SharpXErrorCategory.HttpStatus);
            }

            T? deserialized;
            try
            {
                deserialized = DeserializeBody<T>(envelope.RawBody, envelope.Message, jsonOptions);
            }
            catch (Exception ex)
            {
                throw new SharpXException(
                    "Failed to deserialize response body.",
                    requestConfig: config,
                    statusCode: envelope.Message.StatusCode,
                    responseHeaders: headers,
                    responseBody: envelope.RawBody,
                    category: SharpXErrorCategory.Deserialization,
                    innerException: ex);
            }

            return new SharpXResponse<T>
            {
                Data = deserialized,
                RawBody = envelope.RawBody,
                Status = status,
                StatusCode = envelope.Message.StatusCode,
                StatusText = envelope.Message.ReasonPhrase,
                Headers = headers,
                RequestConfig = config,
                IsSuccess = true,
            };
        }
        catch (OperationCanceledException oce)
        {
            if (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new SharpXException(
                    $"Request timed out after {timeout}.",
                    requestConfig: config,
                    category: SharpXErrorCategory.Timeout,
                    innerException: oce);
            }

            throw new SharpXException(
                "Request was cancelled.",
                requestConfig: config,
                category: SharpXErrorCategory.Cancelled,
                innerException: oce);
        }
        catch (HttpRequestException hre)
        {
            throw new SharpXException(
                "A network error occurred while sending the request.",
                requestConfig: config,
                statusCode: response?.StatusCode,
                category: SharpXErrorCategory.Network,
                innerException: hre);
        }
        catch (SharpXException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SharpXException(
                "Unexpected error while executing the request.",
                requestConfig: config,
                category: SharpXErrorCategory.Unknown,
                innerException: ex);
        }
        finally
        {
            response?.Dispose();
        }
    }

    private SharpXRequestConfig ApplyClientDefaults(SharpXRequestConfig requestConfig)
    {
        var defaults = new SharpXRequestConfig
        {
            BaseUrl = _options.BaseUrl,
            Timeout = _options.Timeout,
            ValidateStatus = _options.ValidateStatus,
            JsonOptions = _options.JsonOptions,
        };
        foreach (var kv in DefaultHeaders)
        {
            defaults.Headers[kv.Key] = kv.Value;
        }

        return defaults.MergeWith(requestConfig);
    }

    private static void ValidateConfig(SharpXRequestConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Url) && string.IsNullOrWhiteSpace(config.BaseUrl))
        {
            throw new SharpXException(
                "Request URL is not specified.",
                requestConfig: config,
                category: SharpXErrorCategory.InvalidConfiguration);
        }

        foreach (var kv in config.Headers)
        {
            HeaderSanitizer.ValidateName(kv.Key);
            HeaderSanitizer.SanitizeValue(kv.Value);
        }
    }

    private static string BuildUrl(SharpXRequestConfig config)
    {
        var combined = CombineBaseAndPath(config.BaseUrl, config.Url);
        return QueryStringBuilder.AppendQuery(combined, config.Params);
    }

    private static string CombineBaseAndPath(string? baseUrl, string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return baseUrl ?? string.Empty;
        }

        if (HasUrlScheme(url))
        {
            return url;
        }

        if (string.IsNullOrEmpty(baseUrl))
        {
            return url;
        }

        if (baseUrl!.EndsWith('/') && url.StartsWith('/'))
        {
            return baseUrl + url.Substring(1);
        }

        if (!baseUrl.EndsWith('/') && !url.StartsWith('/'))
        {
            return baseUrl + "/" + url;
        }

        return baseUrl + url;
    }

    private static bool HasUrlScheme(string url)
    {
        var idx = url.IndexOf("://", StringComparison.Ordinal);
        if (idx <= 0)
        {
            return false;
        }

        for (var i = 0; i < idx; i++)
        {
            var c = url[i];
            var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (i > 0 && ((c >= '0' && c <= '9') || c == '+' || c == '-' || c == '.'));
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static void ApplyHeaders(HttpRequestMessage message, IDictionary<string, string> headers)
    {
        foreach (var kv in headers)
        {
            var name = HeaderSanitizer.ValidateName(kv.Key);
            var value = HeaderSanitizer.SanitizeValue(kv.Value);

            if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                if (message.Content is not null)
                {
                    message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(value);
                }
                continue;
            }

            if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                if (message.Content is not null && long.TryParse(value, out var len))
                {
                    message.Content.Headers.ContentLength = len;
                }
                continue;
            }

            if (!message.Headers.TryAddWithoutValidation(name, value))
            {
                message.Content?.Headers.TryAddWithoutValidation(name, value);
            }
        }
    }

    private static IReadOnlyDictionary<string, string> BuildResponseHeaders(HttpResponseMessage response)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in response.Headers)
        {
            dict[h.Key] = string.Join(", ", h.Value);
        }
        if (response.Content is not null)
        {
            foreach (var h in response.Content.Headers)
            {
                dict[h.Key] = string.Join(", ", h.Value);
            }
        }
        return dict;
    }

    private static T? DeserializeBody<T>(string rawBody, HttpResponseMessage response, JsonSerializerOptions options)
    {
        if (typeof(T) == typeof(SharpXRaw))
        {
            return (T)(object)new SharpXRaw(rawBody);
        }

        if (typeof(T) == typeof(byte[]))
        {
            return (T)(object)System.Text.Encoding.UTF8.GetBytes(rawBody);
        }

        if (string.IsNullOrEmpty(rawBody))
        {
            if (typeof(T) == typeof(string))
            {
                return (T)(object)string.Empty;
            }
            return default;
        }

        var contentType = response.Content?.Headers.ContentType?.MediaType ?? string.Empty;
        var looksJson = contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || (rawBody.Length > 0 && (rawBody[0] is '{' or '[' or '"' || char.IsDigit(rawBody[0])));

        if (typeof(T) == typeof(string))
        {
            if (looksJson && rawBody.Length > 0 && rawBody[0] == '"')
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(rawBody, options);
                }
                catch (JsonException)
                {
                    return (T)(object)rawBody;
                }
            }
            return (T)(object)rawBody;
        }

        return JsonSerializer.Deserialize<T>(rawBody, options);
    }

    private static bool DefaultStatusValidator(int status) => status >= 200 && status < 300;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}

/// <summary>Sentinel type that, when used as the response type, returns the raw body verbatim.</summary>
public sealed class SharpXRaw
{
    /// <summary>The raw body string.</summary>
    public string Body { get; }

    /// <summary>Creates a new <see cref="SharpXRaw"/>.</summary>
    public SharpXRaw(string body) => Body = body ?? string.Empty;
}
