using Anime.Application.Common.Exceptions;
using Anime.Application.Common.Validation;
using Anime.Application.DTOs.User;
using Anime.Application.Interfaces;
using Anime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anime.Infrastructure.Users;

/// <summary>Watchlist + lịch sử xem + nhật ký hoạt động của user.</summary>
public sealed class UserLibraryService(IDbContextFactory<AnimeDbContext> dbFactory) : IUserLibraryService
{
    public async Task<IReadOnlyList<WatchlistItemDto>> GetWatchlistAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Watchlist.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.AddedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new WatchlistItemDto(x.AnimeId, x.Title, x.Cover, x.AddedAt)).ToList();
    }

    public async Task<WatchlistItemDto> AddToWatchlistAsync(int userId, AddWatchlistRequestDto request, CancellationToken cancellationToken = default)
    {
        var animeId = Guard.Positive(request.AnimeId, "animeId");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.Watchlist.FirstOrDefaultAsync(x => x.UserId == userId && x.AnimeId == animeId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var (title, cover) = await ResolveTitleCoverAsync(db, animeId, request.Title, request.Cover, cancellationToken);

        if (item is null)
        {
            item = new WatchlistItem { UserId = userId, AnimeId = animeId, Title = title, Cover = cover, AddedAt = now };
            db.Watchlist.Add(item);
            db.ActivityLogs.Add(Log(userId, UserActivityActions.WatchlistAdd, animeId, title));
        }
        else
        {
            // Đã lưu rồi → cập nhật ảnh/tiêu đề mới nhất.
            item.Title = title;
            item.Cover = cover;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new WatchlistItemDto(item.AnimeId, item.Title, item.Cover, item.AddedAt);
    }

    public async Task RemoveFromWatchlistAsync(int userId, int animeId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var removed = await db.Watchlist
            .Where(x => x.UserId == userId && x.AnimeId == animeId)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed > 0)
        {
            db.ActivityLogs.Add(Log(userId, UserActivityActions.WatchlistRemove, animeId, null));
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<int>> GetWatchlistIdsAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Watchlist.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.AnimeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<WatchHistoryDto> RecordWatchAsync(int userId, RecordWatchRequestDto request, CancellationToken cancellationToken = default)
    {
        var animeId = Guard.Positive(request.AnimeId, "animeId");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var (title, cover) = await ResolveTitleCoverAsync(db, animeId, request.Title, request.Cover, cancellationToken);

        var entry = await db.WatchHistory.FirstOrDefaultAsync(x => x.UserId == userId && x.AnimeId == animeId, cancellationToken);
        if (entry is null)
        {
            entry = new WatchHistoryEntry
            {
                UserId = userId,
                AnimeId = animeId,
                Title = title,
                Cover = cover,
                Episode = request.Episode,
                Provider = request.Provider,
                Count = 1,
                WatchedAt = now
            };
            db.WatchHistory.Add(entry);
        }
        else
        {
            entry.Title = title;
            entry.Cover = cover;
            entry.Episode = request.Episode ?? entry.Episode;
            entry.Provider = request.Provider ?? entry.Provider;
            entry.Count++;
            entry.WatchedAt = now;
        }

        db.ActivityLogs.Add(Log(userId, UserActivityActions.Watch, animeId,
            request.Episode is { } ep ? $"Tập {ep}" : null));
        await db.SaveChangesAsync(cancellationToken);

        return new WatchHistoryDto(entry.AnimeId, entry.Title, entry.Cover, entry.Episode, entry.Provider, entry.Count, entry.WatchedAt);
    }

    public async Task<IReadOnlyList<WatchHistoryDto>> GetHistoryAsync(int userId, int limit, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.WatchHistory.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.WatchedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);

        return rows.Select(x => new WatchHistoryDto(x.AnimeId, x.Title, x.Cover, x.Episode, x.Provider, x.Count, x.WatchedAt)).ToList();
    }

    public async Task<IReadOnlyList<ActivityLogDto>> GetActivityAsync(int userId, int limit, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.ActivityLogs.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);

        return rows.Select(x => new ActivityLogDto(x.Id, x.Action, x.TargetType, x.TargetId, x.Detail, x.CreatedAt)).ToList();
    }

    /// <summary>Ưu tiên tiêu đề/ảnh FE gửi; thiếu thì lấy từ danh mục nếu có.</summary>
    private static async Task<(string? Title, string? Cover)> ResolveTitleCoverAsync(
        AnimeDbContext db, int animeId, string? title, string? cover, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(cover))
            return (title.Trim(), cover.Trim());

        var catalog = await db.Anime.AsNoTracking()
            .Where(x => x.Id == animeId)
            .Select(x => new { x.DisplayTitle })
            .FirstOrDefaultAsync(cancellationToken);

        return (
            string.IsNullOrWhiteSpace(title) ? catalog?.DisplayTitle : title.Trim(),
            string.IsNullOrWhiteSpace(cover) ? null : cover.Trim());
    }

    private static UserActivityLog Log(int userId, string action, int animeId, string? detail) => new()
    {
        UserId = userId,
        Action = action,
        TargetType = "ANIME",
        TargetId = animeId.ToString(),
        Detail = detail,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
