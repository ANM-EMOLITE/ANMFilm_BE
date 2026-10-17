using Anime.Application.DTOs.User;

namespace Anime.Application.Interfaces;

public interface IUserAuthService
{
    /// <summary>Đăng ký tài khoản mới. Trùng username/email → 409.</summary>
    Task<UserAuthResponseDto> RegisterAsync(RegisterRequestDto request, string? ip, CancellationToken cancellationToken = default);

    /// <summary>Đăng nhập bằng username hoặc email. Sai thông tin → 401, bị khoá → 403.</summary>
    Task<UserAuthResponseDto> LoginAsync(UserLoginRequestDto request, string? ip, CancellationToken cancellationToken = default);

    Task<UserProfileDto> GetProfileAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>Phát JWT cho người xem (role "user", audience riêng).</summary>
public interface IUserTokenIssuer
{
    (string Token, DateTimeOffset ExpiresAt) Issue(int userId, string username);
}

/// <summary>Thư viện của user: phim đã lưu, lịch sử xem, nhật ký hoạt động.</summary>
public interface IUserLibraryService
{
    Task<IReadOnlyList<WatchlistItemDto>> GetWatchlistAsync(int userId, CancellationToken cancellationToken = default);

    Task<WatchlistItemDto> AddToWatchlistAsync(int userId, AddWatchlistRequestDto request, CancellationToken cancellationToken = default);

    Task RemoveFromWatchlistAsync(int userId, int animeId, CancellationToken cancellationToken = default);

    /// <summary>Id các phim user đã lưu (để FE đánh dấu nút "đã lưu").</summary>
    Task<IReadOnlyList<int>> GetWatchlistIdsAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Ghi nhận user vừa xem 1 phim/tập (cập nhật tiến độ + ghi log).</summary>
    Task<WatchHistoryDto> RecordWatchAsync(int userId, RecordWatchRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WatchHistoryDto>> GetHistoryAsync(int userId, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityLogDto>> GetActivityAsync(int userId, int limit, CancellationToken cancellationToken = default);
}
