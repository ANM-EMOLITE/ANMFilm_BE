using Anime.Api.Auth;
using Anime.Application.Common;
using Anime.Application.DTOs.Admin;
using Anime.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Anime.Api.Controllers;

[ApiController]
[Route("api/v1/admin/auth")]
[Produces("application/json")]
public sealed class AdminAuthController(IAdminAuthService authService) : ControllerBase
{
    /// <summary>Đăng nhập admin, trả về JWT (gửi kèm header <c>Authorization: Bearer {token}</c>).</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AdminAuthSetup.LoginRateLimit)]
    [ProducesResponseType<LoginResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
        => Ok(await authService.LoginAsync(request, cancellationToken));

    [HttpGet("me")]
    [Authorize(Policy = AdminAuthSetup.AdminPolicy)]
    public IActionResult Me() => Ok(new { username = User.Identity?.Name });

    [HttpPost("change-password")]
    [Authorize(Policy = AdminAuthSetup.AdminPolicy)]
    [EnableRateLimiting(AdminAuthSetup.LoginRateLimit)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        await authService.ChangePasswordAsync(User.Identity?.Name ?? string.Empty, request, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/admin")]
[Produces("application/json")]
[Authorize(Policy = AdminAuthSetup.AdminPolicy)]
public sealed class AdminCatalogController(IAdminCatalogService catalogService, ISyncJobService syncJobService) : ControllerBase
{
    /// <summary>
    /// Lưu sẵn nguồn phát (link stream mọi tập) vào DB, chạy nền. Theo dõi qua /admin/sync/jobs/{id}.
    /// <c>force=true</c>: lấy lại cả tập đã có link.
    /// </summary>
    [HttpPost("anime/{id:int}/streams")]
    [ProducesResponseType<SyncJobDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncJobDto>> PrefetchStreams(int id, [FromQuery] bool force, CancellationToken cancellationToken)
    {
        var job = await syncJobService.StartStreamPrefetchAsync(id, force, User.Identity?.Name, cancellationToken);
        return Accepted(job);
    }

    /// <summary>Thống kê danh mục + các danh sách đã đồng bộ + lần đồng bộ gần nhất.</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> Stats(CancellationToken cancellationToken)
        => Ok(await catalogService.GetStatsAsync(cancellationToken));

    [HttpGet("anime")]
    public async Task<ActionResult<AdminAnimePageDto>> List([FromQuery] AdminAnimeQueryDto query, CancellationToken cancellationToken)
        => Ok(await catalogService.ListAnimeAsync(query, cancellationToken));

    /// <summary>Kiểm tra nguồn phát của 1 anime (chạy nền). Provider lỗi → tự tắt; không còn provider chạy → tự ẩn.</summary>
    [HttpPost("anime/{id:int}/check")]
    [ProducesResponseType<SyncJobDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncJobDto>> Check(int id, CancellationToken cancellationToken)
        => Accepted(await syncJobService.StartHealthCheckAsync(id, null, true, User.Identity?.Name, cancellationToken));

    /// <summary>Bật / tắt 1 provider của anime: <c>{ "isDisabled": true }</c>.</summary>
    [HttpPatch("anime/{id:int}/providers/{provider}")]
    public async Task<ActionResult<AdminAnimeDto>> UpdateProvider(int id, string provider, [FromBody] UpdateProviderRequestDto request, CancellationToken cancellationToken)
        => Ok(await catalogService.UpdateProviderAsync(id, provider, request, cancellationToken));

    /// <summary>Kiểm tra nguồn phát hàng loạt theo tab (chạy nền). Mặc định bỏ qua phim đã kiểm tra trong 24 giờ.</summary>
    [HttpPost("health")]
    [ProducesResponseType<SyncJobDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncJobDto>> HealthCheck([FromBody] StartHealthCheckRequestDto request, CancellationToken cancellationToken)
        => Accepted(await syncJobService.StartHealthCheckAsync(null, request.Lists, request.Force, User.Identity?.Name, cancellationToken));

    /// <summary>Ẩn / hiện anime: <c>{ "isHidden": true }</c>.</summary>
    [HttpPatch("anime/{id:int}")]
    public async Task<ActionResult<AdminAnimeDto>> Update(int id, [FromBody] UpdateAnimeRequestDto request, CancellationToken cancellationToken)
        => Ok(await catalogService.UpdateAnimeAsync(id, request, cancellationToken));

    /// <summary>Lấy mới 1 anime theo ID (thêm vào danh mục nếu chưa có).</summary>
    [HttpPost("anime/{id:int}/sync")]
    public async Task<ActionResult<SyncAnimeResultDto>> Sync(int id, CancellationToken cancellationToken)
        => Ok(await catalogService.SyncAnimeAsync(id, cancellationToken));
}

[ApiController]
[Route("api/v1/admin/sync")]
[Produces("application/json")]
[Authorize(Policy = AdminAuthSetup.AdminPolicy)]
public sealed class AdminSyncController(ISyncJobService syncJobService) : ControllerBase
{
    /// <summary>Bắt đầu đồng bộ (chạy nền). Đang có job chạy → 409 SYNC_IN_PROGRESS.</summary>
    [HttpPost]
    [ProducesResponseType<SyncJobDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncJobDto>> Start([FromBody] StartSyncRequestDto request, CancellationToken cancellationToken)
    {
        var job = await syncJobService.StartAsync(request, User.Identity?.Name, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { id = job.Id }, job);
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyList<SyncJobDto>>> List([FromQuery] int limit = 20, CancellationToken cancellationToken = default)
        => Ok(await syncJobService.ListAsync(limit, cancellationToken));

    [HttpGet("jobs/{id:guid}")]
    public async Task<ActionResult<SyncJobDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await syncJobService.GetAsync(id, cancellationToken));

    /// <summary>Dừng job đang chờ hoặc đang chạy.</summary>
    [HttpPost("jobs/{id:guid}/cancel")]
    [ProducesResponseType<SyncJobDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncJobDto>> Cancel(Guid id, CancellationToken cancellationToken)
        => Ok(await syncJobService.CancelAsync(id, cancellationToken));
}
