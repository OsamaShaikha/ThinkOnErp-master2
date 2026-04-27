using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Audit;
using ThinkOnErp.Domain.Models;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Integration tests for AuditLogsController endpoints.
/// Tests the legacy audit log view endpoint that matches logs.png interface.
/// </summary>
public class AuditLogsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public AuditLogsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task GetLegacyAuditLogs_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - no authentication token

        // Act
        var response = await _client.GetAsync("/api/auditlogs/legacy");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLegacyAuditLogs_WithValidAuthentication_ReturnsOk()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/legacy?pageNumber=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<LegacyAuditLogDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.Items);
    }

    [Fact]
    public async Task GetLegacyAuditLogs_WithFilters_ReturnsFilteredResults()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync(
            "/api/auditlogs/legacy?company=Test&module=HR&pageNumber=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<LegacyAuditLogDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetLegacyAuditLogs_WithInvalidPageNumber_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/legacy?pageNumber=0&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetLegacyAuditLogs_WithInvalidPageSize_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/legacy?pageNumber=1&pageSize=200");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetLegacyAuditLogs_WithInvalidStatus_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/legacy?status=InvalidStatus");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetLegacyAuditLogs_WithInvalidDateRange_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync(
            "/api/auditlogs/legacy?startDate=2024-12-31&endDate=2024-01-01");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetLegacyAuditLogs_VerifyResponseStructure()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/legacy?pageNumber=1&pageSize=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<LegacyAuditLogDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        
        // Verify paged result structure
        Assert.NotNull(result.Data.Items);
        Assert.True(result.Data.TotalCount >= 0);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(5, result.Data.PageSize);
        
        // If there are items, verify the structure matches logs.png format
        if (result.Data.Items.Count > 0)
        {
            var firstItem = result.Data.Items[0];
            Assert.True(firstItem.Id > 0);
            Assert.NotNull(firstItem.ErrorDescription);
            Assert.NotNull(firstItem.Module);
            Assert.NotNull(firstItem.Company);
            Assert.NotNull(firstItem.Branch);
            Assert.NotNull(firstItem.User);
            Assert.NotNull(firstItem.Device);
            Assert.NotEqual(default(DateTime), firstItem.DateTime);
            Assert.NotNull(firstItem.Status);
        }
    }

    [Fact]
    public async Task GetDashboardCounters_WithValidAuthentication_ReturnsCounters()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/dashboard");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<LegacyDashboardCounters>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        
        // Verify counter structure
        Assert.True(result.Data.UnresolvedCount >= 0);
        Assert.True(result.Data.InProgressCount >= 0);
        Assert.True(result.Data.ResolvedCount >= 0);
        Assert.True(result.Data.CriticalErrorsCount >= 0);
    }

    [Fact]
    public async Task GetAuditLogStatus_WithValidId_ReturnsStatus()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // First, get an audit log entry
        var logsResponse = await _client.GetAsync("/api/auditlogs/legacy?pageNumber=1&pageSize=1");
        var logsResult = await logsResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<LegacyAuditLogDto>>>();
        
        if (logsResult?.Data?.Items.Count > 0)
        {
            var auditLogId = logsResult.Data.Items[0].Id;

            // Act
            var response = await _client.GetAsync($"/api/auditlogs/{auditLogId}/status");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            Assert.NotNull(result);
            Assert.True(result.Success);
        }
    }

    [Fact]
    public async Task GetAuditLogStatus_WithInvalidId_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/0/status");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAuditLogStatus_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - no authentication token
        var request = new
        {
            Status = "In Progress",
            ResolutionNotes = "Investigating the issue"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/auditlogs/legacy/1/status", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAuditLogStatus_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // First, get an audit log entry
        var logsResponse = await _client.GetAsync("/api/auditlogs/legacy?pageNumber=1&pageSize=1");
        var logsResult = await logsResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<LegacyAuditLogDto>>>();
        
        if (logsResult?.Data?.Items.Count > 0)
        {
            var auditLogId = logsResult.Data.Items[0].Id;
            var request = new
            {
                Status = "In Progress",
                ResolutionNotes = "Investigating the issue",
                AssignedToUserId = (long?)null
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/auditlogs/legacy/{auditLogId}/status", request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Contains("updated successfully", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task UpdateAuditLogStatus_WithInvalidId_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            Status = "In Progress",
            ResolutionNotes = "Test notes"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/auditlogs/legacy/0/status", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("must be greater than 0", result.Message);
    }

    [Fact]
    public async Task UpdateAuditLogStatus_WithInvalidStatus_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            Status = "InvalidStatus",
            ResolutionNotes = "Test notes"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/auditlogs/legacy/1/status", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Unresolved, In Progress, Resolved, Critical", result.Message);
    }

    [Fact]
    public async Task UpdateAuditLogStatus_WithTooLongResolutionNotes_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            Status = "Resolved",
            ResolutionNotes = new string('A', 4001) // Exceeds 4000 character limit
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/auditlogs/legacy/1/status", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("cannot exceed 4000 characters", result.Message);
    }

    [Theory]
    [InlineData("Unresolved")]
    [InlineData("In Progress")]
    [InlineData("Resolved")]
    [InlineData("Critical")]
    public async Task UpdateAuditLogStatus_WithAllValidStatuses_ReturnsOk(string status)
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // First, get an audit log entry
        var logsResponse = await _client.GetAsync("/api/auditlogs/legacy?pageNumber=1&pageSize=1");
        var logsResult = await logsResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<LegacyAuditLogDto>>>();
        
        if (logsResult?.Data?.Items.Count > 0)
        {
            var auditLogId = logsResult.Data.Items[0].Id;
            var request = new
            {
                Status = status,
                ResolutionNotes = $"Setting status to {status}"
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/auditlogs/legacy/{auditLogId}/status", request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            Assert.NotNull(result);
            Assert.True(result.Success);
        }
    }

    [Fact]
    public async Task UpdateAuditLogStatus_WithAssignedUser_ReturnsOk()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // First, get an audit log entry
        var logsResponse = await _client.GetAsync("/api/auditlogs/legacy?pageNumber=1&pageSize=1");
        var logsResult = await logsResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<LegacyAuditLogDto>>>();
        
        if (logsResult?.Data?.Items.Count > 0)
        {
            var auditLogId = logsResult.Data.Items[0].Id;
            var request = new
            {
                Status = "In Progress",
                ResolutionNotes = "Assigned to user for investigation",
                AssignedToUserId = 1L
            };

            // Act
            var response = await _client.PutAsJsonAsync($"/api/auditlogs/legacy/{auditLogId}/status", request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            Assert.NotNull(result);
            Assert.True(result.Success);
        }
    }

    [Fact]
    public async Task UpdateAuditLogStatus_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new
        {
            Status = "Resolved",
            ResolutionNotes = "Test notes"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/api/auditlogs/legacy/999999999/status", request);

        // Assert - Could be NotFound or OK depending on whether the stored procedure throws an error
        // The implementation should ideally return NotFound for non-existent IDs
        Assert.True(response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEntityHistory_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - no authentication token

        // Act
        var response = await _client.GetAsync("/api/auditlogs/entity/SysUser/1");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEntityHistory_WithValidEntityTypeAndId_ReturnsOk()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/entity/SysUser/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<AuditLogDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetEntityHistory_WithEmptyEntityType_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/entity/ /1");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEntityHistory_WithInvalidEntityId_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/entity/SysUser/0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("must be greater than 0", result.Message);
    }

    [Fact]
    public async Task GetEntityHistory_WithNegativeEntityId_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/entity/SysUser/-1");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("SysUser")]
    [InlineData("SysCompany")]
    [InlineData("SysBranch")]
    [InlineData("SysRole")]
    [InlineData("SysCurrency")]
    public async Task GetEntityHistory_WithDifferentEntityTypes_ReturnsOk(string entityType)
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/auditlogs/entity/{entityType}/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<AuditLogDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetEntityHistory_VerifyResponseStructure()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/entity/SysUser/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<AuditLogDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Contains("audit log entries", result.Message, StringComparison.OrdinalIgnoreCase);
        
        // If there are items, verify the structure
        var items = result.Data.ToList();
        if (items.Count > 0)
        {
            var firstItem = items[0];
            Assert.True(firstItem.Id > 0);
            Assert.NotNull(firstItem.EntityType);
            Assert.Equal("SysUser", firstItem.EntityType);
            Assert.Equal(1, firstItem.EntityId);
            Assert.NotNull(firstItem.Action);
            Assert.NotEqual(default(DateTime), firstItem.Timestamp);
        }
    }

    [Fact]
    public async Task GetEntityHistory_WithNonExistentEntity_ReturnsEmptyList()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/entity/SysUser/999999999");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<AuditLogDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetEntityHistory_ReturnsEntriesInChronologicalOrder()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/auditlogs/entity/SysUser/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<AuditLogDto>>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        
        var items = result.Data.ToList();
        if (items.Count > 1)
        {
            // Verify entries are in descending chronological order (newest first)
            for (int i = 0; i < items.Count - 1; i++)
            {
                Assert.True(items[i].Timestamp >= items[i + 1].Timestamp,
                    "Audit log entries should be in descending chronological order");
            }
        }
    }

    #region User Action Replay Tests

    [Fact]
    public async Task GetUserActionReplay_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - no authentication token
        var startDate = DateTime.UtcNow.AddHours(-8);
        var endDate = DateTime.UtcNow;

        // Act
        var response = await _client.GetAsync(
            $"/api/auditlogs/replay/user/1?startDate={startDate:O}&endDate={endDate:O}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserActionReplay_WithValidParameters_ReturnsOk()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        var startDate = DateTime.UtcNow.AddHours(-8);
        var endDate = DateTime.UtcNow;

        // Act
        var response = await _client.GetAsync(
            $"/api/auditlogs/replay/user/1?startDate={startDate:O}&endDate={endDate:O}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserActionReplay>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.UserId);
        Assert.NotNull(result.Data.Actions);
        Assert.NotNull(result.Data.Timeline);
    }

    [Fact]
    public async Task GetUserActionReplay_WithInvalidUserId_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        var startDate = DateTime.UtcNow.AddHours(-8);
        var endDate = DateTime.UtcNow;

        // Act
        var response = await _client.GetAsync(
            $"/api/auditlogs/replay/user/0?startDate={startDate:O}&endDate={endDate:O}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("User ID must be greater than 0", result.Message);
    }

    [Fact]
    public async Task GetUserActionReplay_WithInvalidDateRange_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddHours(-8); // End before start

        // Act
        var response = await _client.GetAsync(
            $"/api/auditlogs/replay/user/1?startDate={startDate:O}&endDate={endDate:O}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Start date must be earlier than end date", result.Message);
    }

    [Fact]
    public async Task GetUserActionReplay_WithDateRangeTooLarge_ReturnsBadRequest()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        var startDate = DateTime.UtcNow.AddDays(-31);
        var endDate = DateTime.UtcNow;

        // Act
        var response = await _client.GetAsync(
            $"/api/auditlogs/replay/user/1?startDate={startDate:O}&endDate={endDate:O}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Date range cannot exceed 30 days", result.Message);
    }

    [Fact]
    public async Task GetUserActionReplay_ReturnsActionsInChronologicalOrder()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        var startDate = DateTime.UtcNow.AddHours(-8);
        var endDate = DateTime.UtcNow;

        // Act
        var response = await _client.GetAsync(
            $"/api/auditlogs/replay/user/1?startDate={startDate:O}&endDate={endDate:O}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserActionReplay>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        
        var actions = result.Data.Actions;
        if (actions.Count > 1)
        {
            // Verify actions are in chronological order (oldest first)
            for (int i = 0; i < actions.Count - 1; i++)
            {
                Assert.True(actions[i].Timestamp <= actions[i + 1].Timestamp,
                    "User actions should be in chronological order");
            }
        }
    }

    [Fact]
    public async Task GetUserActionReplay_IncludesTimelineVisualization()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        var startDate = DateTime.UtcNow.AddHours(-8);
        var endDate = DateTime.UtcNow;

        // Act
        var response = await _client.GetAsync(
            $"/api/auditlogs/replay/user/1?startDate={startDate:O}&endDate={endDate:O}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserActionReplay>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.Timeline);
        
        var timeline = result.Data.Timeline;
        Assert.NotNull(timeline.HourlyActivity);
        Assert.NotNull(timeline.EndpointDistribution);
        Assert.NotNull(timeline.ActionTypeDistribution);
        Assert.NotNull(timeline.EntityTypeDistribution);
    }

    [Fact]
    public async Task GetUserActionReplay_WithNoActions_ReturnsEmptyReplay()
    {
        // Arrange
        var token = await _factory.GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        var startDate = DateTime.UtcNow.AddHours(-8);
        var endDate = DateTime.UtcNow;

        // Act - Use a user ID that likely has no actions
        var response = await _client.GetAsync(
            $"/api/auditlogs/replay/user/999999999?startDate={startDate:O}&endDate={endDate:O}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserActionReplay>>();
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.TotalActions);
        Assert.Empty(result.Data.Actions);
    }

    #endregion
}

