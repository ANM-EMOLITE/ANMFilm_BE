using Anime.Application.Common;
using Anime.Application.Common.Exceptions;

namespace Anime.Api.Middleware;

/// <summary>
/// Chuyển mọi exception thành JSON { success: false, message, code }.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogDebug("Request {Path} was cancelled by the client.", context.Request.Path);
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode >= 500)
                logger.LogWarning(ex, "Upstream error on {Path}", context.Request.Path);

            await WriteErrorAsync(context, ex.StatusCode, ex.Message, ex.Code);
        }
        catch (TaskCanceledException ex)
        {
            // HttpClient timeout.
            logger.LogWarning(ex, "Upstream AniMapper request timed out.");
            await WriteErrorAsync(context, StatusCodes.Status504GatewayTimeout,
                "Upstream request timed out", ErrorCodes.UpstreamTimeout);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Upstream AniMapper request failed.");
            await WriteErrorAsync(context, StatusCodes.Status502BadGateway,
                "Cannot reach upstream service", ErrorCodes.UpstreamError);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled API exception.");
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError,
                "Internal server error", ErrorCodes.InternalServerError);
        }
    }

    private static Task WriteErrorAsync(HttpContext context, int statusCode, string message, string code)
    {
        if (context.Response.HasStarted)
            return Task.CompletedTask;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;

        return context.Response.WriteAsJsonAsync(new ApiErrorResponse(message, code));
    }
}
