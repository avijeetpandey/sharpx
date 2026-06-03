namespace SharpX;

/// <summary>
/// Options used when creating a <see cref="SharpXClient"/> via <see cref="SharpXClient.Create(SharpXClientOptions)"/>.
/// </summary>
public sealed class SharpXClientOptions
{
    /// <summary>Default base URL prepended to relative URLs.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Default timeout applied to every request unless overridden per-call.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>Default headers added to every request.</summary>
    public IDictionary<string, string> DefaultHeaders { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Default status validator. Defaults to "2xx is success".</summary>
    public ValidateStatusDelegate? ValidateStatus { get; set; }

    /// <summary>Optional <see cref="HttpMessageHandler"/> override (useful for tests or custom transport).</summary>
    public HttpMessageHandler? HttpMessageHandler { get; set; }

    /// <summary>When true, the supplied <see cref="HttpMessageHandler"/> is disposed with the client.</summary>
    public bool DisposeHandler { get; set; } = true;

    /// <summary>Optional default JSON options.</summary>
    public System.Text.Json.JsonSerializerOptions? JsonOptions { get; set; }

    /// <summary>Maximum number of automatic redirects. Defaults to 50.</summary>
    public int MaxAutomaticRedirects { get; set; } = 50;

    /// <summary>If false, the client will not follow redirects automatically.</summary>
    public bool AllowAutoRedirect { get; set; } = true;
}
