using System.Net.Http.Headers;
using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.DTOs;
using Anime.Application.Interfaces;
using Anime.Infrastructure.Persistence;
using Anime.Infrastructure.Streaming;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Anime.Infrastructure.Catalog;

/// <summary>
/// Kiểm tra nguồn phát: với từng provider của anime, lấy server → danh sách tập → link tập đầu tiên,
/// rồi mở thử link đó (playlist HLS phải trả về #EXTM3U).
/// – Provider lỗi → tự tắt cho anime đó (trang xem tự dùng provider khác); chạy lại được → tự bật lại.
/// – Mọi provider đều lỗi → tự ẩn anime; kiểm tra lại thấy chạy được → tự hiện lại.
/// Provider admin tự bật/tắt (DisabledBy = ADMIN) và anime admin tự ẩn thì không bị ghi đè.
/// </summary>
public sealed class StreamHealthRunner(
    IDbContextFactory<AnimeDbContext> dbFactory,
    IAniMapperClient aniMapper,
    IHttpClientFactory httpClientFactory,
    CatalogStore catalogStore,
    ILogger<StreamHealthRunner> logger)
{
    /// <summary>Đã kiểm tra trong khoảng này thì bỏ qua (trừ khi job bật UpdateExisting).</summary>
    private static readonly TimeSpan RecheckAfter = TimeSpan.FromHours(24);

    private static readonly TimeSpan StreamCheckTimeout = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan RateLimitWait = TimeSpan.FromSeconds(30);

    private const int MaxRateLimitRetries = 5;

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

        var autoHidden = new List<string>();

        try
        {
            var animeIds = await ResolveTargetsAsync(job, cancellationToken);
            await UpdateJobAsync(jobId, j => j.Progress = $"Chuẩn bị kiểm tra {animeIds.Count} phim", cancellationToken);

            for (var i = 0; i < animeIds.Count; i++)
            {
                var result = await CheckAnimeAsync(jobId, animeIds[i], $"({i + 1}/{animeIds.Count})", job.UpdateExisting, cancellationToken);
                if (result.HiddenTitle is not null)
                    autoHidden.Add(result.HiddenTitle);

                await UpdateJobAsync(jobId, j =>
                {
                    j.Processed++;
                    switch (result.Outcome)
                    {
                        case CheckOutcome.Skipped: j.Skipped++; break;
                        case CheckOutcome.AllOk: j.Added++; break;
                        default: j.Updated++; break;
                    }

                    if (autoHidden.Count > 0)
                        j.Error = $"Tự ẩn {autoHidden.Count} phim (mọi nguồn đều lỗi): {Summarize(autoHidden)}";
                }, cancellationToken);
            }

            await UpdateJobAsync(jobId, j =>
            {
                j.Status = SyncJobStatus.Succeeded;
                j.Progress = $"Hoàn tất kiểm tra {animeIds.Count} phim";
                j.FinishedAt = DateTimeOffset.UtcNow;
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stream health job {JobId} failed", jobId);
            await UpdateJobAsync(jobId, j =>
            {
                j.Status = SyncJobStatus.Failed;
                j.Error = cancellationToken.IsCancellationRequested ? "Bị dừng do BE tắt" : ex.Message;
                j.FinishedAt = DateTimeOffset.UtcNow;
            }, CancellationToken.None);
        }
        finally
        {
            catalogStore.InvalidateHidden();
        }
    }

    /// <summary>1 anime (AnimeId) hoặc mọi anime có nguồn phát trong các danh sách (tab) đã chọn, giữ thứ tự danh sách.</summary>
    private async Task<List<int>> ResolveTargetsAsync(SyncJob job, CancellationToken cancellationToken)
    {
        if (job.AnimeId is { } id)
            return [id];

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var exact = job.Lists.Where(x => x != "SEASON").ToList();
        var includeSeasons = job.Lists.Contains("SEASON");

        var rows = await (
                from item in db.ListItems
                join anime in db.Anime on item.AnimeId equals anime.Id
                where anime.HasStream && (exact.Contains(item.ListKey) || (includeSeasons && item.ListKey.StartsWith("SEASON:")))
                orderby item.ListKey, item.Position
                select item.AnimeId)
            .ToListAsync(cancellationToken);

        return rows.Distinct().ToList();
    }

    private async Task<CheckResult> CheckAnimeAsync(Guid jobId, int animeId, string step, bool force, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var anime = await db.Anime.FirstOrDefaultAsync(x => x.Id == animeId, cancellationToken);
        if (anime is null || anime.Providers.Count == 0)
            return new CheckResult(CheckOutcome.Skipped, null);

        var statuses = await db.ProviderStatuses.Where(x => x.AnimeId == animeId).ToDictionaryAsync(x => x.Provider, cancellationToken);

        var checkedRecently = anime.Providers.All(p =>
            statuses.TryGetValue(p, out var s) && DateTimeOffset.UtcNow - s.CheckedAt < RecheckAfter);
        if (checkedRecently && !force)
            return new CheckResult(CheckOutcome.Skipped, null);

        var now = DateTimeOffset.UtcNow;
        foreach (var provider in anime.Providers)
        {
            await UpdateJobAsync(jobId, j => j.Progress = $"{step} {anime.DisplayTitle} · {provider}", cancellationToken);
            var error = await CheckProviderAsync(animeId, provider, cancellationToken);

            if (!statuses.TryGetValue(provider, out var status))
            {
                status = new AnimeProviderStatus { AnimeId = animeId, Provider = provider };
                db.ProviderStatuses.Add(status);
                statuses[provider] = status;
            }

            status.Status = error is null ? ProviderCheckStatus.Ok : ProviderCheckStatus.Broken;
            status.LastError = error is null ? null : Truncate(error, 500);
            status.CheckedAt = now;

            // Admin đã tự bật/tắt → giữ nguyên quyết định của admin.
            if (status.DisabledBy != DisabledByAdmin)
            {
                status.IsDisabled = error is not null;
                status.DisabledBy = error is null ? null : DisabledByAuto;
            }
        }

        var current = anime.Providers.Select(p => statuses[p]).ToList();
        var playable = current.Any(s => !s.IsDisabled);
        string? hiddenTitle = null;

        if (!playable && !anime.IsHidden)
        {
            anime.IsHidden = true;
            anime.AutoHidden = true;
            hiddenTitle = anime.DisplayTitle;
        }
        else if (playable && anime.AutoHidden)
        {
            anime.IsHidden = false;
            anime.AutoHidden = false;
        }

        await db.SaveChangesAsync(cancellationToken);

        var outcome = current.All(s => s.Status == ProviderCheckStatus.Ok) ? CheckOutcome.AllOk : CheckOutcome.HasBroken;
        return new CheckResult(outcome, hiddenTitle);
    }

    /// <summary>Trả về null nếu phát được, ngược lại là lý do lỗi.</summary>
    private async Task<string?> CheckProviderAsync(int animeId, string provider, CancellationToken cancellationToken)
    {
        IReadOnlyList<string?> servers;
        try
        {
            var response = await WithRateLimitRetryAsync(() => aniMapper.GetServersAsync(animeId, provider, cancellationToken), cancellationToken);
            servers = response.Servers.Count > 0 ? response.Servers.Select(s => (string?)s).ToList() : [null];
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            if (ex is ApiException { StatusCode: 404 })
                return "Không có mapping cho provider này";
            servers = [null];
        }

        string? lastError = null;
        foreach (var server in servers)
        {
            List<EpisodeDto> episodes;
            try
            {
                var response = await WithRateLimitRetryAsync(() => aniMapper.GetEpisodesAsync(
                    new EpisodesRequestDto { Id = animeId, Provider = provider, Server = server, Limit = 0, Offset = 0 },
                    cancellationToken), cancellationToken);
                episodes = response.Episodes.ToList();
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = $"Không lấy được danh sách tập ({Describe(ex)})";
                continue;
            }

            if (episodes.Count == 0)
            {
                lastError = "Danh sách tập trống";
                continue;
            }

            var episode = episodes[0];
            var sourceServer = string.IsNullOrWhiteSpace(server ?? episode.Server) ? null : (server ?? episode.Server).Trim();

            // Lần 1: link đang dùng (có thể là bản lưu trong DB). Link chết → lần 2 lấy link mới.
            foreach (var refresh in new[] { false, true })
            {
                try
                {
                    var source = await WithRateLimitRetryAsync(() => aniMapper.GetSourceAsync(
                        new SourceRequestDto { EpisodeData = episode.EpisodeId, Provider = provider, Server = sourceServer, Refresh = refresh },
                        cancellationToken), cancellationToken);

                    var streamError = await VerifyStreamAsync(source, cancellationToken);
                    if (streamError is null)
                        return null;

                    lastError = $"Tập {episode.EpisodeNumber}: {streamError}";
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    lastError = $"Tập {episode.EpisodeNumber}: không lấy được link ({Describe(ex)})";
                    break;
                }
            }
        }

        return lastError ?? "Không phát được";
    }

    /// <summary>Mở thử link stream (kèm header Referer... như khi phát). Null = mở được.</summary>
    private async Task<string?> VerifyStreamAsync(StreamSourceDto source, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return "link không hợp lệ";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(StreamCheckTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            foreach (var (key, value) in source.ProxyHeaders ?? new Dictionary<string, string>())
                request.Headers.TryAddWithoutValidation(key, value);

            if (source.Type != "EMBED")
                request.Headers.Range = new RangeHeaderValue(0, 4095);

            var client = httpClientFactory.CreateClient(StreamProxyService.HttpClientName);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if ((int)response.StatusCode >= 400)
                return $"link trả về {(int)response.StatusCode}";

            if (source.Type == "HLS")
            {
                var text = await response.Content.ReadAsStringAsync(timeout.Token);
                if (!text.TrimStart().StartsWith("#EXTM3U", StringComparison.Ordinal))
                    return "playlist không hợp lệ";
            }

            return null;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return $"không mở được link ({Describe(ex)})";
        }
    }

    /// <summary>AniMapper báo vượt hạn mức (429) → chờ rồi thử lại, không tính là lỗi của provider.</summary>
    private static async Task<T> WithRateLimitRetryAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await call();
            }
            catch (ApiException ex) when (ex.StatusCode == 429 && attempt <= MaxRateLimitRetries)
            {
                await Task.Delay(RateLimitWait, cancellationToken);
            }
        }
    }

    private async Task UpdateJobAsync(Guid jobId, Action<SyncJob> update, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.SyncJobs.FirstAsync(x => x.Id == jobId, cancellationToken);
        update(job);
        await db.SaveChangesAsync(cancellationToken);
    }

    public const string DisabledByAuto = "AUTO";

    public const string DisabledByAdmin = "ADMIN";

    private static string Summarize(IReadOnlyList<string> titles)
        => titles.Count <= 5 ? string.Join(", ", titles) : $"{string.Join(", ", titles.Take(5))}… (+{titles.Count - 5})";

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static string Describe(Exception ex) => ex switch
    {
        TaskCanceledException or TimeoutException => "quá thời gian chờ",
        HttpRequestException => "không kết nối được",
        ApiException api => api.Message,
        _ => ex.Message
    };

    private enum CheckOutcome { Skipped, AllOk, HasBroken }

    private sealed record CheckResult(CheckOutcome Outcome, string? HiddenTitle);
}
