using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.Common.Validation;
using Anime.Application.DTOs;
using Anime.Application.Interfaces;

namespace Anime.Infrastructure.Streaming;

public sealed partial class StreamProxyService(IHttpClientFactory httpClientFactory) : IStreamProxyService
{
    public const string HttpClientName = "StreamProxy";

    /// <summary>Header không được phép forward.</summary>
    private static readonly HashSet<string> BlockedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Content-Length", "Connection", "Transfer-Encoding", "Cookie", "Authorization"
    };

    [GeneratedRegex("URI=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex UriAttributeRegex();

    public async Task<ProxyResponse> FetchAsync(
        string? url,
        string? encodedHeaders,
        string? range,
        string proxyPath,
        CancellationToken cancellationToken = default)
    {
        var target = ValidateUrl(Guard.Required(url, "url"));
        var headers = DecodeHeaders(encodedHeaders);

        using var request = new HttpRequestMessage(HttpMethod.Get, target);

        foreach (var (key, value) in headers)
        {
            if (!BlockedHeaders.Contains(key))
                request.Headers.TryAddWithoutValidation(key, value);
        }

        if (!string.IsNullOrWhiteSpace(range))
            request.Headers.TryAddWithoutValidation("Range", range);

        var client = httpClientFactory.CreateClient(HttpClientName);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        try
        {
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                throw new UpstreamApiException(
                    status is >= 400 and < 500 ? status : (int)HttpStatusCode.BadGateway,
                    ErrorCodes.UpstreamError,
                    $"Stream source returned {status} for {target.Host}");
            }

            var finalUri = response.RequestMessage?.RequestUri ?? target;
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

            if (IsPlaylist(finalUri, contentType))
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                response.Dispose();

                if (text.TrimStart().StartsWith("#EXTM3U", StringComparison.Ordinal))
                {
                    return new ProxyResponse
                    {
                        ContentType = "application/vnd.apple.mpegurl",
                        Text = RewritePlaylist(text, finalUri, encodedHeaders, proxyPath)
                    };
                }

                return new ProxyResponse { ContentType = contentType, Text = text };
            }

            return new ProxyResponse
            {
                StatusCode = (int)response.StatusCode,
                ContentType = contentType,
                Body = await response.Content.ReadAsStreamAsync(cancellationToken),
                ContentLength = response.Content.Headers.ContentLength,
                ContentRange = response.Content.Headers.ContentRange?.ToString(),
                AcceptRanges = response.Headers.AcceptRanges.Count > 0
                    ? string.Join(",", response.Headers.AcceptRanges)
                    : null,
                Owner = response
            };
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static bool IsPlaylist(Uri uri, string contentType)
        => contentType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase)
           || uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
           || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Đổi mọi link (segment, playlist con, key mã hóa) trong m3u8 thành link đi qua proxy.
    /// </summary>
    private static string RewritePlaylist(string playlist, Uri baseUri, string? encodedHeaders, string proxyPath)
    {
        string ToProxy(string link)
        {
            var absolute = new Uri(baseUri, link.Trim()).ToString();
            var proxied = $"{proxyPath}?url={Uri.EscapeDataString(absolute)}";

            return string.IsNullOrEmpty(encodedHeaders)
                ? proxied
                : $"{proxied}&h={Uri.EscapeDataString(encodedHeaders)}";
        }

        var sb = new StringBuilder(playlist.Length * 2);

        foreach (var rawLine in playlist.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.Length == 0)
                sb.Append('\n');
            else if (line.StartsWith('#'))
                sb.Append(UriAttributeRegex().Replace(line, m => $"URI=\"{ToProxy(m.Groups[1].Value)}\"")).Append('\n');
            else
                sb.Append(ToProxy(line)).Append('\n');
        }

        return sb.ToString();
    }

    private static Dictionary<string, string> DecodeHeaders(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            return [];

        try
        {
            var base64 = encoded.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new ValidationException("Invalid h: must be base64url-encoded JSON object of headers");
        }
    }

    /// <summary>Chỉ cho phép http/https tới host public (tránh dùng proxy để gọi vào mạng nội bộ).</summary>
    private static Uri ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ValidationException("url must be an absolute http/https URL");

        if (uri.IsLoopback ||
            uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(uri.Host, out var ip) && IsPrivate(ip)))
            throw new ValidationException("url host is not allowed");

        return uri;
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal;

        var b = ip.GetAddressBytes();
        return b[0] == 10
               || b[0] == 0
               || (b[0] == 172 && b[1] is >= 16 and <= 31)
               || (b[0] == 192 && b[1] == 168)
               || (b[0] == 169 && b[1] == 254);
    }
}
