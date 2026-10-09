using Anime.Application.DTOs.Searchs;

namespace Anime.Application.Interfaces;

/// <summary>
/// Đọc danh mục anime đã đồng bộ (DB) cho phía người dùng.
/// Lỗi DB không làm hỏng request: trả null/rỗng để service quay về gọi upstream trực tiếp.
/// </summary>
public interface ICatalogStore
{
    /// <summary>Một trang của danh sách đã đồng bộ (chỉ anime có nguồn phát, không bị ẩn). Null nếu danh sách chưa đồng bộ.</summary>
    Task<CatalogListPage?> GetListPageAsync(string listKey, int offset, int limit, CancellationToken cancellationToken = default);

    /// <summary>Các ID (trong danh sách truyền vào) đang bị admin ẩn.</summary>
    Task<HashSet<int>> GetHiddenIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    /// <summary>Kết quả kiểm tra nguồn phát của anime, key = provider. Rỗng nếu chưa kiểm tra.</summary>
    Task<IReadOnlyDictionary<string, ProviderState>> GetProviderStatesAsync(int animeId, CancellationToken cancellationToken = default);
}

/// <param name="IsOk">Lần kiểm tra gần nhất phát được.</param>
/// <param name="IsDisabled">Đã bị tắt (tự động hoặc admin) → không đưa cho trang xem.</param>
public sealed record ProviderState(bool IsOk, bool IsDisabled);

public sealed record CatalogListPage(IReadOnlyList<MediaSearchResultDto> Results, int Total, int? NextOffset);
