using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using ThinkOnErp.Domain.Interfaces;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Authorization;

namespace ThinkOnErp.SuperAdmin.Tests.Authorization;

public class MultiTenantAuthorizationHandlerTests
{
    private readonly Mock<ISecurityMonitor> _securityMonitor = new();
    private readonly Mock<ILogger<MultiTenantAuthorizationHandler>> _logger = new();
    private readonly DefaultHttpContext _httpContext = new();
    private readonly MultiTenantAuthorizationHandler _handler;

    public MultiTenantAuthorizationHandlerTests()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = _httpContext
        };

        _handler = new MultiTenantAuthorizationHandler(
            _securityMonitor.Object,
            _logger.Object,
            httpContextAccessor);
    }

    [Fact]
    public async Task SuperAdmin_SelectedTargetCompany_SucceedsWithoutSecurityMonitorCalls()
    {
        SetTenantContext(companyId: 41);
        var authorizationContext = CreateAuthorizationContext(
            CreatePrincipal(
                ("userId", "1"),
                ("isAdmin", "true"),
                ("isSuperAdmin", "true")),
            companyId: 41,
            branchId: 7);

        await ((IAuthorizationHandler)_handler).HandleAsync(authorizationContext);

        Assert.True(authorizationContext.HasSucceeded);
        Assert.False(authorizationContext.HasFailed);
        VerifyNoSecurityMonitorCalls();
    }

    [Fact]
    public async Task SuperAdmin_WithoutSelectedTenantContext_Fails()
    {
        var authorizationContext = CreateAuthorizationContext(
            CreatePrincipal(
                ("userId", "1"),
                ("isAdmin", "true"),
                ("isSuperAdmin", "true")),
            companyId: 41);

        await ((IAuthorizationHandler)_handler).HandleAsync(authorizationContext);

        Assert.False(authorizationContext.HasSucceeded);
        Assert.True(authorizationContext.HasFailed);
        VerifyNoSecurityMonitorCalls();
    }

    [Fact]
    public async Task SuperAdmin_SelectedDifferentCompany_Fails()
    {
        SetTenantContext(companyId: 41);
        var authorizationContext = CreateAuthorizationContext(
            CreatePrincipal(
                ("userId", "1"),
                ("isAdmin", "true"),
                ("isSuperAdmin", "true")),
            companyId: 42);

        await ((IAuthorizationHandler)_handler).HandleAsync(authorizationContext);

        Assert.False(authorizationContext.HasSucceeded);
        Assert.True(authorizationContext.HasFailed);
        VerifyNoSecurityMonitorCalls();
    }

    [Fact]
    public async Task RegularUser_SameCompany_Succeeds()
    {
        SetTenantContext(companyId: 41);
        var authorizationContext = CreateAuthorizationContext(
            CreatePrincipal(
                ("userId", "12"),
                ("companyId", "41"),
                ("branchId", "7"),
                ("isAdmin", "false"),
                ("isSuperAdmin", "false")),
            companyId: 41,
            branchId: 7);

        await ((IAuthorizationHandler)_handler).HandleAsync(authorizationContext);

        Assert.True(authorizationContext.HasSucceeded);
        Assert.False(authorizationContext.HasFailed);
        VerifyNoSecurityMonitorCalls();
    }

    [Fact]
    public async Task RegularUser_DifferentCompany_FailsAndTriggersSecurityMonitor()
    {
        SetTenantContext(companyId: 41);
        _securityMonitor
            .Setup(monitor => monitor.DetectUnauthorizedAccessAsync(12, 42, 9))
            .ReturnsAsync((SecurityThreat?)null);

        var authorizationContext = CreateAuthorizationContext(
            CreatePrincipal(
                ("userId", "12"),
                ("companyId", "41"),
                ("branchId", "7"),
                ("isAdmin", "false"),
                ("isSuperAdmin", "false")),
            companyId: 42,
            branchId: 9);

        await ((IAuthorizationHandler)_handler).HandleAsync(authorizationContext);

        Assert.False(authorizationContext.HasSucceeded);
        Assert.True(authorizationContext.HasFailed);
        _securityMonitor.Verify(
            monitor => monitor.DetectUnauthorizedAccessAsync(12, 42, 9),
            Times.Once);
        _securityMonitor.Verify(
            monitor => monitor.TriggerSecurityAlertAsync(It.IsAny<SecurityThreat>()),
            Times.Never);
    }

    private void SetTenantContext(long companyId)
    {
        _httpContext.Items[TenantRequestContext.HttpContextItemKey] =
            new TenantRequestContext(companyId, $"COMPANY_{companyId}", $"SCHEMA_{companyId}");
    }

    private static AuthorizationHandlerContext CreateAuthorizationContext(
        ClaimsPrincipal principal,
        long companyId,
        long? branchId = null)
    {
        var requirement = new MultiTenantAccessRequirement();
        var resource = new MultiTenantResource(companyId, branchId);
        return new AuthorizationHandlerContext(new[] { requirement }, principal, resource);
    }

    private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(claim => new Claim(claim.Type, claim.Value)),
            "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private void VerifyNoSecurityMonitorCalls()
    {
        _securityMonitor.Verify(
            monitor => monitor.DetectUnauthorizedAccessAsync(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long>()),
            Times.Never);
        _securityMonitor.Verify(
            monitor => monitor.TriggerSecurityAlertAsync(It.IsAny<SecurityThreat>()),
            Times.Never);
    }
}
