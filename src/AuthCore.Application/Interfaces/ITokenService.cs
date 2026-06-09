using AuthCore.Domain.Entities;

namespace AuthCore.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Guid GetUserIdFromToken(string token);
}
