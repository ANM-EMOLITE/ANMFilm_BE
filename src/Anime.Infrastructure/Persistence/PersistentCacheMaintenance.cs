using Anime.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Anime.Infrastructure.Persistence;

/// <summary>
/// Khi khởi động: áp migration cho DB và tạo admin đầu tiên (nếu chưa có). Sau đó định kỳ dọn các bản lưu đã hết hạn.
/// DB không kết nối được thì chỉ log, API vẫn chạy bình thường (chỉ mất lớp lưu bền vững).
/// </summary>
public sealed class PersistentCacheMaintenance(
    IServiceProvider services,
    ILogger<PersistentCacheMaintenance> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);

    private static readonly TimeSpan MigrationTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Chạy migration trước khi API nhận request (tối đa 20 giây).</summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(MigrationTimeout);

        try
        {
            await using var db = await services.GetRequiredService<IDbContextFactory<AnimeDbContext>>()
                .CreateDbContextAsync(timeout.Token);
            await db.Database.MigrateAsync(timeout.Token);
            await AdminAuthService.SeedAsync(db, services.GetRequiredService<IConfiguration>(), logger, timeout.Token);
            logger.LogInformation("Persistent cache database is ready");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Cannot migrate persistent cache database; API will run without DB cache");
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);
        do
        {
            try
            {
                var store = services.GetRequiredService<PersistentResponseStore>();
                var deleted = await store.DeleteExpiredAsync(stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Deleted {Count} expired cached responses", deleted);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Cleanup of expired cached responses failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
