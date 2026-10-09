using Npgsql;

namespace Anime.Infrastructure.Persistence;

/// <summary>
/// Chuẩn hóa connection string:
/// – Cho phép dạng URI (Neon, Supabase, Heroku...):
///   <c>postgresql://user:pass@host:5432/db?sslmode=require&amp;channel_binding=require</c> → dạng key-value của Npgsql.
/// – Thêm mặc định chống rớt kết nối với DB serverless (Neon tự cắt kết nối rảnh):
///   keepalive 30s, đóng kết nối rảnh trong pool sau 60s. Giá trị đã cấu hình thì giữ nguyên.
/// </summary>
public static class PostgresConnectionString
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        value = value.Trim();
        var builder = value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                      || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            ? FromUri(value)
            : new NpgsqlConnectionStringBuilder(value);

        if (!value.Contains("keepalive", StringComparison.OrdinalIgnoreCase))
            builder.KeepAlive = 30;

        if (!value.Contains("idle", StringComparison.OrdinalIgnoreCase))
            builder.ConnectionIdleLifetime = 60;

        return builder.ConnectionString;
    }

    private static NpgsqlConnectionStringBuilder FromUri(string value)
    {
        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var name = Uri.UnescapeDataString(parts[0]).ToLowerInvariant();
            var option = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

            switch (name)
            {
                case "sslmode":
                    builder.SslMode = Enum.Parse<SslMode>(option.Replace("-", string.Empty), ignoreCase: true);
                    break;
                case "channel_binding":
                    builder.ChannelBinding = Enum.Parse<ChannelBinding>(option, ignoreCase: true);
                    break;
                default:
                    // Tham số Npgsql không hỗ trợ thì bỏ qua.
                    try { builder[name] = option; }
                    catch (ArgumentException) { }
                    break;
            }
        }

        return builder;
    }
}
