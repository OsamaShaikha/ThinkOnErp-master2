using System.Security.Claims;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Moq;
using ThinkOnErp.API.Authorization;
using ThinkOnErp.API.Controllers.Hr;
using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;
using Xunit;

namespace ThinkOnErp.HR.Api.Tests;

public sealed class HrAccessFilterTests : IDisposable
{
    private readonly OracleDbContext db = new(new DbContextOptionsBuilder<OracleDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    public HrAccessFilterTests()
    {
        db.AddRange(new SysUser { Id = 12, CompanyId = 1, IsActive = true },
            new SysBranch { Id = 7, CompanyId = 1, IsActive = true }, new SysBranch { Id = 8, CompanyId = 1, IsActive = true },
            new SysUserBranch { Id = 1, UserId = 12, BranchId = 7 }, new SysUserBranch { Id = 2, UserId = 12, BranchId = 8 },
            new SysSystem { Id = 50, SystemCode = "hr", IsActive = true },
            new SysScreen { Id = 51, SystemId = 50, ScreenCode = "hr-attendance", IsActive = true },
            new SysScreen { Id = 52, SystemId = 50, ScreenCode = "hr-loans", IsActive = true },
            new SysScreen { Id = 53, SystemId = 50, ScreenCode = "hr-payroll", IsActive = true },
            new SysFeature { Id = 61, FeatureCode = "create", IsActive = true },
            new SysFeature { Id = 62, FeatureCode = "view", IsActive = true },
            new SysFeature { Id = 63, FeatureCode = "approve", IsActive = true },
            new SysScreenFeature { ScreenId = 51, FeatureId = 61 }, new SysScreenFeature { ScreenId = 52, FeatureId = 62 },
            new SysScreenFeature { ScreenId = 53, FeatureId = 63 },
            new SysBranchSystem { Id = 70, BranchId = 7, SystemId = 50 }, new SysBranchSystem { Id = 71, BranchId = 8, SystemId = 50 },
            new Employee { EmployeeCode = "SELF", BranchId = 7, UserId = 12 }, new Employee { EmployeeCode = "OTHER", BranchId = 8 });
        db.SaveChanges(); db.ChangeTracker.Clear();
    }

    private async Task<(IActionResult? Result, bool Invoked)> Execute(Type controller, string method, Dictionary<string, object?>? arguments = null,
        bool tenant = true, bool super = false, string? userId = "12")
    {
        var action = new ControllerActionDescriptor { ControllerTypeInfo = controller.GetTypeInfo(), MethodInfo = controller.GetMethod(method)!,
            ControllerName = controller.Name.Replace("Controller", ""), ActionName = method };
        var claims = new List<Claim> { new("isAdmin", "true") }; // Claim alone must not grant administrator access.
        if (userId != null) claims.Add(new Claim("userId", userId));
        if (super) claims.Add(new Claim("isSuperAdmin", "true"));
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) };
        if (tenant) http.Items[TenantRequestContext.HttpContextItemKey] = new TenantRequestContext(1, "TENANT", "DEV_TEMPLATE");
        var context = new ActionExecutingContext(new ActionContext(http, new(), action, new ModelStateDictionary()), new List<IFilterMetadata>(),
            arguments ?? new(), new object());
        var invoked = false;
        await new HrAccessFilter(db).OnActionExecutionAsync(context, () =>
        {
            invoked = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        });
        return (context.Result, invoked);
    }

    [Fact] public async Task MissingTenantIsDenied() => Assert.False((await Execute(typeof(PayrollController), "ApprovePayroll", tenant: false, super: true)).Invoked);
    [Fact] public async Task MissingIdentityIsDenied() => Assert.IsType<UnauthorizedResult>((await Execute(typeof(PayrollController), "ApprovePayroll", userId: null)).Result);
    [Fact] public async Task ForgedAdminClaimAndNoGrantAreDenied() => Assert.False((await Execute(typeof(PayrollController), "ApprovePayroll")).Invoked);
    [Fact] public async Task EmployeeCannotApprovePayroll() => Assert.IsType<ForbidResult>((await Execute(typeof(PayrollController), "ApprovePayroll")).Result);
    [Fact] public async Task EmployeeCanPunchOnlyForSelf()
    {
        // The DTO constructor is irrelevant to authorization; employee/branch binding is what is tested.
        var result = await Execute(typeof(AttendanceController), "CheckIn", new() { ["employeeCode"] = "SELF" });
        Assert.True(result.Invoked); Assert.Equal("SELF", db.HrScopeEmployeeCode);
    }
    [Fact] public async Task EmployeeCannotPunchForOtherEmployee() => Assert.False((await Execute(typeof(AttendanceController), "CheckIn", new() { ["employeeCode"] = "OTHER" })).Invoked);
    [Fact] public async Task SelfListsFilterOutOtherEmployees()
    {
        db.AddRange(new EmployeeLoan { Id = 1, EmployeeCode = "SELF" }, new EmployeeLoan { Id = 2, EmployeeCode = "OTHER" }); await db.SaveChangesAsync();
        Assert.True((await Execute(typeof(LoansController), "GetLoans")).Invoked);
        Assert.Equal(new long[] { 1 }, await db.EmployeeLoans.Select(l => l.Id).ToArrayAsync());
    }
    [Fact] public async Task ExplicitGrantIsLimitedToGrantedBranch()
    {
        db.Add(new SysUserScreenPermission { Id = 80, BranchId = 7, UserId = 12, ScreenId = 52, FeatureId = 62, IsGranted = true }); await db.SaveChangesAsync();
        Assert.True((await Execute(typeof(LoansController), "GetLoans")).Invoked);
        Assert.Equal(new long[] { 7 }, db.HrScopeBranchIds);
        Assert.Equal(new[] { "SELF" }, await db.Employees.Select(e => e.EmployeeCode).ToArrayAsync());
    }
    [Fact] public async Task ExplicitDenyCannotFallBackToSelfService()
    {
        db.Add(new SysUserScreenPermission { Id = 80, BranchId = 7, UserId = 12, ScreenId = 51, FeatureId = 61, IsGranted = false }); await db.SaveChangesAsync();
        Assert.False((await Execute(typeof(AttendanceController), "CheckIn", new() { ["employeeCode"] = "SELF" })).Invoked);
    }
    [Fact] public async Task DisabledUserIsDenied()
    {
        (await db.SysUsers.SingleAsync()).IsActive = false; await db.SaveChangesAsync();
        Assert.False((await Execute(typeof(LoansController), "GetLoans")).Invoked);
    }
    [Fact] public async Task UnregisteredOperationCannotFallBackToSelfService()
    {
        db.Remove(await db.Set<SysScreenFeature>().SingleAsync(s => s.ScreenId == 51 && s.FeatureId == 61)); await db.SaveChangesAsync();
        Assert.False((await Execute(typeof(AttendanceController), "CheckIn", new() { ["employeeCode"] = "SELF" })).Invoked);
    }
    [Fact] public async Task DisabledHrSystemCannotFallBackToSelfService()
    {
        (await db.Set<SysSystem>().SingleAsync()).IsActive = false; await db.SaveChangesAsync();
        Assert.False((await Execute(typeof(AttendanceController), "CheckIn", new() { ["employeeCode"] = "SELF" })).Invoked);
    }
    [Fact] public async Task ImportedEmployeeCannotBypassBranchWriteScope()
    {
        db.HrScopeEnabled = true; db.HrScopeCompanyId = 1; db.HrScopeBranchIds.Add(7);
        db.Add(new Employee { EmployeeCode = "IMPORTED_OUTSIDE", BranchId = 8 });
        var repository = new ThinkOnErp.Infrastructure.Repositories.Hr.EmployeeRepository(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.SaveChangesAsync());
        db.ChangeTracker.Clear(); db.HrScopeEnabled = false;
        Assert.False(await db.Employees.AnyAsync(e => e.EmployeeCode == "IMPORTED_OUTSIDE"));
    }
    [Fact] public async Task TerminationDisablesLinkedAccountAndRevokesRefreshToken()
    {
        var account = await db.SysUsers.SingleAsync(); account.RefreshToken = "old-refresh";
        account.RefreshTokenExpiry = DateTime.UtcNow.AddDays(1); await db.SaveChangesAsync();
        var employee = await db.Employees.SingleAsync(e => e.EmployeeCode == "SELF"); employee.EmploymentStatus = "TERMINATED";
        await new ThinkOnErp.Infrastructure.Repositories.Hr.EmployeeRepository(db).SaveChangesAsync();
        Assert.False(account.IsActive); Assert.Null(account.RefreshToken); Assert.Null(account.RefreshTokenExpiry); Assert.NotNull(account.ForceLogoutDate);
    }
    [Fact] public async Task ForgedAdminClaimCannotConfigurePayrollAccounts()
    {
        Assert.False((await Execute(typeof(PayrollPostingConfigurationsController), "Save", new() { ["branchId"] = 7L })).Invoked);
    }
    [Fact] public async Task PermissionCheckRequiresExplicitHrGrantAndHonorsRoleDeny()
    {
        var user = await db.SysUsers.SingleAsync(); user.BranchId = 7; user.RoleId = 90;
        db.Add(new SysRole { Id = 90, IsActive = true }); await db.SaveChangesAsync();
        var users = new Moq.Mock<ThinkOnErp.Domain.Interfaces.IUserRepository>();
        users.Setup(r => r.GetByIdAsync(12)).ReturnsAsync(user);
        var service = new ThinkOnErp.Infrastructure.Services.PermissionService(db, users.Object,
            Moq.Mock.Of<ThinkOnErp.Domain.Interfaces.IScreenRepository>(), Moq.Mock.Of<ThinkOnErp.Domain.Interfaces.ISysFeatureRepository>(),
            Moq.Mock.Of<ThinkOnErp.Domain.Interfaces.IBranchSystemRepository>(), Moq.Mock.Of<ThinkOnErp.Domain.Interfaces.IBranchScreenRepository>(),
            Moq.Mock.Of<ThinkOnErp.Domain.Interfaces.IBranchFeatureRepository>(), Moq.Mock.Of<ThinkOnErp.Domain.Interfaces.IRoleScreenPermissionRepository>(),
            Moq.Mock.Of<ThinkOnErp.Domain.Interfaces.IUserScreenPermissionRepository>());
        Assert.False(await service.CanAccessAsync(12, 53, 63));
        db.Add(new SysUserScreenPermission { Id = 80, BranchId = 7, UserId = 12, ScreenId = 53, FeatureId = 63, IsGranted = true }); await db.SaveChangesAsync();
        Assert.True(await service.CanAccessAsync(12, 53, 63));
        db.Add(new SysRoleScreenPermission { Id = 81, BranchId = 7, RoleId = 90, ScreenId = 53, FeatureId = 63, IsGranted = false }); await db.SaveChangesAsync();
        Assert.False(await service.CanAccessAsync(12, 53, 63));
    }
    [Fact] public async Task RevokedSystemCannotFallBackToSelfService()
    {
        (await db.Set<SysBranchSystem>().SingleAsync(b => b.BranchId == 7)).RevokedDate = DateTime.UtcNow; await db.SaveChangesAsync();
        Assert.False((await Execute(typeof(AttendanceController), "CheckIn", new() { ["employeeCode"] = "SELF" })).Invoked);
    }
    [Fact] public async Task AdministratorStillCannotSelectOtherCompany()
    {
        (await db.SysUsers.SingleAsync()).IsAdmin = true; await db.SaveChangesAsync();
        Assert.False((await Execute(typeof(PayrollController), "GetPeriods", new() { ["companyId"] = 2L })).Invoked);
    }
    [Fact] public async Task SelectedSuperAdminCanOperate() => Assert.True((await Execute(typeof(PayrollController), "ApprovePayroll", super: true)).Invoked);
    [Fact] public void DenyOverridesGrantAndMissingGrantDenies()
    {
        Assert.False(HrAccessFilter.ExplicitGrant(Array.Empty<bool>(), Array.Empty<bool>()));
        Assert.False(HrAccessFilter.ExplicitGrant(new[] { true, false }, new[] { true }));
        Assert.False(HrAccessFilter.ExplicitGrant(new[] { true }, new[] { false }));
        Assert.True(HrAccessFilter.ExplicitGrant(Array.Empty<bool>(), new[] { true }));
    }
    [Fact] public void EveryHrEndpointDeclaresOperationPermission()
    {
        foreach (var type in typeof(EmployeesController).Assembly.GetTypes().Where(t => t.Namespace == "ThinkOnErp.API.Controllers.Hr"))
            foreach (var method in type.GetMethods().Where(m => m.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Any()))
                Assert.True(method.GetCustomAttributes(true).OfType<HrPermissionAttribute>().Any(), type.Name + "." + method.Name);
    }
    public void Dispose() => db.Dispose();
}
