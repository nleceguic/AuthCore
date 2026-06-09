using AuthCore.Application.DTOs;
using AuthCore.Application.Interfaces;
using AuthCore.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace AuthCore.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly int _accessTokenExpirationMinutes;
    private readonly int _refreshTokenExpirationDays;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;

        var section = configuration.GetSection("JwtSettings");
        _accessTokenExpirationMinutes = int.Parse(section["AccessTokenExpirationMinutes"] ?? "15");
        _refreshTokenExpirationDays = int.Parse(section["RefreshTokenExpirationDays"] ?? "7");
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await _userRepository.GetByEmailAsync(request.Email) is not null)
            throw new InvalidOperationException("Email is already registered.");

        if (await _userRepository.GetByUsernameAsync(request.Username) is not null)
            throw new InvalidOperationException("Username is already taken.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
        };

        await _userRepository.AddAsync(user);

        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        await _refreshTokenRepository.RevokeAllForUserAsync(user.Id);

        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
    {
        var stored = await _refreshTokenRepository.GetByTokenAsync(refreshToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (stored.IsRevoked || stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token is expired or revoked.");

        stored.IsRevoked = true;
        await _refreshTokenRepository.UpdateAsync(stored);

        return await IssueTokensAsync(stored.User);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var stored = await _refreshTokenRepository.GetByTokenAsync(refreshToken);
        if (stored is null)
            return;

        stored.IsRevoked = true;
        await _refreshTokenRepository.UpdateAsync(stored);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshTokenValue,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays),
            IsRevoked = false,
        };

        await _refreshTokenRepository.AddAsync(refreshToken);

        return new AuthResponse(accessToken, refreshTokenValue, expiresAt);
    }
}
