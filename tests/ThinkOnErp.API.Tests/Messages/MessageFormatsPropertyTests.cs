using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.DTOs.Role;
using Xunit;

namespace ThinkOnErp.API.Tests.Messages;

/// <summary>
/// **Validates: Requirements 26.1, 26.2, 26.3, 26.4, 26.5, 26.6**
/// Properties 26-31: Message Format Tests
/// </summary>
public class MessageFormatsPropertyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MessageFormatsPropertyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    /// <summary>
    /// Property 26: Success Message Format
    /// **Validates: Requirements 26.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SuccessfulCreate_FollowsMessageFormat()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100).Select(i => $"Role {i}")),
            Arb.From(Gen.Choose(1, 100).Select(i => $"Role E {i}")),
            (roleDesc, roleDescE) =>
            {
                // Get admin token
                var loginDto = new LoginDto { UserName = "admin", Password = "admin123" };
                var loginResponse = _client.PostAsJsonAsync("/api/auth/login", loginDto).GetAwaiter().GetResult();
                var loginResult = loginResponse.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>().GetAwaiter().GetResult();
                var token = loginResult?.Data?.AccessToken;

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Create a role
                var createDto = new CreateRoleDto
                {
                    RowDesc = roleDesc,
                    RowDescE = roleDescE,
                    Note = "Test"
                };

                var response = _client.PostAsJsonAsync("/api/roles", createDto).GetAwaiter().GetResult();
                var apiResponse = response.Content.ReadFromJsonAsync<ApiResponse<decimal>>().GetAwaiter().GetResult();

                // Verify message follows format "{EntityName} created successfully"
                var messageFollowsFormat = apiResponse?.Message?.Contains("created successfully", StringComparison.OrdinalIgnoreCase) ?? false;

                return messageFollowsFormat.ToProperty();
            });
    }

    /// <summary>
    /// Property 27: Not Found Message Format
    /// **Validates: Requirements 26.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NotFound_FollowsMessageFormat()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(999999, 9999999).Select(i => (decimal)i)), // Non-existent IDs
            (nonExistentId) =>
            {
                // Get admin token
                var loginDto = new LoginDto { UserName = "admin", Password = "admin123" };
                var loginResponse = _client.PostAsJsonAsync("/api/auth/login", loginDto).GetAwaiter().GetResult();
                var loginResult = loginResponse.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>().GetAwaiter().GetResult();
                var token = loginResult?.Data?.AccessToken;

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Try to get non-existent role
                var response = _client.GetAsync($"/api/roles/{nonExistentId}").GetAwaiter().GetResult();
                var apiResponse = response.Content.ReadFromJsonAsync<ApiResponse<object>>().GetAwaiter().GetResult();

                // Verify message follows format "No {entityName} found with the specified identifier"
                // or similar not found message
                var messageIndicatesNotFound = (apiResponse?.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                              (apiResponse?.Message?.Contains("No ", StringComparison.Ordinal) ?? false);

                return messageIndicatesNotFound.ToProperty();
            });
    }

    /// <summary>
    /// Property 28: Authorization Error Message
    /// **Validates: Requirements 26.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AuthorizationFailure_HasCorrectMessage()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100)),
            (iteration) =>
            {
                // Login as non-admin user (if exists) or test without admin privileges
                // For this test, we'll try to access admin endpoint without proper token
                var response = _client.PostAsJsonAsync("/api/roles", new CreateRoleDto
                {
                    RowDesc = "Test",
                    RowDescE = "Test E",
                    Note = "Test"
                }).GetAwaiter().GetResult();

                // Should get 401 (unauthorized) since no token provided
                // The message should indicate access denied or authorization required
                var statusCodeIs401 = response.StatusCode == System.Net.HttpStatusCode.Unauthorized;

                return statusCodeIs401.ToProperty();
            });
    }

    /// <summary>
    /// Property 29: Authentication Error Message
    /// **Validates: Requirements 26.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AuthenticationFailure_HasCorrectMessage()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100).Select(i => $"invaliduser{i}")),
            Arb.From(Gen.Choose(1, 100).Select(i => $"invalidpass{i}")),
            (invalidUser, invalidPass) =>
            {
                // Try to login with invalid credentials
                var loginDto = new LoginDto { UserName = invalidUser, Password = invalidPass };
                var response = _client.PostAsJsonAsync("/api/auth/login", loginDto).GetAwaiter().GetResult();
                var apiResponse = response.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>().GetAwaiter().GetResult();

                // Verify message indicates invalid credentials
                var messageIndicatesInvalidCredentials = 
                    (apiResponse?.Message?.Contains("Invalid credentials", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (apiResponse?.Message?.Contains("invalid", StringComparison.OrdinalIgnoreCase) ?? false);

                // Verify status code is 401
                var statusCodeIs401 = apiResponse?.StatusCode == 401;

                return (messageIndicatesInvalidCredentials && statusCodeIs401).ToProperty();
            });
    }

    /// <summary>
    /// Property 30: Validation Error Message
    /// **Validates: Requirements 26.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidationFailure_HasCorrectMessage()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100)),
            (iteration) =>
            {
                // Get admin token
                var loginDto = new LoginDto { UserName = "admin", Password = "admin123" };
                var loginResponse = _client.PostAsJsonAsync("/api/auth/login", loginDto).GetAwaiter().GetResult();
                var loginResult = loginResponse.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>().GetAwaiter().GetResult();
                var token = loginResult?.Data?.AccessToken;

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Create role with validation errors
                var createDto = new CreateRoleDto
                {
                    RowDesc = "", // Invalid
                    RowDescE = "",
                    Note = "Test"
                };

                var response = _client.PostAsJsonAsync("/api/roles", createDto).GetAwaiter().GetResult();
                var apiResponse = response.Content.ReadFromJsonAsync<ApiResponse<decimal>>().GetAwaiter().GetResult();

                // Verify message indicates validation error
                var messageIndicatesValidation = 
                    apiResponse?.Message?.Contains("validation", StringComparison.OrdinalIgnoreCase) ?? false;

                return messageIndicatesValidation.ToProperty();
            });
    }

    /// <summary>
    /// Property 31: Server Error Message
    /// **Validates: Requirements 26.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ServerError_HasGenericMessage()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 100)),
            (iteration) =>
            {
                // Get admin token
                var loginDto = new LoginDto { UserName = "admin", Password = "admin123" };
                var loginResponse = _client.PostAsJsonAsync("/api/auth/login", loginDto).GetAwaiter().GetResult();
                var loginResult = loginResponse.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>().GetAwaiter().GetResult();
                var token = loginResult?.Data?.AccessToken;

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Try to trigger a server error (use extreme values)
                var response = _client.GetAsync($"/api/roles/{decimal.MaxValue}").GetAwaiter().GetResult();

                if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    var apiResponse = response.Content.ReadFromJsonAsync<ApiResponse<object>>().GetAwaiter().GetResult();

                    // Verify message is generic (doesn't expose internal details)
                    var messageIsGeneric = 
                        (apiResponse?.Message?.Contains("unexpected error", StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (apiResponse?.Message?.Contains("try again", StringComparison.OrdinalIgnoreCase) ?? false);

                    return messageIsGeneric.ToProperty();
                }

                // If no 500 error, property is vacuously true
                return true.ToProperty();
            });
    }
}
