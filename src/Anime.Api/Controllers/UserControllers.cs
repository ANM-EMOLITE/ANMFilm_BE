using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Anime.Api.Auth;
using Anime.Application.Common;
using Anime.Application.DTOs.User;
using Anime.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Anime.Api.Controllers;

[ApiController]
[Route("api/v1/user/auth")]
[Produces("application/json")]
public sealed class UserAuthController(IUserAuthService authService) : ControllerBase
{
    /// <summary>Đăng ký tài khoản người xem, trả về JWT.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(AdminAuthSetup.LoginRateLimit)]
    [ProducesResponseType<UserAuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserAuthResponseDto>> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
        => Ok(await authService.RegisterAsync(request, ClientIp(), cancellationToken));

    /// <summary>Đăng nhập bằng username hoặc email.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AdminAuthSetup.LoginRateLimit)]
    [ProducesResponseType<UserAuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserAuthResponseDto>> Login([FromBody] UserLoginRequestDto request, CancellationToken cancellationToken)
        => Ok(await authService.LoginAsync(request, ClientIp(), cancellationToken));

    [HttpGet("me")]
    [Authorize(Policy = AdminAuthSetup.UserPolicy)]
    public async Task<ActionResult<UserProfileDto>> Me(CancellationToken cancellationToken)
        => Ok(await authService.GetProfileAsync(this.UserId(), cancellationToken));

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}

[ApiController]
[Route("api/v1/user")]
[Produces("application/json")]
[Authorize(Policy = AdminAuthSetup.UserPolicy)]
public sealed class UserLibraryController(IUserLibraryService library) : ControllerBase
{
    // ----- Watchlist -----

    [HttpGet("watchlist")]
    public async Task<ActionResult<IReadOnlyList<WatchlistItemDto>>> Watchlist(CancellationToken cancellationToken)
        => Ok(await library.GetWatchlistAsync(this.UserId(), cancellationToken));

    [HttpGet("watchlist/ids")]
    public async Task<ActionResult<IReadOnlyList<int>>> WatchlistIds(CancellationToken cancellationToken)
        => Ok(await library.GetWatchlistIdsAsync(this.UserId(), cancellationToken));

    [HttpPost("watchlist")]
    public async Task<ActionResult<WatchlistItemDto>> AddWatchlist([FromBody] AddWatchlistRequestDto request, CancellationToken cancellationToken)
        => Ok(await library.AddToWatchlistAsync(this.UserId(), request, cancellationToken));

    [HttpDelete("watchlist/{animeId:int}")]
    public async Task<IActionResult> RemoveWatchlist(int animeId, CancellationToken cancellationToken)
    {
        await library.RemoveFromWatchlistAsync(this.UserId(), animeId, cancellationToken);
        return NoContent();
    }

    // ----- Lịch sử xem -----

    [HttpPost("history")]
    public async Task<ActionResult<WatchHistoryDto>> RecordWatch([FromBody] RecordWatchRequestDto request, CancellationToken cancellationToken)
        => Ok(await library.RecordWatchAsync(this.UserId(), request, cancellationToken));

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<WatchHistoryDto>>> History([FromQuery] int limit = 30, CancellationToken cancellationToken = default)
        => Ok(await library.GetHistoryAsync(this.UserId(), limit, cancellationToken));

    // ----- Nhật ký hoạt động -----

    [HttpGet("activity")]
    public async Task<ActionResult<IReadOnlyList<ActivityLogDto>>> Activity([FromQuery] int limit = 50, CancellationToken cancellationToken = default)
        => Ok(await library.GetActivityAsync(this.UserId(), limit, cancellationToken));
}

internal static class UserClaimsExtensions
{
    /// <summary>Id user lấy từ claim "sub" của JWT (MapInboundClaims = false).</summary>
    public static int UserId(this ControllerBase controller)
    {
        var sub = controller.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? controller.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(sub, out var id) ? id : 0;
    }
}
