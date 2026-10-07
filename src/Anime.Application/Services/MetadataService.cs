using Anime.Application.Common;
using Anime.Application.Common.Validation;
using Anime.Application.DTOs.Metadata;
using Anime.Application.Interfaces;

namespace Anime.Application.Services;

public sealed class MetadataService(IAniMapperClient aniMapperClient) : IMetadataService
{
    public Task<MetadataResponseDto> GetByIdAsync(string? id, CancellationToken cancellationToken = default)
    {
        var normalizedId = Guard.Required(id, "id");
        return aniMapperClient.GetMetadataAsync(normalizedId, cancellationToken);
    }

    public Task<MetadataBatchResponseDto> GetBatchAsync(string? ids, CancellationToken cancellationToken = default)
    {
        Guard.Required(ids, "ids");
        var normalizedIds = Guard.IdList(ids, "ids", ErrorCodes.InvalidId)!;

        return aniMapperClient.GetMetadataBatchAsync(normalizedIds, cancellationToken);
    }
}
