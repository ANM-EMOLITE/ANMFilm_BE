using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Anime.Infrastructure.AniList;

/// <summary>
/// Gọi AniList GraphQL (https://graphql.anilist.co). AniList giới hạn ~30 request/phút
/// nên kết quả được cache trong bộ nhớ.
/// </summary>
public sealed class AniListClient(HttpClient httpClient, IMemoryCache cache, ILogger<AniListClient> logger) : IAniListClient
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string Query = """
        query ($page: Int, $perPage: Int, $sort: [MediaSort], $season: MediaSeason, $seasonYear: Int, $statusIn: [MediaStatus], $format: MediaFormat, $startDateGreater: FuzzyDateInt) {
          Page(page: $page, perPage: $perPage) {
            pageInfo { hasNextPage }
            media(type: ANIME, isAdult: false, sort: $sort, season: $season, seasonYear: $seasonYear, status_in: $statusIn, format: $format, startDate_greater: $startDateGreater) { id }
          }
        }
        """;

    public async Task<AniListIdPage> GetAnimeIdsAsync(
        AniListQuery query,
        int page,
        int perPage,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"anilist:{query.Sort}:{query.Season}:{query.SeasonYear}:{string.Join('|', query.StatusIn ?? [])}:{query.Format}:{query.StartDateGreater}:{page}:{perPage}";
        if (cache.TryGetValue(cacheKey, out AniListIdPage? cached) && cached is not null)
            return cached;

        var body = new
        {
            query = Query,
            variables = new
            {
                page,
                perPage,
                sort = new[] { query.Sort },
                season = query.Season,
                seasonYear = query.SeasonYear,
                statusIn = query.StatusIn,
                format = query.Format,
                startDateGreater = query.StartDateGreater
            }
        };

        using var response = await httpClient.PostAsJsonAsync(string.Empty, body, JsonOptions, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("AniList returned {StatusCode}: {Body}", (int)response.StatusCode, json.Length > 500 ? json[..500] : json);

            throw response.StatusCode == HttpStatusCode.TooManyRequests
                ? new UpstreamApiException(429, ErrorCodes.RateLimited, "AniList rate limit exceeded, please try again later")
                : new UpstreamApiException((int)HttpStatusCode.BadGateway, ErrorCodes.UpstreamError, $"AniList returned {(int)response.StatusCode}");
        }

        GraphQlResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GraphQlResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new UpstreamApiException((int)HttpStatusCode.BadGateway, ErrorCodes.UpstreamError, "AniList returned an invalid JSON response", ex);
        }

        var pageData = parsed?.Data?.Page
            ?? throw new UpstreamApiException((int)HttpStatusCode.BadGateway, ErrorCodes.UpstreamError, "AniList returned no data");

        var result = new AniListIdPage(
            pageData.Media?.Select(m => m.Id).ToList() ?? [],
            pageData.PageInfo?.HasNextPage ?? false);

        cache.Set(cacheKey, result, CacheDuration);
        return result;
    }

    private sealed record GraphQlResponse(GraphQlData? Data);

    private sealed record GraphQlData(GraphQlPage? Page);

    private sealed record GraphQlPage(GraphQlPageInfo? PageInfo, List<GraphQlMedia>? Media);

    private sealed record GraphQlPageInfo(bool HasNextPage);

    private sealed record GraphQlMedia(int Id);
}
