using System.Text.RegularExpressions;
using Anime.Application.Common;
using Anime.Application.Common.Exceptions;
using Anime.Application.Common.Validation;
using Anime.Application.DTOs.User;
using Anime.Application.Interfaces;
using Anime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Anime.Infrastructure.Users;

public sealed partial class UserAuthService(
    IDbContextFactory<AnimeDbContext> dbFactory,
    IUserTokenIssuer tokenIssuer) : IUserAuthService
{
    private const int MinPasswordLength = 6;

    private static readonly PasswordHasher<AppUser> Hasher = new();

    [GeneratedRegex(@"^[a-z0-9_]{3,32}$")]
    private static partial Regex UsernameRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    public async Task<UserAuthResponseDto> RegisterAsync(RegisterRequestDto request, string? ip, CancellationToken cancellationToken = default)
    {
        var username = Guard.Required(request.Username, "username").ToLowerInvariant();
        if (!UsernameRegex().IsMatch(username))
            throw new ValidationException("Tên đăng nhập 3–32 ký tự, chỉ gồm chữ thường, số và dấu gạch dưới");

        var email = Guard.Required(request.Email, "email").ToLowerInvariant();
        if (!EmailRegex().IsMatch(email))
            throw new ValidationException("Email không hợp lệ");

        var password = request.Password ?? string.Empty;
        if (password.Length < MinPasswordLength)
            throw new ValidationException($"Mật khẩu phải có ít nhất {MinPasswordLength} ký tự");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (await db.Users.AnyAsync(x => x.Username == username, cancellationToken))
            throw new ApiException(409, ErrorCodes.InvalidParameter, "Tên đăng nhập đã được dùng");
        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
            throw new ApiException(409, ErrorCodes.InvalidParameter, "Email đã được dùng");

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username!.Trim() : request.DisplayName!.Trim();
        var user = new AppUser
        {
            Username = username,
            Email = email,
            DisplayName = displayName.Length > 64 ? displayName[..64] : displayName,
            CreatedAt = DateTimeOffset.UtcNow,
            LastLoginAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = Hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        db.ActivityLogs.Add(new UserActivityLog
        {
            UserId = user.Id,
            Action = UserActivityActions.Register,
            Ip = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        return BuildResponse(user);
    }

    public async Task<UserAuthResponseDto> LoginAsync(UserLoginRequestDto request, string? ip, CancellationToken cancellationToken = default)
    {
        var login = Guard.Required(request.UsernameOrEmail, "usernameOrEmail").ToLowerInvariant();
        var password = request.Password ?? string.Empty;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(x => x.Username == login || x.Email == login, cancellationToken);

        var result = user is null
            ? PasswordVerificationResult.Failed
            : Hasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (user is null || result == PasswordVerificationResult.Failed)
            throw new ApiException(401, ErrorCodes.InvalidCredentials, "Sai tài khoản hoặc mật khẩu");

        if (user.IsBanned)
            throw new ApiException(403, ErrorCodes.Unauthorized, "Tài khoản đã bị khoá");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = Hasher.HashPassword(user, password);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        db.ActivityLogs.Add(new UserActivityLog
        {
            UserId = user.Id,
            Action = UserActivityActions.Login,
            Ip = ip,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        return BuildResponse(user);
    }

    public async Task<UserProfileDto> GetProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new ApiException(401, ErrorCodes.Unauthorized, "Tài khoản không tồn tại");

        return ToProfile(user);
    }

    private UserAuthResponseDto BuildResponse(AppUser user)
    {
        var (token, expiresAt) = tokenIssuer.Issue(user.Id, user.Username);
        return new UserAuthResponseDto(token, expiresAt, ToProfile(user));
    }

    private static UserProfileDto ToProfile(AppUser user) =>
        new(user.Id, user.Username, user.Email, user.DisplayName, user.CreatedAt, user.LastLoginAt);
}
