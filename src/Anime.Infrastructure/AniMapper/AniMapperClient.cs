using System.Net;
using System.Text.Json;
using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.DTOs;
using Anime.Application.DTOs.Autocomplete;
using Anime.Application.DTOs.Metadata;
using Anime.Application.DTOs.Searchs;
using Anime.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Anime.Infrastructure.AniMapper;

public sealed class AniMapperClient(HttpClient httpClient, ILogger<AniMapperClient> logger) : IAniMapperClient
{
    private const string ApiPrefix = "api/v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ---------- Search ----------

    public Task<SearchResponseDto> SearchAsync(SearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var url = new QueryStringBuilder($"{ApiPrefix}/search")
            .Add("title", request.Title)
            .Add("season", request.Season)
            .Add("seasonYear", request.SeasonYear)
            .Add("startDateFrom", request.StartDateFrom)
            .Add("startDateTo", request.StartDateTo)
            .Add("endDateFrom", request.EndDateFrom)
            .Add("endDateTo", request.EndDateTo)
            .Add("status", request.Status)
            .Add("format", request.Format)
            .Add("mediaType", request.MediaType)
            .Add("countryOfOrigin", request.CountryOfOrigin)
            .Add("genreIds", request.GenreIds)
            .Add("tagIds", request.TagIds)
            .Add("sortBy", request.SortBy)
            .Add("sortOrder", request.SortOrder)
            .Add("limit", request.Limit)
            .Add("offset", request.Offset);

        return GetJsonAsync<SearchResponseDto>(url.ToString(), cancellationToken);
    }

    // ---------- Autocomplete ----------

    public Task<AutocompleteResponseDto> AutocompleteAsync(
        AutocompleteRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var url = new QueryStringBuilder($"{ApiPrefix}/autocomplete")
            .Add("q", request.Q)
            .Add("mediaType", request.MediaType)
            .Add("locale", request.Locale)
            .Add("limit", request.Limit);

        return GetJsonAsync<AutocompleteResponseDto>(url.ToString(), cancellationToken);
    }

    public Task<string> GetAutocompleteStatusAsync(CancellationToken cancellationToken = default)
        => GetStringAsync($"{ApiPrefix}/autocomplete/status", cancellationToken);

    // ---------- Metadata ----------

    public Task<MetadataResponseDto> GetMetadataAsync(string id, CancellationToken cancellationToken = default)
    {
        var url = new QueryStringBuilder($"{ApiPrefix}/metadata").Add("id", id);
        return GetJsonAsync<MetadataResponseDto>(url.ToString(), cancellationToken);
    }

    public Task<MetadataBatchResponseDto> GetMetadataBatchAsync(string ids, CancellationToken cancellationToken = default)
    {
        var url = new QueryStringBuilder($"{ApiPrefix}/metadata/batch").Add("ids", ids);
        return GetJsonAsync<MetadataBatchResponseDto>(url.ToString(), cancellationToken);
    }

    // ---------- Streaming ----------

    public Task<EpisodesResponseDto> GetEpisodesAsync(
        EpisodesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var url = new QueryStringBuilder($"{ApiPrefix}/stream/episodes")
            .Add("id", request.Id)
            .Add("provider", request.Provider)
            .Add("limit", request.Limit)
            .Add("offset", request.Offset)
            .Add("server", request.Server);

        return GetJsonAsync<EpisodesResponseDto>(url.ToString(), cancellationToken);
    }

    public Task<ServersResponseDto> GetServersAsync(
        int id,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var url = new QueryStringBuilder($"{ApiPrefix}/stream/episodes/servers")
            .Add("id", id)
            .Add("provider", provider);

        return GetJsonAsync<ServersResponseDto>(url.ToString(), cancellationToken);
    }

    public Task<StreamSourceDto> GetSourceAsync(
        SourceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var url = new QueryStringBuilder($"{ApiPrefix}/stream/source")
            .Add("episodeData", request.EpisodeData)
            .Add("provider", request.Provider)
            .Add("server", request.Server);

        return GetJsonAsync<StreamSourceDto>(url.ToString(), cancellationToken);
    }

    public Task<string> GetM3u8Async(string cacheKey, CancellationToken cancellationToken = default)
        => GetStringAsync(
            $"{ApiPrefix}/stream/source/m3u8/{Uri.EscapeDataString(cacheKey)}",
            cancellationToken);

    // ---------- Helpers ----------

    private async Task<T> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        var body = await GetStringAsync(path, cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions)
                ?? throw new JsonException("Response body is null");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Cannot deserialize AniMapper response from {Path}", path);
            throw new UpstreamApiException(
                (int)HttpStatusCode.BadGateway,
                ErrorCodes.UpstreamError,
                "AniMapper returned an invalid JSON response",
                ex);
        }
    }

    private async Task<string> GetStringAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
            return body;

        throw CreateUpstreamException(path, response.StatusCode, body);
    }

    /// <summary>
    /// Đọc lỗi { success, message, code } của AniMapper.
    /// Lỗi 4xx (400, 404, 429) giữ nguyên status cho client; lỗi 5xx chuyển thành 502.
    /// </summary>
    private UpstreamApiException CreateUpstreamException(string path, HttpStatusCode statusCode, string body)
    {
        var status = (int)statusCode;
        var error = TryParseError(body);

        logger.LogWarning(
            "AniMapper {Path} returned {StatusCode}: {Body}",
            path, status, body.Length > 500 ? body[..500] : body);

        if (status is >= 400 and < 500)
        {
            var code = error?.Code ?? (statusCode switch
            {
                HttpStatusCode.NotFound => ErrorCodes.ResourceNotFound,
                HttpStatusCode.TooManyRequests => ErrorCodes.RateLimited,
                _ => ErrorCodes.InvalidParameter
            });

            return new UpstreamApiException(status, code, error?.Message ?? $"AniMapper returned {status}");
        }

        return new UpstreamApiException(
            (int)HttpStatusCode.BadGateway,
            ErrorCodes.UpstreamError,
            error?.Message ?? $"AniMapper returned {status}");
    }

    private static UpstreamError? TryParseError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            return JsonSerializer.Deserialize<UpstreamError>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record UpstreamError(string? Message, string? Code);
}
