namespace Anime.Application.DTOs.User;

// ---------- Auth ----------

public sealed class RegisterRequestDto
{
    public string? Username { get; set; }

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? DisplayName { get; set; }
}

public sealed class UserLoginRequestDto
{
    /// <summary>Username hoặc email.</summary>
    public string? UsernameOrEmail { get; set; }

    public string? Password { get; set; }
}

public sealed record UserProfileDto(
    int Id,
    string Username,
    string Email,
    string? DisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record UserAuthResponseDto(string Token, DateTimeOffset ExpiresAt, UserProfileDto User);

// ---------- Watchlist ----------

public sealed class AddWatchlistRequestDto
{
    public int AnimeId { get; set; }

    /// <summary>Tuỳ chọn: FE gửi kèm để lưu ảnh/tiêu đề hiển thị (phim ngoài danh mục vẫn hiện được).</summary>
    public string? Title { get; set; }

    public string? Cover { get; set; }
}

public sealed record WatchlistItemDto(int AnimeId, string? Title, string? Cover, DateTimeOffset AddedAt);

// ---------- Watch history ----------

public sealed class RecordWatchRequestDto
{
    public int AnimeId { get; set; }

    public int? Episode { get; set; }

    public string? Provider { get; set; }

    public string? Title { get; set; }

    public string? Cover { get; set; }
}

public sealed record WatchHistoryDto(
    int AnimeId,
    string? Title,
    string? Cover,
    int? Episode,
    string? Provider,
    int Count,
    DateTimeOffset WatchedAt);

// ---------- Activity log ----------

public sealed record ActivityLogDto(
    long Id,
    string Action,
    string? TargetType,
    string? TargetId,
    string? Detail,
    DateTimeOffset CreatedAt);
