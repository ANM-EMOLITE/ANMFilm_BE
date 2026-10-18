using Anime.Api.Auth;
using Anime.Application.DTOs.Comments;
using Anime.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Anime.Api.Controllers;

[ApiController]
[Route("api/v1/comments")]
[Produces("application/json")]
public sealed class CommentsController(ICommentService comments) : ControllerBase
{
    /// <summary>Danh sách bình luận của 1 phim (công khai).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<CommentPageDto>> List(
        [FromQuery] int animeId,
        [FromQuery] int limit = 30,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
        => Ok(await comments.ListAsync(animeId, limit, offset, cancellationToken));

    /// <summary>Đăng bình luận (cần đăng nhập).</summary>
    [HttpPost]
    [Authorize(Policy = AdminAuthSetup.UserPolicy)]
    public async Task<ActionResult<CommentDto>> Add([FromBody] AddCommentRequestDto request, CancellationToken cancellationToken)
        => Ok(await comments.AddAsync(this.UserId(), request, cancellationToken));

    /// <summary>Xoá bình luận của chính mình.</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = AdminAuthSetup.UserPolicy)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await comments.DeleteAsync(this.UserId(), id, cancellationToken);
        return NoContent();
    }
}
