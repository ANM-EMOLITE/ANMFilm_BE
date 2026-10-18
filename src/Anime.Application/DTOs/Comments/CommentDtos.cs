namespace Anime.Application.DTOs.Comments;

public sealed record CommentDto(
    long Id,
    int UserId,
    string UserName,
    int AnimeId,
    int? Episode,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt);

public sealed class AddCommentRequestDto
{
    public int AnimeId { get; set; }

    /// <summary>Tập đang xem (tuỳ chọn).</summary>
    public int? Episode { get; set; }

    public string? Content { get; set; }
}

public sealed record CommentPageDto(IReadOnlyList<CommentDto> Items, int Total);
