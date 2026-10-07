namespace Anime.Application.DTOs.Browse;

/// <summary>
/// Query params của GET /api/v1/browse.
/// </summary>
public sealed class BrowseRequestDto
{
    /// <summary>TRENDING, POPULAR, SEASON, NEW, AIRING, FINISHED, MOVIE.</summary>
    public string? Tab { get; set; }

    /// <summary>Chỉ dùng cho tab SEASON (mặc định mùa hiện tại).</summary>
    public string? Season { get; set; }

    /// <summary>Chỉ dùng cho tab SEASON (mặc định năm hiện tại).</summary>
    public int? SeasonYear { get; set; }

    public int Limit { get; set; } = 24;

    /// <summary>Vị trí trong danh sách nguồn (lấy từ <c>nextOffset</c> của response trước).</summary>
    public int Offset { get; set; }
}
