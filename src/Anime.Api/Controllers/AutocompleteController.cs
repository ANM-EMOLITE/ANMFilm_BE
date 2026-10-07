using Anime.Application.Common;
using Anime.Application.DTOs.Autocomplete;
using Anime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Anime.Api.Controllers;

[ApiController]
[Route("api/v1/autocomplete")]
[Produces("application/json")]
public sealed class AutocompleteController(IAutocompleteService autocompleteService) : ControllerBase
{
    /// <summary>Gợi ý tiêu đề theo tiền tố (tối đa 10).</summary>
    [HttpGet]
    [ProducesResponseType<AutocompleteResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AutocompleteResponseDto>> Get(
        [FromQuery] AutocompleteRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await autocompleteService.GetSuggestionsAsync(request, cancellationToken));

    /// <summary>Trạng thái dịch vụ autocomplete của AniMapper.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var json = await autocompleteService.GetStatusAsync(cancellationToken);
        return Content(json, "application/json");
    }
}
