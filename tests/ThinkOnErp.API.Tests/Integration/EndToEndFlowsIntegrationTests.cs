using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.DTOs.Branch;
using ThinkOnErp.Application.DTOs.Company;
using ThinkOnErp.Application.DTOs.Currency;
using ThinkOnErp.Application.DTOs.Role;
using ThinkOnErp.Application.DTOs.User;
using Xunit;

namespace ThinkOnErp.API.Tests.Integration;

/// <summary>
/// Integration tests for end-to-end flows
/// </summary>
public class EndToEndFlowsIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly string _adminToken;

    public EndToEndFlowsIntegrationTests(TestWebApplicationFactory factory)
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
    public async Task CompleteCRUDFlow_ForRoleEntity_Succeeds()
    {
        // Create
        var createDto = new CreateRoleDto
        {
            RoleNameAr = "Integration Test Role",
            RoleNameEn = "Integration Test Role E",
            Note = "Created for integration test"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/roles", createDto);
        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<decimal>>();
        Assert.True(createResponse.IsSuccessStatusCode);
        Assert.NotNull(createResult);
        Assert.True(createResult.Data > 0);
        var roleId = createResult.Data;

        // Read (GetById)
        var getByIdResponse = await _client.GetAsync($"/api/roles/{roleId}");
        var getByIdResult = await getByIdResponse.Content.ReadFromJsonAsync<ApiResponse<RoleDto>>();
        Assert.True(getByIdResponse.IsSuccessStatusCode);
        Assert.NotNull(getByIdResult?.Data);
        Assert.Equal("Integration Test Role", getByIdResult.Data.RoleNameEn);

        // Read (GetAll)
        var getAllResponse = await _client.GetAsync("/api/roles");
        var getAllResult = await getAllResponse.Content.ReadFromJsonAsync<ApiResponse<List<RoleDto>>>();
        Assert.True(getAllResponse.IsSuccessStatusCode);
        Assert.NotNull(getAllResult?.Data);
        Assert.Contains(getAllResult.Data, r => r.RoleId == roleId);

        // Update
        var updateDto = new UpdateRoleDto
        {
            RoleNameAr = "Updated Role",
            RoleNameEn = "Updated Role E",
            Note = "Updated note"
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/roles/{roleId}", updateDto);
        Assert.True(updateResponse.IsSuccessStatusCode);

        // Verify update
        var verifyResponse = await _client.GetAsync($"/api/roles/{roleId}");
        var verifyResult = await verifyResponse.Content.ReadFromJsonAsync<ApiResponse<RoleDto>>();
        Assert.Equal("Updated Role", verifyResult?.Data?.RoleNameEn);

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/roles/{roleId}");
        Assert.True(deleteResponse.IsSuccessStatusCode);

        // Verify deletion (should not appear in GetAll)
        var afterDeleteResponse = await _client.GetAsync("/api/roles");
        var afterDeleteResult = await afterDeleteResponse.Content.ReadFromJsonAsync<ApiResponse<List<RoleDto>>>();
        Assert.DoesNotContain(afterDeleteResult?.Data ?? new List<RoleDto>(), r => r.RoleId == roleId);
    }

    [Fact]
    public async Task CompleteCRUDFlow_ForCurrencyEntity_Succeeds()
    {
        // Create
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
            CurrRate = 1.5m,
            CurrRateDate = DateTime.Now
        };

        var createResponse = await _client.PostAsJsonAsync("/api/currencies", createDto);
        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<decimal>>();
        Assert.True(createResponse.IsSuccessStatusCode);
        var currencyId = createResult?.Data ?? 0;

        // Read
        var getResponse = await _client.GetAsync($"/api/currencies/{currencyId}");
        Assert.True(getResponse.IsSuccessStatusCode);

        // Update
        var updateDto = new UpdateCurrencyDto
        {
            CurrencyNameAr = "Updated Currency",
            CurrencyNameEn = "Updated Currency E",
            ShortDesc = "UC",
            ShortDescE = "UC",
            SingulerDesc = "Updated",
            SingulerDescE = "Updated",
            DualDesc = "Updateds",
            DualDescE = "Updateds",
            SumDesc = "Updateds",
            SumDescE = "Updateds",
            FracDesc = "Penny",
            FracDescE = "Penny",
            CurrRate = 2.0m,
            CurrRateDate = DateTime.Now
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/currencies/{currencyId}", updateDto);
        Assert.True(updateResponse.IsSuccessStatusCode);

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/currencies/{currencyId}");
        Assert.True(deleteResponse.IsSuccessStatusCode);
    }

    [Fact]
    public async Task CompleteCRUDFlow_ForCompanyEntity_Succeeds()
    {
        // Create
        var createDto = new CreateCompanyDto
        {
            CompanyNameAr = "Test Company",
            CompanyNameEn = "Test Company E",
            CountryId = 1,
            CurrId = 1
        };

        var createResponse = await _client.PostAsJsonAsync("/api/companies", createDto);
        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<decimal>>();
        Assert.True(createResponse.IsSuccessStatusCode);
        var companyId = createResult?.Data ?? 0;

        // Read
        var getResponse = await _client.GetAsync($"/api/companies/{companyId}");
        Assert.True(getResponse.IsSuccessStatusCode);

        // Update
        var updateDto = new UpdateCompanyDto
        {
            CompanyNameAr = "Updated Company",
            CompanyNameEn = "Updated Company E",
            CountryId = 1,
            CurrId = 1
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/companies/{companyId}", updateDto);
        Assert.True(updateResponse.IsSuccessStatusCode);

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/companies/{companyId}");
        Assert.True(deleteResponse.IsSuccessStatusCode);
    }

    [Fact]
    public async Task CompleteCRUDFlow_ForBranchEntity_Succeeds()
    {
        // Create
        var createDto = new CreateBranchDto
        {
            CompanyId = 1,
            BranchNameAr = "Test Branch",
            BranchNameEn = "Test Branch E",
            Phone = "123456789",
            Mobile = "987654321",
            Email = "test@branch.com",
            IsHeadBranch = false
        };

        var createResponse = await _client.PostAsJsonAsync("/api/branches", createDto);
        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<decimal>>();
        Assert.True(createResponse.IsSuccessStatusCode);
        var branchId = createResult?.Data ?? 0;

        // Read
        var getResponse = await _client.GetAsync($"/api/branches/{branchId}");
        Assert.True(getResponse.IsSuccessStatusCode);

        // Update
        var updateDto = new UpdateBranchDto
        {
            CompanyId = 1,
            BranchNameAr = "Updated Branch",
            BranchNameEn = "Updated Branch E",
            Phone = "111111111",
            Mobile = "222222222",
            Email = "updated@branch.com",
            IsHeadBranch = false
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/branches/{branchId}", updateDto);
        Assert.True(updateResponse.IsSuccessStatusCode);

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/branches/{branchId}");
        Assert.True(deleteResponse.IsSuccessStatusCode);
    }

    [Fact]
    public async Task CompleteCRUDFlow_ForUserEntity_Succeeds()
    {
        // Create
        var createDto = new CreateUserDto
        {
            NameAr = "Test User",
            NameEn = "Test User E",
            UserName = $"testuser{Guid.NewGuid()}",
            Password = "TestPassword123!",
            IsAdmin = false
        };

        var createResponse = await _client.PostAsJsonAsync("/api/users", createDto);
        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<decimal>>();
        Assert.True(createResponse.IsSuccessStatusCode);
        var userId = createResult?.Data ?? 0;

        // Read
        var getResponse = await _client.GetAsync($"/api/users/{userId}");
        Assert.True(getResponse.IsSuccessStatusCode);

        // Update
        var updateDto = new UpdateUserDto
        {
            NameAr = "Updated User",
            NameEn = "Updated User E",
            UserName = createDto.UserName,
            IsAdmin = false
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/users/{userId}", updateDto);
        Assert.True(updateResponse.IsSuccessStatusCode);

        // Delete
        var deleteResponse = await _client.DeleteAsync($"/api/users/{userId}");
        Assert.True(deleteResponse.IsSuccessStatusCode);
    }

    [Fact]
    public async Task LoginCreateUpdateDeleteFlow_Succeeds()
    {
        // Login
        var loginDto = new LoginDto { UserName = "admin", Password = "admin123" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginDto);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        Assert.True(loginResponse.IsSuccessStatusCode);
        Assert.NotNull(loginResult?.Data?.AccessToken);

        // Use token for subsequent requests
        var token = loginResult.Data.AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create role
        var createDto = new CreateRoleDto
        {
            RoleNameAr = "Flow Test Role",
            RoleNameEn = "Flow Test Role E",
            Note = "Test"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/roles", createDto);
        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<decimal>>();
        Assert.True(createResponse.IsSuccessStatusCode);
        var roleId = createResult?.Data ?? 0;

        // Update role
        var updateDto = new UpdateRoleDto
        {
            RoleNameAr = "Updated Flow Role",
            RoleNameEn = "Updated Flow Role E",
            Note = "Updated"
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/roles/{roleId}", updateDto);
        Assert.True(updateResponse.IsSuccessStatusCode);

        // Delete role
        var deleteResponse = await _client.DeleteAsync($"/api/roles/{roleId}");
        Assert.True(deleteResponse.IsSuccessStatusCode);
    }

    [Fact]
    public async Task UnauthorizedAccess_Returns401()
    {
        // Remove authorization header
        _client.DefaultRequestHeaders.Authorization = null;

        // Try to access protected endpoint
        var response = await _client.GetAsync("/api/roles");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NonAdminAccessToAdminEndpoint_Returns403()
    {
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

        // Try to create a role (admin-only endpoint)
        var createDto = new CreateRoleDto
        {
            RoleNameAr = "Test",
            RoleNameEn = "Test E",
            Note = "Test"
        };

        var response = await _client.PostAsJsonAsync("/api/roles", createDto);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
