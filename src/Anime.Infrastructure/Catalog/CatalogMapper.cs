using System.Text.Json;
using Anime.Application.DTOs.Admin;
using Anime.Application.DTOs.Common;
using Anime.Application.DTOs.Metadata;
using Anime.Application.DTOs.Searchs;
using Anime.Infrastructure.Persistence;

namespace Anime.Infrastructure.Catalog;

internal static class CatalogMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Ghi dữ liệu từ AniMapper vào entity (giữ nguyên IsHidden do admin đặt).</summary>
    public static void Apply(CatalogAnime entity, MediaDetailDto media, DateTimeOffset now)
    {
        var titles = media.Titles ?? new LocalizedText();

        entity.Id = media.Id;
        entity.DisplayTitle = Truncate(PickTitle(titles) ?? $"#{media.Id}", 500);
        entity.TitlesJson = JsonSerializer.Serialize(titles, JsonOptions);
        entity.SearchText = string.Join(" | ", titles.Values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct());
        entity.ImagesJson = JsonSerializer.Serialize(media.Images ?? new MediaImagesDto(), JsonOptions);
        entity.Format = media.Format;
        entity.Status = media.Status;
        entity.Season = media.Season;
        entity.SeasonYear = media.SeasonYear;
        entity.StartDate = media.StartDate;
        entity.TotalUnits = media.TotalUnits;
        entity.Providers = media.StreamingProviders?.Keys.OrderBy(k => k).ToList() ?? [];
        entity.HasStream = entity.Providers.Count > 0;
        entity.SyncedAt = now;
    }

    public static MediaSearchResultDto ToSearchResult(CatalogAnime entity) => new()
    {
        Id = entity.Id,
        MediaType = "ANIME",
        Titles = Deserialize<LocalizedText>(entity.TitlesJson) ?? new LocalizedText(),
        Images = Deserialize<MediaImagesDto>(entity.ImagesJson) ?? new MediaImagesDto(),
        Status = entity.Status,
        Season = entity.Season,
        SeasonYear = entity.SeasonYear,
        StartDate = entity.StartDate,
        Format = entity.Format,
        TotalUnits = entity.TotalUnits
    };

    public static AdminAnimeDto ToAdmin(CatalogAnime entity) => ToAdmin(entity, []);

    public static AdminAnimeDto ToAdmin(CatalogAnime entity, IEnumerable<AnimeProviderStatus> statuses)
    {
        var images = Deserialize<MediaImagesDto>(entity.ImagesJson);
        var providerStatuses = statuses
            .OrderBy(s => s.Provider)
            .Select(s => new AdminProviderStatusDto(s.Provider, s.Status, s.IsDisabled, s.DisabledBy, s.LastError, s.CheckedAt))
            .ToList();

        return new AdminAnimeDto(
            entity.Id,
            entity.DisplayTitle,
            images?.CoverMd ?? images?.CoverLg ?? images?.CoverXl,
            entity.Format,
            entity.Status,
            entity.Season,
            entity.SeasonYear,
            entity.TotalUnits,
            entity.Providers,
            entity.HasStream,
            entity.IsHidden,
            entity.SyncedAt,
            entity.AutoHidden,
            providerStatuses);
    }

    /// <summary>Ưu tiên tiếng Việt → tiếng Anh → tên AniList ưa dùng → romaji.</summary>
    private static string? PickTitle(LocalizedText titles)
    {
        foreach (var key in new[] { "vi", "en", "user-preferred", "main", "ja-ro" })
        {
            if (titles.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return titles.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
    }

    private static T? Deserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
