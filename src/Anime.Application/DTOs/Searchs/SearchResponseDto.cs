namespace Anime.Application.DTOs.Searchs;

public sealed class SearchResponseDto
{
    public bool Success { get; set; }

    public List<MediaSearchResultDto> Results { get; set; } = [];

    /// <summary>Tổng số media upstream (trước khi lọc theo nguồn phát).</summary>
    public int Total { get; set; }

    public int Limit { get; set; }

    /// <summary>Vị trí bắt đầu trong danh sách upstream.</summary>
    public int Offset { get; set; }

    /// <summary>Offset để lấy trang tiếp theo (null nếu hết).</summary>
    public int? NextOffset { get; set; }

    public bool HasNextPage { get; set; }
}
