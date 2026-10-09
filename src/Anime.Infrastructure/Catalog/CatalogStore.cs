using Anime.Application.Interfaces;
using Anime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Anime.Infrastructure.Catalog;

/// <summary>
/// Đọc danh mục đã đồng bộ cho phía người dùng.
/// Danh sách ID bị ẩn được giữ trong RAM 30 giây (admin đổi thì xóa ngay) để mỗi request tìm kiếm không phải hỏi DB.
/// Lỗi DB → log và coi như chưa có dữ liệu.
/// </summary>
public sealed class CatalogStore(
    IDbContextFactory<AnimeDbContext> dbFactory,
    IMemoryCache memory,
    ILogger<CatalogStore> logger) : ICatalogStore
{
    private const string HiddenIdsKey = "catalog:hidden-ids";

    private static readonly TimeSpan HiddenIdsDuration = TimeSpan.FromSeconds(30);

    public async Task<CatalogListPage?> GetListPageAsync(
        string listKey,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var list = await db.Lists.AsNoTracking().FirstOrDefaultAsync(x => x.Key == listKey, cancellationToken);
            if (list is null || list.ItemCount == 0)
                return null;

            var visible =
                from item in db.ListItems
                join anime in db.Anime on item.AnimeId equals anime.Id
                where item.ListKey == listKey && anime.HasStream && !anime.IsHidden
                orderby item.Position
                select anime;

            var total = await visible.CountAsync(cancellationToken);
            var rows = await visible.AsNoTracking().Skip(offset).Take(limit).ToListAsync(cancellationToken);

            var next = offset + rows.Count;
            return new CatalogListPage(
                rows.Select(CatalogMapper.ToSearchResult).ToList(),
                total,
                next < total ? next : null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Cannot read catalog list {ListKey}; falling back to upstream", listKey);
            return null;
        }
    }

    public async Task<HashSet<int>> GetHiddenIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var hidden = await GetAllHiddenIdsAsync(cancellationToken);
        return hidden.Count == 0 ? [] : ids.Where(hidden.Contains).ToHashSet();
    }

    /// <summary>Gọi sau khi admin ẩn/hiện anime, bật/tắt provider hoặc sau khi kiểm tra nguồn phát.</summary>
    public void InvalidateHidden()
    {
        memory.Remove(HiddenIdsKey);
        memory.Remove(ProviderStatesKey);
    }

    public async Task<IReadOnlyDictionary<string, ProviderState>> GetProviderStatesAsync(int animeId, CancellationToken cancellationToken = default)
    {
        var all = await GetAllProviderStatesAsync(cancellationToken);
        return all.TryGetValue(animeId, out var states) ? states : new Dictionary<string, ProviderState>();
    }

    private const string ProviderStatesKey = "catalog:provider-states";

    /// <summary>Toàn bộ kết quả kiểm tra (vài nghìn dòng) giữ trong RAM 30 giây.</summary>
    private async Task<Dictionary<int, Dictionary<string, ProviderState>>> GetAllProviderStatesAsync(CancellationToken cancellationToken)
    {
        if (memory.TryGetValue(ProviderStatesKey, out Dictionary<int, Dictionary<string, ProviderState>>? cached) && cached is not null)
            return cached;

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var rows = await db.ProviderStatuses.AsNoTracking()
                .Select(x => new { x.AnimeId, x.Provider, x.Status, x.IsDisabled })
                .ToListAsync(cancellationToken);

            var map = rows
                .GroupBy(x => x.AnimeId)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(x => x.Provider, x => new ProviderState(x.Status == ProviderCheckStatus.Ok, x.IsDisabled)));

            memory.Set(ProviderStatesKey, map, HiddenIdsDuration);
            return map;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Cannot read provider states");
            var empty = new Dictionary<int, Dictionary<string, ProviderState>>();
            memory.Set(ProviderStatesKey, empty, TimeSpan.FromSeconds(10));
            return empty;
        }
    }

    private async Task<HashSet<int>> GetAllHiddenIdsAsync(CancellationToken cancellationToken)
    {
        if (memory.TryGetValue(HiddenIdsKey, out HashSet<int>? cached) && cached is not null)
            return cached;

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var ids = await db.Anime.Where(x => x.IsHidden).Select(x => x.Id).ToListAsync(cancellationToken);

            var set = ids.ToHashSet();
            memory.Set(HiddenIdsKey, set, HiddenIdsDuration);
            return set;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Cannot read hidden anime ids");
            // Cache ngắn để DB lỗi không làm chậm mọi request.
            memory.Set(HiddenIdsKey, new HashSet<int>(), TimeSpan.FromSeconds(10));
            return [];
        }
    }
}
