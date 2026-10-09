using Anime.Application.Common;
using Anime.Application.Common.Validation;
using Anime.Application.DTOs.Autocomplete;
using Anime.Application.Interfaces;

namespace Anime.Application.Services;

public sealed class AutocompleteService(IAniMapperClient aniMapperClient, ICatalogStore catalogStore) : IAutocompleteService
{
    private const int MaxLimit = 10;

    public async Task<AutocompleteResponseDto> GetSuggestionsAsync(
        AutocompleteRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalized = new AutocompleteRequestDto
        {
            Q = Guard.Required(request.Q, "q"),
            MediaType = Guard.OneOf(request.MediaType, "mediaType", MediaEnums.MediaTypes),
            Locale = string.IsNullOrWhiteSpace(request.Locale) ? null : request.Locale.Trim().ToLowerInvariant(),
            Limit = Guard.InRange(request.Limit, 1, MaxLimit, "limit")
        };

        var response = await aniMapperClient.AutocompleteAsync(normalized, cancellationToken);

        // Chỉ gợi ý media đã có nguồn phát và không bị admin ẩn.
        var streamableIds = await StreamAvailability.GetStreamableIdsAsync(
            aniMapperClient, catalogStore, response.Suggestions.Select(s => s.Id), cancellationToken);
        response.Suggestions = response.Suggestions.Where(s => streamableIds.Contains(s.Id)).ToList();

        return response;
    }

    public Task<string> GetStatusAsync(CancellationToken cancellationToken = default)
        => aniMapperClient.GetAutocompleteStatusAsync(cancellationToken);
}
