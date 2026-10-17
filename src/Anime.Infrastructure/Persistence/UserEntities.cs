namespace Anime.Infrastructure.Persistence;

/// <summary>Tài khoản người xem (tách biệt với <see cref="AdminUser"/>). Đăng nhập bằng username hoặc email.</summary>
public sealed class AppUser
{
    public int Id { get; set; }

    /// <summary>Tên đăng nhập (lưu chữ thường, duy nhất).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Email (lưu chữ thường, duy nhất).</summary>
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Tên hiển thị (giữ nguyên hoa thường người dùng nhập).</summary>
    public string? DisplayName { get; set; }

    /// <summary>Admin khoá tài khoản → không đăng nhập được.</summary>
    public bool IsBanned { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}

/// <summary>Phim user đã lưu (watchlist / yêu thích). Lưu kèm tiêu đề + ảnh để hiển thị kể cả phim ngoài danh mục.</summary>
public sealed class WatchlistItem
{
    public int UserId { get; set; }

    public int AnimeId { get; set; }

    public string? Title { get; set; }

    public string? Cover { get; set; }

    public DateTimeOffset AddedAt { get; set; }
}

/// <summary>Tiến độ xem gần nhất của 1 user với 1 phim — dùng cho "xem tiếp" và lịch sử xem.</summary>
public sealed class WatchHistoryEntry
{
    public int UserId { get; set; }

    public int AnimeId { get; set; }

    public string? Title { get; set; }

    public string? Cover { get; set; }

    /// <summary>Tập xem gần nhất.</summary>
    public int? Episode { get; set; }

    public string? Provider { get; set; }

    /// <summary>Số lần ghi nhận xem phim này.</summary>
    public int Count { get; set; }

    public DateTimeOffset WatchedAt { get; set; }
}

/// <summary>Nhật ký hoạt động của user (đăng ký, đăng nhập, lưu phim, xem...). Thiết kế mở để thêm hành động về sau.</summary>
public sealed class UserActivityLog
{
    public long Id { get; set; }

    /// <summary>Null khi hành động chưa gắn user (vd: đăng nhập thất bại).</summary>
    public int? UserId { get; set; }

    /// <summary>Loại hành động, xem <see cref="UserActivityActions"/>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Loại đối tượng (vd: ANIME).</summary>
    public string? TargetType { get; set; }

    /// <summary>Id đối tượng (vd: id phim).</summary>
    public string? TargetId { get; set; }

    /// <summary>Thông tin thêm (text ngắn hoặc JSON).</summary>
    public string? Detail { get; set; }

    public string? Ip { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public static class UserActivityActions
{
    public const string Register = "REGISTER";
    public const string Login = "LOGIN";
    public const string Watch = "WATCH";
    public const string WatchlistAdd = "WATCHLIST_ADD";
    public const string WatchlistRemove = "WATCHLIST_REMOVE";
}
