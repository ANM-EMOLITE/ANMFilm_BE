namespace Anime.Application.DTOs.Autocomplete;

/// <summary>
/// Query params của GET /api/v1/autocomplete.
/// </summary>
public sealed class AutocompleteRequestDto
{
    /// <summary>Tiền tố cần gợi ý (bắt buộc, tối thiểu 1 ký tự).</summary>
    public string? Q { get; set; }

    /// <summary>ANIME hoặc MANGA.</summary>
    public string? MediaType { get; set; }

    /// <summary>Ngôn ngữ của tiêu đề trả về (en, ja, vi...).</summary>
    public string? Locale { get; set; }

    /// <summary>1-10, mặc định 5.</summary>
    public int Limit { get; set; } = 5;
}
