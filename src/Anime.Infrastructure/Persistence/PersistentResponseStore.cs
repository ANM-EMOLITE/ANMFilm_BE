using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Anime.Infrastructure.Persistence;

/// <summary>Bản lưu đọc từ DB.</summary>
public sealed record StoredResponse<T>(T Value, DateTimeOffset FetchedAt)
{
    public TimeSpan Age => DateTimeOffset.UtcNow - FetchedAt;
}

/// <summary>
/// Đọc/ghi response AniMapper vào PostgreSQL (bảng <c>cached_responses</c>).
/// DB chỉ là lớp phụ: lỗi DB được log và bỏ qua, DB lỗi liên tục thì tạm ngưng dùng DB 30 giây
/// để request không bị chậm theo.
/// </summary>
public sealed class PersistentResponseStore(
    IDbContextFactory<AnimeDbContext> dbFactory,
    ILogger<PersistentResponseStore> logger)
{
    private static readonly TimeSpan PauseOnFailure = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private long pausedUntilTicks;

    private bool IsPaused => DateTime.UtcNow.Ticks < Interlocked.Read(ref pausedUntilTicks);

    public async Task<StoredResponse<T>?> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        if (IsPaused)
            return null;

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var row = await db.CachedResponses
                .AsNoTracking()
                .Where(x => x.Key == key && x.ExpiresAt > now)
                .Select(x => new { x.Payload, x.FetchedAt })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
                return null;

            var value = JsonSerializer.Deserialize<T>(row.Payload, JsonOptions);
            return value is null ? null : new StoredResponse<T>(value, row.FetchedAt);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            OnFailure(ex, "read", key);
            return null;
        }
    }

    public async Task SaveAsync<T>(string key, T value, TimeSpan retention, CancellationToken cancellationToken = default)
        where T : class
    {
        if (IsPaused)
            return;

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            var payload = JsonSerializer.Serialize(value, JsonOptions);
            var kind = KindOf(key);
            var fetchedAt = DateTimeOffset.UtcNow;
            var expiresAt = fetchedAt + retention;

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO cached_responses (key, kind, payload, fetched_at, expires_at)
                VALUES ({key}, {kind}, {payload}::jsonb, {fetchedAt}, {expiresAt})
                ON CONFLICT (key) DO UPDATE
                SET kind = EXCLUDED.kind, payload = EXCLUDED.payload,
                    fetched_at = EXCLUDED.fetched_at, expires_at = EXCLUDED.expires_at
                """, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            OnFailure(ex, "write", key);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (IsPaused)
            return;

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            await db.CachedResponses.Where(x => x.Key == key).ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            OnFailure(ex, "delete", key);
        }
    }

    /// <summary>Xóa các bản lưu đã hết hạn. Trả về số dòng đã xóa.</summary>
    public async Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return await db.CachedResponses.Where(x => x.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
    }

    private void OnFailure(Exception ex, string operation, string key)
    {
        Interlocked.Exchange(ref pausedUntilTicks, (DateTime.UtcNow + PauseOnFailure).Ticks);
        logger.LogWarning(ex, "Persistent cache {Operation} failed for {Key}; DB cache paused for {Seconds}s",
            operation, key, PauseOnFailure.TotalSeconds);
    }

    /// <summary>Key dạng <c>endpoint?a&amp;b</c> → <c>endpoint</c>.</summary>
    private static string KindOf(string key)
    {
        var index = key.IndexOf('?');
        var kind = index < 0 ? key : key[..index];
        return kind.Length > 32 ? kind[..32] : kind;
    }
}
