using System.Net;
using System.Threading.RateLimiting;
using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Anime.Infrastructure.AniMapper;

/// <summary>
/// Điều tiết request gửi lên AniMapper (giới hạn 60 request/phút cho toàn bộ BE):
/// – Token bucket: tối đa <see cref="AniMapperOptions.RequestsPerMinute"/> request/phút, cho phép burst nhỏ.
/// – Khi AniMapper trả 429: tạm dừng MỌI request tới hết Retry-After.
/// Request phải chờ quá <see cref="AniMapperOptions.MaxQueueWaitSeconds"/> thì trả 429 ngay cho client.
/// </summary>
public sealed class AniMapperRateGate : IDisposable
{
    private readonly TokenBucketRateLimiter limiter;
    private readonly TimeSpan maxWait;
    private readonly ILogger<AniMapperRateGate> logger;
    private long pausedUntilTicks;

    public AniMapperRateGate(IOptions<AniMapperOptions> options, ILogger<AniMapperRateGate> logger)
    {
        this.logger = logger;
        var value = options.Value;
        var perMinute = Math.Max(1, value.RequestsPerMinute);

        limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = Math.Clamp(value.BurstSize, 1, perMinute),
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromMilliseconds(60_000.0 / perMinute),
            QueueLimit = 500,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
        maxWait = TimeSpan.FromSeconds(Math.Max(1, value.MaxQueueWaitSeconds));
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(maxWait);

        try
        {
            var pause = PausedFor();
            if (pause > TimeSpan.Zero)
                await Task.Delay(pause, timeout.Token);

            using var lease = await limiter.AcquireAsync(1, timeout.Token);
            if (!lease.IsAcquired)
                throw Overloaded();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Overloaded();
        }
    }

    /// <summary>AniMapper báo 429 → dừng mọi request trong khoảng <paramref name="duration"/>.</summary>
    public void Pause(TimeSpan duration)
    {
        var until = DateTime.UtcNow.Add(duration).Ticks;
        long current;
        do
        {
            current = Interlocked.Read(ref pausedUntilTicks);
            if (until <= current) return;
        } while (Interlocked.CompareExchange(ref pausedUntilTicks, until, current) != current);

        logger.LogWarning("AniMapper rate limit hit, pausing upstream requests for {Seconds}s", duration.TotalSeconds);
    }

    private TimeSpan PausedFor()
    {
        var remaining = new DateTime(Interlocked.Read(ref pausedUntilTicks), DateTimeKind.Utc) - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static UpstreamApiException Overloaded() => new(
        (int)HttpStatusCode.TooManyRequests,
        ErrorCodes.RateLimited,
        "Máy chủ đang quá tải, vui lòng thử lại sau ít phút.");

    public void Dispose() => limiter.Dispose();
}

/// <summary>
/// Handler của HttpClient AniMapper: xin "lượt" từ <see cref="AniMapperRateGate"/> trước mỗi request,
/// gặp 429 thì tạm dừng toàn cục và thử lại 1 lần nếu Retry-After ngắn.
/// </summary>
public sealed class AniMapperRateLimitHandler(AniMapperRateGate gate) : DelegatingHandler
{
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxInlineRetryWait = TimeSpan.FromSeconds(5);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            await gate.WaitAsync(cancellationToken);
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.TooManyRequests)
                return response;

            var retryAfter = RetryAfter(response);
            gate.Pause(retryAfter);

            if (attempt >= 1 || retryAfter > MaxInlineRetryWait)
                return response;

            response.Dispose();
        }
    }

    private static TimeSpan RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        var value = header?.Delta ?? (header?.Date - DateTimeOffset.UtcNow);
        return value is { } v && v > TimeSpan.Zero ? v : DefaultRetryAfter;
    }
}
