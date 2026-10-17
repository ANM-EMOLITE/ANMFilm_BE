using Anime.Application.Interfaces;
using System.Net;
using Anime.Infrastructure.AniList;
using Anime.Infrastructure.Auth;
using Anime.Infrastructure.Catalog;
using Anime.Infrastructure.AniMapper;
using Anime.Infrastructure.Persistence;
using Anime.Infrastructure.Streaming;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Anime.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AniMapperOptions>(configuration.GetSection(AniMapperOptions.SectionName));

        services.AddMemoryCache();

        // PostgreSQL: lưu bền vững response AniMapper (metadata, tập, link stream...) để dùng khi upstream sập.
        services.AddDbContextFactory<AnimeDbContext>(options => options.UseNpgsql(
            PostgresConnectionString.Normalize(configuration.GetConnectionString("AnimeDb")),
            // Tự thử lại khi kết nối rớt tạm thời (Neon cold start / cắt kết nối rảnh).
            npgsql => npgsql.CommandTimeout(30).EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));
        services.AddSingleton<PersistentResponseStore>();
        services.AddHostedService<PersistentCacheMaintenance>();

        // Danh mục anime + admin: đồng bộ chỉ chạy khi admin bấm (hàng đợi + worker), không chạy theo lịch.
        services.AddSingleton<CatalogStore>();
        services.AddSingleton<ICatalogStore>(sp => sp.GetRequiredService<CatalogStore>());
        services.AddTransient<CatalogSyncRunner>();
        services.AddTransient<StreamPrefetchRunner>();
        services.AddTransient<StreamHealthRunner>();
        services.AddSingleton<SyncJobQueue>();
        services.AddSingleton<SyncJobCanceller>();
        services.AddScoped<ISyncJobService, SyncJobService>();
        services.AddScoped<IAdminCatalogService, AdminCatalogService>();
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddHostedService<SyncJobWorker>();

        // Tài khoản người xem: đăng ký/đăng nhập, watchlist, lịch sử xem, nhật ký.
        services.AddScoped<IUserAuthService, Users.UserAuthService>();
        services.AddScoped<IUserLibraryService, Users.UserLibraryService>();

        // AniMapper: giới hạn 60 request/phút → điều tiết request (rate gate) + cache + circuit breaker theo provider.
        services.AddSingleton<AniMapperRateGate>();
        services.AddSingleton<AniMapperResponseCache>();
        services.AddSingleton<ProviderHealth>();
        services.AddTransient<AniMapperRateLimitHandler>();

        services.AddHttpClient<AniMapperClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<AniMapperOptions>>().Value;

                var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";

                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            })
            .AddHttpMessageHandler<AniMapperRateLimitHandler>();

        services.AddTransient<IAniMapperClient, CachingAniMapperClient>();

        // AniList GraphQL: lấy danh sách anime thịnh hành/phổ biến/theo mùa cho /browse.
        services.AddHttpClient<IAniListClient, AniListClient>(client =>
        {
            client.BaseAddress = new Uri("https://graphql.anilist.co/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // HttpClient riêng cho proxy video: theo redirect, giải nén, timeout dài hơn.
        services.AddHttpClient(StreamProxyService.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.All
            });

        services.AddScoped<IStreamProxyService, StreamProxyService>();

        return services;
    }
}
