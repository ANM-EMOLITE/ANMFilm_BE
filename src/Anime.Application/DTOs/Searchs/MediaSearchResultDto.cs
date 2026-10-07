using Anime.Application.DTOs.Common;

namespace Anime.Application.DTOs.Searchs;

public sealed class MediaSearchResultDto
{
    public int Id { get; set; }

    public string MediaType { get; set; } = string.Empty;

    public LocalizedText Titles { get; set; } = new();

    public MediaImagesDto Images { get; set; } = new();

    public string? Status { get; set; }

    public string? Season { get; set; }

    public int? SeasonYear { get; set; }

    public string? StartDate { get; set; }

    public string? Format { get; set; }

    /// <summary>Tổng số tập (chỉ có ở /browse).</summary>
    public int? TotalUnits { get; set; }
}
