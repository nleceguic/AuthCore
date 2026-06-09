using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AuthCore.Application.DTOs;
using AuthCore.Tests;

namespace AuthCore.Tests.Integration;

public class AuthControllerTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AuthControllerTests(AuthApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_ReturnsCreated_WithAuthResponse()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("reg_ok", "reg_ok@test.com", "Password1!"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        Assert.NotNull(auth);
        Assert.NotEmpty(auth.AccessToken);
        Assert.NotEmpty(auth.RefreshToken);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        var client = CreateClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("dup1", "dup@test.com", "Password1!"));

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("dup2", "dup@test.com", "Password1!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithTokens()
    {
        var client = CreateClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("login_user", "login_ok@test.com", "Password1!"));

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("login_ok@test.com", "Password1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        Assert.NotNull(auth);
        Assert.NotEmpty(auth.AccessToken);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = CreateClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("wrongpwd_user", "wrongpwd@test.com", "correct!"));

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("wrongpwd@test.com", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTasks_WithoutToken_ReturnsUnauthorized()
    {
        var response = await CreateClient().GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTasks_WithValidToken_ReturnsOk()
    {
        var auth = await RegisterAndGetTokensAsync("tasks_user@test.com", "tasks_user");
        var client = CreateClient(auth.AccessToken);

        var response = await client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokens()
    {
        var auth = await RegisterAndGetTokensAsync("refresh_user@test.com", "refresh_user");

        var response = await CreateClient().PostAsJsonAsync("/api/auth/refresh",
            new TokenRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newAuth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        Assert.NotNull(newAuth);
        Assert.NotEmpty(newAuth.AccessToken);
        Assert.NotEqual(auth.AccessToken, newAuth.AccessToken);
    }

    [Fact]
    public async Task Logout_WithValidToken_ReturnsNoContent()
    {
        var auth = await RegisterAndGetTokensAsync("logout_user@test.com", "logout_user");

        var response = await CreateClient().PostAsJsonAsync("/api/auth/logout",
            new TokenRequest(auth.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private HttpClient CreateClient(string? bearerToken = null)
    {
        var client = _factory.CreateClient();
        if (bearerToken is not null)
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", bearerToken);
        return client;
    }

    private async Task<AuthResponse> RegisterAndGetTokensAsync(string email, string username)
    {
        var client = CreateClient();
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(username, email, "Password1!"));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "Password1!"));

        return (await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts))!;
    }
}
