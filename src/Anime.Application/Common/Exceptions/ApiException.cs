namespace Anime.Application.Common.Exceptions;

/// <summary>
/// Exception mang theo HTTP status + error code, được middleware chuyển thành JSON lỗi.
/// </summary>
public class ApiException(int statusCode, string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;
}

/// <summary>400 - tham số request không hợp lệ.</summary>
public sealed class ValidationException(string message, string code = ErrorCodes.InvalidParameter)
    : ApiException(400, code, message);

/// <summary>Lỗi trả về từ AniMapper (status code được giữ nguyên nếu là lỗi 4xx).</summary>
public sealed class UpstreamApiException(int statusCode, string code, string message, Exception? innerException = null)
    : ApiException(statusCode, code, message, innerException);
