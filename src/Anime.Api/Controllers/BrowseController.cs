using Anime.Application.Common;
using Anime.Application.DTOs.Browse;
using Anime.Application.DTOs.Searchs;
using Anime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Anime.Api.Controllers;

[ApiController]
[Route("api/v1/browse")]
[Produces("application/json")]
public sealed class BrowseController(IBrowseService browseService) : ControllerBase
{
    /// <summary>
    /// Danh sách anime đã có nguồn phát theo tab: TRENDING, POPULAR, SEASON, NEW, AIRING, FINISHED, MOVIE.
    /// Trang tiếp theo: gọi lại với offset = nextOffset.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<SearchResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SearchResponseDto>> Browse([FromQuery] BrowseRequestDto request, CancellationToken cancellationToken)
        => Ok(await browseService.BrowseAsync(request, cancellationToken));
}
