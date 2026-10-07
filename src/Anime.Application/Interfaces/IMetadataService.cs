using Anime.Application.DTOs.Metadata;

namespace Anime.Application.Interfaces;

public interface IMetadataService
{
    /// <param name="id">Media ID, AniList ID hoặc AniList external key.</param>
    Task<MetadataResponseDto> GetByIdAsync(string? id, CancellationToken cancellationToken = default);

    /// <param name="ids">Danh sách ID phân cách bằng dấu phẩy.</param>
    Task<MetadataBatchResponseDto> GetBatchAsync(string? ids, CancellationToken cancellationToken = default);
}
