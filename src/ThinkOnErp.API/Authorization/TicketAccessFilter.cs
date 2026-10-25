using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Models;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.API.Authorization;

/// <summary>Establishes the database scope before any ticket action runs.</summary>
public sealed class TicketAccessFilter(OracleDbContext db, ILogger<TicketAccessFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated != true) { context.Result = new UnauthorizedResult(); return; }
        if (http.Items[TenantRequestContext.HttpContextItemKey] is not TenantRequestContext tenant)
        { logger.LogWarning("Ticket request rejected: tenant context missing"); context.Result = new ForbidResult(); return; }
        var super = http.User.FindFirst("isSuperAdmin")?.Value == "true";
        var admin = super;
        long userId = 0;
        if (!super)
        {
            if (!long.TryParse(http.User.FindFirst("userId")?.Value, out userId))
            { context.Result = new ForbidResult(); return; }
            var user = await db.SysUsers.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive && u.CompanyId == tenant.CompanyId, http.RequestAborted);
            if (user == null) { logger.LogWarning("Ticket request rejected: active user {UserId} in company {CompanyId} not found", userId, tenant.CompanyId); context.Result = new ForbidResult(); return; }
            admin = user.IsAdmin;
            if (context.ActionDescriptor.RouteValues["action"] == "CreateTicket")
            {
                if (!long.TryParse(http.User.FindFirst("branchId")?.Value, out var branchId) ||
                    !await db.SysBranches.AnyAsync(b => b.Id == branchId && b.IsActive && b.CompanyId == tenant.CompanyId, http.RequestAborted) ||
                    (!admin && !await db.SysUserBranches.AnyAsync(b => b.UserId == userId && b.BranchId == branchId, http.RequestAborted)))
                { logger.LogWarning("Ticket creation rejected: user branch is inactive or outside company"); context.Result = new ForbidResult(); return; }
                http.Items["Ticket.BranchId"] = branchId;
                http.Items["Ticket.UserId"] = user.Id;
            }
        }
        db.TicketScopeEnabled = true;
        db.TicketScopeCompanyId = tenant.CompanyId;
        db.TicketScopeUserId = userId;
        db.TicketScopeAdmin = admin;
        http.Items["Ticket.IsAdmin"] = admin;
        // Re-check privileged mutations against the current account, not a stale token.
        var action = context.ActionDescriptor.RouteValues["action"];
        if (!admin && action is "AssignTicket" or "DeleteTicket" or "UpdateTicketStatus" or "GetTicketVolumeReport" or "GetSlaComplianceReport" or "GetWorkloadReport")
        { context.Result = new ForbidResult(); return; }
        if (!admin && context.ActionArguments.Values.Any(v => v?.GetType().GetProperty("IsInternal")?.GetValue(v) is true))
        { context.Result = new ForbidResult(); return; }
        if (context.ActionArguments.TryGetValue("id", out var value) && value is long id &&
            !await db.SysRequestTickets.AnyAsync(t => t.Id == id, http.RequestAborted))
        { context.Result = new NotFoundResult(); return; }
        await next();
    }
}
