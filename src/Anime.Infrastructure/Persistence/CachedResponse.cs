namespace Anime.Infrastructure.Persistence;

/// <summary>
/// Bản lưu bền vững của một response AniMapper (metadata, servers, danh sách tập, link stream, m3u8...).
/// Dùng khi BE khởi động lại hoặc AniMapper/provider bị sập.
/// </summary>
public sealed class CachedResponse
{
    /// <summary>Cache key, vd: <c>episodes?154587&amp;ANIMEVIETSUB&amp;&amp;0&amp;0</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Loại endpoint: metadata, servers, episodes, source, m3u8...</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Response dạng JSON (jsonb).</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Thời điểm lấy từ AniMapper.</summary>
    public DateTimeOffset FetchedAt { get; set; }

    /// <summary>Sau thời điểm này bản lưu không còn được dùng và sẽ bị dọn.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
