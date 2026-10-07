namespace Anime.Application.DTOs.Metadata;

/// <summary>Response của GET /api/v1/metadata.</summary>
public sealed class MetadataResponseDto
{
    public bool Success { get; set; }

    public MediaDetailDto? Result { get; set; }
}

/// <summary>Response của GET /api/v1/metadata/batch.</summary>
public sealed class MetadataBatchResponseDto
{
    public bool Success { get; set; }

    public List<MediaDetailDto> Results { get; set; } = [];

    /// <summary>Số media tìm thấy. Media không tồn tại sẽ bị bỏ qua.</summary>
    public int Found { get; set; }

    /// <summary>Số ID đã yêu cầu.</summary>
    public int Requested { get; set; }
}
