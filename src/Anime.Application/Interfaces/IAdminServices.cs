using Anime.Application.DTOs.Admin;

namespace Anime.Application.Interfaces;

public interface IAdminAuthService
{
    /// <summary>Sai tài khoản/mật khẩu → 401.</summary>
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(string username, ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
}

public interface IAdminCatalogService
{
    Task<AdminStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<AdminAnimePageDto> ListAnimeAsync(AdminAnimeQueryDto query, CancellationToken cancellationToken = default);

    Task<AdminAnimeDto> UpdateAnimeAsync(int id, UpdateAnimeRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Admin bật/tắt 1 provider của anime (kiểm tra tự động không ghi đè quyết định này).</summary>
    Task<AdminAnimeDto> UpdateProviderAsync(int id, string provider, UpdateProviderRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Lấy mới 1 anime từ AniMapper (tạo nếu chưa có, cập nhật nếu đã có).</summary>
    Task<SyncAnimeResultDto> SyncAnimeAsync(int id, CancellationToken cancellationToken = default);
}

public interface ISyncJobService
{
    /// <summary>Tạo job đồng bộ và chạy nền. Đang có job chạy → 409.</summary>
    Task<SyncJobDto> StartAsync(StartSyncRequestDto request, string? triggeredBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu sẵn nguồn phát (server, danh sách tập, link stream mọi tập) của 1 anime vào DB để khi xem không cần gọi AniMapper.
    /// <paramref name="force"/> = false: tập đã có link trong DB thì bỏ qua.
    /// </summary>
    Task<SyncJobDto> StartStreamPrefetchAsync(int animeId, bool force, string? triggeredBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra nguồn phát: 1 anime (<paramref name="animeId"/>) hoặc mọi anime trong các tab <paramref name="lists"/>.
    /// Provider lỗi → tự tắt; anime không còn provider nào chạy → tự ẩn.
    /// <paramref name="force"/> = false: bỏ qua anime đã kiểm tra trong 24 giờ.
    /// </summary>
    Task<SyncJobDto> StartHealthCheckAsync(int? animeId, List<string>? lists, bool force, string? triggeredBy, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SyncJobDto>> ListAsync(int limit, CancellationToken cancellationToken = default);

    Task<SyncJobDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Dừng job đang chờ hoặc đang chạy. Job đã kết thúc → không đổi gì.</summary>
    Task<SyncJobDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
