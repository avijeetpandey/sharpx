namespace SharpX;

/// <summary>
/// HTTP method enumeration used by <see cref="SharpXRequestConfig"/>.
/// </summary>
public enum SharpXHttpMethod
{
    Get,
    Post,
    Put,
    Delete,
    Patch,
    Head,
    Options,
}

internal static class SharpXHttpMethodExtensions
{
    public static HttpMethod ToHttpMethod(this SharpXHttpMethod method) => method switch
    {
        SharpXHttpMethod.Get => HttpMethod.Get,
        SharpXHttpMethod.Post => HttpMethod.Post,
        SharpXHttpMethod.Put => HttpMethod.Put,
        SharpXHttpMethod.Delete => HttpMethod.Delete,
        SharpXHttpMethod.Patch => new HttpMethod("PATCH"),
        SharpXHttpMethod.Head => HttpMethod.Head,
        SharpXHttpMethod.Options => HttpMethod.Options,
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported HTTP method."),
    };
}
