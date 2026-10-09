using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.Common.Validation;
using Anime.Application.DTOs.Admin;
using Anime.Application.Interfaces;
using Anime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Anime.Infrastructure.Auth;

public sealed class AdminAuthService(
    IDbContextFactory<AnimeDbContext> dbFactory,
    IAdminTokenIssuer tokenIssuer) : IAdminAuthService
{
    private const int MinPasswordLength = 8;

    private static readonly PasswordHasher<AdminUser> Hasher = new();

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var username = Guard.Required(request.Username, "username").ToLowerInvariant();
        var password = request.Password ?? string.Empty;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.AdminUsers.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

        var result = user is null
            ? PasswordVerificationResult.Failed
            : Hasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (user is null || result == PasswordVerificationResult.Failed)
            throw new ApiException(401, ErrorCodes.InvalidCredentials, "Sai tên đăng nhập hoặc mật khẩu");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = Hasher.HashPassword(user, password);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var (token, expiresAt) = tokenIssuer.Issue(user.Id, user.Username);
        return new LoginResponseDto(token, expiresAt, user.Username);
    }

    public async Task ChangePasswordAsync(string username, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var newPassword = Guard.Required(request.NewPassword, "newPassword");
        if (newPassword.Length < MinPasswordLength)
            throw new ValidationException($"Mật khẩu mới phải có ít nhất {MinPasswordLength} ký tự");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.AdminUsers.FirstOrDefaultAsync(x => x.Username == username, cancellationToken)
            ?? throw new ApiException(401, ErrorCodes.Unauthorized, "Tài khoản không tồn tại");

        if (Hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword ?? string.Empty) == PasswordVerificationResult.Failed)
            throw new ValidationException("Mật khẩu hiện tại không đúng", ErrorCodes.InvalidCredentials);

        user.PasswordHash = Hasher.HashPassword(user, newPassword);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Tạo tài khoản admin đầu tiên từ cấu hình <c>Admin:Username</c> / <c>Admin:Password</c> (user-secrets)
    /// nếu DB chưa có admin nào.
    /// </summary>
    public static async Task SeedAsync(AnimeDbContext db, IConfiguration configuration, ILogger logger, CancellationToken cancellationToken)
    {
        if (await db.AdminUsers.AnyAsync(cancellationToken))
            return;

        var username = configuration["Admin:Username"]?.Trim().ToLowerInvariant();
        var password = configuration["Admin:Password"];

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            logger.LogWarning("No admin user exists. Set Admin:Username and Admin:Password (user-secrets) to create one on startup");
            return;
        }

        var user = new AdminUser { Username = username, CreatedAt = DateTimeOffset.UtcNow };
        user.PasswordHash = Hasher.HashPassword(user, password);
        db.AdminUsers.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created admin user {Username}", username);
    }
}
