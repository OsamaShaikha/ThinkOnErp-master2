using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using ThinkOnErp.Infrastructure.Authorization;
using Xunit;

namespace ThinkOnErp.Infrastructure.Tests.Authorization;

/// <summary>
/// Unit tests for AuditDataAuthorizationHandler.
/// Tests role-based access control (RBAC) for audit data access.
/// Validates that SuperAdmins, CompanyAdmins, and regular users have appropriate access levels.
/// </summary>
public class AuditDataAuthorizationHandlerTests
{
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<ILogger<AuditDataAuthorizationHandler>> _mockLogger;
    private readonly AuditDataAuthorizationHandler _handler;

    public AuditDataAuthorizationHandlerTests()
    {
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<AuditDataAuthorizationHandler>>();
        _handler = new AuditDataAuthorizationHandler(_mockHttpContextAccessor.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task HandleRequirementAsync_SuperAdmin_GrantsAccess()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim("isAdmin", "true"),
            new Claim("CompanyId", "1")
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var requirement = new AuditDataAccessRequirement(allowSelfAccess: true);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await ((IAuthorizationHandler)_handler).HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task HandleRequirementAsync_CompanyAdmin_GrantsAccess()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "2"),
            new Claim("isAdmin", "false"),
            new Claim("role", "COMPANY_ADMIN"),
            new Claim("CompanyId", "1")
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var requirement = new AuditDataAccessRequirement(allowSelfAccess: true);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await ((IAuthorizationHandler)_handler).HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task HandleRequirementAsync_CompanyAdmin_MissingCompanyId_DeniesAccess()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "2"),
            new Claim("isAdmin", "false"),
            new Claim("role", "COMPANY_ADMIN")
            // Missing CompanyId claim
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var requirement = new AuditDataAccessRequirement(allowSelfAccess: true);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await ((IAuthorizationHandler)_handler).HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task HandleRequirementAsync_RegularUser_AllowSelfAccess_GrantsAccess()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "3"),
            new Claim("isAdmin", "false"),
            new Claim("role", "USER"),
            new Claim("CompanyId", "1"),
            new Claim("BranchId", "1")
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var requirement = new AuditDataAccessRequirement(allowSelfAccess: true);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await ((IAuthorizationHandler)_handler).HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task HandleRequirementAsync_RegularUser_DisallowSelfAccess_DeniesAccess()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "3"),
            new Claim("isAdmin", "false"),
            new Claim("role", "USER"),
            new Claim("CompanyId", "1"),
            new Claim("BranchId", "1")
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var requirement = new AuditDataAccessRequirement(allowSelfAccess: false);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await ((IAuthorizationHandler)_handler).HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task HandleRequirementAsync_MissingUserId_DeniesAccess()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("isAdmin", "false"),
            new Claim("role", "USER"),
            new Claim("CompanyId", "1")
            // Missing NameIdentifier (UserId) claim
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var requirement = new AuditDataAccessRequirement(allowSelfAccess: true);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await ((IAuthorizationHandler)_handler).HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task HandleRequirementAsync_InvalidUserId_DeniesAccess()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "invalid"),
            new Claim("isAdmin", "false"),
            new Claim("role", "USER"),
            new Claim("CompanyId", "1")
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var requirement = new AuditDataAccessRequirement(allowSelfAccess: true);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await ((IAuthorizationHandler)_handler).HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task HandleRequirementAsync_SuperAdmin_IgnoresAllowSelfAccessSetting()
    {
        // Arrange - SuperAdmin should have access regardless of AllowSelfAccess setting
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim("isAdmin", "true"),
            new Claim("CompanyId", "1")
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var requirement = new AuditDataAccessRequirement(allowSelfAccess: false);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await ((IAuthorizationHandler)_handler).HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task HandleRequirementAsync_CompanyAdmin_IgnoresAllowSelfAccessSetting()
    {
        // Arrange - CompanyAdmin should have access regardless of AllowSelfAccess setting
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "2"),
            new Claim("isAdmin", "false"),
            new Claim("role", "COMPANY_ADMIN"),
            new Claim("CompanyId", "1")
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var requirement = new AuditDataAccessRequirement(allowSelfAccess: false);
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        // Act
        await ((IAuthorizationHandler)_handler).HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public void AuditDataAccessRequirement_DefaultConstructor_AllowsSelfAccess()
    {
        // Arrange & Act
        var requirement = new AuditDataAccessRequirement();

        // Assert
        Assert.True(requirement.AllowSelfAccess);
    }

    [Fact]
    public void AuditDataAccessRequirement_ExplicitConstructor_SetsAllowSelfAccess()
    {
        // Arrange & Act
        var requirementTrue = new AuditDataAccessRequirement(allowSelfAccess: true);
        var requirementFalse = new AuditDataAccessRequirement(allowSelfAccess: false);

        // Assert
        Assert.True(requirementTrue.AllowSelfAccess);
        Assert.False(requirementFalse.AllowSelfAccess);
    }

    [Fact]
    public void RequireAuditDataAccessAttribute_SetsCorrectPolicy()
    {
        // Arrange & Act
        var attribute = new RequireAuditDataAccessAttribute();

        // Assert
        Assert.Equal("AuditDataAccess", attribute.Policy);
    }

    [Fact]
    public void RequireAdminAuditDataAccessAttribute_SetsCorrectPolicy()
    {
        // Arrange & Act
        var attribute = new RequireAdminAuditDataAccessAttribute();

        // Assert
        Assert.Equal("AdminOnlyAuditDataAccess", attribute.Policy);
    }
}
