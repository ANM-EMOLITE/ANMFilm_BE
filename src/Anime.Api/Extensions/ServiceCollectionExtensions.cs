using System.Text.Json.Serialization;
using Anime.Application.Common;
using Anime.Application.Interfaces;
using Anime.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Anime.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public const string CorsPolicy = "Frontend";

    /// <summary>Đăng ký các use case của tầng Application.</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IBrowseService, BrowseService>();
        services.AddScoped<IAutocompleteService, AutocompleteService>();
        services.AddScoped<IMetadataService, MetadataService>();
        services.AddScoped<IStreamingService, StreamingService>();

        return services;
    }

    /// <summary>Controllers + trả lỗi model binding theo format { success, message, code }.</summary>
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                // Bỏ các field null cho response gọn hơn (vd: metadata/batch chỉ có vài field).
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var (field, errors) = context.ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .Select(x => (x.Key, x.Value!.Errors))
                        .FirstOrDefault();

                    var message = string.IsNullOrEmpty(field)
                        ? "Invalid request"
                        : $"Invalid value for '{field}'";

                    if (errors?.FirstOrDefault()?.ErrorMessage is { Length: > 0 } detail)
                        message += $": {detail}";

                    return new BadRequestObjectResult(
                        new ApiErrorResponse(message, ErrorCodes.InvalidParameter));
                };
            });

        return services;
    }

    public static IServiceCollection AddFrontendCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicy, policy => policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod());
        });

        return services;
    }
}
