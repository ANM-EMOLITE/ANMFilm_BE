using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.DTOs.Metadata;
using Anime.Application.Interfaces;
using Anime.Infrastructure.AniMapper;
using Anime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Anime.Infrastructure.Catalog;

/// <summary>
/// Chạy một job đồng bộ:
/// 1. Lấy danh sách ID theo thứ tự từ AniList (thịnh hành, phổ biến, theo mùa...).
/// 2. Anime chưa có trong DB → lấy metadata từ AniMapper và tạo mới.
///    Anime đã có → bỏ qua (trừ khi job bật UpdateExisting) để giữ nguyên dữ liệu và tiết kiệm request.
/// 3. Ghi lại thứ tự của danh sách.
/// </summary>
public sealed class CatalogSyncRunner(
    IDbContextFactory<AnimeDbContext> dbFactory,
    IAniListClient aniListClient,
    AniMapperClient aniMapperClient,
    ILogger<CatalogSyncRunner> logger)
{
    private const int AniListPageSize = 50;

    /// <summary>Số ID tối đa mỗi lần gọi AniMapper metadata/batch.</summary>
    private const int MetadataBatchSize = 50;

    /// <summary>AniList giới hạn ~30 request/phút.</summary>
    private static readonly TimeSpan AniListDelay = TimeSpan.FromSeconds(2.2);

    private static readonly TimeSpan AniListRateLimitWait = TimeSpan.FromSeconds(65);

    /// <summary>Số trang AniList (50 anime/trang) cho mỗi tab.</summary>
    private static readonly Dictionary<string, int> PagesPerTab = new()
    {
        ["TRENDING"] = 4,
        ["POPULAR"] = 6,
        ["NEW"] = 4,
        ["AIRING"] = 6,
        ["FINISHED"] = 4,
        ["MOVIE"] = 4,
        ["SEASON"] = 2
    };

    public async Task RunAsync(Guid jobId, CancellationToken cancellationToken)
    {
        SyncJob job;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            job = await db.SyncJobs.FirstAsync(x => x.Id == jobId, cancellationToken);
            job.Status = SyncJobStatus.Running;
            job.StartedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        // Mỗi anime chỉ tính 1 lần dù xuất hiện trong nhiều danh sách.
        var seen = new HashSet<int>();
        var failedLists = new List<string>();

        try
        {
            var definitions = job.Lists.SelectMany(tab => CatalogLists.ForSync(tab)).ToList();

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                var step = $"({i + 1}/{definitions.Count}) {definition.Label}";
                await UpdateJobAsync(jobId, j => j.Progress = $"{step}: lấy danh sách từ AniList", cancellationToken);

                try
                {
                    var ids = await FetchListIdsAsync(definition, cancellationToken);
                    await UpdateJobAsync(jobId, j => j.Progress = $"{step}: lấy thông tin {ids.Count} phim", cancellationToken);

                    var stats = await SyncListAsync(definition, ids, job.UpdateExisting, seen, cancellationToken);

                    await UpdateJobAsync(jobId, j =>
                    {
                        j.Processed += stats.Processed;
                        j.Added += stats.Added;
                        j.Updated += stats.Updated;
                        j.Skipped += stats.Skipped;
                        j.Progress = $"{step}: xong";
                    }, cancellationToken);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // 1 danh sách lỗi (AniList/AniMapper chậm, sập...) → bỏ qua, chạy tiếp danh sách khác.
                    logger.LogWarning(ex, "Sync job {JobId}: list {List} failed", jobId, definition.Key);
                    failedLists.Add($"{definition.Label} ({Describe(ex)})");
                    await UpdateJobAsync(jobId, j => j.Error = "Lỗi: " + string.Join("; ", failedLists), CancellationToken.None);
                }
            }

            var allFailed = failedLists.Count == definitions.Count;
            await UpdateJobAsync(jobId, j =>
            {
                j.Status = allFailed ? SyncJobStatus.Failed : SyncJobStatus.Succeeded;
                j.Progress = failedLists.Count == 0
                    ? $"Hoàn tất {definitions.Count} danh sách"
                    : $"Hoàn tất {definitions.Count - failedLists.Count}/{definitions.Count} danh sách";
                j.FinishedAt = DateTimeOffset.UtcNow;
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync job {JobId} failed", jobId);
            await UpdateJobAsync(jobId, j =>
            {
                j.Status = SyncJobStatus.Failed;
                j.Error = cancellationToken.IsCancellationRequested ? "Bị dừng do BE tắt" : ex.Message;
                j.FinishedAt = DateTimeOffset.UtcNow;
            }, CancellationToken.None);
        }
    }

    /// <summary>Lấy / cập nhật 1 anime từ AniMapper. Trả về (entity, đã tạo mới?).</summary>
    public async Task<(CatalogAnime Anime, bool Created)> SyncOneAsync(int id, CancellationToken cancellationToken)
    {
        var response = await aniMapperClient.GetMetadataAsync(id.ToString(), cancellationToken);
        var media = response.Result
            ?? throw new ApiException(404, ErrorCodes.MediaNotFound, $"Media not found: {id}");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var entity = await db.Anime.FirstOrDefaultAsync(x => x.Id == media.Id, cancellationToken);
        var created = entity is null;
        if (entity is null)
        {
            entity = new CatalogAnime { CreatedAt = now };
            db.Anime.Add(entity);
        }

        CatalogMapper.Apply(entity, media, now);
        await db.SaveChangesAsync(cancellationToken);
        return (entity, created);
    }

    private async Task<List<int>> FetchListIdsAsync(CatalogListDefinition definition, CancellationToken cancellationToken)
    {
        var pages = PagesPerTab.GetValueOrDefault(definition.Tab, 2);
        var ids = new List<int>();

        for (var page = 1; page <= pages; page++)
        {
            var result = await GetAniListPageAsync(definition, page, cancellationToken);
            ids.AddRange(result.Ids);

            if (!result.HasNextPage)
                break;

            await Task.Delay(AniListDelay, cancellationToken);
        }

        return ids.Distinct().ToList();
    }

    private async Task<AniListIdPage> GetAniListPageAsync(CatalogListDefinition definition, int page, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await aniListClient.GetAnimeIdsAsync(definition.Query, page, AniListPageSize, cancellationToken);
            }
            catch (UpstreamApiException ex) when (ex.StatusCode == 429 && attempt < MaxAttempts)
            {
                logger.LogWarning("AniList rate limited while syncing {List}, waiting {Seconds}s", definition.Key, AniListRateLimitWait.TotalSeconds);
                await Task.Delay(AniListRateLimitWait, cancellationToken);
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex) && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("AniList failed while syncing {List} page {Page} ({Error}), retry {Attempt}", definition.Key, page, ex.Message, attempt);
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt), cancellationToken);
            }
        }
    }

    private const int MaxAttempts = 3;

    /// <summary>Timeout, mất kết nối, 5xx.</summary>
    private static bool IsTransient(Exception ex) => ex switch
    {
        ApiException api => api.StatusCode >= 500,
        TaskCanceledException or TimeoutException or HttpRequestException => true,
        _ => false
    };

    private static string Describe(Exception ex) => ex switch
    {
        TaskCanceledException or TimeoutException => "quá thời gian chờ",
        HttpRequestException => "không kết nối được",
        ApiException api => api.Message,
        _ => ex.Message
    };

    private async Task<ListStats> SyncListAsync(
        CatalogListDefinition definition,
        IReadOnlyList<int> ids,
        bool updateExisting,
        HashSet<int> seen,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = (await db.Anime.Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken)).ToHashSet();

        var newIds = ids.Where(id => !existing.Contains(id) && !seen.Contains(id)).ToList();
        var refreshIds = updateExisting ? ids.Where(id => existing.Contains(id) && !seen.Contains(id)).ToList() : [];

        var stats = new ListStats
        {
            Processed = ids.Count(id => !seen.Contains(id)),
            Skipped = updateExisting ? 0 : ids.Count(id => existing.Contains(id) && !seen.Contains(id))
        };

        var media = await FetchMetadataAsync(newIds.Concat(refreshIds).ToList(), cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var tracked = await db.Anime.Where(x => refreshIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var item in media)
        {
            if (tracked.TryGetValue(item.Id, out var entity))
            {
                CatalogMapper.Apply(entity, item, now);
                stats.Updated++;
            }
            else if (!existing.Contains(item.Id))
            {
                entity = new CatalogAnime { CreatedAt = now };
                CatalogMapper.Apply(entity, item, now);
                db.Anime.Add(entity);
                existing.Add(item.Id);
                stats.Added++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // Ghi lại thứ tự: chỉ những ID đã có trong DB (AniMapper không có thì bỏ).
        var ordered = ids.Where(existing.Contains).ToList();

        // Thay toàn bộ thứ tự trong 1 transaction; bọc trong execution strategy để tự thử lại khi rớt kết nối.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            await db.ListItems.Where(x => x.ListKey == definition.Key).ExecuteDeleteAsync(cancellationToken);
            db.ListItems.AddRange(ordered.Select((id, index) => new CatalogListItem { ListKey = definition.Key, Position = index, AnimeId = id }));

            var list = await db.Lists.FirstOrDefaultAsync(x => x.Key == definition.Key, cancellationToken);
            if (list is null)
            {
                list = new CatalogList { Key = definition.Key };
                db.Lists.Add(list);
            }

            list.ItemCount = ordered.Count;
            list.SyncedAt = now;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        seen.UnionWith(ids);
        return stats;
    }

    private async Task<List<MediaDetailDto>> FetchMetadataAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        var results = new List<MediaDetailDto>();

        foreach (var chunk in ids.Chunk(MetadataBatchSize))
        {
            var batch = await aniMapperClient.GetMetadataBatchAsync(string.Join(',', chunk), cancellationToken);
            results.AddRange(batch.Results);
        }

        return results.DistinctBy(x => x.Id).ToList();
    }

    private async Task UpdateJobAsync(Guid jobId, Action<SyncJob> update, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.SyncJobs.FirstAsync(x => x.Id == jobId, cancellationToken);
        update(job);
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed class ListStats
    {
        public int Processed { get; set; }

        public int Added { get; set; }

        public int Updated { get; set; }

        public int Skipped { get; set; }
    }
}
