using AuthCore.Application.DTOs;
using AuthCore.Application.Interfaces;
using AuthCore.Application.Services;
using AuthCore.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Moq;

namespace AuthCore.Tests.Unit;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepo = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:AccessTokenExpirationMinutes"] = "15",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7",
            })
            .Build();

        _sut = new AuthService(
            _userRepo.Object,
            _refreshTokenRepo.Object,
            _tokenService.Object,
            config);
    }

    // ── Register ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithExistingEmail_ThrowsInvalidOperationException()
    {
        var existing = BuildUser();
        _userRepo.Setup(r => r.GetByEmailAsync(existing.Email)).ReturnsAsync(existing);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(new RegisterRequest("newuser", existing.Email, "pass")));
    }

    [Fact]
    public async Task Register_WithExistingUsername_ThrowsInvalidOperationException()
    {
        var existing = BuildUser();
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.GetByUsernameAsync(existing.Username)).ReturnsAsync(existing);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(new RegisterRequest(existing.Username, "other@test.com", "pass")));
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsAuthResponse()
    {
        SetupEmptyRepo();
        SetupTokenService();

        var result = await _sut.RegisterAsync(new RegisterRequest("newuser", "new@test.com", "pass"));

        Assert.NotNull(result);
        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
    }

    // ── Login ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithInvalidEmail_ThrowsUnauthorizedAccessException()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.LoginAsync(new LoginRequest("nobody@test.com", "pass")));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ThrowsUnauthorizedAccessException()
    {
        var user = BuildUserWithHash("correct-password");
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.LoginAsync(new LoginRequest(user.Email, "wrong-password")));
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAuthResponse()
    {
        var user = BuildUserWithHash("correct-password");
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _refreshTokenRepo.Setup(r => r.RevokeAllForUserAsync(user.Id)).Returns(Task.CompletedTask);
        SetupTokenService();

        var result = await _sut.LoginAsync(new LoginRequest(user.Email, "correct-password"));

        Assert.NotNull(result);
        Assert.Equal("access-token", result.AccessToken);
    }

    // ── RefreshToken ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshToken_WithRevokedToken_ThrowsUnauthorizedAccessException()
    {
        var stored = BuildRefreshToken(isRevoked: true);
        _refreshTokenRepo.Setup(r => r.GetByTokenAsync(stored.Token)).ReturnsAsync(stored);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RefreshTokenAsync(stored.Token));
    }

    [Fact]
    public async Task RefreshToken_WithExpiredToken_ThrowsUnauthorizedAccessException()
    {
        var stored = BuildRefreshToken(expiresAt: DateTime.UtcNow.AddDays(-1));
        _refreshTokenRepo.Setup(r => r.GetByTokenAsync(stored.Token)).ReturnsAsync(stored);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RefreshTokenAsync(stored.Token));
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewAuthResponse()
    {
        var stored = BuildRefreshToken();
        _refreshTokenRepo.Setup(r => r.GetByTokenAsync(stored.Token)).ReturnsAsync(stored);
        _refreshTokenRepo.Setup(r => r.UpdateAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
        SetupTokenService();

        var result = await _sut.RefreshTokenAsync(stored.Token);

        Assert.NotNull(result);
        Assert.Equal("access-token", result.AccessToken);
        Assert.True(stored.IsRevoked);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private void SetupEmptyRepo()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.GetByUsernameAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
        _userRepo.Setup(r => r.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
        _refreshTokenRepo.Setup(r => r.AddAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
    }

    private void SetupTokenService()
    {
        _tokenService.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");
        _tokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token");
        _refreshTokenRepo.Setup(r => r.AddAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
    }

    private static User BuildUser() => new()
    {
        Id = Guid.NewGuid(),
        Username = "existing",
        Email = "existing@test.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
        CreatedAt = DateTime.UtcNow,
    };

    private static User BuildUserWithHash(string password) => new()
    {
        Id = Guid.NewGuid(),
        Username = "user",
        Email = "user@test.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        CreatedAt = DateTime.UtcNow,
    };

    private static RefreshToken BuildRefreshToken(
        bool isRevoked = false,
        DateTime? expiresAt = null)
    {
        var userId = Guid.NewGuid();
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString(),
            IsRevoked = isRevoked,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            User = new User
            {
                Id = userId,
                Username = "user",
                Email = "user@test.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
            },
        };
    }
}
