using Anime.Application.DTOs.Comments;

namespace Anime.Application.Interfaces;

public interface ICommentService
{
    /// <summary>Danh sách bình luận của 1 phim (mới nhất trước). Công khai, không cần đăng nhập.</summary>
    Task<CommentPageDto> ListAsync(int animeId, int limit, int offset, CancellationToken cancellationToken = default);

    Task<CommentDto> AddAsync(int userId, AddCommentRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Xoá bình luận của chính mình (xoá mềm). Không phải của mình → 403.</summary>
    Task DeleteAsync(int userId, long commentId, CancellationToken cancellationToken = default);
}
