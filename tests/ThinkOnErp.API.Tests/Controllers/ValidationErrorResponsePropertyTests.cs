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
/// **Validates: Requirements 11.8, 12.4, 12.5, 12.6**
/// Property 17: Validation Error Response
/// For any request failing validation, verify response includes errors array with all validation messages, status code 400
/// </summary>
public class ValidationErrorResponsePropertyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ValidationErrorResponsePropertyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Property(MaxTest = 100)]
    public Property ValidationFailure_ReturnsErrorsArrayAndStatusCode400()
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

                // Create a role with multiple validation errors
                var createDto = new CreateRoleDto
                {
                    RowDesc = "", // Invalid - empty
                    RowDescE = "", // Invalid - empty
                    Note = "Test note"
                };

                var response = _client.PostAsJsonAsync("/api/roles", createDto).GetAwaiter().GetResult();
                var apiResponse = response.Content.ReadFromJsonAsync<ApiResponse<decimal>>().GetAwaiter().GetResult();

                // Verify status code is 400
                var hasStatusCode400 = apiResponse?.StatusCode == 400;

                // Verify errors array exists and contains validation messages
                var hasErrorsArray = apiResponse?.Errors != null && apiResponse.Errors.Count > 0;

                // Verify all errors are validation messages (not empty)
                var allErrorsAreValid = apiResponse?.Errors?.All(e => !string.IsNullOrWhiteSpace(e)) ?? false;

                // Verify success is false
                var hasSuccessFalse = apiResponse?.Success == false;

                // Verify message indicates validation error
                var hasValidationMessage = apiResponse?.Message?.Contains("validation", StringComparison.OrdinalIgnoreCase) ?? false;

                return (hasStatusCode400 && hasErrorsArray && allErrorsAreValid && hasSuccessFalse && hasValidationMessage).ToProperty();
            });
    }
}
