using Anime.Application.DTOs.Browse;
using Anime.Application.DTOs.Searchs;

namespace Anime.Application.Interfaces;

public interface IBrowseService
{
    /// <summary>Danh sách anime theo tab, chỉ gồm anime đã có nguồn phát.</summary>
    Task<SearchResponseDto> BrowseAsync(BrowseRequestDto request, CancellationToken cancellationToken = default);
}
