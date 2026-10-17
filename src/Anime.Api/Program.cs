using Anime.Api.Auth;
using Anime.Api.Extensions;
using Anime.Api.Middleware;
using Anime.Infrastructure;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Render / Railway / Fly cấp cổng động qua biến PORT → lắng nghe đúng cổng đó.
// Local không có PORT → dùng cấu hình trong launchSettings (localhost:5200).
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

using var startupLogger = LoggerFactory.Create(logging => logging.AddConsole());

builder.Services
    .AddApiControllers()
    .AddApplicationServices()
    .AddInfrastructure(builder.Configuration)
    .AddAdminAuth(builder.Configuration, startupLogger.CreateLogger("Startup"))
    .AddFrontendCors();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swagger =>
{
    // Nút "Authorize" trên Swagger để thử các API admin.
    swagger.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Token lấy từ POST /api/v1/admin/auth/login"
    });
    swagger.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(ServiceCollectionExtensions.CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// Health check cho Render/giám sát (luôn bật, kể cả Production).
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
