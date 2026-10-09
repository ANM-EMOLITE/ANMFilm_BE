using System.Collections.Concurrent;
using System.Net;
using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Anime.Infrastructure.AniMapper;

/// <summary>Thời gian giữ cache cho một loại response.</summary>
/// <param name="Fresh">Trong khoảng này trả cache (RAM hoặc DB), không gọi AniMapper.</param>
/// <param name="Stale">Bản dự phòng trong RAM: AniMapper lỗi tạm thời (timeout/5xx/429) thì trả bản này thay vì báo lỗi.</param>
/// <param name="NotFound">Cache lỗi 404 (mapping/tập không tồn tại) để không hỏi lại liên tục.</param>
/// <param name="Persist">Thời gian giữ bản lưu trong DB (null = không lưu DB). Dùng khi BE khởi động lại hoặc AniMapper sập lâu.</param>
public sealed record CachePolicy(TimeSpan Fresh, TimeSpan Stale, TimeSpan NotFound, TimeSpan? Persist = null);

/// <summary>
/// Cache 2 lớp cho response AniMapper: RAM (nhanh) + PostgreSQL (bền vững, xem <see cref="PersistentResponseStore"/>).
/// – gộp các request trùng đang chạy (N client cùng xem 1 tập → 1 request upstream),
/// – bản lưu DB còn mới thì dùng luôn, không gọi AniMapper (kể cả sau khi BE khởi động lại),
/// – bản lưu đã cũ → vẫn trả ngay và làm mới ở nền (stale-while-revalidate), người xem không phải chờ AniMapper,
/// – AniMapper lỗi tạm thời thì trả bản cũ (RAM, rồi tới DB),
/// – cache ngắn các lỗi 404.
/// </summary>
public sealed class AniMapperResponseCache(
    IMemoryCache memory,
    PersistentResponseStore store,
    ILogger<AniMapperResponseCache> logger)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<object>>> inFlight = new();

    public async Task<T> GetOrFetchAsync<T>(
        string key,
        CachePolicy policy,
        Func<CancellationToken, Task<T>> fetch,
        CancellationToken cancellationToken,
        bool allowStale = true)
        where T : class
    {
        if (memory.TryGetValue(FreshKey(key), out T? cached) && cached is not null)
            return cached;

        if (memory.TryGetValue(NotFoundKey(key), out ApiException? notFound) && notFound is not null)
            throw notFound;

        // Có bản cũ (loại được lưu bền vững) → trả ngay, làm mới ở nền: người xem không phải chờ AniMapper.
        if (allowStale && policy.Persist is not null && memory.TryGetValue(StaleKey(key), out T? stale) && stale is not null)
        {
            RefreshInBackground(key, policy, fetch);
            return stale;
        }

        // Request upstream dùng chung cho mọi client đang chờ cùng key → không gắn với CancellationToken của 1 client.
        var shared = inFlight.GetOrAdd(key, k => new Lazy<Task<object>>(() => FetchAsync(k, policy, fetch, allowStale)));
        return (T)await shared.Value.WaitAsync(cancellationToken);
    }

    /// <summary>Xóa cache của một key (vd: client báo link đã hỏng, cần lấy link mới).</summary>
    public void Invalidate(string key)
    {
        memory.Remove(FreshKey(key));
        memory.Remove(NotFoundKey(key));
    }

    private async Task<object> FetchAsync<T>(
        string key,
        CachePolicy policy,
        Func<CancellationToken, Task<T>> fetch,
        bool allowStale)
        where T : class
    {
        try
        {
            // allowStale = false (vd: refresh link hỏng) → bỏ qua bản lưu, bắt buộc lấy mới.
            var stored = policy.Persist is not null && allowStale
                ? await store.TryGetAsync<T>(key)
                : null;

            if (stored is not null && stored.Age < policy.Fresh)
            {
                memory.Set(FreshKey(key), stored.Value, policy.Fresh - stored.Age);
                memory.Set(StaleKey(key), stored.Value, policy.Stale);
                return stored.Value;
            }

            // Bản lưu đã cũ → vẫn trả ngay (không chờ AniMapper), làm mới ở nền.
            if (stored is not null)
            {
                memory.Set(StaleKey(key), stored.Value, policy.Stale);
                RefreshInBackground(key, policy, fetch);
                return stored.Value;
            }

            try
            {
                var value = await fetch(CancellationToken.None);
                memory.Set(FreshKey(key), value, policy.Fresh);
                memory.Set(StaleKey(key), value, policy.Stale);

                if (policy.Persist is { } retention)
                    await store.SaveAsync(key, value, retention);

                return value;
            }
            catch (ApiException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound && policy.NotFound > TimeSpan.Zero)
            {
                memory.Set(NotFoundKey(key), ex, policy.NotFound);
                throw;
            }
            catch (Exception ex) when (allowStale && IsTransient(ex) && TryGetStale(key, stored, out var stale))
            {
                logger.LogWarning("AniMapper failed for {Key} ({Error}), serving stale response", key, ex.Message);
                return stale;
            }
        }
        finally
        {
            inFlight.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Làm mới 1 key ở nền (mỗi key tối đa 1 lần chạy cùng lúc).
    /// AniMapper lỗi → chờ 2 phút mới thử lại, tránh mỗi lượt xem lại gọi vào upstream đang sập.
    /// </summary>
    private void RefreshInBackground<T>(string key, CachePolicy policy, Func<CancellationToken, Task<T>> fetch)
        where T : class
    {
        if (memory.TryGetValue(RefreshBackoffKey(key), out _) || !refreshing.TryAdd(key, 0))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                var value = await fetch(CancellationToken.None);
                memory.Set(FreshKey(key), value, policy.Fresh);
                memory.Set(StaleKey(key), value, policy.Stale);

                if (policy.Persist is { } retention)
                    await store.SaveAsync(key, value, retention);
            }
            catch (Exception ex)
            {
                memory.Set(RefreshBackoffKey(key), true, RefreshBackoff);
                logger.LogInformation("Background refresh failed for {Key} ({Error}); keeping stored response", key, ex.Message);
            }
            finally
            {
                refreshing.TryRemove(key, out _);
            }
        });
    }

    private static readonly TimeSpan RefreshBackoff = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, byte> refreshing = new();

    /// <summary>Có bản lưu trong DB chưa (dùng khi lưu sẵn nguồn phát để bỏ qua tập đã có).</summary>
    public async Task<bool> HasStoredAsync<T>(string key) where T : class
        => await store.TryGetAsync<T>(key) is not null;

    private static string RefreshBackoffKey(string key) => $"animapper:refresh-backoff:{key}";

    /// <summary>Bản dự phòng: ưu tiên RAM, không có thì dùng bản lưu DB (dù đã cũ).</summary>
    private bool TryGetStale<T>(string key, StoredResponse<T>? stored, out T stale)
        where T : class
    {
        if (memory.TryGetValue(StaleKey(key), out T? fromMemory) && fromMemory is not null)
        {
            stale = fromMemory;
            return true;
        }

        if (stored is not null)
        {
            stale = stored.Value;
            return true;
        }

        stale = null!;
        return false;
    }

    /// <summary>Lỗi tạm thời: timeout, mất kết nối, 429, 5xx (kể cả provider bị ngắt).</summary>
    public static bool IsTransient(Exception ex) => ex switch
    {
        ApiException api => api.StatusCode >= 500 || api.StatusCode == (int)HttpStatusCode.TooManyRequests,
        TaskCanceledException or TimeoutException or HttpRequestException => true,
        _ => false
    };

    private static string FreshKey(string key) => $"animapper:fresh:{key}";

    private static string StaleKey(string key) => $"animapper:stale:{key}";

    private static string NotFoundKey(string key) => $"animapper:404:{key}";
}

/// <summary>Chính sách cache cho từng endpoint AniMapper.</summary>
public static class AniMapperCachePolicies
{
    private static readonly TimeSpan NoNotFoundCache = TimeSpan.Zero;

    public static readonly CachePolicy Search = new(TimeSpan.FromMinutes(5), TimeSpan.FromHours(6), NoNotFoundCache, TimeSpan.FromDays(7));

    /// <summary>Không lưu DB: số key quá nhiều (mỗi tiền tố gõ là 1 key), giá trị lưu thấp.</summary>
    public static readonly CachePolicy Autocomplete = new(TimeSpan.FromMinutes(10), TimeSpan.FromHours(6), NoNotFoundCache);

    public static readonly CachePolicy Metadata = new(TimeSpan.FromMinutes(30), TimeSpan.FromDays(1), TimeSpan.FromMinutes(10), TimeSpan.FromDays(30));

    /// <summary>Danh sách server gần như không đổi.</summary>
    public static readonly CachePolicy Servers = new(TimeSpan.FromHours(6), TimeSpan.FromDays(2), TimeSpan.FromMinutes(10), TimeSpan.FromDays(30));

    /// <summary>AniMapper tự làm mới danh sách tập mỗi 7 ngày; giữ 30 phút để anime đang chiếu có tập mới sớm.</summary>
    public static readonly CachePolicy Episodes = new(TimeSpan.FromMinutes(30), TimeSpan.FromDays(2), TimeSpan.FromMinutes(10), TimeSpan.FromDays(30));

    /// <summary>
    /// Link M3U8 cache phía AniMapper sống 24h → giữ 20 phút, bản dự phòng 12h.
    /// Bản DB giữ 30 ngày và được trả ngay khi xem (làm mới ở nền): nhiều provider trả link tĩnh (vd: file m3u8 trên storage) dùng được lâu;
    /// link đã chết thì client gọi lại với refresh=true.
    /// </summary>
    public static readonly CachePolicy Source = new(TimeSpan.FromMinutes(20), TimeSpan.FromHours(12), TimeSpan.FromMinutes(5), TimeSpan.FromDays(30));

    public static readonly CachePolicy M3u8 = new(TimeSpan.FromMinutes(30), TimeSpan.FromHours(12), NoNotFoundCache, TimeSpan.FromDays(30));
}
