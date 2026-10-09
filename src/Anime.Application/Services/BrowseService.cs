using Anime.Application.Common;
using Anime.Application.Common.Validation;
using Anime.Application.DTOs.Browse;
using Anime.Application.DTOs.Metadata;
using Anime.Application.DTOs.Searchs;
using Anime.Application.Interfaces;

namespace Anime.Application.Services;

/// <summary>
/// Danh sách anime theo tab.
/// – Danh sách đã được admin đồng bộ → đọc từ DB (nhanh, không phụ thuộc upstream).
/// – Chưa đồng bộ (vd: mùa cũ) → lấy thứ tự từ AniList, lọc qua AniMapper metadata/batch
///   để chỉ giữ anime đã có nguồn phát.
/// Anime bị admin ẩn luôn bị loại.
/// </summary>
public sealed class BrowseService(
    IAniListClient aniListClient,
    IAniMapperClient aniMapperClient,
    ICatalogStore catalogStore) : IBrowseService
{
    private const int MaxLimit = 50;

    /// <summary>Số item AniList mỗi trang (tối đa của AniList).</summary>
    private const int PageSize = 50;

    /// <summary>Số trang AniList tối đa quét cho một request.</summary>
    private const int MaxSourcePages = 4;

    public async Task<SearchResponseDto> BrowseAsync(BrowseRequestDto request, CancellationToken cancellationToken = default)
    {
        Guard.InRange(request.Limit, 1, MaxLimit, "limit");
        Guard.NotNegative(request.Offset, "offset");

        var list = CatalogLists.Resolve(request.Tab, request.Season, request.SeasonYear);

        var stored = await catalogStore.GetListPageAsync(list.Key, request.Offset, request.Limit, cancellationToken);
        if (stored is not null)
        {
            return new SearchResponseDto
            {
                Success = true,
                Results = stored.Results.ToList(),
                Total = stored.Total,
                Limit = request.Limit,
                Offset = request.Offset,
                NextOffset = stored.NextOffset,
                HasNextPage = stored.NextOffset is not null
            };
        }

        return await BrowseLiveAsync(list.Query, request.Limit, request.Offset, cancellationToken);
    }

    private async Task<SearchResponseDto> BrowseLiveAsync(
        AniListQuery query,
        int limit,
        int startOffset,
        CancellationToken cancellationToken)
    {
        var offset = startOffset;
        var results = new List<MediaSearchResultDto>();
        var hasMore = true;

        for (var i = 0; i < MaxSourcePages && hasMore && results.Count < limit; i++)
        {
            var page = offset / PageSize + 1;
            var skip = offset % PageSize;

            var source = await aniListClient.GetAnimeIdsAsync(query, page, PageSize, cancellationToken);
            var ids = source.Ids.Skip(skip).ToList();

            var streamable = await StreamAvailability.GetStreamableAsync(aniMapperClient, catalogStore, ids, cancellationToken);

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
            Offset = startOffset,
            NextOffset = hasMore ? offset : null,
            HasNextPage = hasMore
        };
    }

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
