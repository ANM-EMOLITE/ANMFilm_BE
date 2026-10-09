using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.Common.Validation;
using Anime.Application.DTOs.Searchs;
using Anime.Application.Interfaces;

namespace Anime.Application.Services;

public sealed class SearchService(IAniMapperClient aniMapperClient, ICatalogStore catalogStore) : ISearchService
{
    private const int MaxLimit = 100;

    /// <summary>Số trang upstream tối đa quét cho một request (mỗi trang = 1 search + 1 metadata/batch).</summary>
    private const int MaxUpstreamPages = 5;

    /// <summary>
    /// Chỉ trả về media đã có nguồn phát. Vì phần lớn media upstream không có provider,
    /// quét nhiều trang upstream (mỗi trang 100) cho tới khi đủ <c>limit</c>.
    /// <c>Offset</c>/<c>NextOffset</c> là vị trí trong danh sách upstream: trang sau gọi lại với <c>offset = nextOffset</c>.
    /// </summary>
    public async Task<SearchResponseDto> SearchAsync(SearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var limit = normalized.Limit;
        var upstreamOffset = normalized.Offset;

        var results = new List<MediaSearchResultDto>();
        var total = 0;
        var hasMore = true;

        normalized.Limit = MaxLimit;

        for (var page = 0; page < MaxUpstreamPages && hasMore && results.Count < limit; page++)
        {
            normalized.Offset = upstreamOffset;
            var upstream = await aniMapperClient.SearchAsync(normalized, cancellationToken);
            total = upstream.Total;

            var streamableIds = await StreamAvailability.GetStreamableIdsAsync(
                aniMapperClient, catalogStore, upstream.Results.Select(r => r.Id), cancellationToken);

            var consumed = 0;
            foreach (var item in upstream.Results)
            {
                consumed++;
                if (streamableIds.Contains(item.Id))
                {
                    results.Add(item);
                    if (results.Count == limit)
                        break;
                }
            }

            upstreamOffset += consumed;
            hasMore = upstream.Results.Count > 0 && (consumed < upstream.Results.Count || upstream.HasNextPage);
        }

        return new SearchResponseDto
        {
            Success = true,
            Results = results,
            Total = total,
            Limit = limit,
            Offset = request.Offset,
            NextOffset = hasMore ? upstreamOffset : null,
            HasNextPage = hasMore
        };
    }

    /// <summary>
    /// Validate và chuẩn hóa request (enum -> UPPER, trim, chuẩn hóa danh sách ID).
    /// </summary>
    private static SearchRequestDto Normalize(SearchRequestDto request)
    {
        Guard.InRange(request.Limit, 1, MaxLimit, "limit");
        Guard.NotNegative(request.Offset, "offset");

        if (request.SeasonYear is { } year)
            Guard.InRange(year, 1900, 2100, "seasonYear");

        var startFrom = Guard.Date(request.StartDateFrom, "startDateFrom");
        var startTo = Guard.Date(request.StartDateTo, "startDateTo");
        var endFrom = Guard.Date(request.EndDateFrom, "endDateFrom");
        var endTo = Guard.Date(request.EndDateTo, "endDateTo");

        Guard.DateRange(startFrom, startTo, "startDateFrom", "startDateTo");
        Guard.DateRange(endFrom, endTo, "endDateFrom", "endDateTo");

        var country = request.CountryOfOrigin?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(country) && (country.Length != 2 || !country.All(char.IsAsciiLetter)))
            throw new ValidationException(
                $"Invalid countryOfOrigin: {request.CountryOfOrigin}. Must be a 2-letter country code (e.g. JP, KR, CN)");

        return new SearchRequestDto
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            Season = Guard.OneOf(request.Season, "season", MediaEnums.Seasons),
            SeasonYear = request.SeasonYear,
            StartDateFrom = request.StartDateFrom?.Trim(),
            StartDateTo = request.StartDateTo?.Trim(),
            EndDateFrom = request.EndDateFrom?.Trim(),
            EndDateTo = request.EndDateTo?.Trim(),
            Status = Guard.OneOf(request.Status, "status", MediaEnums.Statuses),
            Format = Guard.OneOf(request.Format, "format", MediaEnums.Formats),
            MediaType = Guard.OneOf(request.MediaType, "mediaType", MediaEnums.MediaTypes),
            CountryOfOrigin = string.IsNullOrEmpty(country) ? null : country,
            GenreIds = Guard.IdList(request.GenreIds, "genreIds"),
            TagIds = Guard.IdList(request.TagIds, "tagIds"),
            SortBy = Guard.OneOf(request.SortBy, "sortBy", MediaEnums.SortFields),
            SortOrder = Guard.OneOf(request.SortOrder, "sortOrder", MediaEnums.SortOrders),
            Limit = request.Limit,
            Offset = request.Offset
        };
    }
}
