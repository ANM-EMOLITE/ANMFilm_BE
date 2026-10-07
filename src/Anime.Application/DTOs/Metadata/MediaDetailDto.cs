using Anime.Application.DTOs.Common;

namespace Anime.Application.DTOs.Metadata;

public sealed class MediaDetailDto
{
    public int Id { get; set; }

    public string MediaType { get; set; } = string.Empty;

    public string? Format { get; set; }

    public string? Status { get; set; }

    public string? Source { get; set; }

    public string? CountryOfOrigin { get; set; }

    public string? StartDate { get; set; }

    public string? EndDate { get; set; }

    public string? Season { get; set; }

    public int? SeasonYear { get; set; }

    public int? TotalUnits { get; set; }

    public int? UnitDurationMin { get; set; }

    public string? Hashtag { get; set; }

    /// <summary>Unix timestamp (ms).</summary>
    public long? CreatedAt { get; set; }

    /// <summary>Unix timestamp (ms).</summary>
    public long? UpdatedAt { get; set; }

    public LocalizedText? Titles { get; set; }

    public LocalizedText? Descriptions { get; set; }

    public MediaImagesDto? Images { get; set; }

    public TrailerDto? Trailer { get; set; }

    public List<MediaRelationDto>? Relations { get; set; }

    public List<GenreDto>? Genres { get; set; }

    public List<StudioDto>? Studios { get; set; }

    public List<TagDto>? Tags { get; set; }

    public List<ExternalIdDto>? ExternalIds { get; set; }

    /// <summary>Danh sách tập (anime) hoặc chương (manga).</summary>
    public List<MediaUnitDto>? Units { get; set; }

    /// <summary>Key là tên provider (vd: ANIMEVIETSUB).</summary>
    public Dictionary<string, StreamingProviderMappingDto>? StreamingProviders { get; set; }
}

public sealed class TrailerDto
{
    public string? Site { get; set; }

    public string? TrailerId { get; set; }

    public string? Thumbnail { get; set; }
}

public sealed class MediaRelationDto
{
    public int RelatedMediaId { get; set; }

    public string? RelationType { get; set; }
}

public sealed class GenreDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class StudioDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsAnimationStudio { get; set; }

    public bool IsMain { get; set; }
}

public sealed class TagDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public sealed class MediaUnitDto
{
    public int Id { get; set; }

    /// <summary>EPISODE hoặc CHAPTER.</summary>
    public string? UnitKind { get; set; }

    public int? SeasonNumber { get; set; }

    public double? Number { get; set; }

    public double? AbsoluteNumber { get; set; }

    public string? ReleaseDate { get; set; }

    public int? DurationMinutes { get; set; }

    public string? ImageUrl { get; set; }

    public LocalizedText? Titles { get; set; }

    public LocalizedText? Descriptions { get; set; }

    public string? SeasonName { get; set; }

    public List<ExternalIdDto>? ExternalIds { get; set; }
}

public sealed class StreamingProviderMappingDto
{
    public string ProviderMediaId { get; set; } = string.Empty;

    /// <summary>Độ khớp 0-100.</summary>
    public double? Similarity { get; set; }

    /// <summary>AUTO hoặc MANUAL.</summary>
    public string? MappingType { get; set; }
}
