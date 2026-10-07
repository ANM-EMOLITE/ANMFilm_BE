namespace Anime.Infrastructure.AniMapper;

public sealed class AniMapperOptions
{
    public const string SectionName = "AniMapper";

    /// <summary>Base URL của AniMapper (không gồm /api/v1).</summary>
    public string BaseUrl { get; set; } = "https://api.animapper.net/";

    public int TimeoutSeconds { get; set; } = 30;

    public string UserAgent { get; set; } = "AnimeStreamingApi/1.0";

    /// <summary>Số request tối đa/phút gửi lên AniMapper (hạn mức của AniMapper là 60, chừa lại khoảng an toàn).</summary>
    public int RequestsPerMinute { get; set; } = 50;

    /// <summary>Số request được gửi dồn ngay lập tức trước khi bắt đầu giãn cách.</summary>
    public int BurstSize { get; set; } = 10;

    /// <summary>Chờ lượt quá thời gian này thì trả 429 cho client thay vì treo request.</summary>
    public int MaxQueueWaitSeconds { get; set; } = 15;
}
