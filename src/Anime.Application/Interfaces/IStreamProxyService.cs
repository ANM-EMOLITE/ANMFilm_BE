using Anime.Application.DTOs;

namespace Anime.Application.Interfaces;

/// <summary>
/// Proxy tải playlist HLS / segment video kèm header (Referer...) mà trình duyệt không tự gửi được.
/// </summary>
public interface IStreamProxyService
{
    /// <param name="url">URL tuyệt đối cần tải (http/https).</param>
    /// <param name="encodedHeaders">Header cần gửi kèm: JSON object, mã hóa base64url.</param>
    /// <param name="range">Header Range của client (để tua video).</param>
    /// <param name="proxyPath">Đường dẫn của endpoint proxy, dùng để rewrite link trong playlist.</param>
    Task<ProxyResponse> FetchAsync(
        string? url,
        string? encodedHeaders,
        string? range,
        string proxyPath,
        CancellationToken cancellationToken = default);
}
