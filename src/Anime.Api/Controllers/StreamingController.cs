using Anime.Application.Common;
using Anime.Application.DTOs;
using Anime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Anime.Api.Controllers;

[ApiController]
[Route("api/v1/stream")]
public sealed class StreamingController(
    IStreamingService streamingService,
    IStreamProxyService streamProxyService) : ControllerBase
{
    /// <summary>Danh sách tập của một media theo provider.</summary>
    [HttpGet("episodes")]
    [Produces("application/json")]
    [ProducesResponseType<EpisodesResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EpisodesResponseDto>> GetEpisodes(
        [FromQuery] EpisodesRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await streamingService.GetEpisodesAsync(request, cancellationToken));

    /// <summary>Danh sách server khả dụng.</summary>
    [HttpGet("episodes/servers")]
    [Produces("application/json")]
    [ProducesResponseType<ServersResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServersResponseDto>> GetServers(
        [FromQuery] int id,
        [FromQuery] string? provider,
        CancellationToken cancellationToken)
        => Ok(await streamingService.GetServersAsync(id, provider, cancellationToken));

    /// <summary>Nguồn stream (HLS / EMBED / DIRECT) của một tập.</summary>
    [HttpGet("source")]
    [Produces("application/json")]
    [ProducesResponseType<StreamSourceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StreamSourceDto>> GetSource(
        [FromQuery] SourceRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await streamingService.GetSourceAsync(request, cancellationToken));

    /// <summary>Nội dung playlist M3U8 đã cache.</summary>
    [HttpGet("source/m3u8/{cacheKey}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetM3u8(
        [FromRoute] string cacheKey,
        CancellationToken cancellationToken)
    {
        var playlist = await streamingService.GetM3u8Async(cacheKey, cancellationToken);
        return Content(playlist, "application/vnd.apple.mpegurl");
    }

    /// <summary>
    /// Proxy playlist HLS / segment / video kèm header (dùng khi corsProxyRequired = true).
    /// </summary>
    /// <param name="url">URL gốc cần tải.</param>
    /// <param name="h">proxyHeaders dạng JSON, mã hóa base64url.</param>
    [HttpGet("proxy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task Proxy(
        [FromQuery] string? url,
        [FromQuery] string? h,
        CancellationToken cancellationToken)
    {
        var proxyPath = $"{Request.PathBase}/api/v1/stream/proxy";

        using var result = await streamProxyService.FetchAsync(
            url, h, Request.Headers.Range.ToString(), proxyPath, cancellationToken);

        Response.StatusCode = result.StatusCode;
        Response.ContentType = result.ContentType;
        Response.Headers.CacheControl = "no-cache";

        if (result.Text is not null)
        {
            await Response.WriteAsync(result.Text, cancellationToken);
            return;
        }

        if (result.ContentLength is { } length)
            Response.ContentLength = length;

        if (result.ContentRange is not null)
            Response.Headers.ContentRange = result.ContentRange;

        if (result.AcceptRanges is not null)
            Response.Headers.AcceptRanges = result.AcceptRanges;

        await result.Body!.CopyToAsync(Response.Body, cancellationToken);
    }
}
