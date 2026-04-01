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
/// **Validates: Requirements 30.3**
/// Property 32: No Domain Entities in API Responses
/// For any API endpoint response, verify data is DTO, never domain entity directly
/// </summary>
public class NoDomainEntitiesInResponsesPropertyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public NoDomainEntitiesInResponsesPropertyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Property(MaxTest = 100)]
    public Property ApiResponse_ContainsDtoNotDomainEntity()
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
                    Note = "Test DTO"
                };

                var createResponse = _client.PostAsJsonAsync("/api/roles", createDto).GetAwaiter().GetResult();
                var createResult = createResponse.Content.ReadFromJsonAsync<ApiResponse<decimal>>().GetAwaiter().GetResult();
                var roleId = createResult?.Data ?? 0;

                // Get all roles
                var getAllResponse = _client.GetAsync("/api/roles").GetAwaiter().GetResult();
                var getAllContent = getAllResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                // Verify response doesn't contain domain entity property names that aren't in DTOs
                // Domain entities have properties like CreationUser, CreationDate, UpdateUser, UpdateDate
                // DTOs should not expose these internal audit fields
                var doesNotContainDomainOnlyFields = 
                    !getAllContent.Contains("\"creationUser\"", StringComparison.OrdinalIgnoreCase) &&
                    !getAllContent.Contains("\"creationDate\"", StringComparison.OrdinalIgnoreCase) &&
                    !getAllContent.Contains("\"updateUser\"", StringComparison.OrdinalIgnoreCase) &&
                    !getAllContent.Contains("\"updateDate\"", StringComparison.OrdinalIgnoreCase);

                // Get single role
                var getByIdResponse = _client.GetAsync($"/api/roles/{roleId}").GetAwaiter().GetResult();
                var getByIdContent = getByIdResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                var singleResponseDoesNotContainDomainFields = 
                    !getByIdContent.Contains("\"creationUser\"", StringComparison.OrdinalIgnoreCase) &&
                    !getByIdContent.Contains("\"creationDate\"", StringComparison.OrdinalIgnoreCase) &&
                    !getByIdContent.Contains("\"updateUser\"", StringComparison.OrdinalIgnoreCase) &&
                    !getByIdContent.Contains("\"updateDate\"", StringComparison.OrdinalIgnoreCase);

                // Verify we can deserialize as DTO (not domain entity)
                var canDeserializeAsDto = false;
                try
                {
                    var getAllResult = JsonSerializer.Deserialize<ApiResponse<List<RoleDto>>>(getAllContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    canDeserializeAsDto = getAllResult?.Data != null;
                }
                catch
                {
                    canDeserializeAsDto = false;
                }

                return (doesNotContainDomainOnlyFields && singleResponseDoesNotContainDomainFields && canDeserializeAsDto).ToProperty();
            });
    }
}
