using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThinkOnErp.API.Controllers;
using Xunit;

namespace ThinkOnErp.API.Tests.Controllers;

/// <summary>
/// Unit tests to verify authorization configuration on AuditLogsController.
/// These tests verify that the controller and its endpoints are properly protected with AdminOnly authorization.
/// </summary>
public class AuditLogsControllerAuthorizationTests
{
    /// <summary>
    /// Verifies that the AuditLogsController class has the AdminOnly authorization policy applied.
    /// This ensures all endpoints in the controller require admin privileges.
    /// </summary>
    [Fact]
    public void AuditLogsController_HasAdminOnlyAuthorizationAttribute()
    {
        // Arrange
        var controllerType = typeof(AuditLogsController);

        // Act
        var authorizeAttributes = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true);

        // Assert
        Assert.NotEmpty(authorizeAttributes);
        var authorizeAttribute = Assert.Single(authorizeAttributes.Cast<AuthorizeAttribute>());
        Assert.Equal("AdminOnly", authorizeAttribute.Policy);
    }

    /// <summary>
    /// Verifies that the status update endpoint (PUT /api/auditlogs/legacy/{id}/status) 
    /// inherits the AdminOnly authorization from the controller level.
    /// This ensures only administrators can update audit log status (resolve errors).
    /// </summary>
    [Fact]
    public void UpdateAuditLogStatus_InheritsAdminOnlyAuthorization()
    {
        // Arrange
        var controllerType = typeof(AuditLogsController);
        var method = controllerType.GetMethod("UpdateAuditLogStatus");

        // Assert method exists
        Assert.NotNull(method);

        // Verify it's an HTTP PUT endpoint
        var httpPutAttribute = method!.GetCustomAttributes(typeof(HttpPutAttribute), true).FirstOrDefault();
        Assert.NotNull(httpPutAttribute);
        Assert.Equal("legacy/{id}/status", ((HttpPutAttribute)httpPutAttribute!).Template);

        // Verify the controller has AdminOnly authorization (which applies to all methods)
        var controllerAuthorizeAttributes = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        Assert.NotEmpty(controllerAuthorizeAttributes);
        var authorizeAttribute = Assert.Single(controllerAuthorizeAttributes.Cast<AuthorizeAttribute>());
        Assert.Equal("AdminOnly", authorizeAttribute.Policy);

        // Verify the method doesn't have AllowAnonymous (which would override controller-level authorization)
        var allowAnonymousAttribute = method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).FirstOrDefault();
        Assert.Null(allowAnonymousAttribute);
    }

    /// <summary>
    /// Verifies that all endpoints in the AuditLogsController inherit the AdminOnly authorization.
    /// This ensures comprehensive protection of all audit log operations.
    /// </summary>
    [Theory]
    [InlineData("GetLegacyAuditLogs")]
    [InlineData("GetDashboardCounters")]
    [InlineData("UpdateAuditLogStatus")]
    [InlineData("GetAuditLogStatus")]
    [InlineData("TransformToLegacyFormat")]
    public void AllAuditLogsEndpoints_InheritAdminOnlyAuthorization(string methodName)
    {
        // Arrange
        var controllerType = typeof(AuditLogsController);
        var method = controllerType.GetMethod(methodName);

        // Assert method exists
        Assert.NotNull(method);

        // Verify the controller has AdminOnly authorization
        var controllerAuthorizeAttributes = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        Assert.NotEmpty(controllerAuthorizeAttributes);
        var authorizeAttribute = Assert.Single(controllerAuthorizeAttributes.Cast<AuthorizeAttribute>());
        Assert.Equal("AdminOnly", authorizeAttribute.Policy);

        // Verify the method doesn't have AllowAnonymous
        var allowAnonymousAttribute = method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).FirstOrDefault();
        Assert.Null(allowAnonymousAttribute);
    }

    /// <summary>
    /// Verifies that the controller is decorated with the ApiController attribute.
    /// This ensures proper API behavior including automatic model validation.
    /// </summary>
    [Fact]
    public void AuditLogsController_HasApiControllerAttribute()
    {
        // Arrange
        var controllerType = typeof(AuditLogsController);

        // Act
        var apiControllerAttribute = controllerType.GetCustomAttributes(typeof(ApiControllerAttribute), true).FirstOrDefault();

        // Assert
        Assert.NotNull(apiControllerAttribute);
    }

    /// <summary>
    /// Verifies that the controller has the correct route configuration.
    /// </summary>
    [Fact]
    public void AuditLogsController_HasCorrectRouteAttribute()
    {
        // Arrange
        var controllerType = typeof(AuditLogsController);

        // Act
        var routeAttribute = controllerType.GetCustomAttributes(typeof(RouteAttribute), true).FirstOrDefault();

        // Assert
        Assert.NotNull(routeAttribute);
        Assert.Equal("api/auditlogs", ((RouteAttribute)routeAttribute!).Template);
    }
}
