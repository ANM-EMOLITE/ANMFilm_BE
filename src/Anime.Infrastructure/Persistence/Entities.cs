namespace Anime.Infrastructure.Persistence;

/// <summary>Anime trong danh mục (đồng bộ từ AniList + AniMapper). Id = AniList ID = AniMapper media ID.</summary>
public sealed class CatalogAnime
{
    public int Id { get; set; }

    /// <summary>Tiêu đề hiển thị (dùng để tìm kiếm, sắp xếp trong admin).</summary>
    public string DisplayTitle { get; set; } = string.Empty;

    /// <summary>Mọi tiêu đề (en, ja, vi, tên khác...) gộp lại, dùng cho tìm kiếm trong admin.</summary>
    public string SearchText { get; set; } = string.Empty;

    /// <summary>LocalizedText dạng JSON.</summary>
    public string TitlesJson { get; set; } = "{}";

    /// <summary>MediaImagesDto dạng JSON.</summary>
    public string ImagesJson { get; set; } = "{}";

    public string? Format { get; set; }

    public string? Status { get; set; }

    public string? Season { get; set; }

    public int? SeasonYear { get; set; }

    public string? StartDate { get; set; }

    public int? TotalUnits { get; set; }

    /// <summary>Tên các provider có nguồn phát, vd: ["ANIMEVIETSUB","NINIYO"].</summary>
    public List<string> Providers { get; set; } = [];

    public bool HasStream { get; set; }

    /// <summary>Admin ẩn → không hiển thị cho người dùng (danh sách, tìm kiếm, trang xem).</summary>
    public bool IsHidden { get; set; }

    /// <summary>Bị ẩn tự động do kiểm tra thấy mọi nguồn phát đều hỏng (kiểm tra lại thấy chạy được → tự hiện lại).</summary>
    public bool AutoHidden { get; set; }

    public DateTimeOffset SyncedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Vị trí của anime trong một danh sách (tab), vd: TRENDING, SEASON:FALL:2026.</summary>
public sealed class CatalogListItem
{
    public string ListKey { get; set; } = string.Empty;

    public int Position { get; set; }

    public int AnimeId { get; set; }
}

/// <summary>Thông tin lần đồng bộ gần nhất của một danh sách.</summary>
public sealed class CatalogList
{
    public string Key { get; set; } = string.Empty;

    public int ItemCount { get; set; }

    public DateTimeOffset SyncedAt { get; set; }
}

/// <summary>Một lần đồng bộ do admin bấm.</summary>
public sealed class SyncJob
{
    public Guid Id { get; set; }

    /// <summary>CATALOG: đồng bộ danh mục. STREAMS: lưu sẵn nguồn phát của 1 anime.</summary>
    public string Kind { get; set; } = SyncJobKind.Catalog;

    /// <summary>Anime đích (job STREAMS).</summary>
    public int? AnimeId { get; set; }

    /// <summary>Tên hiển thị của đích (vd: tên anime).</summary>
    public string? Target { get; set; }

    /// <summary>QUEUED, RUNNING, SUCCEEDED, FAILED.</summary>
    public string Status { get; set; } = SyncJobStatus.Queued;

    /// <summary>Các danh sách cần đồng bộ, vd: ["TRENDING","SEASON"].</summary>
    public List<string> Lists { get; set; } = [];

    /// <summary>Đang làm gì (hiển thị cho admin).</summary>
    public string? Progress { get; set; }

    /// <summary>Có cập nhật lại các anime đã tồn tại không (mặc định: bỏ qua).</summary>
    public bool UpdateExisting { get; set; }

    public int Processed { get; set; }

    /// <summary>Anime mới được tạo.</summary>
    public int Added { get; set; }

    /// <summary>Anime đã tồn tại và được cập nhật lại (chỉ khi UpdateExisting).</summary>
    public int Updated { get; set; }

    /// <summary>Anime đã tồn tại nên bỏ qua.</summary>
    public int Skipped { get; set; }

    public string? Error { get; set; }

    public string? TriggeredBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}

public static class SyncJobKind
{
    public const string Catalog = "CATALOG";
    public const string Streams = "STREAMS";
    public const string Health = "HEALTH";
}

public static class SyncJobStatus
{
    public const string Queued = "QUEUED";
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";

    /// <summary>Admin bấm dừng khi job đang chờ hoặc đang chạy.</summary>
    public const string Canceled = "CANCELED";
}

public sealed class AdminUser
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}

/// <summary>Kết quả kiểm tra 1 provider của 1 anime (có phát được không).</summary>
public sealed class AnimeProviderStatus
{
    public int AnimeId { get; set; }

    public string Provider { get; set; } = string.Empty;

    /// <summary>OK hoặc BROKEN.</summary>
    public string Status { get; set; } = ProviderCheckStatus.Ok;

    /// <summary>Tắt provider này cho anime → trang xem không dùng tới (tự đổi sang provider khác).</summary>
    public bool IsDisabled { get; set; }

    /// <summary>AUTO: do kiểm tra tự tắt/bật. ADMIN: admin tự đặt, kiểm tra không ghi đè.</summary>
    public string? DisabledBy { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CheckedAt { get; set; }
}

public static class ProviderCheckStatus
{
    public const string Ok = "OK";
    public const string Broken = "BROKEN";
}
