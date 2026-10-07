namespace Anime.Application.DTOs.Searchs;

/// <summary>
/// Query params của GET /api/v1/search.
/// </summary>
public sealed class SearchRequestDto
{
    public string? Title { get; set; }

    public string? Season { get; set; }

    public int? SeasonYear { get; set; }

    public string? StartDateFrom { get; set; }

    public string? StartDateTo { get; set; }

    public string? EndDateFrom { get; set; }

    public string? EndDateTo { get; set; }

    public string? Status { get; set; }

    public string? Format { get; set; }

    public string? MediaType { get; set; }

    public string? CountryOfOrigin { get; set; }

    /// <summary>ID thể loại, phân cách bằng dấu phẩy (vd: 1,2,3).</summary>
    public string? GenreIds { get; set; }

    /// <summary>ID tag, phân cách bằng dấu phẩy (vd: 1,2,3).</summary>
    public string? TagIds { get; set; }

    public string? SortBy { get; set; }

    public string? SortOrder { get; set; }

    public int Limit { get; set; } = 20;

    public int Offset { get; set; }
}
