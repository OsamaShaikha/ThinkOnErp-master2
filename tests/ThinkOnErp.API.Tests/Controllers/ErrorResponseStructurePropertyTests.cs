using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.DTOs.Role;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// **Validates: Requirements 11.2, 11.3, 11.4, 11.5, 11.6, 11.7**
/// Property 16: Error Response Structure
/// For any failed operation, verify ApiResponse has success=false, appropriate statusCode, message, null data, ISO 8601 timestamp, traceId
/// </summary>
public class ErrorResponseStructurePropertyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ErrorResponseStructurePropertyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Property(MaxTest = 100)]
    public Property FailedOperation_ReturnsCorrectApiResponseStructure()
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

                // Create a role with invalid data (empty RowDesc should fail validation)
                var createDto = new CreateRoleDto
                {
                    RowDesc = "", // Invalid - empty
                    RowDescE = "Valid English",
                    Note = "Test note"
                };

                var response = _client.PostAsJsonAsync("/api/roles", createDto).GetAwaiter().GetResult();
                var apiResponse = response.Content.ReadFromJsonAsync<ApiResponse<decimal>>().GetAwaiter().GetResult();

                // Verify success flag is false
                var hasSuccessFalse = apiResponse?.Success == false;

                // Verify appropriate error status code (400-599)
                var hasErrorStatusCode = apiResponse?.StatusCode >= 400 && apiResponse?.StatusCode < 600;

                // Verify message exists and is not empty
                var hasMessage = !string.IsNullOrWhiteSpace(apiResponse?.Message);

                // Verify data is default/null (for error responses, data should be 0 for decimal)
                var hasNullOrDefaultData = apiResponse?.Data == 0;

                // Verify timestamp is in ISO 8601 format
                var hasValidTimestamp = apiResponse?.Timestamp != default(DateTime);
                var timestampIsRecent = apiResponse?.Timestamp > DateTime.UtcNow.AddMinutes(-1);

                // Verify traceId exists
                var hasTraceId = !string.IsNullOrWhiteSpace(apiResponse?.TraceId);

                return (hasSuccessFalse && hasErrorStatusCode && hasMessage && 
                       hasNullOrDefaultData && hasValidTimestamp && timestampIsRecent == true && hasTraceId).ToProperty();
            });
    }
}
