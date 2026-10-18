using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.Common.Validation;
using Anime.Application.DTOs.Comments;
using Anime.Application.Interfaces;
using Anime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anime.Infrastructure.Users;

public sealed class CommentService(IDbContextFactory<AnimeDbContext> dbFactory) : ICommentService
{
    private const int MaxLength = 2000;

    public async Task<CommentPageDto> ListAsync(int animeId, int limit, int offset, CancellationToken cancellationToken = default)
    {
        animeId = Guard.Positive(animeId, "animeId");
        limit = Math.Clamp(limit, 1, 100);
        offset = Math.Max(0, offset);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var query = from c in db.Comments.AsNoTracking()
                    join u in db.Users.AsNoTracking() on c.UserId equals u.Id
                    where c.AnimeId == animeId && !c.IsDeleted
                    orderby c.CreatedAt descending
                    select new { c, Name = u.DisplayName ?? u.Username };

        var total = await db.Comments.CountAsync(x => x.AnimeId == animeId && !x.IsDeleted, cancellationToken);
        var rows = await query.Skip(offset).Take(limit).ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new CommentDto(r.c.Id, r.c.UserId, r.Name, r.c.AnimeId, r.c.Episode, r.c.Content, r.c.CreatedAt, r.c.EditedAt))
            .ToList();

        return new CommentPageDto(items, total);
    }

    public async Task<CommentDto> AddAsync(int userId, AddCommentRequestDto request, CancellationToken cancellationToken = default)
    {
        var animeId = Guard.Positive(request.AnimeId, "animeId");
        var content = Guard.Required(request.Content, "content");
        if (content.Length > MaxLength)
            content = content[..MaxLength];

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new ApiException(401, ErrorCodes.Unauthorized, "Tài khoản không tồn tại");
        if (user.IsBanned)
            throw new ApiException(403, ErrorCodes.Unauthorized, "Tài khoản đã bị khoá");

        var comment = new Comment
        {
            UserId = userId,
            AnimeId = animeId,
            Episode = request.Episode,
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);

        var name = user.DisplayName ?? user.Username;
        return new CommentDto(comment.Id, userId, name, animeId, comment.Episode, comment.Content, comment.CreatedAt, null);
    }

    public async Task DeleteAsync(int userId, long commentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var comment = await db.Comments.FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken)
            ?? throw new ApiException(404, ErrorCodes.ResourceNotFound, "Bình luận không tồn tại");

        if (comment.UserId != userId)
            throw new ApiException(403, ErrorCodes.Unauthorized, "Không thể xoá bình luận của người khác");

        comment.IsDeleted = true;
        comment.EditedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
