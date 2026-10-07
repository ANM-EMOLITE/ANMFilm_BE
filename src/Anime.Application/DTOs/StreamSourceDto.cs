namespace Anime.Application.DTOs;

/// <summary>
/// Nguồn stream. Type: HLS, EMBED hoặc DIRECT.
/// </summary>
public sealed record StreamSourceDto(
    string Server,
    string Type,
    bool CorsProxyRequired,
    IReadOnlyDictionary<string, string>? ProxyHeaders,
    string Url);
