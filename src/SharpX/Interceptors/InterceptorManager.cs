namespace SharpX.Interceptors;

/// <summary>
/// Receives a <see cref="SharpXRequestConfig"/> and returns a (possibly mutated) configuration before the request is sent.
/// </summary>
public delegate Task<SharpXRequestConfig> RequestInterceptorDelegate(SharpXRequestConfig config, CancellationToken cancellationToken);

/// <summary>
/// Receives a non-typed response wrapper and may mutate it (or trigger side effects) before it is returned to the caller.
/// </summary>
public delegate Task<SharpXResponseEnvelope> ResponseInterceptorDelegate(SharpXResponseEnvelope response, CancellationToken cancellationToken);

/// <summary>
/// Optional error interceptor invoked when a downstream stage throws.
/// Implementations may rethrow, return a substitute envelope, or wrap the exception.
/// </summary>
public delegate Task<SharpXResponseEnvelope?> ErrorInterceptorDelegate(Exception exception, SharpXRequestConfig config, CancellationToken cancellationToken);

/// <summary>
/// Untyped response envelope used by interceptors. Holds the raw HttpResponseMessage and a buffered string body.
/// </summary>
public sealed class SharpXResponseEnvelope
{
    /// <summary>The underlying response (already read into <see cref="RawBody"/>).</summary>
    public HttpResponseMessage Message { get; }

    /// <summary>The raw response body string.</summary>
    public string RawBody { get; set; }

    /// <summary>The originating request configuration.</summary>
    public SharpXRequestConfig RequestConfig { get; }

    /// <summary>Creates a new envelope.</summary>
    public SharpXResponseEnvelope(HttpResponseMessage message, string rawBody, SharpXRequestConfig requestConfig)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        RawBody = rawBody ?? string.Empty;
        RequestConfig = requestConfig ?? throw new ArgumentNullException(nameof(requestConfig));
    }
}

/// <summary>
/// Identifier returned by <see cref="InterceptorManager{T}.Use"/> so interceptors can be removed.
/// </summary>
public readonly struct InterceptorHandle : IEquatable<InterceptorHandle>
{
    internal int Id { get; }

    internal InterceptorHandle(int id)
    {
        Id = id;
    }

    /// <inheritdoc />
    public bool Equals(InterceptorHandle other) => Id == other.Id;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is InterceptorHandle h && Equals(h);

    /// <inheritdoc />
    public override int GetHashCode() => Id;

    /// <summary>Equality operator.</summary>
    public static bool operator ==(InterceptorHandle a, InterceptorHandle b) => a.Equals(b);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(InterceptorHandle a, InterceptorHandle b) => !a.Equals(b);
}

/// <summary>
/// Generic ordered registry that mimics axios <c>interceptors.use</c> / <c>eject</c>.
/// </summary>
public sealed class InterceptorManager<T> where T : Delegate
{
    private readonly List<(int Id, T Fulfilled, ErrorInterceptorDelegate? Rejected)> _items = new();
    private int _nextId;
    private readonly object _lock = new();

    /// <summary>Registers a new interceptor and returns a handle that can be passed to <see cref="Eject"/>.</summary>
    public InterceptorHandle Use(T onFulfilled, ErrorInterceptorDelegate? onRejected = null)
    {
        if (onFulfilled is null)
        {
            throw new ArgumentNullException(nameof(onFulfilled));
        }

        lock (_lock)
        {
            var id = ++_nextId;
            _items.Add((id, onFulfilled, onRejected));
            return new InterceptorHandle(id);
        }
    }

    /// <summary>Removes an interceptor previously returned by <see cref="Use"/>. Returns true if removed.</summary>
    public bool Eject(InterceptorHandle handle)
    {
        lock (_lock)
        {
            var index = _items.FindIndex(t => t.Id == handle.Id);
            if (index < 0)
            {
                return false;
            }

            _items.RemoveAt(index);
            return true;
        }
    }

    /// <summary>Removes all interceptors.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }

    /// <summary>Returns a snapshot of registered interceptors in registration order.</summary>
    public IReadOnlyList<(T Fulfilled, ErrorInterceptorDelegate? Rejected)> Snapshot()
    {
        lock (_lock)
        {
            return _items.Select(i => (i.Fulfilled, i.Rejected)).ToArray();
        }
    }
}
