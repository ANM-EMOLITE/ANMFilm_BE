using System.Collections.Concurrent;
using System.Net;
using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace Anime.Infrastructure.AniMapper;

/// <summary>
/// Circuit breaker theo provider (ANIMEVIETSUB, NINIYO…):
/// lỗi tạm thời (timeout / 5xx) liên tiếp <see cref="FailureThreshold"/> lần trong <see cref="FailureWindow"/>
/// → "ngắt" provider trong <see cref="OpenDuration"/>: trả 503 ngay thay vì bắt client chờ timeout 30s,
/// để FE chuyển sang provider khác. Hết thời gian ngắt thì cho 1 request thử lại.
/// </summary>
public sealed class ProviderHealth(ILogger<ProviderHealth> logger)
{
    private const int FailureThreshold = 3;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, State> states = new(StringComparer.OrdinalIgnoreCase);

    public void ThrowIfUnavailable(string provider)
    {
        if (!states.TryGetValue(provider, out var state))
            return;

        lock (state)
        {
            if (state.OpenUntil > DateTime.UtcNow)
            {
                throw new UpstreamApiException(
                    (int)HttpStatusCode.ServiceUnavailable,
                    ErrorCodes.ProviderUnavailable,
                    $"Provider {provider} is temporarily unavailable");
            }
        }
    }

    public void RecordSuccess(string provider)
    {
        if (!states.TryGetValue(provider, out var state))
            return;

        lock (state)
        {
            state.Failures.Clear();
            state.OpenUntil = DateTime.MinValue;
        }
    }

    public void RecordFailure(string provider)
    {
        var state = states.GetOrAdd(provider, _ => new State());
        var now = DateTime.UtcNow;

        lock (state)
        {
            state.Failures.Enqueue(now);
            while (state.Failures.Count > 0 && now - state.Failures.Peek() > FailureWindow)
                state.Failures.Dequeue();

            if (state.Failures.Count < FailureThreshold)
                return;

            state.OpenUntil = now + OpenDuration;
            state.Failures.Clear();
        }

        logger.LogWarning("Provider {Provider} failing repeatedly, short-circuiting for {Seconds}s",
            provider, OpenDuration.TotalSeconds);
    }

    private sealed class State
    {
        public Queue<DateTime> Failures { get; } = new();

        public DateTime OpenUntil { get; set; } = DateTime.MinValue;
    }
}
