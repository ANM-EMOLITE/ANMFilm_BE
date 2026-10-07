using System.Text.Json.Serialization;

namespace Anime.Application.Common;

/// <summary>
/// Format lỗi chung: { success: false, message, code }.
/// </summary>
public sealed record ApiErrorResponse(string Message, string Code)
{
    [JsonPropertyOrder(-1)]
    public bool Success => false;
}
