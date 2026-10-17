using System.Collections.Concurrent;
using System.Threading.Channels;
using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.DTOs.Admin;
using Anime.Application.Interfaces;
using Anime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Anime.Infrastructure.Catalog;

/// <summary>Hàng đợi job (admin bấm nút → đưa vào đây → <see cref="SyncJobWorker"/> chạy).</summary>
public sealed class SyncJobQueue
{
    private readonly Channel<(Guid Id, string Kind)> channel = Channel.CreateUnbounded<(Guid, string)>();

    public ValueTask EnqueueAsync(SyncJob job, CancellationToken cancellationToken) => channel.Writer.WriteAsync((job.Id, job.Kind), cancellationToken);

    public IAsyncEnumerable<(Guid Id, string Kind)> ReadAllAsync(CancellationToken cancellationToken) => channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>
/// Giữ token huỷ của job đang chạy để admin bấm dừng giữa chừng.
/// Mỗi job chạy với token ghép từ token tắt-BE và token riêng của job này.
/// </summary>
public sealed class SyncJobCanceller
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> running = new();

    /// <summary>Đăng ký khi worker bắt đầu chạy job; trả về CTS ghép với <paramref name="linkedTo"/> (token tắt BE).</summary>
    public CancellationTokenSource Register(Guid jobId, CancellationToken linkedTo)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(linkedTo);
        running[jobId] = cts;
        return cts;
    }

    public void Unregister(Guid jobId) => running.TryRemove(jobId, out _);

    /// <summary>Yêu cầu dừng job đang chạy. Trả về false nếu job không còn chạy.</summary>
    public bool Cancel(Guid jobId)
    {
        if (!running.TryGetValue(jobId, out var cts) || cts.IsCancellationRequested)
            return false;

        try
        {
            cts.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    public bool IsRunning(Guid jobId) => running.ContainsKey(jobId);
}

public sealed class SyncJobService(IDbContextFactory<AnimeDbContext> dbFactory, SyncJobQueue queue, SyncJobCanceller canceller) : ISyncJobService
{
    private static readonly SemaphoreSlim StartLock = new(1, 1);

    public async Task<SyncJobDto> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.SyncJobs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiException(404, ErrorCodes.ResourceNotFound, $"Sync job not found: {id}");

        // Đã kết thúc → không làm gì.
        if (job.Status is SyncJobStatus.Succeeded or SyncJobStatus.Failed or SyncJobStatus.Canceled)
            return ToDto(job);

        if (job.Status == SyncJobStatus.Queued)
        {
            // Chưa chạy → huỷ ngay (worker sẽ bỏ qua khi tới lượt).
            job.Status = SyncJobStatus.Canceled;
            job.Progress = "Đã huỷ";
            job.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return ToDto(job);
        }

        // Đang chạy → báo worker dừng; worker sẽ cập nhật trạng thái CANCELED khi thoát.
        canceller.Cancel(id);
        return ToDto(job);
    }

    public Task<SyncJobDto> StartAsync(StartSyncRequestDto request, string? triggeredBy, CancellationToken cancellationToken = default)
    {
        var lists = request.Lists is { Count: > 0 }
            ? request.Lists.Select(x => x.Trim().ToUpperInvariant()).Distinct().ToList()
            : CatalogLists.Tabs.ToList();

        var invalid = lists.Where(x => !CatalogLists.Tabs.Contains(x)).ToList();
        if (invalid.Count > 0)
            throw new ValidationException($"Invalid lists: {string.Join(", ", invalid)}. Must be one of: {string.Join(", ", CatalogLists.Tabs)}");

        return EnqueueAsync(new SyncJob
        {
            Kind = SyncJobKind.Catalog,
            Lists = lists,
            UpdateExisting = request.UpdateExisting,
            TriggeredBy = triggeredBy
        }, cancellationToken);
    }

    public async Task<SyncJobDto> StartStreamPrefetchAsync(int animeId, bool force, string? triggeredBy, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var anime = await db.Anime.AsNoTracking().FirstOrDefaultAsync(x => x.Id == animeId, cancellationToken)
            ?? throw new ApiException(404, ErrorCodes.MediaNotFound, $"Anime {animeId} chưa có trong danh mục");

        if (!anime.HasStream)
            throw new ValidationException($"“{anime.DisplayTitle}” chưa có nguồn phát nào trên AniMapper");

        return await EnqueueAsync(new SyncJob
        {
            Kind = SyncJobKind.Streams,
            AnimeId = anime.Id,
            Target = anime.DisplayTitle,
            Lists = anime.Providers,
            UpdateExisting = force,
            TriggeredBy = triggeredBy
        }, cancellationToken);
    }

    public async Task<SyncJobDto> StartHealthCheckAsync(int? animeId, List<string>? lists, bool force, string? triggeredBy, CancellationToken cancellationToken = default)
    {
        if (animeId is { } id)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var anime = await db.Anime.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new ApiException(404, ErrorCodes.MediaNotFound, $"Anime {id} chưa có trong danh mục");

            if (!anime.HasStream)
                throw new ValidationException($"“{anime.DisplayTitle}” chưa có nguồn phát nào trên AniMapper");

            return await EnqueueAsync(new SyncJob
            {
                Kind = SyncJobKind.Health,
                AnimeId = anime.Id,
                Target = anime.DisplayTitle,
                Lists = anime.Providers,
                UpdateExisting = true, // kiểm tra 1 phim = luôn kiểm tra lại
                TriggeredBy = triggeredBy
            }, cancellationToken);
        }

        var tabs = lists is { Count: > 0 }
            ? lists.Select(x => x.Trim().ToUpperInvariant()).Distinct().ToList()
            : CatalogLists.Tabs.ToList();

        var invalid = tabs.Where(x => !CatalogLists.Tabs.Contains(x)).ToList();
        if (invalid.Count > 0)
            throw new ValidationException($"Invalid lists: {string.Join(", ", invalid)}. Must be one of: {string.Join(", ", CatalogLists.Tabs)}");

        return await EnqueueAsync(new SyncJob
        {
            Kind = SyncJobKind.Health,
            Lists = tabs,
            UpdateExisting = force,
            TriggeredBy = triggeredBy
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<SyncJobDto>> ListAsync(int limit, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var jobs = await db.SyncJobs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);

        return jobs.Select(ToDto).ToList();
    }

    public async Task<SyncJobDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.SyncJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiException(404, ErrorCodes.ResourceNotFound, $"Sync job not found: {id}");

        return ToDto(job);
    }

    /// <summary>Mỗi lúc chỉ chạy 1 job (đồng bộ danh mục hoặc lưu sẵn nguồn phát) để không vượt hạn mức AniMapper.</summary>
    private async Task<SyncJobDto> EnqueueAsync(SyncJob job, CancellationToken cancellationToken)
    {
        await StartLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var active = await db.SyncJobs.AsNoTracking()
                .Where(x => x.Status == SyncJobStatus.Queued || x.Status == SyncJobStatus.Running)
                .FirstOrDefaultAsync(cancellationToken);
            if (active is not null)
            {
                var what = active.Kind switch
                {
                    SyncJobKind.Streams => $"lưu nguồn phát “{active.Target}”",
                    SyncJobKind.Health => "kiểm tra nguồn phát",
                    _ => "đồng bộ danh mục"
                };
                throw new ApiException(409, ErrorCodes.SyncInProgress, $"Đang {what}, vui lòng đợi xong");
            }

            job.Id = Guid.NewGuid();
            job.Status = SyncJobStatus.Queued;
            job.Progress = "Đang chờ chạy";
            job.CreatedAt = DateTimeOffset.UtcNow;

            db.SyncJobs.Add(job);
            await db.SaveChangesAsync(cancellationToken);
            await queue.EnqueueAsync(job, cancellationToken);

            return ToDto(job);
        }
        finally
        {
            StartLock.Release();
        }
    }

    internal static SyncJobDto ToDto(SyncJob job) => new(
        job.Id, job.Kind, job.AnimeId, job.Target, job.Status, job.Lists, job.UpdateExisting, job.Progress,
        job.Processed, job.Added, job.Updated, job.Skipped, job.Error, job.TriggeredBy,
        job.CreatedAt, job.StartedAt, job.FinishedAt);
}

/// <summary>Chạy lần lượt các job trong hàng đợi. Không tự chạy theo lịch – chỉ chạy khi admin bấm.</summary>
public sealed class SyncJobWorker(
    SyncJobQueue queue,
    SyncJobCanceller canceller,
    IServiceProvider services,
    IDbContextFactory<AnimeDbContext> dbFactory,
    ILogger<SyncJobWorker> logger) : BackgroundService
{
    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;

    /// <summary>Dọn job dở dang của lần chạy trước trước khi API nhận request (tránh đánh dấu nhầm job mới).</summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await FailInterruptedJobsAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (jobId, kind) in queue.ReadAllAsync(stoppingToken))
        {
            // Admin đã huỷ khi job còn trong hàng đợi → bỏ qua.
            if (await IsCanceledWhileQueuedAsync(jobId, stoppingToken))
                continue;

            using var cts = canceller.Register(jobId, stoppingToken);
            var token = cts.Token;
            try
            {
                using var scope = services.CreateScope();
                var run = kind switch
                {
                    SyncJobKind.Streams => scope.ServiceProvider.GetRequiredService<StreamPrefetchRunner>().RunAsync(jobId, token),
                    SyncJobKind.Health => scope.ServiceProvider.GetRequiredService<StreamHealthRunner>().RunAsync(jobId, token),
                    _ => scope.ServiceProvider.GetRequiredService<CatalogSyncRunner>().RunAsync(jobId, token)
                };
                await run;
            }
            finally
            {
                canceller.Unregister(jobId);
            }

            // Token riêng của job bị huỷ nhưng BE không tắt → admin đã bấm dừng.
            if (cts.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                await MarkCanceledAsync(jobId);
        }
    }

    private async Task<bool> IsCanceledWhileQueuedAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var status = await db.SyncJobs.Where(x => x.Id == jobId).Select(x => x.Status).FirstOrDefaultAsync(cancellationToken);
        return status != SyncJobStatus.Queued;
    }

    private async Task MarkCanceledAsync(Guid jobId)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(CancellationToken.None);
            await db.SyncJobs.Where(x => x.Id == jobId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, SyncJobStatus.Canceled)
                    .SetProperty(x => x.Progress, "Đã huỷ")
                    .SetProperty(x => x.Error, (string?)null)
                    .SetProperty(x => x.FinishedAt, DateTimeOffset.UtcNow), CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cannot mark job {JobId} as canceled", jobId);
        }
    }

    /// <summary>Job đang chạy dở khi BE tắt → đánh dấu thất bại để admin chạy lại được.</summary>
    private async Task FailInterruptedJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var count = await db.SyncJobs
                .Where(x => (x.Status == SyncJobStatus.Queued || x.Status == SyncJobStatus.Running) && x.CreatedAt < startedAt)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, SyncJobStatus.Failed)
                    .SetProperty(x => x.Error, "Bị gián đoạn do BE khởi động lại")
                    .SetProperty(x => x.FinishedAt, now), cancellationToken);

            if (count > 0)
                logger.LogWarning("Marked {Count} interrupted sync jobs as failed", count);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Cannot clean up interrupted sync jobs");
        }
    }
}
