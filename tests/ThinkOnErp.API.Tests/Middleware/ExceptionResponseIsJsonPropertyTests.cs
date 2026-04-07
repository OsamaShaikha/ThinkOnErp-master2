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
/// **Validates: Requirements 14.7**
/// Property 21: Exception Response is JSON
/// For any exception handled by middleware, verify response content type is application/json
/// </summary>
public class ExceptionResponseIsJsonPropertyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ExceptionResponseIsJsonPropertyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Property(MaxTest = 100)]
    public Property ExceptionResponse_HasJsonContentType()
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

                // Trigger a validation exception
                var createDto = new CreateRoleDto
                {
                    RoleNameAr = "", // Invalid
                    RoleNameEn = "",
                    Note = "Test"
                };

                var response = _client.PostAsJsonAsync("/api/roles", createDto).GetAwaiter().GetResult();

                // Verify content type is application/json
                var contentType = response.Content.Headers.ContentType?.MediaType;
                var isJson = contentType == "application/json";

                // Verify we can deserialize as JSON
                var canDeserialize = false;
                try
                {
                    var apiResponse = response.Content.ReadFromJsonAsync<ApiResponse<decimal>>().GetAwaiter().GetResult();
                    canDeserialize = apiResponse != null;
                }
                catch
                {
                    canDeserialize = false;
                }

                return (isJson && canDeserialize).ToProperty();
            });
    }
}
