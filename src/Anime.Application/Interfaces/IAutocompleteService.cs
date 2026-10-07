using Anime.Application.DTOs.Autocomplete;

namespace Anime.Application.Interfaces;

public interface IAutocompleteService
{
    Task<AutocompleteResponseDto> GetSuggestionsAsync(AutocompleteRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Trả về JSON thô trạng thái autocomplete của AniMapper.</summary>
    Task<string> GetStatusAsync(CancellationToken cancellationToken = default);
}
