namespace Anime.Application.Interfaces;

/// <summary>Phát token đăng nhập cho admin (triển khai JWT ở tầng Api).</summary>
public interface IAdminTokenIssuer
{
    (string Token, DateTimeOffset ExpiresAt) Issue(int userId, string username);
}
