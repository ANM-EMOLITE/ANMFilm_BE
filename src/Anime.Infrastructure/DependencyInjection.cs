using Anime.Application.Interfaces;
using System.Net;
using Anime.Infrastructure.AniList;
using Anime.Infrastructure.AniMapper;
using Anime.Infrastructure.Streaming;
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
            client.Timeout = TimeSpan.FromSeconds(20);
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
