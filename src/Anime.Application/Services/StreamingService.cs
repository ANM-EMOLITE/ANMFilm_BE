using Anime.Application.Common.Validation;
using Anime.Application.DTOs;
using Anime.Application.Interfaces;

namespace Anime.Application.Services;

public sealed class StreamingService(IAniMapperClient aniMapperClient) : IStreamingService
{
    public Task<EpisodesResponseDto> GetEpisodesAsync(
        EpisodesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalized = new EpisodesRequestDto
        {
            Id = Guard.Positive(request.Id, "id"),
            Provider = NormalizeProvider(request.Provider),
            Limit = Guard.NotNegative(request.Limit, "limit"),
            Offset = Guard.NotNegative(request.Offset, "offset"),
            Server = NullIfEmpty(request.Server)
        };

        return aniMapperClient.GetEpisodesAsync(normalized, cancellationToken);
    }

    public Task<ServersResponseDto> GetServersAsync(
        int id,
        string? provider,
        CancellationToken cancellationToken = default)
        => aniMapperClient.GetServersAsync(
            Guard.Positive(id, "id"),
            NormalizeProvider(provider),
            cancellationToken);

    public Task<StreamSourceDto> GetSourceAsync(
        SourceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalized = new SourceRequestDto
        {
            EpisodeData = Guard.Required(request.EpisodeData, "episodeData"),
            Provider = NormalizeProvider(request.Provider),
            Server = NullIfEmpty(request.Server)
        };

        return aniMapperClient.GetSourceAsync(normalized, cancellationToken);
    }

    public Task<string> GetM3u8Async(string? cacheKey, CancellationToken cancellationToken = default)
        => aniMapperClient.GetM3u8Async(Guard.Required(cacheKey, "cacheKey"), cancellationToken);

    private static string NormalizeProvider(string? provider)
        => Guard.Required(provider, "provider").ToUpperInvariant();

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
