using Anime.Api.Auth;
using Anime.Api.Extensions;
using Anime.Api.Middleware;
using Anime.Infrastructure;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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

app.Run();
