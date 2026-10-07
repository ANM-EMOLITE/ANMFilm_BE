namespace Anime.Application.DTOs;

/// <summary>Query params của GET /api/v1/stream/episodes.</summary>
public sealed class EpisodesRequestDto
{
    /// <summary>AniList media ID.</summary>
    public int Id { get; set; }

    public string? Provider { get; set; }

    /// <summary>0 = lấy tất cả.</summary>
    public int Limit { get; set; }

    public int Offset { get; set; }

    public string? Server { get; set; }
}

/// <summary>Query params của GET /api/v1/stream/source.</summary>
public sealed class SourceRequestDto
{
    /// <summary>Giá trị episodeId lấy từ danh sách tập.</summary>
    public string? EpisodeData { get; set; }

    public string? Provider { get; set; }

    public string? Server { get; set; }

    /// <summary>true = bỏ qua cache, lấy link mới (link cũ đã hỏng).</summary>
    public bool Refresh { get; set; }
}
