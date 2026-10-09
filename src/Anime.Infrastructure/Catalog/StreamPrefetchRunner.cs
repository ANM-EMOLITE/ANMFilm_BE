using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.DTOs;
using Anime.Application.Interfaces;
using Anime.Infrastructure.AniMapper;
using Anime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Anime.Infrastructure.Catalog;

/// <summary>
/// Lưu sẵn nguồn phát của 1 anime vào DB: server → danh sách tập → link stream từng tập.
/// Gọi qua <see cref="IAniMapperClient"/> (lớp cache) nên dữ liệu được lưu đúng key mà trang xem phim đọc,
/// sau đó khi xem không cần chờ AniMapper (xem <see cref="AniMapperResponseCache"/>).
/// </summary>
public sealed class StreamPrefetchRunner(
    IDbContextFactory<AnimeDbContext> dbFactory,
    IAniMapperClient aniMapper,
    AniMapperResponseCache cache,
    ILogger<StreamPrefetchRunner> logger)
{
    /// <summary>Số tập lỗi liên tiếp của 1 server → coi như server đang sập, bỏ qua phần còn lại.</summary>
    private const int MaxConsecutiveFailures = 3;

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

        var failures = new List<string>();
        var force = job.UpdateExisting;
        var animeId = job.AnimeId!.Value;

        try
        {
            // Trang xem phim cần cả thông tin phim (/metadata) → lưu luôn.
            await UpdateJobAsync(jobId, j => j.Progress = "Lưu thông tin phim", cancellationToken);
            try
            {
                await aniMapper.GetMetadataAsync(animeId.ToString(), cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                failures.Add($"Thông tin phim ({Describe(ex)})");
            }

            foreach (var provider in job.Lists.Select(p => p.ToUpperInvariant()))
            {
                var servers = await GetServersAsync(animeId, provider, failures, cancellationToken);

                foreach (var server in servers)
                {
                    var label = server is null ? provider : $"{provider}/{server}";
                    await UpdateJobAsync(jobId, j => j.Progress = $"{label}: lấy danh sách tập", cancellationToken);

                    List<EpisodeDto> episodes;
                    try
                    {
                        var response = await aniMapper.GetEpisodesAsync(
                            new EpisodesRequestDto { Id = animeId, Provider = provider, Server = server, Limit = 0, Offset = 0 },
                            cancellationToken);
                        episodes = response.Episodes.ToList();
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        failures.Add($"{label}: không lấy được danh sách tập ({Describe(ex)})");
                        continue;
                    }

                    await PrefetchSourcesAsync(jobId, label, provider, server, episodes, force, failures, cancellationToken);
                }
            }

            await UpdateJobAsync(jobId, j =>
            {
                var saved = j.Added + j.Updated + j.Skipped;
                j.Status = saved == 0 && failures.Count > 0 ? SyncJobStatus.Failed : SyncJobStatus.Succeeded;
                j.Progress = $"Hoàn tất: {saved}/{j.Processed} tập đã có link trong DB";
                j.Error = failures.Count > 0 ? string.Join("; ", failures) : null;
                j.FinishedAt = DateTimeOffset.UtcNow;
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stream prefetch job {JobId} failed", jobId);
            await UpdateJobAsync(jobId, j =>
            {
                j.Status = SyncJobStatus.Failed;
                j.Error = cancellationToken.IsCancellationRequested ? "Bị dừng do BE tắt" : ex.Message;
                j.FinishedAt = DateTimeOffset.UtcNow;
            }, CancellationToken.None);
        }
    }

    /// <summary>Danh sách server của provider; không có / lỗi → dùng server mặc định (null) giống trang xem phim.</summary>
    private async Task<IReadOnlyList<string?>> GetServersAsync(int animeId, string provider, List<string> failures, CancellationToken cancellationToken)
    {
        try
        {
            var response = await aniMapper.GetServersAsync(animeId, provider, cancellationToken);
            return response.Servers.Count > 0 ? response.Servers.Select(s => (string?)s).ToList() : [null];
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Cannot get servers of {Provider} for {AnimeId}: {Error}", provider, animeId, ex.Message);
            return [null];
        }
    }

    private async Task PrefetchSourcesAsync(
        Guid jobId,
        string label,
        string provider,
        string? server,
        IReadOnlyList<EpisodeDto> episodes,
        bool force,
        List<string> failures,
        CancellationToken cancellationToken)
    {
        var failed = new List<string>();
        string? lastError = null;
        var consecutiveFailures = 0;

        for (var i = 0; i < episodes.Count; i++)
        {
            var episode = episodes[i];
            // Giống trang xem phim: server đã chọn, không có thì server của tập.
            var sourceServer = string.IsNullOrWhiteSpace(server ?? episode.Server) ? null : (server ?? episode.Server).Trim();
            var key = CachingAniMapperClient.SourceKey(episode.EpisodeId, provider, sourceServer);

            await UpdateJobAsync(jobId, j =>
            {
                j.Processed++;
                j.Progress = $"{label}: tập {episode.EpisodeNumber} ({i + 1}/{episodes.Count})";
            }, cancellationToken);

            var existed = await cache.HasStoredAsync<StreamSourceDto>(key);
            if (existed && !force)
            {
                await UpdateJobAsync(jobId, j => j.Skipped++, cancellationToken);
                continue;
            }

            try
            {
                await FetchSourceAsync(jobId, label, episode, provider, sourceServer, force, cancellationToken);

                consecutiveFailures = 0;
                await UpdateJobAsync(jobId, j =>
                {
                    if (existed) j.Updated++;
                    else j.Added++;
                }, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                failed.Add(episode.EpisodeNumber);
                lastError = Describe(ex);
                consecutiveFailures++;

                var providerDown = ex is ApiException { Code: ErrorCodes.ProviderUnavailable };
                if (providerDown || consecutiveFailures >= MaxConsecutiveFailures)
                {
                    // Server đang sập → không thử tiếp từng tập (mỗi tập có thể mất tới 30 giây).
                    var remaining = episodes.Skip(i + 1).Select(e => e.EpisodeNumber).ToList();
                    failed.AddRange(remaining);
                    await UpdateJobAsync(jobId, j => j.Processed += remaining.Count, cancellationToken);
                    lastError = $"{lastError}, dừng vì lỗi liên tục";
                    break;
                }
            }
        }

        if (failed.Count > 0)
            failures.Add($"{label}: {failed.Count} tập lỗi ({Summarize(failed)}) – {lastError}");
    }

    /// <summary>
    /// Lấy link 1 tập. AniMapper báo vượt hạn mức (429) → chờ rồi thử lại chính tập đó,
    /// không tính là lỗi (hạn mức chung 60 request/phút, không phải tập/provider hỏng).
    /// </summary>
    private async Task FetchSourceAsync(
        Guid jobId,
        string label,
        EpisodeDto episode,
        string provider,
        string? server,
        bool force,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await aniMapper.GetSourceAsync(
                    new SourceRequestDto { EpisodeData = episode.EpisodeId, Provider = provider, Server = server, Refresh = force },
                    cancellationToken);
                return;
            }
            catch (ApiException ex) when (ex.StatusCode == 429 && attempt <= MaxRateLimitRetries)
            {
                await UpdateJobAsync(jobId, j => j.Progress = $"{label}: tập {episode.EpisodeNumber} – AniMapper giới hạn request, chờ {RateLimitWait.TotalSeconds:0}s", cancellationToken);
                await Task.Delay(RateLimitWait, cancellationToken);
            }
        }
    }

    private const int MaxRateLimitRetries = 5;

    private static readonly TimeSpan RateLimitWait = TimeSpan.FromSeconds(30);

    private async Task UpdateJobAsync(Guid jobId, Action<SyncJob> update, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.SyncJobs.FirstAsync(x => x.Id == jobId, cancellationToken);
        update(job);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Summarize(IReadOnlyList<string> episodes)
        => episodes.Count <= 6 ? string.Join(", ", episodes) : $"{string.Join(", ", episodes.Take(5))}… {episodes[^1]}";

    private static string Describe(Exception ex) => ex switch
    {
        TaskCanceledException or TimeoutException => "quá thời gian chờ",
        HttpRequestException => "không kết nối được",
        ApiException api => api.Message,
        _ => ex.Message
    };
}
