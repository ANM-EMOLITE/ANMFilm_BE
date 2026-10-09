using Anime.Application.Common.Exceptions;
using Anime.Application.DTOs;
using Anime.Application.DTOs.Autocomplete;
using Anime.Application.DTOs.Metadata;
using Anime.Application.DTOs.Searchs;
using Anime.Application.Interfaces;

namespace Anime.Infrastructure.AniMapper;

/// <summary>
/// Bọc <see cref="AniMapperClient"/>: cache + gộp request trùng + trả bản dự phòng khi upstream lỗi,
/// và circuit breaker theo provider cho các endpoint streaming.
/// Mục tiêu: giữ trong hạn mức 60 request/phút của AniMapper và vẫn phục vụ được khi provider sập.
/// </summary>
public sealed class CachingAniMapperClient(
    AniMapperClient inner,
    AniMapperResponseCache cache,
    ProviderHealth providerHealth) : IAniMapperClient
{
    // ---------- Search / Autocomplete ----------

    public Task<SearchResponseDto> SearchAsync(SearchRequestDto request, CancellationToken cancellationToken = default)
        => cache.GetOrFetchAsync(
            Key("search", request.Title, request.Season, request.SeasonYear, request.StartDateFrom, request.StartDateTo,
                request.EndDateFrom, request.EndDateTo, request.Status, request.Format, request.MediaType,
                request.CountryOfOrigin, request.GenreIds, request.TagIds, request.SortBy, request.SortOrder,
                request.Limit, request.Offset),
            AniMapperCachePolicies.Search,
            ct => inner.SearchAsync(request, ct),
            cancellationToken);

    public Task<AutocompleteResponseDto> AutocompleteAsync(
        AutocompleteRequestDto request,
        CancellationToken cancellationToken = default)
        => cache.GetOrFetchAsync(
            Key("autocomplete", request.Q?.Trim().ToLowerInvariant(), request.MediaType, request.Locale, request.Limit),
            AniMapperCachePolicies.Autocomplete,
            ct => inner.AutocompleteAsync(request, ct),
            cancellationToken);

    public Task<string> GetAutocompleteStatusAsync(CancellationToken cancellationToken = default)
        => inner.GetAutocompleteStatusAsync(cancellationToken);

    // ---------- Metadata ----------

    public Task<MetadataResponseDto> GetMetadataAsync(string id, CancellationToken cancellationToken = default)
        => cache.GetOrFetchAsync(
            Key("metadata", id),
            AniMapperCachePolicies.Metadata,
            ct => inner.GetMetadataAsync(id, ct),
            cancellationToken);

    public Task<MetadataBatchResponseDto> GetMetadataBatchAsync(string ids, CancellationToken cancellationToken = default)
        => cache.GetOrFetchAsync(
            Key("metadata-batch", ids),
            AniMapperCachePolicies.Metadata,
            ct => inner.GetMetadataBatchAsync(ids, ct),
            cancellationToken);

    // ---------- Streaming ----------

    public Task<EpisodesResponseDto> GetEpisodesAsync(
        EpisodesRequestDto request,
        CancellationToken cancellationToken = default)
        => cache.GetOrFetchAsync(
            Key("episodes", request.Id, request.Provider, request.Server, request.Limit, request.Offset),
            AniMapperCachePolicies.Episodes,
            ct => GuardedAsync(request.Provider, () => inner.GetEpisodesAsync(request, ct)),
            cancellationToken);

    public Task<ServersResponseDto> GetServersAsync(int id, string provider, CancellationToken cancellationToken = default)
        => cache.GetOrFetchAsync(
            Key("servers", id, provider),
            AniMapperCachePolicies.Servers,
            ct => GuardedAsync(provider, () => inner.GetServersAsync(id, provider, ct)),
            cancellationToken);

    public Task<StreamSourceDto> GetSourceAsync(SourceRequestDto request, CancellationToken cancellationToken = default)
    {
        var key = SourceKey(request.EpisodeData, request.Provider, request.Server);
        if (request.Refresh)
            cache.Invalidate(key);

        return cache.GetOrFetchAsync(
            key,
            AniMapperCachePolicies.Source,
            ct => GuardedAsync(request.Provider, () => inner.GetSourceAsync(request, ct)),
            cancellationToken,
            allowStale: !request.Refresh);
    }

    public Task<string> GetM3u8Async(string cacheKey, CancellationToken cancellationToken = default)
        => cache.GetOrFetchAsync(
            Key("m3u8", cacheKey),
            AniMapperCachePolicies.M3u8,
            ct => inner.GetM3u8Async(cacheKey, ct),
            cancellationToken);

    // ---------- Helpers ----------

    /// <summary>Gọi upstream qua circuit breaker của provider.</summary>
    private async Task<T> GuardedAsync<T>(string? provider, Func<Task<T>> call)
    {
        if (string.IsNullOrEmpty(provider))
            return await call();

        providerHealth.ThrowIfUnavailable(provider);
        try
        {
            var result = await call();
            providerHealth.RecordSuccess(provider);
            return result;
        }
        catch (Exception ex) when (AniMapperResponseCache.IsTransient(ex) && ex is not ApiException { StatusCode: 429 })
        {
            // 429 là do hạn mức chung, không phải lỗi của provider.
            providerHealth.RecordFailure(provider);
            throw;
        }
    }

    /// <summary>Cache key của link stream 1 tập (dùng chung với job lưu sẵn nguồn phát).</summary>
    public static string SourceKey(string? episodeData, string? provider, string? server)
        => Key("source", episodeData, provider, server);

    private static string Key(string endpoint, params object?[] parts)
        => endpoint + "?" + string.Join('&', parts.Select(p => p?.ToString() ?? string.Empty));
}
