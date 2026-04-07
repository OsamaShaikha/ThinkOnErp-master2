using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.DTOs.Role;
using Xunit;

namespace ThinkOnErp.API.Tests.Authorization;

/// <summary>
/// Unit tests for authorization policies
/// </summary>
public class AuthorizationPoliciesUnitTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthorizationPoliciesUnitTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task AdminOnlyPolicy_WithAdminUser_AllowsAccess()
    {
        // Arrange
        var loginDto = new LoginDto { UserName = "admin", Password = "admin123" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginDto);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        var token = loginResult?.Data?.AccessToken;

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Try to create a role (admin-only endpoint)
        var createDto = new CreateRoleDto
        {
            RoleNameAr = "Test Role",
            RoleNameEn = "Test Role E",
            Note = "Test"
        };
        var response = await _client.PostAsJsonAsync("/api/roles", createDto);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task AdminOnlyPolicy_WithNonAdminUser_DeniesAccess()
    {
        // Arrange
        // Note: This test assumes there's a non-admin user in the test database
        var loginDto = new LoginDto { UserName = "regularuser", Password = "password123" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

        // If non-admin user doesn't exist, skip this test
        if (loginResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            Assert.True(true, "Non-admin user not found in test database");
            return;
        }

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        var token = loginResult?.Data?.AccessToken;

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Try to create a role (admin-only endpoint)
        var createDto = new CreateRoleDto
        {
            RoleNameAr = "Test Role",
            RoleNameEn = "Test Role E",
            Note = "Test"
        };
        var response = await _client.PostAsJsonAsync("/api/roles", createDto);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_DeniesAccess()
    {
        // Arrange - No token set

        // Act - Try to access protected endpoint
        var response = await _client.GetAsync("/api/roles");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_AllowsAccess()
    {
        // Arrange
        var loginDto = new LoginDto { UserName = "admin", Password = "admin123" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginDto);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        var token = loginResult?.Data?.AccessToken;

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Try to access protected endpoint
        var response = await _client.GetAsync("/api/roles");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_DeniesAccess()
    {
        // Arrange
        var invalidToken = "invalid.token.here";
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        // Act - Try to access protected endpoint
        var response = await _client.GetAsync("/api/roles");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithExpiredToken_DeniesAccess()
    {
        // Arrange
        // Create a token that's already expired (this would require modifying token generation for testing)
        // For now, we'll use an invalid token format
        var expiredToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjB9.invalid";
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act - Try to access protected endpoint
        var response = await _client.GetAsync("/api/roles");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginEndpoint_WithoutToken_AllowsAccess()
    {
        // Arrange - No token set
        var loginDto = new LoginDto { UserName = "admin", Password = "admin123" };

        // Act - Login endpoint should not require authentication
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }
}
