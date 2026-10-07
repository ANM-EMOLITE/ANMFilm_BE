namespace Anime.Application.DTOs;

public sealed record ServersResponseDto(
    string Provider,
    IReadOnlyList<string> Servers);
