using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.Common.Validation;
using Anime.Application.DTOs.Browse;
using Anime.Application.DTOs.Metadata;
using Anime.Application.DTOs.Searchs;
using Anime.Application.Interfaces;

namespace Anime.Application.Services;

/// <summary>
/// Danh sách anime theo tab. Thứ tự lấy từ AniList (sort/lọc chuẩn hơn AniMapper),
/// sau đó lọc qua AniMapper metadata/batch để chỉ giữ anime đã có nguồn phát.
/// </summary>
public sealed class BrowseService(IAniListClient aniListClient, IAniMapperClient aniMapperClient) : IBrowseService
{
    public static readonly IReadOnlyCollection<string> Tabs =
        ["TRENDING", "POPULAR", "SEASON", "NEW", "AIRING", "FINISHED", "MOVIE"];

    private const int MaxLimit = 50;

    /// <summary>Số item AniList mỗi trang (tối đa của AniList).</summary>
    private const int PageSize = 50;

    /// <summary>Số trang AniList tối đa quét cho một request.</summary>
    private const int MaxSourcePages = 4;

    public async Task<SearchResponseDto> BrowseAsync(BrowseRequestDto request, CancellationToken cancellationToken = default)
    {
        Guard.InRange(request.Limit, 1, MaxLimit, "limit");
        Guard.NotNegative(request.Offset, "offset");

        var query = BuildQuery(request);
        var limit = request.Limit;
        var offset = request.Offset;

        var results = new List<MediaSearchResultDto>();
        var hasMore = true;

        for (var i = 0; i < MaxSourcePages && hasMore && results.Count < limit; i++)
        {
            var page = offset / PageSize + 1;
            var skip = offset % PageSize;

            var source = await aniListClient.GetAnimeIdsAsync(query, page, PageSize, cancellationToken);
            var ids = source.Ids.Skip(skip).ToList();

            var streamable = await StreamAvailability.GetStreamableAsync(aniMapperClient, ids, cancellationToken);

            var consumed = 0;
            foreach (var id in ids)
            {
                consumed++;
                if (streamable.TryGetValue(id, out var media))
                {
                    results.Add(ToResult(media));
                    if (results.Count == limit)
                        break;
                }
            }

            offset += consumed;
            hasMore = ids.Count > 0 && (consumed < ids.Count || source.HasNextPage);
        }

        return new SearchResponseDto
        {
            Success = true,
            Results = results,
            Total = results.Count,
            Limit = limit,
            Offset = request.Offset,
            NextOffset = hasMore ? offset : null,
            HasNextPage = hasMore
        };
    }

    private static AniListQuery BuildQuery(BrowseRequestDto request)
    {
        var tab = Guard.OneOf(request.Tab, "tab", Tabs) ?? "TRENDING";

        return tab switch
        {
            "TRENDING" => new AniListQuery("TRENDING_DESC"),
            "POPULAR" => new AniListQuery("POPULARITY_DESC"),
            "SEASON" => BuildSeasonQuery(request),
            // Anime mới chiếu gần như chưa có nguồn phát, nên lấy anime ra mắt trong 6 tháng gần đây, sắp theo độ phổ biến.
            "NEW" => new AniListQuery(
                "POPULARITY_DESC",
                StartDateGreater: int.Parse(DateTime.UtcNow.AddMonths(-6).ToString("yyyyMMdd"))),
            "AIRING" => new AniListQuery("POPULARITY_DESC", StatusIn: ["RELEASING"]),
            "FINISHED" => new AniListQuery("END_DATE_DESC", StatusIn: ["FINISHED"]),
            "MOVIE" => new AniListQuery("POPULARITY_DESC", Format: "MOVIE"),
            _ => throw new ValidationException($"Invalid tab: {request.Tab}")
        };
    }

    private static AniListQuery BuildSeasonQuery(BrowseRequestDto request)
    {
        var (currentSeason, currentYear) = CurrentSeason(DateTime.UtcNow);

        var season = Guard.OneOf(request.Season, "season", MediaEnums.Seasons) ?? currentSeason;
        var year = request.SeasonYear ?? currentYear;
        Guard.InRange(year, 1940, 2100, "seasonYear");

        return new AniListQuery("POPULARITY_DESC", Season: season, SeasonYear: year);
    }

    private static (string Season, int Year) CurrentSeason(DateTime now) => now.Month switch
    {
        <= 3 => ("WINTER", now.Year),
        <= 6 => ("SPRING", now.Year),
        <= 9 => ("SUMMER", now.Year),
        _ => ("FALL", now.Year)
    };

    private static MediaSearchResultDto ToResult(MediaDetailDto media) => new()
    {
        Id = media.Id,
        MediaType = media.MediaType,
        Titles = media.Titles ?? new(),
        Images = media.Images ?? new(),
        Status = media.Status,
        Season = media.Season,
        SeasonYear = media.SeasonYear,
        StartDate = media.StartDate,
        Format = media.Format,
        TotalUnits = media.TotalUnits
    };
}
