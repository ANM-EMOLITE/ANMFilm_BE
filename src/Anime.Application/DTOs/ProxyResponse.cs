namespace Anime.Application.DTOs;

/// <summary>
/// Kết quả proxy: hoặc là playlist đã rewrite (<see cref="Text"/>), hoặc là stream nhị phân (<see cref="Body"/>).
/// Phải dispose sau khi ghi xong response.
/// </summary>
public sealed class ProxyResponse : IDisposable
{
    public int StatusCode { get; init; } = 200;

    public string ContentType { get; init; } = "application/octet-stream";

    public string? Text { get; init; }

    public Stream? Body { get; init; }

    public long? ContentLength { get; init; }

    public string? ContentRange { get; init; }

    public string? AcceptRanges { get; init; }

    /// <summary>Đối tượng cần giải phóng cùng response (HttpResponseMessage gốc).</summary>
    public IDisposable? Owner { get; init; }

    public void Dispose()
    {
        Body?.Dispose();
        Owner?.Dispose();
    }
}
