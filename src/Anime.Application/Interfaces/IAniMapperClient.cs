using Anime.Application.DTOs;
using Anime.Application.DTOs.Autocomplete;
using Anime.Application.DTOs.Metadata;
using Anime.Application.DTOs.Searchs;

namespace Anime.Application.Interfaces;

/// <summary>
/// Client gọi AniMapper upstream. Tham số truyền vào đã được validate ở tầng service.
/// </summary>
public interface IAniMapperClient
{
    // Search
    Task<SearchResponseDto> SearchAsync(SearchRequestDto request, CancellationToken cancellationToken = default);

    // Autocomplete
    Task<AutocompleteResponseDto> AutocompleteAsync(AutocompleteRequestDto request, CancellationToken cancellationToken = default);

    Task<string> GetAutocompleteStatusAsync(CancellationToken cancellationToken = default);

    // Metadata
    Task<MetadataResponseDto> GetMetadataAsync(string id, CancellationToken cancellationToken = default);

    Task<MetadataBatchResponseDto> GetMetadataBatchAsync(string ids, CancellationToken cancellationToken = default);

    // Streaming
    Task<EpisodesResponseDto> GetEpisodesAsync(EpisodesRequestDto request, CancellationToken cancellationToken = default);

    Task<ServersResponseDto> GetServersAsync(int id, string provider, CancellationToken cancellationToken = default);

    Task<StreamSourceDto> GetSourceAsync(SourceRequestDto request, CancellationToken cancellationToken = default);

    Task<string> GetM3u8Async(string cacheKey, CancellationToken cancellationToken = default);
}
