namespace Anime.Application.Common;

/// <summary>
/// Các giá trị hợp lệ cho bộ lọc media (theo tài liệu AniMapper).
/// </summary>
public static class MediaEnums
{
    public static readonly IReadOnlyCollection<string> Seasons =
        ["WINTER", "SPRING", "SUMMER", "FALL"];

    public static readonly IReadOnlyCollection<string> Statuses =
        ["RELEASING", "FINISHED", "NOT_YET_RELEASED", "CANCELLED", "HIATUS"];

    public static readonly IReadOnlyCollection<string> Formats =
        ["TV", "TV_SHORT", "MOVIE", "SPECIAL", "OVA", "ONA", "MUSIC", "MANGA", "NOVEL", "ONE_SHOT"];

    public static readonly IReadOnlyCollection<string> MediaTypes =
        ["ANIME", "MANGA"];

    public static readonly IReadOnlyCollection<string> SortFields =
        ["ID", "UPDATED_AT", "START_DATE", "SEASON_YEAR", "POPULARITY"];

    public static readonly IReadOnlyCollection<string> SortOrders =
        ["ASC", "DESC"];
}
