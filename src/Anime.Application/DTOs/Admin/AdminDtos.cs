namespace Anime.Application.DTOs.Admin;

// ---------- Auth ----------

public sealed class LoginRequestDto
{
    public string? Username { get; set; }

    public string? Password { get; set; }
}

public sealed record LoginResponseDto(string Token, DateTimeOffset ExpiresAt, string Username);

public sealed class ChangePasswordRequestDto
{
    public string? CurrentPassword { get; set; }

    public string? NewPassword { get; set; }
}

// ---------- Catalog ----------

public sealed record AdminListDto(string Key, string Label, int ItemCount, DateTimeOffset SyncedAt);

public sealed record AdminStatsDto(
    int TotalAnime,
    int WithStream,
    int Hidden,
    IReadOnlyList<AdminListDto> Lists,
    SyncJobDto? LastJob,
    int Broken = 0);

/// <summary>Query params của GET /api/v1/admin/anime.</summary>
public sealed class AdminAnimeQueryDto
{
    /// <summary>Tìm theo tên hoặc ID.</summary>
    public string? Q { get; set; }

    public bool? HasStream { get; set; }

    public bool? Hidden { get; set; }

    /// <summary>broken: có provider lỗi · ok: mọi provider chạy tốt · unchecked: chưa kiểm tra.</summary>
    public string? Health { get; set; }

    /// <summary>Chỉ lấy anime thuộc danh sách này (vd: TRENDING, SEASON:FALL:2026), giữ thứ tự danh sách.</summary>
    public string? List { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 30;
}

public sealed record AdminAnimeDto(
    int Id,
    string Title,
    string? Cover,
    string? Format,
    string? Status,
    string? Season,
    int? SeasonYear,
    int? TotalUnits,
    IReadOnlyList<string> Providers,
    bool HasStream,
    bool IsHidden,
    DateTimeOffset SyncedAt,
    bool AutoHidden = false,
    IReadOnlyList<AdminProviderStatusDto>? ProviderStatuses = null);

/// <summary>Kết quả kiểm tra 1 provider. Status: OK / BROKEN.</summary>
/// <param name="DisabledBy">AUTO (kiểm tra tự tắt) hoặc ADMIN.</param>
public sealed record AdminProviderStatusDto(
    string Provider,
    string Status,
    bool IsDisabled,
    string? DisabledBy,
    string? LastError,
    DateTimeOffset CheckedAt);

public sealed class UpdateProviderRequestDto
{
    public bool IsDisabled { get; set; }
}

public sealed class StartHealthCheckRequestDto
{
    /// <summary>Tab cần kiểm tra (TRENDING, POPULAR, SEASON...). Bỏ trống = tất cả.</summary>
    public List<string>? Lists { get; set; }

    /// <summary>false (mặc định): bỏ qua phim đã kiểm tra trong 24 giờ.</summary>
    public bool Force { get; set; }
}

public sealed record AdminAnimePageDto(IReadOnlyList<AdminAnimeDto> Items, int Total, int Page, int PageSize);

public sealed class UpdateAnimeRequestDto
{
    public bool? IsHidden { get; set; }
}

/// <summary>Kết quả đồng bộ 1 anime theo ID.</summary>
public sealed record SyncAnimeResultDto(AdminAnimeDto Anime, bool Created);

// ---------- Sync jobs ----------

public sealed class StartSyncRequestDto
{
    /// <summary>Tab cần đồng bộ (TRENDING, POPULAR, SEASON...). Bỏ trống = tất cả.</summary>
    public List<string>? Lists { get; set; }

    /// <summary>false (mặc định): anime đã có trong DB thì bỏ qua. true: cập nhật lại.</summary>
    public bool UpdateExisting { get; set; }
}

public sealed record SyncJobDto(
    Guid Id,
    string Kind,
    int? AnimeId,
    string? Target,
    string Status,
    IReadOnlyList<string> Lists,
    bool UpdateExisting,
    string? Progress,
    int Processed,
    int Added,
    int Updated,
    int Skipped,
    string? Error,
    string? TriggeredBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);
