using Anime.Application.Common.Exceptions;
using Anime.Application.Common.Validation;
using Anime.Application.Interfaces;

namespace Anime.Application.Common;

/// <summary>Một danh sách cụ thể, vd: TRENDING hoặc SEASON:FALL:2026.</summary>
/// <param name="Key">Khóa lưu trong DB.</param>
/// <param name="Label">Tên hiển thị cho admin.</param>
public sealed record CatalogListDefinition(string Key, string Tab, string Label, AniListQuery Query);

/// <summary>
/// Định nghĩa các danh sách (tab) dùng chung cho /browse và đồng bộ của admin.
/// Thứ tự lấy từ AniList.
/// </summary>
public static class CatalogLists
{
    public static readonly IReadOnlyCollection<string> Tabs =
        ["TRENDING", "POPULAR", "SEASON", "NEW", "AIRING", "FINISHED", "MOVIE"];

    /// <summary>Đồng bộ tab SEASON = mùa hiện tại + N mùa trước.</summary>
    public const int SyncedSeasons = 4;

    private static readonly Dictionary<string, string> Labels = new()
    {
        ["TRENDING"] = "Thịnh hành",
        ["POPULAR"] = "Phổ biến",
        ["SEASON"] = "Theo mùa",
        ["NEW"] = "Phim mới",
        ["AIRING"] = "Đang chiếu",
        ["FINISHED"] = "Trọn bộ",
        ["MOVIE"] = "Phim lẻ"
    };

    private static readonly Dictionary<string, string> SeasonLabels = new()
    {
        ["WINTER"] = "Đông",
        ["SPRING"] = "Xuân",
        ["SUMMER"] = "Hạ",
        ["FALL"] = "Thu"
    };

    private static readonly string[] SeasonOrder = ["WINTER", "SPRING", "SUMMER", "FALL"];

    /// <summary>Danh sách ứng với 1 request /browse.</summary>
    public static CatalogListDefinition Resolve(string? tab, string? season, int? seasonYear, DateTime? now = null)
    {
        var normalizedTab = Guard.OneOf(tab, "tab", Tabs) ?? "TRENDING";
        var today = now ?? DateTime.UtcNow;

        if (normalizedTab != "SEASON")
            return Simple(normalizedTab, today);

        var (currentSeason, currentYear) = CurrentSeason(today);
        var s = Guard.OneOf(season, "season", MediaEnums.Seasons) ?? currentSeason;
        var y = seasonYear ?? currentYear;
        Guard.InRange(y, 1940, 2100, "seasonYear");

        return SeasonList(s, y);
    }

    /// <summary>Các danh sách cần đồng bộ cho 1 tab (SEASON → nhiều mùa).</summary>
    public static IReadOnlyList<CatalogListDefinition> ForSync(string tab, DateTime? now = null)
    {
        var today = now ?? DateTime.UtcNow;
        var normalizedTab = Guard.OneOf(tab, "lists", Tabs)
            ?? throw new ValidationException("lists cannot contain empty values");

        if (normalizedTab != "SEASON")
            return [Simple(normalizedTab, today)];

        var (season, year) = CurrentSeason(today);
        var index = year * 4 + Array.IndexOf(SeasonOrder, season);

        return Enumerable.Range(0, SyncedSeasons)
            .Select(i => index - i)
            .Select(i => SeasonList(SeasonOrder[i % 4], i / 4))
            .ToList();
    }

    public static string LabelOf(string key)
    {
        var parts = key.Split(':');
        if (parts is ["SEASON", var s, var y])
            return $"Mùa {SeasonLabels.GetValueOrDefault(s, s)} {y}";

        return Labels.GetValueOrDefault(key, key);
    }

    private static CatalogListDefinition Simple(string tab, DateTime today) => tab switch
    {
        "TRENDING" => new(tab, tab, Labels[tab], new AniListQuery("TRENDING_DESC")),
        "POPULAR" => new(tab, tab, Labels[tab], new AniListQuery("POPULARITY_DESC")),
        // Phim mới = sắp theo ngày ra mắt (mới nhất trước), trong 6 tháng gần đây.
        // Loại NOT_YET_RELEASED (chưa chiếu) để không chiếm chỗ bằng phim tương lai chưa có nguồn.
        // Khác POPULARITY_DESC cũ: phim vừa chiếu dù chưa nổi vẫn được kéo về.
        "NEW" => new(tab, tab, Labels[tab], new AniListQuery(
            "START_DATE_DESC",
            StatusIn: ["RELEASING", "FINISHED"],
            StartDateGreater: int.Parse(today.AddMonths(-6).ToString("yyyyMMdd")))),
        "AIRING" => new(tab, tab, Labels[tab], new AniListQuery("POPULARITY_DESC", StatusIn: ["RELEASING"])),
        "FINISHED" => new(tab, tab, Labels[tab], new AniListQuery("END_DATE_DESC", StatusIn: ["FINISHED"])),
        "MOVIE" => new(tab, tab, Labels[tab], new AniListQuery("POPULARITY_DESC", Format: "MOVIE")),
        _ => throw new ValidationException($"Invalid tab: {tab}")
    };

    private static CatalogListDefinition SeasonList(string season, int year)
    {
        var key = $"SEASON:{season}:{year}";
        return new(key, "SEASON", LabelOf(key), new AniListQuery("POPULARITY_DESC", Season: season, SeasonYear: year));
    }

    private static (string Season, int Year) CurrentSeason(DateTime now)
        => (SeasonOrder[(now.Month - 1) / 3], now.Year);
}
