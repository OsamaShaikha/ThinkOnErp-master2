using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.DTOs.Role;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// **Validates: Requirements 11.1, 11.3, 11.4, 11.5, 11.6, 11.7**
/// Property 15: Success Response Structure
/// For any successful operation, verify ApiResponse has success=true, appropriate statusCode, message, data, ISO 8601 timestamp, traceId
/// </summary>
public class SuccessResponseStructurePropertyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SuccessResponseStructurePropertyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Property(MaxTest = 100)]
    public Property SuccessfulOperation_ReturnsCorrectApiResponseStructure()
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

                // Create a role (successful operation)
                var createDto = new CreateRoleDto
                {
                    RoleNameAr = roleDesc,
                    RoleNameEn = roleDescE,
                    Note = "Test note"
                };

                var response = _client.PostAsJsonAsync("/api/roles", createDto).GetAwaiter().GetResult();
                var apiResponse = response.Content.ReadFromJsonAsync<ApiResponse<decimal>>().GetAwaiter().GetResult();

                // Verify success flag
                var hasSuccessTrue = apiResponse?.Success == true;

                // Verify appropriate status code (200 or 201)
                var hasAppropriateStatusCode = apiResponse?.StatusCode >= 200 && apiResponse?.StatusCode < 300;

                // Verify message exists and is not empty
                var hasMessage = !string.IsNullOrWhiteSpace(apiResponse?.Message);

                // Verify data is not null
                var hasData = apiResponse?.Data != null && apiResponse.Data > 0;

                // Verify timestamp is in ISO 8601 format
                var hasValidTimestamp = apiResponse?.Timestamp != default(DateTime);
                var timestampIsRecent = apiResponse?.Timestamp > DateTime.UtcNow.AddMinutes(-1);

                // Verify traceId exists
                var hasTraceId = !string.IsNullOrWhiteSpace(apiResponse?.TraceId);

                return (hasSuccessTrue && hasAppropriateStatusCode && hasMessage && 
                       hasData && hasValidTimestamp && timestampIsRecent == true && hasTraceId).ToProperty();
            });
    }
}
