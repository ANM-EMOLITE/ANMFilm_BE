using System.Collections.Concurrent;
using System.Net;
using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Anime.Infrastructure.AniMapper;

/// <summary>Thời gian giữ cache cho một loại response.</summary>
/// <param name="Fresh">Trong khoảng này trả cache, không gọi AniMapper.</param>
/// <param name="Stale">Bản dự phòng: AniMapper lỗi tạm thời (timeout/5xx/429) thì trả bản này thay vì báo lỗi.</param>
/// <param name="NotFound">Cache lỗi 404 (mapping/tập không tồn tại) để không hỏi lại liên tục.</param>
public sealed record CachePolicy(TimeSpan Fresh, TimeSpan Stale, TimeSpan NotFound);

/// <summary>
/// Cache in-memory cho response AniMapper:
/// – gộp các request trùng đang chạy (N client cùng xem 1 tập → 1 request upstream),
/// – trả bản cũ (stale) khi upstream lỗi tạm thời,
/// – cache ngắn các lỗi 404.
/// </summary>
public sealed class AniMapperResponseCache(IMemoryCache memory, ILogger<AniMapperResponseCache> logger)
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
            var value = await fetch(CancellationToken.None);
            memory.Set(FreshKey(key), value, policy.Fresh);
            memory.Set(StaleKey(key), value, policy.Stale);
            return value;
        }
        catch (ApiException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound && policy.NotFound > TimeSpan.Zero)
        {
            memory.Set(NotFoundKey(key), ex, policy.NotFound);
            throw;
        }
        catch (Exception ex) when (allowStale && IsTransient(ex) && memory.TryGetValue(StaleKey(key), out T? stale) && stale is not null)
        {
            logger.LogWarning("AniMapper failed for {Key} ({Error}), serving stale response", key, ex.Message);
            return stale;
        }
        finally
        {
            inFlight.TryRemove(key, out _);
        }
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

    public static readonly CachePolicy Search = new(TimeSpan.FromMinutes(5), TimeSpan.FromHours(6), NoNotFoundCache);
    public static readonly CachePolicy Autocomplete = new(TimeSpan.FromMinutes(10), TimeSpan.FromHours(6), NoNotFoundCache);
    public static readonly CachePolicy Metadata = new(TimeSpan.FromMinutes(30), TimeSpan.FromDays(1), TimeSpan.FromMinutes(10));

    /// <summary>Danh sách server gần như không đổi.</summary>
    public static readonly CachePolicy Servers = new(TimeSpan.FromHours(6), TimeSpan.FromDays(2), TimeSpan.FromMinutes(10));

    /// <summary>AniMapper tự làm mới danh sách tập mỗi 7 ngày; giữ 30 phút để anime đang chiếu có tập mới sớm.</summary>
    public static readonly CachePolicy Episodes = new(TimeSpan.FromMinutes(30), TimeSpan.FromDays(2), TimeSpan.FromMinutes(10));

    /// <summary>Link M3U8 cache phía AniMapper sống 24h → giữ 20 phút, bản dự phòng 12h.</summary>
    public static readonly CachePolicy Source = new(TimeSpan.FromMinutes(20), TimeSpan.FromHours(12), TimeSpan.FromMinutes(5));

    public static readonly CachePolicy M3u8 = new(TimeSpan.FromMinutes(30), TimeSpan.FromHours(12), NoNotFoundCache);
}
