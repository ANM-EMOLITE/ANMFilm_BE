using Anime.Application.Common;
using Anime.Application.DTOs.Searchs;
using Anime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Anime.Api.Controllers;

[ApiController]
[Route("api/v1/search")]
[Produces("application/json")]
public sealed class SearchController(ISearchService searchService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SearchResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SearchResponseDto>> Search([FromQuery] SearchRequestDto request, CancellationToken cancellationToken)
        => Ok(await searchService.SearchAsync(request, cancellationToken));
}
