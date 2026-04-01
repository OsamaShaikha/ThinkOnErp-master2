using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.DTOs.Role;
using Xunit;

namespace ThinkOnErp.API.Tests.Middleware;

/// <summary>
/// **Validates: Requirements 14.6**
/// Property 20: ValidationException Returns 400
/// For any ValidationException, verify middleware converts to ApiResponse with status code 400 and all validation errors
/// </summary>
public class ValidationExceptionReturns400PropertyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ValidationExceptionReturns400PropertyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Property(MaxTest = 100)]
    public Property ValidationException_Returns400WithAllErrors()
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

                // Create a role with validation errors
                var createDto = new CreateRoleDto
                {
                    RowDesc = "", // Invalid - empty
                    RowDescE = "", // Invalid - empty
                    Note = "Test"
                };

                var response = _client.PostAsJsonAsync("/api/roles", createDto).GetAwaiter().GetResult();
                var apiResponse = response.Content.ReadFromJsonAsync<ApiResponse<decimal>>().GetAwaiter().GetResult();

                // Verify status code is 400
                var hasStatusCode400 = apiResponse?.StatusCode == 400;

                // Verify success is false
                var hasSuccessFalse = apiResponse?.Success == false;

                // Verify errors array contains validation errors
                var hasValidationErrors = apiResponse?.Errors != null && apiResponse.Errors.Count > 0;

                // Verify all errors are present (should have at least 2 for RowDesc and RowDescE)
                var hasMultipleErrors = apiResponse?.Errors?.Count >= 2;

                // Verify message indicates validation error
                var hasValidationMessage = apiResponse?.Message?.Contains("validation", StringComparison.OrdinalIgnoreCase) ?? false;

                return (hasStatusCode400 && hasSuccessFalse && hasValidationErrors && 
                       hasMultipleErrors == true && hasValidationMessage).ToProperty();
            });
    }
}
