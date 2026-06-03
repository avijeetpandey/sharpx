#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill so init-only setters compile on netstandard2.1.</summary>
    internal static class IsExternalInit
    {
    }
}
#endif
