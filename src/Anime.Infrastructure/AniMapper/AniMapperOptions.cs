namespace Anime.Infrastructure.AniMapper;

public sealed class AniMapperOptions
{
    public const string SectionName = "AniMapper";

    /// <summary>Base URL của AniMapper (không gồm /api/v1).</summary>
    public string BaseUrl { get; set; } = "https://api.animapper.net/";

    public int TimeoutSeconds { get; set; } = 30;

    public string UserAgent { get; set; } = "AnimeStreamingApi/1.0";
}
