namespace Anime.Application.Common;

/// <summary>
/// Mã lỗi trả về cho client, khớp với AniMapper.
/// </summary>
public static class ErrorCodes
{
    public const string MissingParameter = "MISSING_PARAMETER";
    public const string InvalidParameter = "INVALID_PARAMETER";
    public const string InvalidId = "INVALID_ID";
    public const string InvalidProvider = "INVALID_PROVIDER";
    public const string UnsupportedProvider = "UNSUPPORTED_PROVIDER";
    public const string MediaNotFound = "MEDIA_NOT_FOUND";
    public const string MappingNotFound = "MAPPING_NOT_FOUND";
    public const string EpisodesNotFound = "EPISODES_NOT_FOUND";
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string RateLimited = "RATE_LIMITED";
    public const string UpstreamError = "UPSTREAM_ERROR";
    public const string UpstreamTimeout = "UPSTREAM_TIMEOUT";
    public const string ProviderUnavailable = "PROVIDER_UNAVAILABLE";
    public const string InternalServerError = "INTERNAL_SERVER_ERROR";
}
