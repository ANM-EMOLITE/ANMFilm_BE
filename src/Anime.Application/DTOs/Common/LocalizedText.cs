namespace Anime.Application.DTOs.Common;

/// <summary>
/// Text theo ngôn ngữ: { "en": "...", "ja": "...", "vi": "..." }.
/// Dùng Dictionary để không mất các locale khác mà AniMapper trả về.
/// </summary>
public sealed class LocalizedText : Dictionary<string, string?>
{
    public LocalizedText() : base(StringComparer.OrdinalIgnoreCase)
    {
    }
}
