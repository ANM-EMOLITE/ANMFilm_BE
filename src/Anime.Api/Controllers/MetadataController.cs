using Anime.Application.Common;
using Anime.Application.DTOs.Metadata;
using Anime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Anime.Api.Controllers;

[ApiController]
[Route("api/v1/metadata")]
[Produces("application/json")]
public sealed class MetadataController(IMetadataService metadataService) : ControllerBase
{
    /// <summary>Chi tiết đầy đủ của một media.</summary>
    /// <param name="id">Media ID, AniList ID hoặc AniList external key.</param>
    [HttpGet]
    [ProducesResponseType<MetadataResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MetadataResponseDto>> GetById(
        [FromQuery] string? id,
        CancellationToken cancellationToken)
        => Ok(await metadataService.GetByIdAsync(id, cancellationToken));

    /// <summary>Metadata rút gọn của nhiều media. ID không tồn tại sẽ bị bỏ qua.</summary>
    /// <param name="ids">Danh sách ID phân cách bằng dấu phẩy, vd: 16498,11061,1535.</param>
    [HttpGet("batch")]
    [ProducesResponseType<MetadataBatchResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MetadataBatchResponseDto>> GetBatch(
        [FromQuery] string? ids,
        CancellationToken cancellationToken)
        => Ok(await metadataService.GetBatchAsync(ids, cancellationToken));
}
