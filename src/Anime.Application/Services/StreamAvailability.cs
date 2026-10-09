using Anime.Application.DTOs.Metadata;
using Anime.Application.Interfaces;

namespace Anime.Application.Services;

/// <summary>
/// Kiểm tra media nào đã có nguồn phát (streamingProviders không rỗng) qua metadata/batch,
/// và loại các media bị admin ẩn.
/// </summary>
internal static class StreamAvailability
{
    /// <summary>Trả về metadata của các media có nguồn phát và không bị ẩn, key = media ID.</summary>
    public static async Task<Dictionary<int, MediaDetailDto>> GetStreamableAsync(
        IAniMapperClient aniMapperClient,
        ICatalogStore catalogStore,
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
            return [];

        var batch = await aniMapperClient.GetMetadataBatchAsync(string.Join(',', distinctIds), cancellationToken);

        var streamable = batch.Results
            .Where(m => m.StreamingProviders is { Count: > 0 })
            .DistinctBy(m => m.Id)
            .ToDictionary(m => m.Id);

        var hidden = await catalogStore.GetHiddenIdsAsync(streamable.Keys, cancellationToken);
        foreach (var id in hidden)
            streamable.Remove(id);

        return streamable;
    }

    public static async Task<HashSet<int>> GetStreamableIdsAsync(
        IAniMapperClient aniMapperClient,
        ICatalogStore catalogStore,
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
        => (await GetStreamableAsync(aniMapperClient, catalogStore, ids, cancellationToken)).Keys.ToHashSet();
}
