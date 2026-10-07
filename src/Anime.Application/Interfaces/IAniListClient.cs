namespace Anime.Application.Interfaces;

/// <summary>
/// Client gọi AniList GraphQL để lấy danh sách ID anime theo thứ tự (thịnh hành, phổ biến, theo mùa...).
/// ID AniList trùng với media ID của AniMapper.
/// </summary>
public interface IAniListClient
{
    Task<AniListIdPage> GetAnimeIdsAsync(AniListQuery query, int page, int perPage, CancellationToken cancellationToken = default);
}

/// <param name="Sort">Giá trị MediaSort của AniList (vd: TRENDING_DESC, POPULARITY_DESC).</param>
/// <param name="StatusIn">Giá trị MediaStatus của AniList (vd: RELEASING, FINISHED).</param>
/// <param name="StartDateGreater">Ngày bắt đầu sau (FuzzyDateInt: yyyyMMdd).</param>
public sealed record AniListQuery(
    string Sort,
    string? Season = null,
    int? SeasonYear = null,
    IReadOnlyList<string>? StatusIn = null,
    string? Format = null,
    int? StartDateGreater = null);

public sealed record AniListIdPage(IReadOnlyList<int> Ids, bool HasNextPage);
