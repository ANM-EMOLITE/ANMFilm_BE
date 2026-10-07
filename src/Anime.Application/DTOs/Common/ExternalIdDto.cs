namespace Anime.Application.DTOs.Common;

public sealed class ExternalIdDto
{
    public string Source { get; set; } = string.Empty;

    public string? ExternalKey { get; set; }

    public long? ExternalInt { get; set; }
}
