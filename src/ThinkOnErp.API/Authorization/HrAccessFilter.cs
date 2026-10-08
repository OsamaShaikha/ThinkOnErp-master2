using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.API.Authorization;

// Runs after tenant routing and binding, before any HR action or business mutation.
public sealed class HrAccessFilter(OracleDbContext db) : IAsyncActionFilter, IOrderedFilter
{
    public int Order => -2000;

    public static bool ExplicitGrant(IEnumerable<bool> userPermissions, IEnumerable<bool> rolePermissions) =>
        ThinkOnErp.Infrastructure.Services.Hr.HrPermissionRules.ExplicitGrant(userPermissions, rolePermissions);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor action ||
            action.ControllerTypeInfo.Namespace != "ThinkOnErp.API.Controllers.Hr") { await next(); return; }
        var permission = action.MethodInfo.GetCustomAttributes(typeof(HrPermissionAttribute), true).Cast<HrPermissionAttribute>().SingleOrDefault();
        if (permission == null || context.HttpContext.Items[TenantRequestContext.HttpContextItemKey] is not TenantRequestContext tenant)
        { context.Result = new ForbidResult(); return; }
        var principal = context.HttpContext.User;
        var superAdmin = principal.Identity?.IsAuthenticated == true && principal.FindFirst("isSuperAdmin")?.Value == "true";
        var cancellation = context.HttpContext.RequestAborted;
        if (context.ActionArguments.Values.Any(v => v?.GetType().GetProperty("Approved")?.GetValue(v) is false))
            permission = new HrPermissionAttribute(permission.Screen, "reject", permission.SelfService);
        SysUser? user = null;
        if (!superAdmin)
        {
            if (!long.TryParse(principal.FindFirst("userId")?.Value, out var userId) || userId <= 0)
            { context.Result = new UnauthorizedResult(); return; }
            user = await db.SysUsers.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive && u.CompanyId == tenant.CompanyId, cancellation);
            if (user == null) { context.Result = new ForbidResult(); return; }
            var administratorOnly = action.MethodInfo.GetCustomAttributes(true)
                .Concat(action.ControllerTypeInfo.GetCustomAttributes(true))
                .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Any(a => a.Policy == "AdminOnly");
            if (administratorOnly && !user.IsAdmin) { context.Result = new ForbidResult(); return; }
            if (user.ForceLogoutDate.HasValue && (!long.TryParse(principal.FindFirst("iat")?.Value, out var issuedAt) || issuedAt < -62135596800L || issuedAt > 253402300799L ||
                DateTimeOffset.FromUnixTimeSeconds(issuedAt).UtcDateTime <= user.ForceLogoutDate.Value))
            { context.Result = new UnauthorizedResult(); return; }
        }

        db.HrScopeCompanyId = tenant.CompanyId;
        db.HrScopeAllBranches = superAdmin || user!.IsAdmin;
        var assigned = superAdmin ? new List<long>() : await db.SysUserBranches.Where(b => b.UserId == user!.Id)
            .Join(db.SysBranches.Where(b => b.CompanyId == tenant.CompanyId && b.IsActive), a => a.BranchId, b => b.Id, (a,b) => b.Id)
            .Distinct().ToListAsync(cancellation);

        if (!db.HrScopeAllBranches)
        {
            var deniedBranches = new HashSet<long>();
            var screen = await db.Set<SysScreen>().SingleOrDefaultAsync(s => s.ScreenCode == permission.Screen && s.IsActive, cancellation);
            var feature = await db.Set<SysFeature>().SingleOrDefaultAsync(f => f.FeatureCode == permission.Feature && f.IsActive, cancellation);
            var operationRegistered = screen != null && feature != null &&
                await db.Set<SysSystem>().AnyAsync(s => s.Id == screen.SystemId && s.IsActive, cancellation) &&
                await db.Set<SysScreenFeature>().AnyAsync(sf => sf.ScreenId == screen.Id && sf.FeatureId == feature.Id, cancellation);
            if (operationRegistered)
            {
                foreach (var branch in assigned)
                {
                    var decision = await ThinkOnErp.Infrastructure.Services.Hr.HrPermissionRules.BranchDecisionAsync(db, user!, screen!, feature!.Id, branch, cancellation);
                    if (decision == false) deniedBranches.Add(branch);
                    if (decision == true) db.HrScopeBranchIds.Add(branch);
                }
            }
            if (db.HrScopeBranchIds.Count == 0)
            {
                if (!permission.SelfService || !operationRegistered) { context.Result = new ForbidResult(); return; }
                var employee = await db.Employees.SingleOrDefaultAsync(e => e.UserId == user!.Id && e.IsActive && e.EmploymentStatus == "ACTIVE", cancellation);
                if (employee?.BranchId == null || !assigned.Contains(employee.BranchId.Value) || deniedBranches.Contains(employee.BranchId.Value)) { context.Result = new ForbidResult(); return; }
                db.HrScopeEmployeeCode = employee.EmployeeCode;
                db.HrScopeBranchIds.Add(employee.BranchId.Value);
            }
        }
        db.HrScopeEnabled = true;
        if (context.ActionArguments.TryGetValue("companyId", out var requestedCompany) && requestedCompany == null)
            context.ActionArguments["companyId"] = tenant.CompanyId;
        foreach (var argument in context.ActionArguments)
        {
            var value = argument.Value;
            long? company = argument.Key == "companyId" ? value as long? : value?.GetType().GetProperty("CompanyId")?.GetValue(value) as long?;
            long? branch = argument.Key == "branchId" ? value as long? : value?.GetType().GetProperty("BranchId")?.GetValue(value) as long?;
            string? employee = argument.Key == "employeeCode" ? value as string : value?.GetType().GetProperty("EmployeeCode")?.GetValue(value) as string;
            if (argument.Key == "code" && action.ControllerName == "Employees") employee = value as string;
            if ((company.HasValue && company != tenant.CompanyId) ||
                (branch.HasValue && !await db.SysBranches.AnyAsync(b => b.Id == branch && b.CompanyId == tenant.CompanyId && b.IsActive, cancellation)) ||
                (branch.HasValue && !db.HrScopeAllBranches && !db.HrScopeBranchIds.Contains(branch.Value)) ||
                (employee != null && !await db.Employees.AnyAsync(e => e.EmployeeCode == employee, cancellation) && action.ActionName != "CreateEmployee"))
            { context.Result = new ForbidResult(); return; }
            if (action.ActionName is "CreateEmployee" or "UpdateEmployee" && !db.HrScopeAllBranches && !branch.HasValue)
            { context.Result = new ForbidResult(); return; }
        }
        // A scoped payroll calculation must explicitly target one permitted branch.
        if (!db.HrScopeAllBranches && action.ActionName == "CalculatePayroll" &&
            !context.ActionArguments.Values.Any(v => v?.GetType().GetProperty("BranchId")?.GetValue(v) is long))
        { context.Result = new ForbidResult(); return; }
        await next();
    }
}
