using Anime.Api.Extensions;
using Anime.Api.Middleware;
using Anime.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiControllers()
    .AddApplicationServices()
    .AddInfrastructure(builder.Configuration)
    .AddFrontendCors();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(ServiceCollectionExtensions.CorsPolicy);

app.MapControllers();

app.Run();
