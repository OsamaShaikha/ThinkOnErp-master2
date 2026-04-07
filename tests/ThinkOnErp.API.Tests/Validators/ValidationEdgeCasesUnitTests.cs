using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.DTOs.Currency;
using ThinkOnErp.Application.DTOs.Role;
using ThinkOnErp.Application.DTOs.User;
using Xunit;

namespace ThinkOnErp.API.Tests.Validators;

/// <summary>
/// Unit tests for validation edge cases
/// </summary>
public class ValidationEdgeCasesUnitTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly string _adminToken;

    public ValidationEdgeCasesUnitTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Get admin token for tests
        var loginDto = new LoginDto { UserName = "admin", Password = "admin123" };
        var loginResponse = _client.PostAsJsonAsync("/api/auth/login", loginDto).GetAwaiter().GetResult();
        var loginResult = loginResponse.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>().GetAwaiter().GetResult();
        _adminToken = loginResult?.Data?.AccessToken ?? "";
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
    }

    [Fact]
    public async Task CreateRole_WithEmptyRowDesc_FailsValidation()
    {
        // Arrange
        var createDto = new CreateRoleDto
        {
            RoleNameAr = "", // Invalid - empty
            RoleNameEn = "Valid English",
            Note = "Test"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/roles", createDto);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<decimal>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.NotNull(result.Errors);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task CreateRole_WithRowDescExceeding100Characters_FailsValidation()
    {
        // Arrange
        var longString = new string('A', 101); // 101 characters
        var createDto = new CreateRoleDto
        {
            RoleNameAr = longString,
            RoleNameEn = "Valid English",
            Note = "Test"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/roles", createDto);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<decimal>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task CreateRole_WithNullNote_Succeeds()
    {
        // Arrange
        var createDto = new CreateRoleDto
        {
            RoleNameAr = "Valid Role",
            RoleNameEn = "Valid Role E",
            Note = null // Null note should be allowed
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/roles", createDto);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<decimal>>();

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data > 0);
    }

    [Fact]
    public async Task CreateCurrency_WithNegativeCurrRate_FailsValidation()
    {
        // Arrange
        var createDto = new CreateCurrencyDto
        {
            CurrencyNameAr = "Test Currency",
            CurrencyNameEn = "Test Currency E",
            ShortDesc = "TC",
            ShortDescE = "TC",
            SingulerDesc = "Test",
            SingulerDescE = "Test",
            DualDesc = "Tests",
            DualDescE = "Tests",
            SumDesc = "Tests",
            SumDescE = "Tests",
            FracDesc = "Cent",
            FracDescE = "Cent",
            CurrRate = -1.5m, // Invalid - negative
            CurrRateDate = DateTime.Now
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/currencies", createDto);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<decimal>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateUserName_Fails()
    {
        // Arrange
        var createDto1 = new CreateUserDto
        {
            NameAr = "User 1",
            NameEn = "User 1 E",
            UserName = $"uniqueuser{Guid.NewGuid()}",
            Password = "Password123!",
            IsAdmin = false
        };

        // Create first user
        var response1 = await _client.PostAsJsonAsync("/api/users", createDto1);
        Assert.True(response1.IsSuccessStatusCode);

        // Try to create second user with same username
        var createDto2 = new CreateUserDto
        {
            NameAr = "User 2",
            NameEn = "User 2 E",
            UserName = createDto1.UserName, // Duplicate username
            Password = "Password456!",
            IsAdmin = false
        };

        // Act
        var response2 = await _client.PostAsJsonAsync("/api/users", createDto2);

        // Assert
        // This might fail at database level or validation level
        Assert.False(response2.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithShortPassword_FailsValidation()
    {
        // Arrange
        var changePasswordDto = new ChangePasswordDto
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "123", // Too short
            ConfirmPassword = "123"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/users/1/change-password", changePasswordDto);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<int>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }
}
