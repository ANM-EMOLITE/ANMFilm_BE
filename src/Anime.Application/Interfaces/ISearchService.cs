using Anime.Application.DTOs.Searchs;

namespace Anime.Application.Interfaces;

public interface ISearchService
{
    Task<SearchResponseDto> SearchAsync(SearchRequestDto request, CancellationToken cancellationToken = default);
}
