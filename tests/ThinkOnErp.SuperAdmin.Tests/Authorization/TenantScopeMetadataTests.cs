using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Controllers;

namespace ThinkOnErp.SuperAdmin.Tests.Authorization;

public class TenantScopeMetadataTests
{
    public static IEnumerable<object[]> RequiredTenantControllers()
    {
        yield return new object[] { typeof(BranchAccessController) };
        yield return new object[] { typeof(BranchController) };
        yield return new object[] { typeof(CompanyPermissionsController) };
        yield return new object[] { typeof(ComplianceController) };
        yield return new object[] { typeof(ConfigurationController) };
        yield return new object[] { typeof(FiscalYearController) };
        yield return new object[] { typeof(PermissionsController) };
        yield return new object[] { typeof(RolesController) };
        yield return new object[] { typeof(SavedSearchesController) };
        yield return new object[] { typeof(TicketsController) };
        yield return new object[] { typeof(TicketTypesController) };
        yield return new object[] { typeof(UsersController) };
    }

    public static IEnumerable<object[]> PlatformControllers()
    {
        yield return new object[] { typeof(AlertsController) };
        yield return new object[] { typeof(AuditLogsController) };
        yield return new object[] { typeof(AuditTrailController) };
        yield return new object[] { typeof(CompanyController) };
        yield return new object[] { typeof(FeaturesController) };
        yield return new object[] { typeof(KeyManagementController) };
        yield return new object[] { typeof(ModulesController) };
        yield return new object[] { typeof(MonitoringController) };
        yield return new object[] { typeof(ScreensController) };
        yield return new object[] { typeof(SuperAdminController) };
    }

    [Theory]
    [MemberData(nameof(RequiredTenantControllers))]
    public void CompanyController_RequiresExplicitTenantSelectionForSuperAdmin(
        Type controllerType)
    {
        var attribute = controllerType.GetCustomAttribute<TenantScopedAttribute>(
            inherit: true);

        Assert.NotNull(attribute);
        Assert.True(attribute.SelectionRequired);
    }

    [Fact]
    public void DocumentsController_AllowsCentralOrExplicitTenantContext()
    {
        var attribute = typeof(DocumentsController)
            .GetCustomAttribute<TenantScopedAttribute>(inherit: true);

        Assert.NotNull(attribute);
        Assert.False(attribute.SelectionRequired);
    }

    [Theory]
    [MemberData(nameof(PlatformControllers))]
    public void PlatformController_RequiresSuperAdminPolicy(Type controllerType)
    {
        var authorizeAttributes = controllerType
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true);

        Assert.Contains(
            authorizeAttributes,
            attribute => attribute.Policy == "SuperAdminOnly");
    }
}
