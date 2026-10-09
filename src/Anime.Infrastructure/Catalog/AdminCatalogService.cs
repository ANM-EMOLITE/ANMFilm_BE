using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.Common.Validation;
using Anime.Application.DTOs.Admin;
using Anime.Application.Interfaces;
using Anime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anime.Infrastructure.Catalog;

public sealed class AdminCatalogService(
    IDbContextFactory<AnimeDbContext> dbFactory,
    CatalogStore catalogStore,
    CatalogSyncRunner syncRunner) : IAdminCatalogService
{
    private const int MaxPageSize = 100;

    public async Task<AdminStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var total = await db.Anime.CountAsync(cancellationToken);
        var withStream = await db.Anime.CountAsync(x => x.HasStream, cancellationToken);
        var hidden = await db.Anime.CountAsync(x => x.IsHidden, cancellationToken);
        var broken = await db.ProviderStatuses.Where(x => x.Status == ProviderCheckStatus.Broken)
            .Select(x => x.AnimeId).Distinct().CountAsync(cancellationToken);

        var lists = await db.Lists.AsNoTracking().OrderBy(x => x.Key).ToListAsync(cancellationToken);
        var lastJob = await db.SyncJobs.AsNoTracking().OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);

        return new AdminStatsDto(
            total,
            withStream,
            hidden,
            lists.Select(x => new AdminListDto(x.Key, CatalogLists.LabelOf(x.Key), x.ItemCount, x.SyncedAt)).ToList(),
            lastJob is null ? null : SyncJobService.ToDto(lastJob),
            broken);
    }

    public async Task<AdminAnimePageDto> ListAnimeAsync(AdminAnimeQueryDto query, CancellationToken cancellationToken = default)
    {
        Guard.InRange(query.Page, 1, 100_000, "page");
        Guard.InRange(query.PageSize, 1, MaxPageSize, "pageSize");
        var health = Guard.OneOf(query.Health, "health", ["BROKEN", "OK", "UNCHECKED"]);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var anime = db.Anime.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var q = query.Q.Trim();
            anime = int.TryParse(q, out var id)
                ? anime.Where(x => x.Id == id || EF.Functions.ILike(x.DisplayTitle, $"%{q}%"))
                : anime.Where(x => EF.Functions.ILike(x.DisplayTitle, $"%{q}%") || EF.Functions.ILike(x.SearchText, $"%{q}%"));
        }

        if (query.HasStream is { } hasStream)
            anime = anime.Where(x => x.HasStream == hasStream);

        if (query.Hidden is { } hidden)
            anime = anime.Where(x => x.IsHidden == hidden);

        var statuses = db.ProviderStatuses;
        anime = health switch
        {
            "BROKEN" => anime.Where(a => statuses.Any(s => s.AnimeId == a.Id && s.Status == ProviderCheckStatus.Broken)),
            "OK" => anime.Where(a => statuses.Any(s => s.AnimeId == a.Id) && !statuses.Any(s => s.AnimeId == a.Id && s.Status == ProviderCheckStatus.Broken)),
            "UNCHECKED" => anime.Where(a => a.HasStream && !statuses.Any(s => s.AnimeId == a.Id)),
            _ => anime
        };

        IQueryable<CatalogAnime> ordered;
        if (!string.IsNullOrWhiteSpace(query.List))
        {
            var listKey = query.List.Trim().ToUpperInvariant();
            ordered =
                from item in db.ListItems
                join a in anime on item.AnimeId equals a.Id
                where item.ListKey == listKey
                orderby item.Position
                select a;
        }
        else
        {
            ordered = anime.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id);
        }

        var total = await ordered.CountAsync(cancellationToken);
        var rows = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var ids = rows.Select(x => x.Id).ToList();
        var byAnime = (await db.ProviderStatuses.AsNoTracking().Where(x => ids.Contains(x.AnimeId)).ToListAsync(cancellationToken))
            .ToLookup(x => x.AnimeId);

        return new AdminAnimePageDto(rows.Select(a => CatalogMapper.ToAdmin(a, byAnime[a.Id])).ToList(), total, query.Page, query.PageSize);
    }

    public async Task<AdminAnimeDto> UpdateAnimeAsync(int id, UpdateAnimeRequestDto request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.Anime.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiException(404, ErrorCodes.MediaNotFound, $"Anime {id} chưa có trong danh mục");

        if (request.IsHidden is { } isHidden)
        {
            entity.IsHidden = isHidden;
            // Admin tự quyết → kiểm tra nguồn phát sau này không tự hiện/ẩn lại.
            entity.AutoHidden = false;
        }

        await db.SaveChangesAsync(cancellationToken);
        catalogStore.InvalidateHidden();

        return await ToAdminAsync(db, entity, cancellationToken);
    }

    public async Task<AdminAnimeDto> UpdateProviderAsync(int id, string provider, UpdateProviderRequestDto request, CancellationToken cancellationToken = default)
    {
        var name = Guard.Required(provider, "provider").ToUpperInvariant();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.Anime.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiException(404, ErrorCodes.MediaNotFound, $"Anime {id} chưa có trong danh mục");

        if (!entity.Providers.Contains(name))
            throw new ValidationException($"“{entity.DisplayTitle}” không có provider {name}");

        var status = await db.ProviderStatuses.FirstOrDefaultAsync(x => x.AnimeId == id && x.Provider == name, cancellationToken);
        if (status is null)
        {
            status = new AnimeProviderStatus { AnimeId = id, Provider = name, Status = ProviderCheckStatus.Ok, CheckedAt = DateTimeOffset.UtcNow };
            db.ProviderStatuses.Add(status);
        }

        status.IsDisabled = request.IsDisabled;
        // Admin tự bật/tắt → kiểm tra tự động không ghi đè.
        status.DisabledBy = StreamHealthRunner.DisabledByAdmin;

        await db.SaveChangesAsync(cancellationToken);
        catalogStore.InvalidateHidden();

        return await ToAdminAsync(db, entity, cancellationToken);
    }

    public async Task<SyncAnimeResultDto> SyncAnimeAsync(int id, CancellationToken cancellationToken = default)
    {
        Guard.Positive(id, "id");
        var (anime, created) = await syncRunner.SyncOneAsync(id, cancellationToken);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return new SyncAnimeResultDto(await ToAdminAsync(db, anime, cancellationToken), created);
    }

    private static async Task<AdminAnimeDto> ToAdminAsync(AnimeDbContext db, CatalogAnime entity, CancellationToken cancellationToken)
    {
        var statuses = await db.ProviderStatuses.AsNoTracking().Where(x => x.AnimeId == entity.Id).ToListAsync(cancellationToken);
        return CatalogMapper.ToAdmin(entity, statuses);
    }
}
