using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Anime.Application.Common;
using Anime.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Anime.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "AnimeStreamingApi";

    public string Audience { get; set; } = "AnimeStreamingAdmin";

    /// <summary>Khóa ký token (≥ 32 ký tự). Đặt bằng user-secrets: <c>Jwt:Key</c>.</summary>
    public string? Key { get; set; }

    public int ExpiresHours { get; set; } = 12;
}

/// <summary>Phát JWT cho admin.</summary>
public sealed class JwtAdminTokenIssuer(JwtOptions options, SymmetricSecurityKey key) : IAdminTokenIssuer
{
    public (string Token, DateTimeOffset ExpiresAt) Issue(int userId, string username)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(options.ExpiresHours);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
                new Claim(ClaimTypes.Role, AdminAuthSetup.AdminRole)
            ],
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

public static class AdminAuthSetup
{
    public const string AdminRole = "admin";

    public const string AdminPolicy = "Admin";

    public const string LoginRateLimit = "admin-login";

    public static IServiceCollection AddAdminAuth(this IServiceCollection services, IConfiguration configuration, ILogger? logger = null)
    {
        var options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        byte[] keyBytes;
        if (string.IsNullOrWhiteSpace(options.Key) || Encoding.UTF8.GetByteCount(options.Key) < 32)
        {
            // Không cấu hình khóa → sinh khóa tạm: token mất hiệu lực mỗi lần BE khởi động lại.
            keyBytes = RandomNumberGenerator.GetBytes(64);
            logger?.LogWarning("Jwt:Key is missing or shorter than 32 characters; using a temporary key (admin tokens reset on restart)");
        }
        else
        {
            keyBytes = Encoding.UTF8.GetBytes(options.Key);
        }

        var key = new SymmetricSecurityKey(keyBytes);
        services.AddSingleton(options);
        services.AddSingleton<IAdminTokenIssuer>(new JwtAdminTokenIssuer(options, key));

        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = options.Issuer,
                    ValidAudience = options.Audience,
                    IssuerSigningKey = key,
                    NameClaimType = JwtRegisteredClaimNames.UniqueName,
                    RoleClaimType = ClaimTypes.Role,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                // Trả lỗi cùng format { success, message, code } với các API khác.
                jwt.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(
                            new ApiErrorResponse("Phiên đăng nhập không hợp lệ hoặc đã hết hạn", ErrorCodes.Unauthorized));
                    },
                    OnForbidden = context =>
                        context.Response.WriteAsJsonAsync(
                            new ApiErrorResponse("Không có quyền truy cập", ErrorCodes.Unauthorized))
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy.RequireRole(AdminRole));

        // Chống dò mật khẩu: tối đa 10 lần đăng nhập / phút / IP.
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = (context, cancellationToken) => new ValueTask(
                context.HttpContext.Response.WriteAsJsonAsync(
                    new ApiErrorResponse("Đăng nhập sai quá nhiều lần, thử lại sau 1 phút", ErrorCodes.RateLimited),
                    cancellationToken));

            limiter.AddPolicy(LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }
}
