using Anime.Application.DTOs.Metadata;
using Anime.Application.Interfaces;

namespace Anime.Application.Services;

/// <summary>
/// Kiểm tra media nào đã có nguồn phát (streamingProviders không rỗng) qua metadata/batch.
/// </summary>
internal static class StreamAvailability
{
    /// <summary>Trả về metadata của các media có nguồn phát, key = media ID.</summary>
    public static async Task<Dictionary<int, MediaDetailDto>> GetStreamableAsync(
        IAniMapperClient aniMapperClient,
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
            return [];

        var batch = await aniMapperClient.GetMetadataBatchAsync(string.Join(',', distinctIds), cancellationToken);

        return batch.Results
            .Where(m => m.StreamingProviders is { Count: > 0 })
            .DistinctBy(m => m.Id)
            .ToDictionary(m => m.Id);
    }

    public static async Task<HashSet<int>> GetStreamableIdsAsync(
        IAniMapperClient aniMapperClient,
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
        => (await GetStreamableAsync(aniMapperClient, ids, cancellationToken)).Keys.ToHashSet();
}
