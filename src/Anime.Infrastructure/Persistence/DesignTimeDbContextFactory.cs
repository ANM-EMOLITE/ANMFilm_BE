using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Anime.Infrastructure.Persistence;

/// <summary>
/// Cho phép <c>dotnet ef migrations add</c> chạy với project Infrastructure làm startup
/// (không cần build Anime.Api, kể cả khi API đang chạy). Chỉ dùng để sinh migration, không kết nối DB.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AnimeDbContext>
{
    public AnimeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AnimeDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time")
            .Options;

        return new AnimeDbContext(options);
    }
}
