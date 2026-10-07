namespace Anime.Application.DTOs;

public sealed record EpisodesResponseDto(
    string Provider,
    int Limit,
    int Offset,
    int Total,
    bool HasNextPage,
    IReadOnlyList<EpisodeDto> Episodes);
