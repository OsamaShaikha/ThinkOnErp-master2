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
            new Employee { EmployeeCode = "SELF", BranchId = 7, UserId = 12, NameLocal = "Self", NameEn = "Self", NationalId = "SELF_ID", Nationality = "X", DateOfBirth = new(1990, 1, 1), HireDate = new(2020, 1, 1) }, new Employee { EmployeeCode = "OTHER", BranchId = 8, NameLocal = "Other", NameEn = "Other", NationalId = "OTHER_ID", Nationality = "X", DateOfBirth = new(1990, 1, 1), HireDate = new(2020, 1, 1) });
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
        await new HrAccessFilter(db,new ThinkOnErp.Application.Services.Hr.EmployeeFieldAccess()).OnActionExecutionAsync(context, () =>
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
        var employee = await db.Employees.SingleAsync(e => e.EmployeeCode == "SELF"); employee.EmploymentStatus = "TERMINATED"; employee.IsActive = false; employee.TerminationDate = DateTime.UtcNow.Date;
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
    [Fact] public void ManualLeaveBalanceEndpointsRequireEditPermissionAndBindReversalToEmployeeMovement()
    {
        var grant = typeof(LeavesController).GetMethod("GrantOrAdjustBalance")!;
        var reverse = typeof(LeavesController).GetMethod("ReverseManualBalanceMovement")!;
        foreach (var action in new[] { grant, reverse })
        {
            var permission = Assert.Single(action.GetCustomAttributes(true).OfType<HrPermissionAttribute>());
            Assert.Equal("hr-leaves", permission.Screen);
            Assert.Equal("edit", permission.Feature);
            Assert.False(permission.SelfService);
        }
        Assert.Equal("balances/{employeeCode}/movements/{movementId:long}/reverse",
            Assert.Single(reverse.GetCustomAttributes(true).OfType<HttpPostAttribute>()).Template);
    }

    [Fact]
    public async Task FinancialTransitionPermissionMatrixRequiresExactGrantAndKeepsItsBranchScope()
    {
        var actions = new (Type Controller, string Method, string Screen, string Feature)[]
        {
            (typeof(PayrollController), "ApprovePayroll", "hr-payroll", "approve"),
            (typeof(PayrollController), "CorrectPayroll", "hr-payroll", "reverse"),
            (typeof(PayrollController), "CancelPayroll", "hr-payroll", "reverse"),
            (typeof(PayrollController), "ClosePeriod", "hr-payroll", "edit"),
            (typeof(PayrollController), "ReopenPeriod", "hr-payroll", "reverse"),
            (typeof(PayrollController), "PostToGl", "hr-payroll", "post"),
            (typeof(PayrollController), "ReverseGl", "hr-payroll", "reverse"),
            (typeof(PayrollController), "ExportBankFile", "hr-payroll", "export"),
            (typeof(PayrollController), "PreviewBankExport", "hr-payroll", "export"),
            (typeof(PayrollAdjustmentsController), "CreateOvertimeAdjustment", "hr-payroll-adjustments", "create"),
            (typeof(PayrollAdjustmentsController), "ApproveAdjustment", "hr-payroll-adjustments", "approve"),
            (typeof(PayrollAdjustmentsController), "RejectAdjustment", "hr-payroll-adjustments", "reject"),
            (typeof(PayrollAdjustmentsController), "PostAdjustment", "hr-payroll-adjustments", "post"),
            (typeof(PayrollAdjustmentsController), "UnpostAdjustment", "hr-payroll-adjustments", "reverse"),
            (typeof(LeavesController), "ProcessLeaveRequest", "hr-leaves", "approve"),
            (typeof(LeavesController), "PostYearEndClose", "hr-leaves", "post"),
            (typeof(LeavesController), "UnpostYearEndClose", "hr-leaves", "reverse"),
            (typeof(AccountingHandoffController), "Accept", "accounting-handoff", "review"),
            (typeof(AccountingHandoffController), "Return", "accounting-handoff", "review"),
            (typeof(AccountingHandoffController), "CreateJournalVoucher", "accounting-handoff", "post"),
            (typeof(AccountingHandoffController), "ReverseJournalVoucher", "accounting-handoff", "reverse"),
            (typeof(AccountingHandoffController), "PostingReceipt", "accounting-handoff", "post"),
            (typeof(AccountingHandoffController), "PreparePayment", "accounting-handoff", "pay"),
            (typeof(AccountingHandoffController), "FailPayment", "accounting-handoff", "pay"),
            (typeof(AccountingHandoffController), "ReversePayment", "accounting-handoff", "reverse"),
            (typeof(AccountingHandoffController), "ConfirmPayment", "accounting-handoff", "pay"),
            (typeof(AccountingHandoffController), "ConfirmLoanFunding", "accounting-handoff", "fund"),
            (typeof(AccountingHandoffController), "ReverseLoanFunding", "accounting-handoff", "reverse"),
            (typeof(EndOfServiceController), "ApproveSettlement", "hr-end-of-service", "approve"),
            (typeof(EndOfServiceController), "CancelSettlement", "hr-end-of-service", "reverse"),
            (typeof(EndOfServiceController), "SubmitPayoutRequest", "hr-end-of-service", "approve"),
            (typeof(ShortLeavesController), "ApproveRequest", "hr-leaves", "approve"),
            (typeof(ShortLeavesController), "CancelRequest", "hr-leaves", "reverse"),
            (typeof(ShortLeavesController), "SavePolicy", "hr-leaves", "edit"),
            (typeof(LoansController), "ApproveLoan", "hr-loans", "approve"),
            (typeof(LoansController), "CancelLoan", "hr-loans", "reverse"),
            (typeof(LoansController), "SkipInstallment", "hr-loans", "edit"),
            (typeof(LoansController), "SettleLoanEarly", "hr-loans", "edit"),
            (typeof(WorkCalendarsController), "CreateCalendar", "hr-work-calendars", "create"),
            (typeof(WorkCalendarsController), "CreateHoliday", "hr-work-calendars", "create")
        };

        long id = 1000;
        foreach (var action in actions)
        {
            var screen = await db.SysScreens.SingleOrDefaultAsync(s => s.ScreenCode == action.Screen);
            if (screen == null)
            {
                screen = new SysScreen { Id = id++, SystemId = 50, ScreenCode = action.Screen, IsActive = true };
                db.SysScreens.Add(screen);
            }
            var feature = await db.SysFeatures.SingleOrDefaultAsync(f => f.FeatureCode == action.Feature);
            if (feature == null)
            {
                feature = new SysFeature { Id = id++, FeatureCode = action.Feature, IsActive = true };
                db.SysFeatures.Add(feature);
            }
            if (!await db.Set<SysScreenFeature>().AnyAsync(sf => sf.ScreenId == screen.Id && sf.FeatureId == feature.Id))
                db.Add(new SysScreenFeature { ScreenId = screen.Id, FeatureId = feature.Id });
            var grant = new SysUserScreenPermission
            {
                Id = id++, BranchId = 7, UserId = 12, ScreenId = screen.Id, FeatureId = feature.Id, IsGranted = true
            };
            db.Add(grant);
            await db.SaveChangesAsync();

            ResetHrScope();
            var allowed = await Execute(action.Controller, action.Method);
            Assert.True(allowed.Invoked, $"Exact grant should allow {action.Controller.Name}.{action.Method} ({action.Screen}:{action.Feature}).");
            Assert.Equal(new long[] { 7 }, db.HrScopeBranchIds);

            grant.IsGranted = false;
            await db.SaveChangesAsync();
            ResetHrScope();
            var denied = await Execute(action.Controller, action.Method);
            Assert.False(denied.Invoked, $"Revoked grant should deny {action.Controller.Name}.{action.Method}.");

            db.Remove(grant);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task OvertimeDecisionBodyMustMatchExplicitlyGrantedBranch()
    {
        db.AddRange(
            new SysScreen { Id = 90, SystemId = 50, ScreenCode = "hr-payroll-adjustments", IsActive = true },
            new SysScreenFeature { ScreenId = 90, FeatureId = 63 },
            new SysUserScreenPermission { Id = 91, BranchId = 7, UserId = 12, ScreenId = 90, FeatureId = 63, IsGranted = true });
        await db.SaveChangesAsync();

        var allowed = await Execute(typeof(PayrollAdjustmentsController), "ApproveAdjustment",
            new() { ["id"] = 501L, ["decision"] = new PayrollAdjustmentDecisionDto("reviewed", 7) });
        Assert.True(allowed.Invoked);
        Assert.Equal(new long[] { 7 }, db.HrScopeBranchIds);

        ResetHrScope();
        var denied = await Execute(typeof(PayrollAdjustmentsController), "ApproveAdjustment",
            new() { ["id"] = 501L, ["decision"] = new PayrollAdjustmentDecisionDto("reviewed", 8) });
        Assert.False(denied.Invoked);
    }

    private void ResetHrScope()
    {
        db.HrScopeEnabled = false;
        db.HrScopeCompanyId = 0;
        db.HrScopeAllBranches = false;
        db.HrScopeBranchIds.Clear();
        db.HrScopeEmployeeCode = null;
    }

    [Fact]
    public void CashoutAndBalanceMigrationTransitionsRemainAdminOnly()
    {
        foreach (var method in new[] { "GetLeaveCashoutDraft", "PostLeaveCashout", "ReverseLeaveCashout",
                     "CreateBalanceMigrationDraft", "GetBalanceMigration", "PostBalanceMigration", "ReverseBalanceMigration" })
        {
            var action = typeof(LeavesController).GetMethod(method)!;
            Assert.Contains(action.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(),
                attribute => attribute.Policy == "AdminOnly");
        }
    }

    private async Task RegisterAttendanceReadAndConfigure()
    {
        db.AddRange(new SysFeature { Id = 65, FeatureCode = "configure", IsActive = true }, new SysFeature { Id = 66, FeatureCode = "reject", IsActive = true },
            new SysScreenFeature { ScreenId = 51, FeatureId = 62 }, new SysScreenFeature { ScreenId = 51, FeatureId = 65 },
            new SysScreen { Id = 54, SystemId = 50, ScreenCode = "hr-attendance-corrections", IsActive = true },
            new SysScreenFeature { ScreenId = 54, FeatureId = 63 }, new SysScreenFeature { ScreenId = 54, FeatureId = 66 });
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(typeof(RawAttendanceController), "Ingest")]
    [InlineData(typeof(RawAttendanceController), "Process")]
    [InlineData(typeof(RawAttendanceController), "Batch")]
    [InlineData(typeof(RawAttendanceController), "Ignore")]
    [InlineData(typeof(RawAttendanceController), "Pending")]
    [InlineData(typeof(AttendanceTimeSettingsController), "Initialize")]
    [InlineData(typeof(ShiftDaysController), "Generate")]
    public async Task EmployeeCannotUseAdministrativeAttendanceOperations(Type controller, string method)
    {
        await RegisterAttendanceReadAndConfigure();
        Assert.False((await Execute(controller, method)).Invoked);
    }

    [Fact] public async Task ManagerRelationshipDoesNotGrantConfigurationPermission()
    {
        await RegisterAttendanceReadAndConfigure();
        (await db.Employees.SingleAsync(e => e.EmployeeCode == "OTHER")).ManagerEmployeeCode = "SELF"; await db.SaveChangesAsync();
        Assert.False((await Execute(typeof(RawAttendanceController), "Process")).Invoked);
    }

    [Fact] public async Task SelfServiceRawQueryHidesOtherEmployeeEvenWhenTheirBranchIsAssigned()
    {
        await RegisterAttendanceReadAndConfigure();
        db.AddRange(new RawAttendance { Id = 100, EmployeeCode = "SELF" }, new RawAttendance { Id = 101, EmployeeCode = "OTHER" }); await db.SaveChangesAsync();
        Assert.True((await Execute(typeof(RawAttendanceController), "Get", new() { ["employeeCode"] = "SELF" })).Invoked);
        var repository = new ThinkOnErp.Infrastructure.Repositories.Hr.AttendanceCorrectionRepository(db);
        Assert.NotNull(await repository.GetRawEventAsync(100)); Assert.Null(await repository.GetRawEventAsync(101));
    }

    [Fact] public async Task ApproveGrantCannotAuthorizeCorrectionRejection()
    {
        await RegisterAttendanceReadAndConfigure();
        db.Add(new SysUserScreenPermission { Id = 80, BranchId = 7, UserId = 12, ScreenId = 54, FeatureId = 63, IsGranted = true }); await db.SaveChangesAsync();
        Assert.False((await Execute(typeof(AttendanceCorrectionsController), "ProcessCorrection", new() { ["dto"] = new ProcessAttendanceCorrectionDto(false, "Reviewed") })).Invoked);
    }

    [Fact] public async Task ExplicitRejectGrantAuthorizesOnlyItsBranch()
    {
        await RegisterAttendanceReadAndConfigure();
        db.Add(new SysUserScreenPermission { Id = 80, BranchId = 7, UserId = 12, ScreenId = 54, FeatureId = 66, IsGranted = true }); await db.SaveChangesAsync();
        Assert.True((await Execute(typeof(AttendanceCorrectionsController), "ProcessCorrection", new() { ["dto"] = new ProcessAttendanceCorrectionDto(false, "Reviewed") })).Invoked);
        Assert.Equal(new long[] { 7 }, db.HrScopeBranchIds);
    }

    [Fact] public async Task AdministratorCannotReadForeignCompanyEmployeesOrEvidence()
    {
        db.AddRange(new SysBranch { Id = 9, CompanyId = 2, IsActive = true }, new Employee { EmployeeCode = "FOREIGN", BranchId = 9 },
            new RawAttendance { Id = 100, EmployeeCode = "FOREIGN" }); await db.SaveChangesAsync();
        (await db.SysUsers.SingleAsync()).IsAdmin = true; await db.SaveChangesAsync();
        Assert.True((await Execute(typeof(EmployeesController), "GetEmployees")).Invoked);
        Assert.False(await db.Employees.AnyAsync(e => e.EmployeeCode == "FOREIGN"));
        Assert.False(await db.RawAttendances.AnyAsync(r => r.Id == 100));
    }

    [Fact] public async Task AdministratorCannotReadForeignCompanyPayrollOrPostingAccounts()
    {
        db.AddRange(new SysBranch { Id = 9, CompanyId = 2, IsActive = true }, new PayrollRun { Id = 33, BranchId = 9 },
            new PayrollPostingConfiguration { BranchId = 9 }); await db.SaveChangesAsync();
        (await db.SysUsers.SingleAsync()).IsAdmin = true; await db.SaveChangesAsync();
        Assert.True((await Execute(typeof(PayrollController), "GetPayrollRun")).Invoked);
        Assert.False(await db.PayrollRuns.AnyAsync(r => r.Id == 33)); Assert.False(await db.Set<PayrollPostingConfiguration>().AnyAsync(r => r.BranchId == 9));
    }

    [Fact] public async Task PayrollLineSnapshotAndComponentsFollowParentRunBranchScope()
    {
        db.AddRange(new PayrollRun { Id = 31, BranchId = 7 }, new PayrollRun { Id = 32, BranchId = 8 },
            new PayrollRunLine { Id = 41, PayrollRunId = 31, EmployeeCode = "SELF" }, new PayrollRunLine { Id = 42, PayrollRunId = 32, EmployeeCode = "SELF" },
            new PayrollCalculationSnapshot { Id = 51, PayrollRunLineId = 41, EmployeeCode = "SELF" }, new PayrollCalculationSnapshot { Id = 52, PayrollRunLineId = 42, EmployeeCode = "SELF" },
            new PayrollRunLineComponent { Id = 61, PayrollLineId = 41 }, new PayrollRunLineComponent { Id = 62, PayrollLineId = 42 }); await db.SaveChangesAsync();
        db.HrScopeEnabled = true; db.HrScopeCompanyId = 1; db.HrScopeBranchIds.Add(7);
        Assert.Equal(new long[] { 41 }, await db.PayrollRunLines.OrderBy(l => l.Id).Select(l => l.Id).ToArrayAsync());
        Assert.Equal(new long[] { 51 }, await db.PayrollCalculationSnapshots.OrderBy(l => l.Id).Select(l => l.Id).ToArrayAsync());
        Assert.Equal(new long[] { 61 }, await db.Set<PayrollRunLineComponent>().OrderBy(l => l.Id).Select(l => l.Id).ToArrayAsync());
    }

    [Fact] public async Task IdentityUniquenessIncludesHiddenBranchesButNotAnotherCompany()
    {
        (await db.Employees.SingleAsync(e => e.EmployeeCode == "OTHER")).NationalId = "  DUPLICATE  ";
        db.AddRange(new SysBranch { Id = 9, CompanyId = 2, IsActive = true }, new Employee { EmployeeCode = "FOREIGN", BranchId = 9, NationalId = "FOREIGN_ID" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.HrScopeEnabled = true; db.HrScopeCompanyId = 1; db.HrScopeBranchIds.Add(7);
        var repository = new ThinkOnErp.Infrastructure.Repositories.Hr.EmployeeRepository(db);
        Assert.Null(await repository.GetEmployeeByCodeAsync("OTHER"));
        Assert.True(await repository.ExistsByNationalIdAsync("DUPLICATE"));
        Assert.False(await repository.ExistsByNationalIdAsync("FOREIGN_ID"));
        Assert.True(await repository.ExistsByCodeAsync(" other "));
    }
    public void Dispose() => db.Dispose();
}
