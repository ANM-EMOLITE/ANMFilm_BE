namespace Anime.Application.DTOs.Autocomplete;

public sealed class AutocompleteResponseDto
{
    public bool Success { get; set; }

    public string Query { get; set; } = string.Empty;

    public List<AutocompleteSuggestionDto> Suggestions { get; set; } = [];
}

public sealed class AutocompleteSuggestionDto
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string MediaType { get; set; } = string.Empty;
}
