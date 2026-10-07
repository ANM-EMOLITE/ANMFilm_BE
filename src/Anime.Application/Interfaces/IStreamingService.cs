using Anime.Application.DTOs;

namespace Anime.Application.Interfaces;

public interface IStreamingService
{
    Task<EpisodesResponseDto> GetEpisodesAsync(EpisodesRequestDto request, CancellationToken cancellationToken = default);

    Task<ServersResponseDto> GetServersAsync(int id, string? provider, CancellationToken cancellationToken = default);

    Task<StreamSourceDto> GetSourceAsync(SourceRequestDto request, CancellationToken cancellationToken = default);

    Task<string> GetM3u8Async(string? cacheKey, CancellationToken cancellationToken = default);
}
