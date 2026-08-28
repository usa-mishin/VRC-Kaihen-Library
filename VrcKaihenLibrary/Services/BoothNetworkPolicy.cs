using System;

namespace VrcKaihenLibrary.Services;

public static class BoothNetworkPolicy
{
    public static bool IsTrustedImageUri(Uri uri) =>
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && (uri.Host.Equals("booth.pximg.net", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("booth.pm", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".booth.pm", StringComparison.OrdinalIgnoreCase));

    public static string? FilterImageSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) return source;
        if (uri.IsFile) return source;
        if (uri.Scheme is "http" or "https") return IsTrustedImageUri(uri) ? source : null;
        return null;
    }
}
