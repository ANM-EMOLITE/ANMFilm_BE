using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.Common.Validation;
using Anime.Application.DTOs.Metadata;
using Anime.Application.Interfaces;

namespace Anime.Application.Services;

public sealed class MetadataService(IAniMapperClient aniMapperClient, ICatalogStore catalogStore) : IMetadataService
{
    /// <summary>
    /// Anime bị ẩn → 404 như không tồn tại.
    /// Provider bị tắt (kiểm tra thấy lỗi / admin tắt) bị bỏ khỏi <c>streamingProviders</c>,
    /// provider đã kiểm tra chạy tốt được xếp lên đầu → trang xem tự dùng provider khác.
    /// </summary>
    public async Task<MetadataResponseDto> GetByIdAsync(string? id, CancellationToken cancellationToken = default)
    {
        var normalizedId = Guard.Required(id, "id");
        var response = await aniMapperClient.GetMetadataAsync(normalizedId, cancellationToken);

        if (response.Result is not { } media)
            return response;

        if ((await catalogStore.GetHiddenIdsAsync([media.Id], cancellationToken)).Count > 0)
            throw new ApiException(404, ErrorCodes.MediaNotFound, $"Media not found: {normalizedId}");

        var states = await catalogStore.GetProviderStatesAsync(media.Id, cancellationToken);
        if (states.Count == 0 || media.StreamingProviders is not { Count: > 0 } providers)
            return response;

        var ordered = providers
            .Where(p => !(states.TryGetValue(p.Key, out var s) && s.IsDisabled))
            .OrderBy(p => states.TryGetValue(p.Key, out var s) && s.IsOk ? 0 : 1)
            .ToList();

        // Không sửa object đang nằm trong cache (dùng chung cho mọi request) → tạo bản sao nông.
        return new MetadataResponseDto
        {
            Success = response.Success,
            Result = media.WithStreamingProviders(ordered.ToDictionary(p => p.Key, p => p.Value))
        };
    }

    public async Task<MetadataBatchResponseDto> GetBatchAsync(string? ids, CancellationToken cancellationToken = default)
    {
        Guard.Required(ids, "ids");
        var normalizedIds = Guard.IdList(ids, "ids", ErrorCodes.InvalidId)!;

        var response = await aniMapperClient.GetMetadataBatchAsync(normalizedIds, cancellationToken);

        var hidden = await catalogStore.GetHiddenIdsAsync(response.Results.Select(m => m.Id), cancellationToken);
        if (hidden.Count == 0)
            return response;

        var visible = response.Results.Where(m => !hidden.Contains(m.Id)).ToList();
        return new MetadataBatchResponseDto
        {
            Success = response.Success,
            Results = visible,
            Found = visible.Count,
            Requested = response.Requested
        };
    }
}
